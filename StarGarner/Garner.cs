using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace StarGarner {

    // garner for star or seed.
    public class Garner {
        // 星 or 種
        internal readonly Boolean isSeed;
        internal readonly String itemName;
        internal readonly String itemNameEn;
        internal readonly String jsonFile;


        // 最近開いた部屋のリスト
        internal readonly List<String> lastRooms = new List<String>();

        // 「強制的に開く」フラグ
        internal String? forceOpenReason = null;

        // ギフト取得時刻の履歴
        public readonly GiftHistory giftHistory;

        // 配信開始時刻のリスト
        public readonly LiveStarts liveStarts = new LiveStarts();

        public readonly GiftCounts giftCounts;

        // 取得制限の解除時刻
        internal Int64 expireExceed;

        internal Int64 lastPlayHistoryClear;
        internal Int64 lastPlayLiveStart;
        internal Int64 lastPlayThirdLap;

        internal String soundActor;

        // データをファイルに保存する
        internal void save()
            => new JObject {
                { Config.KEY_EXPIRE_EXCEED, expireExceed.ToString() },
                { Config.KEY_HISTORY, giftHistory.encodeJson() },
                { Config.KEY_LAST_PLAY_HISTORY_CLEAR, lastPlayHistoryClear.ToString() },
                { Config.KEY_LAST_PLAY_LIVE_START, lastPlayLiveStart.ToString() },
                { Config.KEY_LAST_PLAY_THIRD_LAP, lastPlayThirdLap.ToString() },
                { Config.KEY_SOUND_ACTOR, soundActor },
            }.saveTo( jsonFile );

        // 初期化
        internal Garner(Boolean isSeed) {
            this.isSeed = isSeed;
            this.itemName = isSeed ? "種" : "星";
            this.itemNameEn = isSeed ? "seed" : "star";
            this.jsonFile = $"{itemNameEn}.json";
            this.soundActor = isSeed ? "akane" : "sora";
            this.giftHistory = new GiftHistory( itemName );
            this.giftCounts = new GiftCounts( itemName );

            try {
                if (File.Exists( jsonFile )) {
                    var root = Utils.loadJson( jsonFile );

                    var historyList = root.Value<JArray>( Config.KEY_HISTORY );
                    if (historyList != null) {
                        giftHistory.load( historyList );
                        giftHistory.dump( "load" );
                    }

                    Int64 parseTime(String key) {
                        var sv = root.Value<String?>( key );
                        if (sv != null && sv.Length > 0)
                            return Int64.Parse( sv );
                        return 0L;
                    }

                    var sv = root.Value<String?>( Config.KEY_SOUND_ACTOR );
                    if (sv != null && sv.Length > 0)
                        soundActor = sv;

                    expireExceed = parseTime( Config.KEY_EXPIRE_EXCEED );
                    lastPlayHistoryClear = parseTime( Config.KEY_LAST_PLAY_HISTORY_CLEAR );
                    lastPlayLiveStart = parseTime( Config.KEY_LAST_PLAY_LIVE_START );
                    lastPlayThirdLap = parseTime( Config.KEY_LAST_PLAY_THIRD_LAP );
                }
            } catch (Exception ex) {
                Log.e( ex, $"{itemName} load failed." );
            }
        }

        // 最近開いた部屋を覚えておく
        internal void addLastRoom(Room room) {
            lock (lastRooms) {
                lastRooms.Add( room.roomUrlKey );
                if (lastRooms.Count > 300)
                    lastRooms.RemoveAt( 0 );
            }
        }

        // 通知音を既に鳴らしたならfalse,鳴らすべきならtrue
        internal Boolean willPlayLiveStart(Int64 time) {
            if (lastPlayLiveStart != time) {
                lastPlayLiveStart = time;
                save();
                return true;
            }
            return false;
        }

        // 通知音を既に鳴らしたならfalse,鳴らすべきならtrue
        internal Boolean willPlayThirdLap(Int64 time) {
            if (lastPlayThirdLap != time) {
                lastPlayThirdLap = time;
                save();
                return true;
            }
            return false;
        }

        internal Boolean willPlayHistoryClear(Int64 now, Boolean cleared) {
            if (cleared) {
                if (lastPlayHistoryClear == 0L) {
                    lastPlayHistoryClear = now;
                    save();
                    return true;
                }
            } else {
                if (lastPlayHistoryClear != 0L) {
                    lastPlayHistoryClear = 0L;
                    save();
                }
            }
            return false;
        }

        // 取得制限を更新する
        internal void setExceed(Int64 now, Int64 expire) {
            // 制限解除が解除予測と近い位置にあるなら、秒部分を補う
            var expectedReset = giftHistory.expectedReset( now );
            if (expectedReset > expire && expectedReset < expire + UnixTime.minute1) {
                expire = expectedReset;
            }

            expireExceed = expire;
            giftHistory.clear();
            save();
        }


        // ギフト取得回数を増やす
        internal Int32 increment(Int64 now) {
            expireExceed = 0L;

            giftCounts.increment( now );

            giftHistory.increment( now );
            giftHistory.dump( "increment" );
            save();
            return giftHistory.count();
        }

        internal class EventTime : IComparable<EventTime> {
            internal Int32 order;
            internal String name;
            internal Int64 time;
            internal Int64 remain;

            internal EventTime(Int32 order, String name, Int64 time, Int64 remain) {
                this.order = order;
                this.name = name;
                this.time = time;
                this.remain = remain;
            }

            public Int32 CompareTo(EventTime other)
                => time.CompareTo( other.time ).notZero() ?? order.CompareTo( other.order );
        }

        // 現在の状態を文字列に出力する
        internal StatusCollection dumpStatus(Int64 now) {
            var expireExceed = this.expireExceed;
            var remainExpireExceed = expireExceed - now;
            var hasExceed = remainExpireExceed > 0L;
            var expectedReset = giftHistory.expectedReset( now );

            var sc = new StatusCollection();
            sc.addRun( $"{itemName} 所持数 {giftCounts.sumInTime( now )?.ToString() ?? "不明"} " );
            giftHistory.addCountTo( sc, hasExceed );
            /*
                        var hyperLink = new Hyperlink() {
                            NavigateUri = new Uri( $"stargarner://{itemNameEn}/settings" )
                        };
                        hyperLink.Inlines.Add( "設定" );
                        hyperLink.RequestNavigate += (sender, e) => window.openSetting(this);
                        sc.addLink( hyperLink );
                        */
            giftHistory.addTo( sc );


            var eventList = new List<EventTime>();
            if (hasExceed)
                eventList.Add( new EventTime( 1, "制限解除", expireExceed, remainExpireExceed ) );

            if (!hasExceed || expectedReset > expireExceed) {
                var delta = expectedReset - now;
                if (delta > 0L)
                    eventList.Add( new EventTime( 2, "解除予測", expectedReset, delta ) );
            }

            do {
                var st = liveStarts.current( now );

                if (st == null)
                    break; // 配信予定がない

                var firstLap = st.time + st.offset - UnixTime.hour1 * 2L;
                var delta = firstLap - now;
                if (delta > 0L) {
                    eventList.Add( new EventTime( 3, "1周目集め", firstLap, delta ) );
                    break;
                }

                var dropTime = st.time + st.offset - UnixTime.hour1;
                delta = dropTime - now;
                if (delta > 0L) {
                    eventList.Add( new EventTime( 4, "捨て時刻", dropTime, delta ) );
                    break;
                }

                delta = st.time - now;
                if (delta > 0L) {
                    eventList.Add( new EventTime( 5, "配信開始", st.time, delta ) );
                    break;
                }

                var thrdLapStart = st.time + st.offset;
                delta = thrdLapStart - now;
                // 3周目は過去になっても表示する
                eventList.Add( new EventTime( 6, "3周目開始", thrdLapStart, delta ) );

            } while (false);

            eventList.Sort();

            foreach (var ev in eventList) {
                sc.add( $"{ev.time.formatTime()} 残{ev.remain.formatDuration()} {ev.name}" );
            }

            return sc;
        }
    }
}
