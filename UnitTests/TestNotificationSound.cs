using Microsoft.VisualStudio.TestTools.UnitTesting;
using StarGarner.Util;
using System.Threading;

namespace StarGarner {
    [TestClass]
    public class TestNotificationSound {
        static readonly Log log = new Log( "TestNotificationSound" );
        [TestMethod]
        public void TestMethod1() {
            log.d( "test start" );
            {
                var actor = "sora";
                using var notificationSound = new NotificationSound();
                var file = NotificationSound.liveStart;
                notificationSound.play( actor, file );
                Thread.Sleep( 1000 );
                notificationSound.play( actor, file );
                while (notificationSound.isPlaying( actor, file )) {
                    Thread.Sleep( 1000 );
                }
            }
            log.d( "test end" );
        }

        [TestMethod]
        public void TestMethod2() {
            log.d( "test start" );
            {
                var actor = "akane";
                using var notificationSound = new NotificationSound();
                foreach (var c in NotificationSound.counts) {
                    notificationSound.play( actor, c );
                }
                Thread.Sleep( 5000 );
            }
            log.d( "test end" );
        }

        [TestMethod]
        public void TestSoundfileExists() {
            foreach(var soundName in NotificationSound.all) {
                foreach (var actor in NotificationSound.actors) {
                    if (actor == "none")
                        continue;
                    var file = NotificationSound.getSoundFile( actor, soundName );
                    Assert.IsNotNull( file );
                    log.e( $"file={file}" );
                }
            }
        }
    }
}
