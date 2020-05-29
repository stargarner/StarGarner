using Newtonsoft.Json.Linq;
using StarGarner.Model;
using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace StarGarner {
    // onlive room list
    class OnliveChecker {
        static readonly Log log = new Log( "OnliveChecker" );

        public volatile List<Room> starRooms = new List<Room>();
        public volatile List<Room> seedRooms = new List<Room>();

        private readonly MainWindow window;

        internal volatile String? cookie = null;

        public OnliveChecker(MainWindow window) 
            => this.window = window;

        //######################################################
        // ギフト所持数のチェック

        private Int64 lastGiftStart = 0L;
        private Task? lastGiftTask = null;

        private async Task checkGiftCount(List<Room> rooms, String itemName) {
            try {
                if (rooms == null || rooms.Count == 0 || cookie == null || window.isLogin != 1)
                    return;

                var tryCount = 0;

                foreach (var room in rooms) {

                    // 未来の番組は参照しない
                    var now = UnixTime.now;
                    if (room.startedAt > now)
                        continue;

                    var url = $"{Config.URL_TOP}api/live/current_user?room_id={ room.roomId }&_={UnixTime.now / 1000L}";
                    var request = new HttpRequestMessage( HttpMethod.Get, url );
                    request.Headers.Add( "Accept", "application/json" );
                    request.Headers.Add( "Cookie", cookie );
                    var response = await Config.httpClient.SendAsync( request ).ConfigureAwait( false );

                    // レスポンスを受け取った時刻
                    now = UnixTime.now;
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );
                    MyResourceRequestHandler.responceLog( UnixTime.now, url, content );
                    var list = MyResourceRequestHandler.handleCurrentUser( window, now, url, content, fromChecker: true );
                    if (list == null) {
                        // 配信してなかった…
                        log.e( $"checkGiftCount: {itemName} gift list is null! (parse error)" );
                        break;
                    } else if (list.Count == 0) {
                        // 配信してなかった…
                        log.e( $"checkGiftCount: {itemName} gift list is empty! (not in live) retry other room…" );
                        // 他の部屋でリトライしたい
                    } else {
                        break;
                    }

                    // 一定回数以上のリトライは行わない
                    if (++tryCount >= 2) {
                        log.e( $"checkGiftCount: {itemName} too many retry." );
                        break;
                    }
                    await Task.Delay( 3000 );
                    continue;
                }
            } catch (Exception ex) {
                log.e( ex, $"checkGiftCount: {itemName} failed." );
            }
        }

        public void runGifCount() {
            lock (this) {
                if (cookie == null || starRooms == null || seedRooms == null || window.isLogin != 1)
                    return;

                if (lastGiftTask != null && !lastGiftTask.IsCompleted)
                    return;

                var now = UnixTime.now;
                if (now - lastGiftStart < Config.onLiveCheckGiftInterval)
                    return;
                lastGiftStart = now;

                lastGiftTask = Task.Run( async () => {
                    await checkGiftCount( starRooms, "星" );
                    await Task.Delay( 2000 );
                    await checkGiftCount( seedRooms, "種" );
                } );
            }
        }

        //######################################################
        // オンライブ部屋のチェック

        private Int64 lastOnLiveStart = 0L;
        private Task? lastOnLiveTask = null;

        private async Task checkOnLive() {
            try {
                var now = UnixTime.now;
                var url = $"{Config.URL_TOP}api/live/onlives?skip_serial_code_live=1&_={UnixTime.now / 1000L}";
                var request = new HttpRequestMessage( HttpMethod.Get, url );
                request.Headers.Add( "Accept", "application/json" );
                var response = await Config.httpClient.SendAsync( request ).ConfigureAwait( false );
                now = UnixTime.now;
                var content = await response.Content.ReadAsStringAsync().ConfigureAwait( false );

                var tmpStarRooms = new Dictionary<String, Room>();
                var tmpSeedRooms = new Dictionary<String, Room>();
                foreach (JObject genre in JToken.Parse( content ).Value<JArray>( "onlives" )) {
                    foreach (JObject live in genre.Value<JArray>( "lives" )) {
                        var roomUrlKey = live.Value<String>( "room_url_key" );
                        var roomId = live.Value<Int64?>( "room_id" ) ?? -1L;
                        var startedAt = live.Value<Int64?>( "started_at" ) ?? -1L;
                        var officialLevel = live.Value<Int32?>( "official_lv" ) ?? -1;
                        var streamingList = live.Value<JArray>( "streaming_url_list" );

                        if (roomId < 0 || startedAt < 0 || officialLevel < 0)
                            continue;

                        if (startedAt > now || roomUrlKey == null)
                            continue;

                        if (officialLevel == 0) {
                            var old = tmpSeedRooms.GetValueOrDefault( roomUrlKey );
                            tmpSeedRooms[ roomUrlKey ] = new Room(
                                roomId, roomUrlKey, true, startedAt, streamingList ?? old?.streamingList );
                        } else {
                            var old = tmpStarRooms.GetValueOrDefault( roomUrlKey );
                            tmpStarRooms[ roomUrlKey ] = new Room(
                                roomId, roomUrlKey, false, startedAt, streamingList ?? old?.streamingList );
                        }
                    }
                }
                log.d( $"checkOnLive: starRoom={tmpStarRooms.Count},seedRoom={tmpSeedRooms.Count}" );

                static List<Room> mapToList(Dictionary<String, Room> map) {
                    var dst = new List<Room>();
                    dst.AddRange( map.Values );
                    dst.Sort();
                    return dst;
                }

                this.starRooms = mapToList( tmpStarRooms );
                this.seedRooms = mapToList( tmpSeedRooms );

                runGifCount();

            } catch (Exception ex) {
                log.e( ex, "load failed." );
            }
        }

        private void runOnLive() {
            lock (this) {
                if (lastOnLiveTask != null && !lastOnLiveTask.IsCompleted)
                    return;

                var now = UnixTime.now;
                if (now - lastOnLiveStart < Config.onLiveCheckInterval)
                    return;

                lastOnLiveStart = now;
                lastOnLiveTask = Task.Run( async () => await checkOnLive() );
            }
        }

        //######################################################

        // タイマーから定期的に呼ばれる
        internal void run() {
            runOnLive();
            runGifCount();
        }

        // ブラウザイベントから呼ばれる
        internal void setCookie(String cookie) {
            this.cookie = cookie;
            runGifCount();
        }

        // 自動録画チェッカーから呼ばれる
        internal Room? findRoom(String roomName)
            => findRoom( roomName, starRooms ) ?? findRoom( roomName, seedRooms );

        private Room? findRoom(String roomName, List<Room> list)
            => list.Find( (x) => x.roomUrlKey == roomName );

    }
}
