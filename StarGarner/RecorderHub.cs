using Newtonsoft.Json.Linq;
using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StarGarner {

    // 録画中情報
    internal class Recording : IDisposable {
        internal String url;
        internal String roomName;
        internal LinkedList<String> lines = new LinkedList<String>();
        internal Process process;
        internal Boolean isRunning => !process.HasExited;

        static readonly Regex reSpaces = new Regex( @"\s+" );
        static readonly Regex reSpeed = new Regex( @"\s*speed=[\s\d.]+x\s*\z|\s*q=-1\.0\b" );


        private async Task readStream(StreamReader sr) {
            try {
                Log.d( "readStream start." );
                while (true) {
                    var line = await sr.ReadLineAsync();
                    if (line == null)
                        break;
                    // not log
                    if (line.Contains( " Skip " ) || line.Contains( " Opening " )) {
                        continue;
                    }
                    line = reSpeed.Replace( reSpaces.Replace( line, " " ), "" );

                    Log.d( line );
                    lock (lines) {
                        lines.AddLast( line );
                        if (lines.Count > 10)
                            lines.RemoveFirst();
                    }
                }
                Log.d( "readStream end." );
            } catch (Exception ex) {
                Log.e( ex, "readStream failed." );
            }
        }

        public Recording(RecorderHub hub, String url, String roomName) {
            this.url = url;
            this.roomName = roomName;

            var timeStr = UnixTime.now.formatFileTime();

            // todo 保存フォルダの設定
            Directory.CreateDirectory( $"{hub.saveDir}/{roomName}" );
            var file = $"{hub.saveDir}/{roomName}/{timeStr}-{roomName}.ts";
            var count = 1;
            while (File.Exists( file )) {
                ++count;
                file = $"{hub.saveDir}/{roomName}/{timeStr}-{roomName}-{++count}.ts";
            }

            var p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = hub.ffmpegPath;
            p.StartInfo.Arguments = $"-loglevel info -timeout 30000 -user_agent \"{Config.userAgent}\" -i \"{url}\" -c copy \"{ file}\"";
            p.StartInfo.UseShellExecute = false; // require to read output
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
            Task.Run( async () => await readStream( p.StandardOutput ) );
            Task.Run( async () => await readStream( p.StandardError ) );
            p.Exited += (sender, args) => hub.showStatus();
            this.process = p;
            hub.showStatus();
        }

        public void Dispose() {
            try {
                if (!process.HasExited) {
                    process.Kill();
                }
            } catch (Exception ex) {
                Log.e( ex, "Dispose has error" );
            }
        }

        internal String? getLine() {
            lock (lines) {
                return lines.Count == 0 ? null : lines.Last();
            }
        }
    }

    // ストリーミング情報
    internal class StreamingInfo : IComparable<StreamingInfo> {
        public String url;
        public String type;
        public Int64 quality;
        public Boolean is_default;

        public StreamingInfo(
            String url,
            String type,
            Boolean is_default,
            Int64 quality
            ) {
            this.url = url;
            this.type = type;
            this.is_default = is_default;
            this.quality = quality;
        }

        public Int32 CompareTo(StreamingInfo other)
            => other.is_default.CompareTo( is_default ).notZero() ?? other.quality.CompareTo( quality );
    }

    // 録画対象の部屋
    internal class RecordRoom : IComparable<RecordRoom> {
        private readonly RecorderHub hub;
        internal readonly String roomName;
        internal Int64 roomId;
        internal String roomCaption;

        internal RecordRoom? parentRoom;
        internal Boolean isRecordingUi;

        internal Recording? recording = null;
        private Int64 nextCheckTime = 0L;
        private Task? lastCheckTask = null;

        internal Int64 lastFound = 0L;

        internal String url => $"{Config.URL_TOP}{roomName}";

        internal RecordRoom(RecorderHub hub, String roomName, Int64 roomId, String roomCaption) {
            this.hub = hub;
            this.roomName = roomName;
            this.roomId = roomId;
            this.roomCaption = roomCaption;
        }

        public String Text {
            get {
                var recordStr = isRecordingUi ? " (録画中)" : "";
                return $"{roomName} ({roomId}) {roomCaption}{recordStr}";
            }
        }

        async Task check() {
            try {
                // オンライブ部屋一覧にあるストリーミング情報はあまり信用できないので読み直す
                var url = $"{Config.URL_TOP}api/live/streaming_url?room_id={roomId}&ignore_low_stream=1&_={UnixTime.now / 1000L}";
                var request = new HttpRequestMessage( HttpMethod.Get, url );
                request.Headers.Add( "Accept", "application/json" );
                var response = await hub.client.SendAsync( request ).ConfigureAwait( false );
                var now = UnixTime.now;
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );
                var root = JToken.Parse( content );

                var streaming_url_list = root.Value<JArray>( "streaming_url_list" );
                if (streaming_url_list == null) {
                    // Log.d( $"{roomName}: not on live." );
                    return;
                }

                var list = new List<StreamingInfo>();
                foreach (JObject src in streaming_url_list) {
                    try {
                        var si = new StreamingInfo(
                                src.Value<String>( "url" )!,
                                src.Value<String>( "type" )!,
                                src.Value<Boolean>( "is_default" ),
                                src.Value<Int64>( "quality" )
                                );
                        if (si.type != "hls")
                            continue;
                        list.Add( si );
                    } catch (Exception ex) {
                        Log.e( ex, $"{roomName}: StreamingInfo parse failed." );
                    }
                }
                if (list.Count == 0) {
                    Log.d( $"{roomName}: streaming_url_list is empty." );
                    return;
                }
                list.Sort();
                var item = list[ 0 ];

                if (recording?.isRunning == true && recording?.url == item.url) {
                    Log.d( $"{roomName}: already recording {item.url}" );
                    return;
                }
                recording?.Dispose();

                if (hub.isDisposed) {
                    Log.d( $"{roomName}: hub was disposed." );
                    return;
                }

                Log.d( $"{roomName}: recording start!" );

                recording = new Recording( hub, item.url, roomName );
            } catch (Exception ex) {
                Log.e( ex, $"{roomName}: check failed." );
            }
        }

        internal RecordRoom clone() => new RecordRoom( hub, roomName, roomId, roomCaption );

        internal void step() {
            if (lastCheckTask != null && !lastCheckTask.IsCompleted)
                return;

            if (recording?.isRunning == true)
                return;

            var now = UnixTime.now;
            if (now < nextCheckTime)
                return;
            var dtNow = now.toDateTime();
            var x = Math.Abs( ( dtNow.Minute * 60 + dtNow.Second ) % 300 - 30 ) /30;
            nextCheckTime = now + (x/30) switch
            {
                0 => UnixTime.second1 * 10,
                1 => UnixTime.second1 * 20,
                8 => UnixTime.second1 * 20,
                _ => UnixTime.second1 * 30,
            };

            lastCheckTask = Task.Run( async () => await check() );
        }

        public Int32 CompareTo(RecordRoom other)
            => roomName.CompareTo( other.roomName ).notZero() ??
            roomId.CompareTo( other.roomId );

        public override Boolean Equals(Object? obj) {
            if (obj is RecordRoom other)
                return roomName.Equals( other.roomName );

            return false;
        }

        public override Int32 GetHashCode()
            => HashCode.Combine( roomName );
    }

    internal class RecorderHub : IDisposable {

        internal volatile String saveDir = "./record";
        internal volatile String ffmpegPath = "C:/cygwin64/bin/ffmpeg.exe";

        internal readonly HttpClient client = new HttpClient();
        internal readonly MainWindow mainWindow;
        internal readonly List<RecordRoom> roomList = new List<RecordRoom>();
        public volatile Boolean isDisposed = false;


        internal RecorderHub(MainWindow mainWindow) {
            this.mainWindow = mainWindow;
            client.DefaultRequestHeaders.Add( "User-Agent", Config.userAgent );
        }

        public void Dispose() {
            isDisposed = true;
            lock (roomList) {
                foreach (var room in roomList) {
                    room.recording?.Dispose();
                }
            }
        }


        internal JObject encodeSetting() {
            lock (roomList) {
                return new JObject {
                { Config.KEY_SAVE_DIR, saveDir },
                { Config.KEY_FFMPEG_PATH, ffmpegPath },
                { Config.KEY_ROOMS,
                    new JArray( roomList.Select( (room)
                        => new JObject {
                            { Config.KEY_ROOM_NAME, room.roomName },
                            { Config.KEY_ROOM_ID, room.roomId },
                            { Config.KEY_ROOM_CAPTION, room.roomCaption }
                        }
                    ) )
                }
            };
            }
        }

        internal void load(JObject src) {
            var sv = src.Value<String>( Config.KEY_SAVE_DIR );
            if (sv != null)
                saveDir = sv;

            sv = src.Value<String>( Config.KEY_FFMPEG_PATH );
            if (sv != null)
                ffmpegPath = sv;

            var av = src.Value<JArray>( Config.KEY_ROOMS );
            if (av != null) {
                lock (roomList) {
                    roomList.Clear();
                    foreach (var r in av) {
                        var id = r.Value<Int64?>( Config.KEY_ROOM_ID );

                        if (id == null)
                            continue;

                        roomList.Add( new RecordRoom(
                            this
                            , r.Value<String>( Config.KEY_ROOM_NAME )
                            , id.Value
                            , r.Value<String>( Config.KEY_ROOM_CAPTION )
                            ) );
                    }
                }
            }
        }

        internal void setRoomList(IEnumerable<RecordRoom>? src) {
            if (src == null)
                return;

            lock (roomList) {
                var oldList = new List<RecordRoom>( roomList );
                roomList.Clear();
                foreach (var s in src) {
                    var old = oldList.Find( (x) => x.roomName.Equals( s.roomName ) );
                    if (old != null) {
                        old.roomId = s.roomId;
                        old.roomCaption = s.roomCaption;
                        oldList.Remove( old );
                        roomList.Add( old );
                    } else {
                        roomList.Add( s );
                    }
                }
                foreach (var a in oldList) {
                    a.recording?.Dispose();
                }
            }
        }

        internal void step() {
            lock (roomList) {
                foreach (var room in roomList) {
                    Task.Run( () => room.step() );
                }
            }
        }

        internal void showStatus() {
            if (isDisposed)
                return;
            mainWindow.showRecorderStatus();
        }

        internal Boolean isRecording {
            get {
                lock (roomList) {
                    foreach (var room in roomList) {
                        if (room.recording?.isRunning == true)
                            return true;
                    }
                    return false;
                }
            }
        }

        internal List<RecordRoom> getList() {
            lock (roomList) {
                var dst = new List<RecordRoom>();
                dst.AddRange( roomList );
                return dst;
            }
        }

        internal Boolean equalsRoomList(IEnumerable<RecordRoom> uiRoomList) {
            lock (roomList) {
                var uiSet = new SortedSet<RecordRoom>( uiRoomList );
                var currentSet = new SortedSet<RecordRoom>( roomList );
                if (uiSet.Count != currentSet.Count)
                    return false;
                var ia = uiSet.GetEnumerator();
                var ib = currentSet.GetEnumerator();
                while (ia.MoveNext()) {
                    ib.MoveNext();
                    var a = ia.Current;
                    var b = ib.Current;
                    if (a.roomName != b.roomName || a.roomId != b.roomId || a.roomCaption != b.roomCaption)
                        return false;
                }
                return true;
            }
        }

        internal async Task<RecordRoom> findRoom(String text) {

            var m = new Regex( $"\\A{Config.URL_TOP}([^/#?]+)" ).Match( text );
            if (!m.Success) {
                throw new ArgumentException( "部屋のURLの形式が変です" );
            }
            var roomName = m.Groups[ 1 ].Value;

            var url = $"{Config.URL_TOP}{roomName}";
            var request = new HttpRequestMessage( HttpMethod.Get, url );
            request.Headers.Add( "Accept", "text/html" );
            var response = await client.SendAsync( request ).ConfigureAwait( false );
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

            m = new Regex( @"\?room_id=(\d+)" ).Match( content );
            if (!m.Success) {
                throw new DataException( "can't find room_id." );
            }
            var roomId = Int64.Parse( m.Groups[ 1 ].Value );

            String roomCaption;
            m = new Regex( "<meta property=\"og:title\" content=\"([^\"]+)\">" ).Match( content );
            roomCaption = !m.Success ? "?" : WebUtility.HtmlDecode( m.Groups[ 1 ].Value );
            return new RecordRoom( this, roomName, roomId, roomCaption );
        }

        internal void dumpStatus(StatusCollection dst) {
            lock (roomList) {
                foreach (var room in roomList) {
                    var r = room.recording;
                    if (r == null || !r.isRunning)
                        continue;
                    dst.add( $"録画中 {room.roomName} {room.roomCaption}" );
                    var line = r.getLine();
                    if (line != null)
                        dst.add( line );
                }
            }
        }
    }
}
