using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace StarGarner {
    public class NotificationSound : IDisposable {

        public const String liveStart = "liveStart.m4a";
        public const String exceedReset = "exceedReset.m4a";
        public const String thirdLap = "thirdLap.m4a";
        public const String exceedError = "exceedError.m4a";

        public static readonly List<String> counts = Enumerable.Range( 0, 11 ).Select( x => $"count-{x}.m4a" ).ToList();

        private static readonly String soundDir = findSoundDirectory();

        // CWDから上に辿ってsoundフォルダを探す
        private static String findSoundDirectory() {
            var d = Directory.GetCurrentDirectory();
            while (true) {
                var soundDir = Path.Combine( d, "sound" );
                if (Directory.Exists( soundDir ))
                    return soundDir;
                try {
                    var di = Directory.GetParent( d );
                    if (di == null)
                        throw new DirectoryNotFoundException( "can't find parent directory." );
                    d = di.FullName;
                } catch (Exception ex) {
                    Log.e( ex, "findSoundDirectory failed." );
                    throw ex;
                }
            }
        }

        private static String getSoundFile(Boolean isSeed, String file) {
            var who = isSeed ? "akane" : "sora";
            return $"{soundDir}/{who}-{file}";
        }

        public class PlayingInfo : IDisposable {
            AudioFileReader? reader;
            WaveOutEvent? output;

            public PlayingInfo(String fullPath) {
                try {
                    this.reader = new AudioFileReader( fullPath );
                    this.output = new WaveOutEvent();
                    output.PlaybackStopped += (sender, e) => Dispose();
                    output.Init( reader );
                    output.Play();
                } catch (Exception ex) {
                    Dispose();
                    throw ex;
                }
            }

            public void Dispose() {
                lock (this) {
                    try {
                        output?.Stop();
                        output?.Dispose();
                        output = null;
                    } catch (Exception) { }

                    try {
                        reader?.Dispose();
                        reader = null;
                    } catch (Exception) { }
                }
            }

            internal Boolean isPlaying() {
                lock (this) {
                    try {
                        return output?.PlaybackState == PlaybackState.Playing;
                    } catch (Exception ex) {
                        Dispose();
                        throw ex;
                    }
                }
            }
        }

        private readonly Dictionary<String, PlayingInfo> playerMap = new Dictionary<String, PlayingInfo>();

        public void stop(Boolean isSeed, String file) {
            var fullPath = getSoundFile( isSeed, file );
            try {
                lock (playerMap) {
                    playerMap.TryGetValue( fullPath, out var p );
                    if (p != null) {
                        playerMap.Remove( fullPath );
                        p.Dispose();
                    }
                }
            } catch (Exception ex) {
                Log.e( ex, "NotificationSound.stop failed." );
            }
        }

        public void play(Boolean isSeed, String file) {
            stop( isSeed, file );
            var fullPath = getSoundFile( isSeed, file );
            lock (playerMap) {
                try {
                    playerMap[ fullPath ] = new PlayingInfo( fullPath );
                } catch (Exception ex) {
                    Log.e( ex, "NotificationSound.play failed." );
                }
            }
        }

        public Boolean isPlaying(Boolean isSeed, String file) {
            var fullPath = getSoundFile( isSeed, file );
            lock (playerMap) {
                playerMap.TryGetValue( fullPath, out var p );
                return p?.isPlaying() ?? false;
            }
        }

        public void Dispose() {
            Log.d( "NotificationSound.Dispose" );
            lock (playerMap) {
                foreach (var entry in playerMap) {
                    entry.Value.Dispose();
                }
                playerMap.Clear();
            }
        }
    }
}
