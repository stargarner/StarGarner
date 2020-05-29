using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using uhttpsharp.Attributes;

namespace StarGarner.Util {

    public class Log {
        private const String logFile = "StarGarner.log";
        private static readonly Object lockObject = new Object();
        private static StreamWriter? writer;

        private static void log(String prefix, String level, String msg) {
            var line = $"{DateTime.Now.formatTime()}/{level} {prefix} {msg}";
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

        private static void log(String prefix, String level, Exception ex, String msg) {
            var line = $"{DateTime.Now.formatTime()}/{level} {prefix} {msg}";
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

        static Log() {
            try {
                writer = new StreamWriter( logFile, true, Encoding.UTF8 );
            } catch (Exception ex) {
                writer = null;
                log( "", "E", ex, $"can't open log file. {logFile}" );
            }
        }

        private readonly String prefix = "";

        public Log(String prefix) => this.prefix = prefix;

        public void d(String msg) => log( prefix, "D", msg );
        public void e(String msg) => log( prefix, "E", msg );
        public void e(Exception ex, String msg) => log( prefix, "E", ex, msg );
    }
}
