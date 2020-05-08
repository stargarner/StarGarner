using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace StarGarner {

    public class GiftHistory {
        private class Item : IComparable<Item> {

            public readonly Int64 time;
            public readonly Int32 count;

            public Item(Int64 time, Int32 count) {
                this.time = time;
                this.count = count;
            }

            public JObject encodeJson()
                => new JObject {
                { Config.KEY_TIME, time.ToString() },
                { Config.KEY_COUNT, count }
                };

            public static Item? decodeJson(JObject item) {
                try {
                    return new Item(
                        Int64.Parse( item.Value<String>( Config.KEY_TIME ) ),
                        item.Value<Int32>( Config.KEY_COUNT )
                        );
                } catch (Exception ex) {
                    Log.e( ex, "History.decodeJson failed." );
                    return null;
                }
            }

            public Int32 CompareTo(Item other) => time.CompareTo( other.time );

            public override String ToString() => $"{time.formatTime()} {count}";
        }

        private readonly List<Item> list = new List<Item>();

        private readonly String itemName;

        public GiftHistory(String itemName) => this.itemName = itemName;

        public Int32 count() => list.Count;

        // 取得履歴をStatusCollectionに追加する
        public void addTo(StatusCollection sc, Boolean hasExceed) {

            var countStr = hasExceed ? "X" : list.Count.ToString();
            sc.addRun( $"取得履歴 {countStr}" );

            if (list.Count > 0) {
                sc.add( String.Join( ", ", list ), fontSize: Config.giftHistoryFontSize );
            }
        }

        // 取得履歴をログに出力する
        public void dump(String caption) {
            foreach (var h in list) {
                Log.d( $"{caption} {itemName} {h}" );
            }
        }

        // JSONデータにエンコード
        public JArray encodeJson() {
            var dst = new JArray();
            foreach (var item in list) {
                dst.Add( item.encodeJson() );
            }
            return dst;
        }

        // JSONデータをデコードして内容を取り込む
        public void load(JArray src) {
            foreach (JObject item in src) {
                var h = Item.decodeJson( item );
                if (h != null)
                    list.Add( h );
            }
            list.Sort();

            trim( UnixTime.now );
        }

        // 内容をクリア
        public void clear() => list.Clear();

        // trim old unnecessary entry.
        private Item? trim(Int64 now, Boolean add = false) {
            Item? lastStart = null;

            lock (list) {
                // count==1の要素を調べる
                for (var i = list.Count - 1; i >= 0; --i) {
                    var h = list[ i ];
                    if (h.count == 1) {
                        lastStart = h;
                        break;
                    }
                }

                if (lastStart == null || lastStart.time < now - UnixTime.hour1 * 1L - 1000L) {
                    // count==1の要素がないか、古すぎるなら履歴を初期化する
                    if (list.Count > 0) {
                        Log.d( "History.trim: history initialize!" );
                        list.Clear();
                        lastStart = null;
                    }
                } else {
                    var removeCount = 0;
                    // lastStartより古い要素は要らないので削除する
                    for (var i = list.Count - 1; i >= 0; --i) {
                        if (list[ i ].time < lastStart.time) {
                            list.RemoveAt( i );
                            ++removeCount;
                        }
                    }
                    if (removeCount > 0) {
                        Log.d( $"History.trim: history remove {removeCount}" );
                    }
                }

                if (add) {
                    var newCount = 1 + ( list.lastOrNull()?.count ?? 0 );
                    list.Add( new Item( now, newCount > 10 ? 1 : newCount ) );
                }
            }

            return lastStart;
        }

        // 履歴を追加
        public void increment(Int64 now) => trim( now, true );

        // 解除予測の変化時にログ出力..
        private Int64 lastExpectedReset;

        // 解除予測時刻を調べる
        // 副作用として古い取得履歴を削除する
        // count() を呼び出す前に必ずこのメソッドを呼び出す必要がある
        internal Int64 expectedReset(Int64 now) {
            // count==1の要素を調べる
            var lastStart = trim( now );
            var newValue = lastStart != null ? lastStart.time + UnixTime.hour1 : 0L;

            // 解除予測の変化時にログ出力
            if (lastExpectedReset != newValue) {
                lastExpectedReset = newValue;
                var delta = newValue - now;
                if (newValue == 0L) {
                    Log.d( $"{itemName} 解除予測が変わりました。 0" );
                } else {
                    Log.d( $"{itemName} 解除予測が変わりました。{newValue.formatTime()} 残り{delta.formatDuration()}" );
                }
            }

            return newValue;
        }
    }
}
