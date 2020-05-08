using System;
using System.Collections.Generic;
using System.Text;

namespace StarGarner {

    public class GiftCounts {

        private readonly String itemName;

        // ギフト所持数を読んだ時刻
        public Int64 updatedAt;

        // ギフトIDと所持数のマップ
        private Dictionary<Int32, Int32>? map = null;

        // 所持数のダイジェスト
        private String? lastDigest;

        public GiftCounts(String itemName) => this.itemName = itemName;

        // ギフト所持数を最近読んだなら真
        public Boolean isInTime(Int64 now)
            => now - updatedAt < Config.giftCountInvestigateInterval;

        // ギフト所持数を最近読んだならその合計値、もしくはnull
        public Int32? sumInTime(Int64 now)
            => isInTime( now ) ? sum() : (Int32?)null;

        // ギフト所持数の合計値、もしくはnull
        public Int32? sum() {
            var map = this.map;

            if (map == null)
                return null;

            var sum = 0;
            foreach (var k in map.Keys) {
                sum += map[ k ];
            }
            return sum;
        }

        // ギフト所持数のダイジェスト文字列
        private static String makeDigest(Dictionary<Int32, Int32> src) {
            var keys = new List<Int32>( src.Keys );
            keys.Sort();
            var sb = new StringBuilder();
            foreach (var k in keys) {
                sb.Append( String.Format( "{0}={1},", k, src[ k ] ) );
            }
            return sb.ToString();
        }

        // ギフト所持数を更新する
        // 最新の情報を得たら1、関連ギフトの情報を含まなかったら0
        public Int32 set(Int64 now, Dictionary<Int32, Int32> src) {
            if (src.Count == 0)
                return 0;

            // make digest
            var digest = makeDigest( src );
            if (digest != lastDigest) {
                lastDigest = digest;
                map = src;
                Log.d( $"GiftCounts.set {itemName} {digest}" );
            }

            // 調査完了を検知するため、変化がなくても更新時刻は上書きする
            updatedAt = now;
            return 1;
        }

        // ギフト取得
        public void increment(Int64 now) {
            var oldCounts = map;
            if (oldCounts == null)
                return;

            var newCounts = new Dictionary<Int32, Int32>();
            foreach (var pair in oldCounts) {
                newCounts[ pair.Key ] = Math.Min( 99, 10 + pair.Value );
            }
            set( now, newCounts );

        }
    }
}
