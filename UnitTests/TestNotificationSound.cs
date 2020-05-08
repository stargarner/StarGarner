using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace StarGarner {
    [TestClass]
    public class TestNotificationSound {
        [TestMethod]
        public void TestMethod1() {
            Log.d( "test start" );
            {
                var isSeed = false;
                using var notificationSound = new NotificationSound();
                var file = NotificationSound.liveStart;
                notificationSound.play( isSeed, file );
                Thread.Sleep( 1000 );
                notificationSound.play( isSeed, file );
                while (notificationSound.isPlaying( isSeed, file )) {
                    Thread.Sleep( 1000 );
                }
            }
            Log.d( "test end" );
        }

        [TestMethod]
        public void TestMethod2() {
            Log.d( "test start" );
            {
                var isSeed = true;
                using var notificationSound = new NotificationSound();
                foreach (var c in NotificationSound.counts) {
                    notificationSound.play( isSeed,c );
                }
                Thread.Sleep( 5000 );
            }
            Log.d( "test end" );
        }
    }
}
