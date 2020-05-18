using NAudio.Wave;
using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace StarGarner {
    public class NotificationSound : IDisposable {

        public const String liveStart = "liveStart.m4a";
        public const String exceedReset = "exceedReset.m4a";
        public const String thirdLap = "thirdLap.m4a";
        public const String exceedError = "exceedError.m4a";

        public static readonly List<String> counts = Enumerable.Range( 0, 11 ).Select( x => $"count-{x}.m4a" ).ToList();

        public static readonly List<String> all = allSoundName();

        private static List<String> allSoundName() {
            var dst = new List<String>() { liveStart, exceedReset, thirdLap, exceedError };
            dst.AddRange( counts );
            return dst;
        }

        private static readonly String? soundDir = findSoundDirectory();

        // CWDから上に辿ってsoundフォルダを探す
        private static String? findSoundDirectory() {
            static String? sub(String? d) {
                while (d != null) {

                    var soundDir = Path.Combine( d, "sound" );

                    if (Directory.Exists( soundDir ))
                        return soundDir;

                    try {
                        d = Directory.GetParent( d )?.FullName;
                    } catch (Exception ex) {
                        Log.e( ex, "Directory.GetParent failed." );
                        break;
                    }
                }
                return null;
            }
            return sub( Directory.GetCurrentDirectory() ) ?? sub( Assembly.GetEntryAssembly()?.Location );
        }

        public static readonly List<String> actors = new List<String>() {
            "none","akane","aoi","sora","yukari"
        };

        private static readonly HashSet<String> actorsMap = actors.ToHashSet();

        private static String? getSoundFile(String actorName, String file)
            => soundDir == null ? null :
                actorName == "none" || !actorsMap.Contains( actorName ) ? null :
                $"{soundDir}/{actorName}-{file}";

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

        private void stop(String fullPath) {
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

        public void play(String actorName, String file) {
            var fullPath = getSoundFile( actorName, file );
            if (fullPath == null)
                return;

            stop( fullPath );

            lock (playerMap) {
                try {
                    playerMap[ fullPath ] = new PlayingInfo( fullPath );
                } catch (Exception ex) {
                    Log.e( ex, "NotificationSound.play failed." );
                }
            }
        }

        public Boolean isPlaying(String actorName, String file) {
            var fullPath = getSoundFile( actorName, file );
            if (fullPath == null)
                return false;

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
