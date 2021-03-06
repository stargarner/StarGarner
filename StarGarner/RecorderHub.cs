﻿using Newtonsoft.Json.Linq;
using StarGarner.Model;
using StarGarner.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StarGarner {

    // 録画中情報
    internal class Recording : IDisposable {
        static readonly Log log = new Log( "Recording" );

        static readonly Regex reSpaces = new Regex( @"\s+" );
        static readonly Regex reSpeed = new Regex( @"\s*\bspeed=[\s\d.]+x\s*\z|\s*q=-1\.0\b" );
        static readonly Regex reFps = new Regex( @"\s*\bfps=\s*(\d+)" );


        internal readonly String url;
        internal readonly String roomName;
        internal readonly LinkedList<String> lines = new LinkedList<String>();
        internal readonly Process process;

        internal Boolean isRunning => !process.HasExited;
        internal Int64 timeExit = 0L;

        Int32 countWarningNonMonotonous = 0;

        private async Task readStream(StreamReader sr) {
            try {
                while (true) {
                    var line = await sr.ReadLineAsync().ConfigureAwait( false );
                    if (line == null)
                        break;

                    if (line.Contains( " Skip " ) || line.Contains( " Opening " )) {
                        // not log, not status
                        continue;
                    } else if (line.Contains( "Non-monotonous DTS in output stream" )) {

                        if (countWarningNonMonotonous >= 10)
                            continue;

                        log.d( line );

                        if (++countWarningNonMonotonous == 10) {
                            log.d( "(Suppresses repeated warnings…)" );
                        }

                    } else {
                        line = reSpeed.Replace( reSpaces.Replace( line, " " ), "" );
                        var fps = reFps.matchOrNull( line )?.Groups[ 1 ].Value.toInt32() ?? -1;
                        if (fps >= 1) {
                            // not log, but show in status window
                        } else {
                            log.d( line );
                        }
                    }

                    lock (lines) {
                        lines.AddLast( line );
                        if (lines.Count > 10)
                            lines.RemoveFirst();
                    }
                }
            } catch (Exception ex) {
                log.e( ex, "readStream failed." );
            }
        }

        public Recording(RecorderHub hub, String roomName, String url, String folder) {
            this.url = url;
            this.roomName = roomName;

            var timeStr = UnixTime.now.formatFileTime();

            // todo 保存フォルダの設定
            var file = $"{folder}/{timeStr}-{roomName}.ts";
            var count = 1;
            while (File.Exists( file )) {
                ++count;
                file = $"{folder}/{timeStr}-{roomName}-{++count}.ts";
            }

            var p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = hub.ffmpegPath;
            p.StartInfo.Arguments = $"{Config.ffmpegOptions} -user_agent \"{Config.userAgent}\" -i \"{url}\" -c copy \"{ file}\"";
            p.StartInfo.UseShellExecute = false; // require to read output
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.EnableRaisingEvents = true;
            p.Exited += (sender, args) => {
                timeExit = UnixTime.now;
                log.d( $"{roomName}: recording process was exited." );
                hub.mainWindow.play( NotificationSound.recordingEnd );

                hub.showStatus();
            };
            p.Start();
            Task.Run( async () => await readStream( p.StandardOutput ) );
            Task.Run( async () => await readStream( p.StandardError ) );
            this.process = p;
            hub.showStatus();
            hub.mainWindow.play( NotificationSound.recordingStart );
        }

        public void Dispose() {
            try {
                if (!process.HasExited) {
                    log.d( $"{roomName}: Dispose: call Kill()." );
                    process.Kill();
                }
            } catch (Exception ex) {
                log.e( ex, "Dispose error." );
            }
        }

        internal String? getLine() {
            lock (lines) {
                return lines.Count == 0 ? null : lines.Last();
            }
        }
    }

    // 録画対象の部屋
    internal class RecordRoom : IComparable<RecordRoom> {
        static readonly Log log = new Log( "RecordRoom" );

        static readonly Regex rePathDelimiter = new Regex( @"[/\\]+" );

        internal readonly String roomName;
        internal Int64 roomId;
        internal String roomCaption;

        internal Boolean isRecordingUi;

        private Int64 lastCheckTime = 0L;
        private Task? lastCheckTask = null;

        internal Int64 lastFound = 0L;

        internal String url => $"{Config.URL_TOP}{roomName}";

        internal String getFolder(RecorderHub hub) {
            var dir = rePathDelimiter.Replace( $"{hub.saveDir}/{roomName}", "/" );

            try {
                Directory.CreateDirectory( dir );
            } catch (Exception ex) {
                log.e( ex, "CreateDirectory() failed." );
            }

            return dir;
        }

        internal RecordRoom(String roomName, Int64 roomId, String roomCaption) {
            this.roomName = roomName;
            this.roomId = roomId;
            this.roomCaption = roomCaption;
        }

        internal RecordRoom(RecordRoom src) {
            this.roomName = src.roomName;
            this.roomId = src.roomId;
            this.roomCaption = src.roomCaption;
        }

        public Int32 CompareTo(RecordRoom other)
            => roomName.CompareTo( other.roomName ).notZero()
            ?? roomId.CompareTo( other.roomId );

        public override Boolean Equals(Object? obj) {
            if (obj is RecordRoom other)
                return roomName.Equals( other.roomName );

            return false;
        }

        public override Int32 GetHashCode()
            => HashCode.Combine( roomName );

        public String Text {
            get {
                var recordStr = isRecordingUi ? " (録画中)" : "";
                return $"{roomName} ({roomId}) {roomCaption}{recordStr}";
            }
        }

        async Task check(RecorderHub hub) {
            try {
                if (!File.Exists( hub.ffmpegPath )) {
                    log.e( $"ffmpegPath '${hub.ffmpegPath}' is not valid." );
                }

                var folder = getFolder( hub );
                if (!Directory.Exists( folder )) {
                    log.e( $"folder '${folder}' is not valid." );
                }

                // オンライブ部屋一覧にあるストリーミング情報はあまり信用できないので読み直す
                var url = $"{Config.URL_TOP}api/live/streaming_url?room_id={roomId}&ignore_low_stream=1&_={UnixTime.now / 1000L}";
                var request = new HttpRequestMessage( HttpMethod.Get, url );
                request.Headers.Add( "Accept", "application/json" );
                var response = await Config.httpClient.SendAsync( request ).ConfigureAwait( false );
                var now = UnixTime.now;
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

                var root = JToken.Parse( content );
                var streaming_url_list = root.Value<JArray>( "streaming_url_list" );

                if (streaming_url_list == null)
                    return;

                if (streaming_url_list.Count == 0) {
                    log.d( $"{roomName}: there is streaming_url_list, but it's empty." );
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
                        if (si.type != "hls") {
                            log.d( $"{roomName}: not hls. type={si.type}" );
                            continue;
                        }
                        list.Add( si );
                    } catch (Exception ex) {
                        log.e( ex, $"{roomName}: StreamingInfo parse failed." );
                    }
                }

                if (list.Count == 0) {
                    log.d( $"{roomName}: StreamingInfo list is empty. original size={streaming_url_list.Count}" );
                    return;
                }
                list.Sort();

                var streamUrl = list[ 0 ].url;

                var recording = hub.getRecording( roomName );
                if (recording?.isRunning == true && recording?.url == streamUrl) {
                    log.d( $"{roomName}: already recording {streamUrl}" );
                    return;
                }

                // streamUrl が決定しても動画が開始しているとは限らない
                request = new HttpRequestMessage( HttpMethod.Get, streamUrl );
                response = await Config.httpClient.SendAsync( request ).ConfigureAwait( false );
                var code = response.codeInt();
                if (!response.IsSuccessStatusCode) {
                    log.d( $"{roomName}: {code} {response.ReasonPhrase} {streamUrl}" );
                    return;
                }

                content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );
                var reLineFeed = new Regex( "[\x0d\x0a]+" );
                String? chunk = null;
                foreach (var a in reLineFeed.Split( content )) {
                    var line = a.Trim();
                    if (line.Length == 0 || line.StartsWith( "#" ))
                        continue;
                    log.d( line );
                    if (chunk == null)
                        chunk = line;
                }
                if (chunk == null) {
                    log.d( $"{roomName}: missing chunk in playlsit." );
                    return;
                }

                if (!Uri.TryCreate( new Uri( streamUrl ), chunk, out var chunkUrl )) {
                    log.d( $"{roomName}: can't combile chunk url. {chunk}" );
                    return;
                }
                request = new HttpRequestMessage( HttpMethod.Head, chunkUrl );
                response = await Config.httpClient.SendAsync( request ).ConfigureAwait( false );
                code = response.codeInt();
                if (!response.IsSuccessStatusCode) {
                    log.d( $"{roomName}: {code} {response.ReasonPhrase} {chunkUrl}" );
                    return;
                }

                if (hub.isDisposed) {
                    log.d( $"{roomName}: hub was disposed." );
                    return;
                }
                log.d( $"{roomName}: recording start!" );
                hub.setRecodring( roomName, streamUrl, folder );
            } catch (Exception ex) {
                log.e( ex, $"{roomName}: check failed." );
            }
        }


        internal void step(RecorderHub hub) {
            if (lastCheckTask != null && !lastCheckTask.IsCompleted)
                return;

            var recording = hub.getRecording( roomName );
            if (recording?.isRunning == true)
                return;

            var now = UnixTime.now;

            var lastRecordEnd = recording?.timeExit ?? 0L;
            if (now - lastRecordEnd < 300000L) {
                // 前回の録画が終わってから5分間は 最も短い間隔でチェックする
                if (now < lastCheckTime + 5000L)
                    return;
            } else {
                // 最低限10秒は待つ
                if (now < lastCheckTime + 10000L)
                    return;

                // 分数が0,5,10, ... に近いなら間隔を短くする
                var dtNow = now.toDateTime();
                var x = Math.Abs( ( dtNow.Minute * 60 + dtNow.Second ) % 300 - 30 ) / 30;
                var interval = x switch
                {
                    0 => UnixTime.second1 * 10,
                    1 => UnixTime.second1 * 20,
                    8 => UnixTime.second1 * 20,
                    _ => UnixTime.second1 * 30,
                };
                if (now < lastCheckTime + interval)
                    return;
            }

            lastCheckTime = now;
            lastCheckTask = Task.Run( async () => await check( hub ) );
        }


        internal static async Task<RecordRoom> find(String roomName) {

            var url = $"{Config.URL_TOP}{roomName}";

            var request = new HttpRequestMessage( HttpMethod.Get, url );
            request.Headers.Add( "Accept", "text/html" );
            var response = await Config.httpClient.SendAsync( request ).ConfigureAwait( false );
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

            var roomId = new Regex( "content=\"showroom:///room\\?room_id=(\\d+)" ).matchOrNull( content )
                ?.Groups[ 1 ].Value.toInt64()
                ?? throw new DataException( "can't find room_id." );

            var roomCaption =
                new Regex( "<meta property=\"og:title\" content=\"([^\"]+)\">" ).matchOrNull( content )
                ?.Groups[ 1 ].Value.decodeEntity()
                ?? "?";

            return new RecordRoom( roomName, roomId, roomCaption );
        }
    }

    internal class RecorderHub : IDisposable {

        internal volatile String saveDir = "./record";
        internal volatile String ffmpegPath = "C:/cygwin64/bin/ffmpeg.exe";

        internal readonly MainWindow mainWindow;
        public volatile Boolean isDisposed = false;

        private readonly ConcurrentDictionary<String, RecordRoom> roomMap
            = new ConcurrentDictionary<String, RecordRoom>();

        private readonly ConcurrentDictionary<String, Recording> recordingMap
            = new ConcurrentDictionary<String, Recording>();



        internal Recording? getRecording(String roomName)
            => recordingMap.getOrNull( roomName );

        internal void setRecodring(String roomName, String streamUrl, String folder) {
            recordingMap.removeOrNull( roomName )?.Dispose();
            recordingMap[ roomName ] = new Recording( this, roomName, streamUrl, folder );
        }

        public void Dispose() {
            isDisposed = true;
            recordingMap.Values.ForEach( (it) => it.Dispose() );
        }

        internal RecorderHub(MainWindow mainWindow)
            => this.mainWindow = mainWindow;

        internal JObject encodeSetting() => new JObject {
            { Config.KEY_SAVE_DIR, saveDir },
            { Config.KEY_FFMPEG_PATH, ffmpegPath },
            { Config.KEY_ROOMS,
                new JArray( roomMap.Values.Select( (room)
                    => new JObject {
                        { Config.KEY_ROOM_NAME, room.roomName },
                        { Config.KEY_ROOM_ID, room.roomId },
                        { Config.KEY_ROOM_CAPTION, room.roomCaption }
                    }
                ) )
            }
        };

        internal void load(JObject src) {
            var sv = src.Value<String>( Config.KEY_SAVE_DIR );
            if (sv != null)
                saveDir = sv;

            sv = src.Value<String>( Config.KEY_FFMPEG_PATH );
            if (sv != null)
                ffmpegPath = sv;

            var av = src.Value<JArray>( Config.KEY_ROOMS );
            if (av != null) {
                foreach (var r in av) {
                    var id = r.Value<Int64?>( Config.KEY_ROOM_ID );
                    var name = r.Value<String?>( Config.KEY_ROOM_NAME );
                    var caption = r.Value<String?>( Config.KEY_ROOM_CAPTION ) ?? "";
                    if (id == null || name == null)
                        continue;
                    roomMap[ name ] = new RecordRoom( name, id.Value, caption );
                }
            }
        }



        // タイマーから定期的に呼ばれる
        internal void step()
            => roomMap.Values.ForEach( (r) => Task.Run( () => r.step( this ) ) );

        // 録画状態の変化をUIに通知する
        internal void showStatus() {
            if (isDisposed)
                return;
            mainWindow.showRecorderStatus();
        }

        // 録画対象リストを返す
        internal List<RecordRoom> getRoomList()
            => new List<RecordRoom>( roomMap.Values );

        internal void setRoomList(IEnumerable<RecordRoom>? src) {
            if (src == null)
                return;

            var oldNames = roomMap.Values.Select( (it) => it.roomName ).ToHashSet();
            foreach (var s in src) {
                oldNames.Remove( s.roomName );
                var old = roomMap.GetValueOrDefault( s.roomName );
                if (old != null) {
                    old.roomId = s.roomId;
                    old.roomCaption = s.roomCaption;
                } else {
                    roomMap[ s.roomName ] = new RecordRoom( s );
                }
            }
            foreach (var name in oldNames) {
                roomMap.removeOrNull( name );
                recordingMap.removeOrNull( name )?.Dispose();
            }
        }
        // 設定UIと現在の設定が異なるなら真
        internal Boolean isRoomListChanged(IEnumerable<RecordRoom> uiRoomList) {
            var uiSet = new SortedSet<RecordRoom>( uiRoomList );
            var currentSet = new SortedSet<RecordRoom>( roomMap.Values );

            if (uiSet.Count != currentSet.Count)
                return true;

            var ia = uiSet.GetEnumerator();
            var ib = currentSet.GetEnumerator();
            while (ia.MoveNext()) {
                ib.MoveNext();
                var a = ia.Current;
                var b = ib.Current;

                if (a.roomName != b.roomName || a.roomId != b.roomId || a.roomCaption != b.roomCaption)
                    return true;
            }

            return false;
        }

        // 録画ステータスの表示
        internal void dumpStatus(StatusCollection dst) {
            var rooms = roomMap.Values.ToList();
            rooms.Sort();
            foreach (var room in rooms) {
                var recording = getRecording( room.roomName );
                if (recording?.isRunning == true) {
                    dst.add( $"録画中 {room.roomName} {room.roomCaption}" );
                    var line = recording.getLine();
                    if (line != null)
                        dst.add( line );
                }
            }
        }
    }
}
