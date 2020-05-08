using System;
using System.Threading.Tasks;
using System.Windows;

namespace StarGarner {

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {
        private static void handleException(Exception? ex, String caughtBy) {
            if (ex == null) {
                Log.e( $"caught by {caughtBy}, but Exception is null!!" );
            } else {
                Log.e( ex, $"(caught by{caughtBy})" );
            }
            Environment.Exit( 1 );
        }

        public App() {

            // UI スレッドで実行されているコードで処理されなかったら発生する（.NET 3.0 より）
            DispatcherUnhandledException += (sender, ev) => handleException(
                   ev?.Exception,
                    "Application.DispatcherUnhandledException"
                    );

            // バックグラウンドタスク内で処理されなかったら発生する（.NET 4.0 より）
            TaskScheduler.UnobservedTaskException += (sender, ev) => handleException(
                    ev.Exception?.InnerException ?? ev.Exception,
                    "TaskScheduler.UnobservedTaskException"
                    );

            // 例外が処理されなかったら発生する（.NET 1.0 より）
            AppDomain.CurrentDomain.UnhandledException += (sender, ev) => handleException(
                   ev?.ExceptionObject as Exception,
                   "AppDomain.CurrentDomain.UnhandledException"
                   );
        }
    }
}
