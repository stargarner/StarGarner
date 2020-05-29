using NAudio.Wave;
using StarGarner.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace StarGarner {

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

    public class NotificationSound : IDisposable {
        static readonly Log log = new Log( "NotificationSound" );

        public const String exceedError = "exceedError.m4a";
        public const String exceedReset = "exceedReset.m4a";
        public const String liveStart = "liveStart.m4a";
        public const String thirdLap = "thirdLap.m4a";
        public const String recordingStart = "recordingStart.m4a";
        public const String recordingEnd = "recordingEnd.m4a";

        public static readonly List<String> counts = Enumerable.Range( 0, 11 ).Select( x => $"count-{x}.m4a" ).ToList();

        public static readonly List<String> all = allSoundName();

        public static readonly List<String> allOther = new List<String>() { recordingStart, recordingEnd };

        public static readonly List<String> allGarner = allSoundName( exclude: allOther );

        private static List<String> allSoundName(List<String>? exclude = null) {
            var dst = new List<String>() { exceedError, exceedReset, liveStart, thirdLap, recordingStart, recordingEnd };
            dst.AddRange( counts );
            exclude?.ForEach( (it) => dst.Remove( it ) );
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
                        log.e( ex, "Directory.GetParent failed." );
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

        internal static String? getSoundFile(String actorName, String file)
            => soundDir == null ? null :
                actorName == "none" || !actorsMap.Contains( actorName ) ? null :
                $"{soundDir}/{actorName}-{file}";

        private Boolean isDisposed = false;

        private readonly ConcurrentDictionary<String, PlayingInfo> playerMap = new ConcurrentDictionary<String, PlayingInfo>();

        private void stop(String fullPath) {
            try {
                playerMap.removeOrNull( fullPath )?.Dispose();
            } catch (Exception ex) {
                log.e( ex, "stop failed." );
            }
        }

        public void play(String actorName, String file) {
            if (isDisposed)
                return;

            var fullPath = getSoundFile( actorName, file );
            if (fullPath == null)
                return;

            stop( fullPath );

            try {
                playerMap[ fullPath ] = new PlayingInfo( fullPath );
            } catch (Exception ex) {
                log.e( ex, "play failed." );
            }
        }

        public Boolean isPlaying(String actorName, String file) {
            var fullPath = getSoundFile( actorName, file );
            if (fullPath == null)
                return false;

            return playerMap.getOrNull( fullPath )?.isPlaying() ?? false;
        }

        public void Dispose() {
            log.d( "Dispose" );
            isDisposed = true;
            playerMap.Values.ForEach( (it) => it.Dispose() );
        }
    }
}
