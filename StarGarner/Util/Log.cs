using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace StarGarner.Util {

    public static class Log {
        private const String logFile = "StarGarner.log";
        private static readonly Object lockObject = new Object();
        private static StreamWriter? writer;

        static Log() {
            try {
                writer = new StreamWriter( logFile, true, Encoding.UTF8 );
            } catch (Exception ex) {
                writer = null;
                e( ex, $"can't open log file. {logFile}" );
            }
        }

        private static void log(String level, String msg) {
            var line = $"{DateTime.Now.formatTime()}/{level} {msg}";
            lock (lockObject) {
                Debug.WriteLine( line );
                try {
                    writer?.WriteLine( line );
                    writer?.Flush();
                } catch (Exception) {
                    writer = null;
                }
            }
        }

        private static void log(String level, Exception ex, String msg) {
            var line = $"{DateTime.Now.formatTime()}/{level} {msg}";
            lock (lockObject) {
                Debug.WriteLine( line );
                Debug.WriteLine( ex.ToString() );
                try {
                    writer?.WriteLine( line );
                    writer?.WriteLine( ex.ToString() );
                    writer?.Flush();
                } catch (Exception) {
                    writer = null;
                }
            }
        }

        public static void d(String msg) => log( "D", msg );
        public static void e(String msg) => log( "E", msg );
        public static void e(Exception ex, String msg) => log( "E", ex, msg );
    }
}
