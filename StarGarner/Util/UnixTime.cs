using System;

namespace StarGarner.Util {

    public static class UnixTime {
        public static readonly DateTime dtEpoch = new DateTime( 1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc );
        public static readonly TimeSpan tsDay = TimeSpan.FromDays( 1 );
        public static readonly TimeSpan tsHour12 = TimeSpan.FromDays( 0.5 );

        public const Int64 day1 = 86400000L;
        public const Int64 hour1 = 3600000L;
        public const Int64 minute1 = 60000L;
        public const Int64 second1 = 1000L;

        public static Int64 toUnixTime(this DateTime dateTime)
            => (Int64)( dateTime.ToUniversalTime() - dtEpoch ).TotalMilliseconds;

        public static DateTime toDateTime(this Int64 t)
            => ( dtEpoch + TimeSpan.FromMilliseconds( t ) ).ToLocalTime();

        public static Int64 now => DateTime.Now.toUnixTime();

        public static String formatFileTime(this DateTime dt)
            => String.Format( "{0}{1:00}{2:00}T{3:00}{4:00}{5:00}", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second );

        public static String formatFileTime(this Int64 t)
            => t.toDateTime().formatFileTime();

        public static String formatTime(this DateTime dt, Boolean showMillisecond = false)
            => showMillisecond
            ? String.Format( "{0}h{1:00}m{2:00}.{3:000}s", dt.Hour, dt.Minute, dt.Second, dt.Millisecond )
            : String.Format( "{0}h{1:00}m{2:00}.{3:0}s", dt.Hour, dt.Minute, dt.Second, dt.Millisecond / 100L )
            ;

        public static String formatTime(this Int64 t, Boolean showMillisecond = false)
            => t.toDateTime().formatTime( showMillisecond );

        public static String formatDuration(this Int64 t) {
            var sign = t < 0 ? "-" : "";

            t = Math.Abs( t );

            var h = t / hour1;
            t %= hour1;

            var m = t / minute1;
            t %= minute1;

            var s = t / second1;
            t %= second1;

            if (h > 0) {
                return String.Format( "{0}{1}h{2}m{3}.{4}s", sign, h, m, s, t / 100L );
            } else if (m > 0) {
                return String.Format( "{0}{1}m{2}.{3}s", sign, m, s, t / 100L );
            } else {
                return String.Format( "{0}{1}.{2}s", sign, s, t / 100L );
            }
        }
    }
}
