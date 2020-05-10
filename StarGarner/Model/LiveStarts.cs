using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace StarGarner.Model {

    public class LiveStarts {

        // 配信開始時刻と3周目オフセットのペア
        public class TimeAndOffset {
            public readonly Int64 time;
            public readonly Int64 offset;

            public TimeAndOffset(Int64 time, Int64 thirdLapOffset) {
                this.time = time;
                offset = thirdLapOffset;
            }
        }

        // 配信開始の時、分と3周目開始オフセット
        private class Item {
            public readonly Int32 hour;
            public readonly Int32 minute;
            public readonly Int64 thirdLapOffset;

            public Item(Int32 hour, Int32 minute, Int64 thirdLapOffset) {
                this.hour = hour;
                this.minute = minute;
                this.thirdLapOffset = thirdLapOffset;
            }

            // 次の開始時刻を調べる
            private Int64 nextStartTime(Int64 now) {
                var dtNow = now.toDateTime();
                var t = new DateTime( dtNow.Year, dtNow.Month, dtNow.Day, hour, minute, 0, 0, DateTimeKind.Local ).toUnixTime();

                var tsMin = thirdLapOffset + Config.startTimeOldLimit;
                var tsMax = UnixTime.day1 - tsMin;

                while (t > now + tsMax)
                    t -= UnixTime.day1;

                while (t < now - tsMin)
                    t += UnixTime.day1;

                return t;
            }

            public TimeAndOffset next(Int64 now) => new TimeAndOffset( nextStartTime( now ), thirdLapOffset );

            //#######################################

            private static readonly Regex reStartTime = new Regex( @"(\d+)\s*\:\s*(\d+)\s*\+\s*(\d+)" );

            public static Item? parse(String src) {
                try {
                    var m = reStartTime.Match( src );
                    if (m.Success) {
                        var hour = ( Int32.Parse( m.Groups[ 1 ].Value ) + 24 ) % 24;
                        var minute = Int32.Parse( m.Groups[ 2 ].Value );
                        var offset = Int64.Parse( m.Groups[ 3 ].Value ) * UnixTime.minute1;
                        if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59 && offset > 0L) {
                            return new Item( hour, minute, offset );
                        }
                    }
                } catch (Exception ex) {
                    Log.e( ex, "StartTimeItem.parse failed." );
                }
                return null;
            }
        }

        private List<Item> list = new List<Item>();

        // 配信開始時刻のリストを更新する
        internal void set(String src) {
            var tmpList = new List<Item>();
            foreach (var col in src.Split( "," )) {
                var st = Item.parse( col );
                if (st != null)
                    tmpList.Add( st );
            }
            list = tmpList;
        }

        // 現在注目している配信はどれか
        internal TimeAndOffset? current(Int64 now) {
            var dst = list.Select( it => it.next( now ) ).ToList();
            dst.Sort( (a, b) => a.time.CompareTo( b.time ) );
            return dst.firstOrNull();
        }

        // 現在注目している配信の次を知りたい
        internal TimeAndOffset? nextOf(Int64 now, Int64 current) {
            var dst = list.Select( it => it.next( now ) ).ToList();
            dst.Sort( (a, b) => a.time.CompareTo( b.time ) );
            // 同一時刻の配信が複数ある場合、timeが一致する最後の配信がcurrentだと解釈する
            for (var i = dst.Count - 1; i >= 0; --i) {
                if (dst[ i ].time == current)
                    return dst.elementOrNull( i + 1 );
            }
            return null;
        }
    }
}
