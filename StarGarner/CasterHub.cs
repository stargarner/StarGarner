using Newtonsoft.Json.Linq;
using StarGarner.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StarGarner {

    internal class CastRoom : IComparable<CastRoom>, IDisposable {
        readonly Log log;

        internal readonly String roomName;
        internal Int64 roomId;

        internal volatile Boolean isCasting;
        internal Boolean isCastingUi;

        private Int64 lastCheckTime = 0L;

        internal Int64 lastFound = 0L;

        internal String url => $"{Config.URL_TOP}{roomName}";

        private readonly CancellationTokenSource canceller = new CancellationTokenSource();

        internal CastRoom(String roomName, Int64 roomId) {
            this.roomName = roomName;
            this.roomId = roomId;
            this.log = new Log( $"CastRoom {roomName}" );
        }

        internal CastRoom(CastRoom src) {
            this.roomName = src.roomName;
            this.roomId = src.roomId;
            this.log = new Log( $"CastRoom {roomName}:" );
        }

        public Int32 CompareTo(CastRoom other)
            => roomName.CompareTo( other.roomName ).notZero()
            ?? roomId.CompareTo( other.roomId );

        public override Boolean Equals(Object? obj) {
            if (obj is RecordRoom other)
                return roomName.Equals( other.roomName );

            return false;
        }

        public override Int32 GetHashCode()
            => HashCode.Combine( roomName );


        volatile Int32 httpErrorCode = 0;
        volatile String httpError = "";
        volatile String countStr = "";

        public String Text {
            get {
                var recordStr = isCastingUi ? "(配信中)" : "";
                return $"{roomName} ({roomId}) {recordStr} {countStr} {httpError}";
            }
        }

        private Boolean isDisposed => canceller.IsCancellationRequested;

        ConfiguredTaskAwaitable delayEx(Int64 delay) {
            if (isDisposed) {
                return Task.CompletedTask.ConfigureAwait( false );
            } else {
                return Task.Delay( (Int32)delay, canceller.Token ).ConfigureAwait( false );
            }
        }

        public void Dispose() {
            log.d( "Dispose." );
            canceller.Cancel();
        }

        static readonly Regex reInitialData = new Regex( "id=\"js-initial-data\" data-json=\"([^\"]+)" );
        static readonly Regex reLiveData = new Regex( "id=\"js-live-data\" data-json=\"([^\"]+)" );

        Int64 lastCountStart = 0L;

        async void count(CasterHub hub, String liveId, String csrfToken, Int64 lastPageOpen) {
            if (lastPageOpen - lastCountStart < UnixTime.hour1) {
                countStr = "カウント(前回から1時間経過してない)";
                hub.showStatus();
                log.d( "dont count due to rapidly open pages." );
                return;
            }
            lastCountStart = lastPageOpen;

            countStr = "カウント開始";
            hub.showStatus();

            for (var count = 0; count <= 50; ++count) {
                for (var nTry = 4; nTry >= 1; --nTry) {
                    try {
                        if (isDisposed) {
                            log.d( "count cancelled." );
                            return;
                        }

                        var parameters = new Dictionary<String, String>(){
                            { "live_id", liveId },
                            { "comment", count.ToString()  },
                            { "is_delay", "0" },
                            {"csrf_token", csrfToken }
                        };
                        var url = $"{Config.URL_TOP}api/live/post_live_comment";
                        var request = new HttpRequestMessage( HttpMethod.Post, url );
                        request.Headers.Add( "Cookie", hub.cookie );
                        request.Headers.Add( "Accept", "application/json" );
                        request.Content = new FormUrlEncodedContent( parameters );
                        //
                        var response = await Config.httpClient.SendAsync( request, canceller.Token ).ConfigureAwait( false );
                        var code = response.codeInt();
                        log.d( $"count {count} HTTP {code} {response.ReasonPhrase}" );
                        await delayEx( UnixTime.second1 * 2 );

                        if (response.IsSuccessStatusCode) {
                            countStr = $"カウント{count} 成功";
                            hub.showStatus();
                            break;
                        }

                        if (nTry == 1) {
                            log.d( $"count {count} too many retry. stop counting…" );
                            countStr = $"カウント{count} 中断 HTTP {response.ReasonPhrase}";
                            hub.showStatus();
                            return;
                        }
                    } catch (Exception ex) {
                        countStr = $"カウント{count} {ex}";
                        hub.showStatus();
                        return;
                    }
                }
            }
        }

        internal async Task check(CasterHub hub) {

            if (httpErrorCode >= 400 && httpErrorCode < 500) {
                log.e( $"httpErrorCode={httpErrorCode}" );
                return;
            }

            // 最低限3分は待つ
            var now = UnixTime.now;
            if (now < lastCheckTime + UnixTime.minute1 * 3)
                return;
            lastCheckTime = now;

            log.d( "check start." );

            var csrfToken = "";
            var liveId = "";
            var hasLive = false;
            var castTotal = 0;

            while (!isDisposed) {
                try {
                    var list = new List<JObject>();

                    String pageUrl;
                    Int64 timePageOpen;
                    if (!hasLive) {
                        // 初回は部屋のHTMLを読む
                        pageUrl = $"{Config.URL_TOP}{roomName}";
                        var request = new HttpRequestMessage( HttpMethod.Get, pageUrl );
                        request.Headers.Add( "Cookie", hub.cookie );
                        request.Headers.Add( "Accept", "text/html" );
                        var response = await Config.httpClient.SendAsync( request, canceller.Token ).ConfigureAwait( false );
                        timePageOpen = UnixTime.now;
                        var code = response.codeInt();
                        if (!response.IsSuccessStatusCode) {
                            log.d( $"HTTP {code} {response.ReasonPhrase} {pageUrl}" );
                            httpErrorCode = code;
                            httpError = $"HTTP {code} {response.ReasonPhrase}";
                            hub.showStatus();
                            if (code >= 400 && code < 500) {
                                log.d( $"don't retry for error {code}." );
                                break;
                            }
                            await delayEx( UnixTime.second1 * 10L );
                            continue;
                        }
                        httpErrorCode = 0;
                        httpError = "";

                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

                        var json = reInitialData.matchOrNull( content )?.Groups[ 1 ].Value.decodeEntity();
                        if (json == null) {
                            log.e( "missing jsinitialData. (maybe not in live)." );
                            break;
                        }
                        var jsinitialData = JToken.Parse( json );
                        csrfToken = jsinitialData.Value<String>( "csrfToken" ) ?? "";
                        liveId = jsinitialData.Value<String>( "liveId" ) ?? "";
                        if (csrfToken == "" || liveId == "") {
                            log.e( "missing csrfToken or liveId in jsinitialData. (maybe not in live)." );
                            break;
                        }
                        //
                        json = reLiveData.matchOrNull( content )?.Groups[ 1 ].Value.decodeEntity();
                        if (json == null) {
                            log.d( "missing jsLiveData. (maybe not in live)." );
                            break;
                        }
                        var giftList = JToken.Parse( json ).Value<JArray>( "gift_list" );
                        if (giftList == null) {
                            log.d( "missing giftList." );
                            break;
                        }

                        foreach (JObject gift in giftList) {
                            list.Add( gift );
                        }
                    } else {
                        // 2回目以降は current_user APIを読む
                        pageUrl = $"{Config.URL_TOP}api/live/current_user?room_id={ roomId }&_={UnixTime.now / 1000L}";
                        var request = new HttpRequestMessage( HttpMethod.Get, pageUrl );
                        request.Headers.Add( "Accept", "application/json" );
                        request.Headers.Add( "Cookie", hub.cookie );

                        var response = await Config.httpClient.SendAsync( request, canceller.Token ).ConfigureAwait( false );
                        timePageOpen = UnixTime.now;
                        var code = response.codeInt();

                        if (!response.IsSuccessStatusCode) {
                            log.d( $"HTTP {code} {response.ReasonPhrase} {pageUrl}" );
                            httpErrorCode = code;
                            httpError = $"HTTP {code} {response.ReasonPhrase}";
                            hub.showStatus();
                            if (code >= 400 && code < 500) {
                                log.d( $"don't retry for error {code}." );
                                break;
                            }
                            await delayEx( UnixTime.second1 * 10L );
                            continue;
                        }
                        httpErrorCode = 0;
                        httpError = "";

                        // レスポンスを受け取った時刻
                        timePageOpen = UnixTime.now;
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );
                        MyResourceRequestHandler.responceLog( UnixTime.now, url, content );
                        try {
                            foreach (JObject gift in JToken.Parse( content ).Value<JObject>( "gift_list" ).Value<JArray>( "normal" )) {
                                list.Add( gift );
                            }
                        } catch (Exception ex) {
                            log.e( ex, "gift list is null. (parse error)" );
                            break;
                        }
                    }

                    if (list.Count == 0) {
                        // 配信してなかった…
                        log.d( "gift list is empty. (not in live)" );
                        break;
                    }

                    if (!hasLive) {
                        var dontWait = Task.Run( () => count( hub, liveId, csrfToken, timePageOpen ) );
                    }

                    hasLive = true;

                    isCasting = true;
                    hub.showStatus();
                    hub.mainWindow.onGiftCount( UnixTime.now, list, pageUrl );

                    list.Sort( (a, b) => a.Value<Int32>( "gift_id" ).CompareTo( b.Value<Int32>( "gift_id" ) ) );

                    while (true) {
                        var castCount = 0;
                        foreach (var gift in list) {
                            var giftId = gift.Value<Int32?>( "gift_id" ) ?? -1;
                            var freeNum = gift.Value<Int32?>( "free_num" ) ?? -1;
                            if (giftId < 0 || freeNum <= 0)
                                continue;

                            if (!Config.starIds.Contains( giftId ) && !Config.seedIds.Contains( giftId ))
                                continue;

                            var step = Math.Min( 10, freeNum );
                            log.d( $"gifting giftId={giftId}, num={step}" );

                            var parameters = new Dictionary<String, String>(){
                                { "gift_id", giftId.ToString() },
                                { "live_id", liveId },
                                { "num", step.ToString() },
                                {"csrf_token", csrfToken }
                            };
                            var url = $"{Config.URL_TOP}api/live/gifting_free";
                            var request = new HttpRequestMessage( HttpMethod.Post, url );
                            request.Headers.Add( "Cookie", hub.cookie );
                            request.Headers.Add( "Accept", "application/json" );
                            request.Content = new FormUrlEncodedContent( parameters );
                            //
                            var response = await Config.httpClient.SendAsync( request, canceller.Token ).ConfigureAwait( false );
                            var code = response.codeInt();
                            if (!response.IsSuccessStatusCode) {
                                log.d( $"HTTP {code} {response.ReasonPhrase} {url}" );
                                break;
                            }
                            castCount += step;
                            freeNum -= step;
                            ( (JObject)gift )[ "free_num" ] = freeNum;
                        }

                        if (castCount == 0)
                            break;
                        castTotal += castCount;

                        log.d( $"castCount={castCount}, total={castTotal}" );

                        hub.mainWindow.onGiftCount( UnixTime.now, list, pageUrl );
                        await delayEx( UnixTime.second1 * 2L );
                    }

                    // ページを開いてから31秒は待つ
                    // (自動取得と足並みを揃える)
                    var remain = UnixTime.second1 * 31 + timePageOpen - UnixTime.now;
                    if (remain > 0L) {
                        log.d( $"wait {remain}ms" );
                        await delayEx( remain );
                    }
                } catch (TaskCanceledException ex) {
                    log.e( ex, "task was cancelled." );
                    break;
                } catch (Exception ex) {
                    log.e( ex, "check() failed." );
                    await delayEx( UnixTime.second1 * 60L );
                }
            }

            isCasting = false;
            hub.showStatus();

            if (hasLive)
                log.d( "check() end." );
        }

        internal static async Task<CastRoom> find(String roomName) {

            var url = $"{Config.URL_TOP}{roomName}";

            var request = new HttpRequestMessage( HttpMethod.Get, url );
            request.Headers.Add( "Accept", "text/html" );
            var response = await Config.httpClient.SendAsync( request ).ConfigureAwait( false );
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

            var roomId = new Regex( "content=\"showroom:///room\\?room_id=(\\d+)" ).matchOrNull( content )
                ?.Groups[ 1 ].Value.toInt64()
                ?? throw new DataException( "can't find room_id." );

            return new CastRoom( roomName, roomId );
        }
    }

    internal class CasterHub {
        internal readonly MainWindow mainWindow;

        public volatile Boolean isDisposed = false;

        private readonly ConcurrentDictionary<String, CastRoom> roomMap
            = new ConcurrentDictionary<String, CastRoom>();

        public void Dispose() {
            isDisposed = true;
            roomMap.Values.ForEach( (it) => it.Dispose() );
        }

        internal CasterHub(MainWindow mainWindow)
            => this.mainWindow = mainWindow;

        internal JObject encodeSetting() => new JObject {
            { Config.KEY_ROOMS, new JArray( roomMap.Values.Select(
                (room) => new JObject {
                    { Config.KEY_ROOM_NAME, room.roomName },
                    { Config.KEY_ROOM_ID, room.roomId },
                }
            ) ) }
        };

        internal void load(JObject src) => src.Value<JArray>( Config.KEY_ROOMS )?.ForEach( (r) => {
            var id = r.Value<Int64?>( Config.KEY_ROOM_ID );
            var name = r.Value<String?>( Config.KEY_ROOM_NAME );
            if (id != null && name != null)
                roomMap[ name ] = new CastRoom( name, id.Value );
        } );

        private Task? lastTask = null;

        // タイマーから定期的に呼ばれる
        internal void step() {

            if (cookie == null)
                return;

            if (lastTask != null && lastTask.IsCompleted == false)
                return;

            lastTask = Task.Run( async () => {
                foreach (var room in roomMap.Values) {
                    await room.check( this );
                }
            } );
        }

        // 録画状態の変化をUIに通知する
        internal void showStatus() {
            if (isDisposed)
                return;
            mainWindow.showCasterStatus();
        }

        // 録画対象リストを返す
        internal List<CastRoom> getRoomList()
            => new List<CastRoom>( roomMap.Values );

        internal void setRoomList(IEnumerable<CastRoom>? src) {
            if (src == null)
                return;

            var oldNames = roomMap.Values.Select( (it) => it.roomName ).ToHashSet();
            foreach (var s in src) {
                oldNames.Remove( s.roomName );
                var old = roomMap.GetValueOrDefault( s.roomName );
                if (old != null) {
                    old.roomId = s.roomId;
                } else {
                    roomMap[ s.roomName ] = new CastRoom( s );
                }
            }
            foreach (var name in oldNames) {
                roomMap.removeOrNull( name )?.Dispose();
            }
        }
        // 設定UIと現在の設定が異なるなら真
        internal Boolean isRoomListChanged(IEnumerable<CastRoom> uiRoomList) {
            var uiSet = new SortedSet<CastRoom>( uiRoomList );
            var currentSet = new SortedSet<CastRoom>( roomMap.Values );

            if (uiSet.Count != currentSet.Count)
                return true;

            var ia = uiSet.GetEnumerator();
            var ib = currentSet.GetEnumerator();
            while (ia.MoveNext()) {
                ib.MoveNext();
                var a = ia.Current;
                var b = ib.Current;

                if (a.roomName != b.roomName || a.roomId != b.roomId)
                    return true;
            }

            return false;
        }

        // 録画ステータスの表示
        internal void dumpStatus(StatusCollection dst) {
            var rooms = roomMap.Values.ToList();
            rooms.Sort();
            foreach (var room in rooms) {
                if (room.isCasting) {
                    dst.add( $"自動投げ {room.Text}" );
                }
            }
        }

        internal CastRoom getCasting(String roomName) => roomMap[ roomName ];

        internal volatile String? cookie = null;

        internal void setCookie(String cookie) {
            this.cookie = cookie;
            step();
        }
    }
}
