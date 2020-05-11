using StarGarner.Dialog;
using StarGarner.Util;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using uhttpsharp;
using uhttpsharp.Handlers;
using uhttpsharp.Listeners;
using uhttpsharp.RequestProviders;

namespace StarGarner {

    internal class MyHttpServer {

        public volatile Boolean enabled = false;
        public volatile String listenAddr = "0.0.0.0";
        public volatile String listenPort = "8485";

        private volatile Task? lastTask = null;
        private volatile Boolean isCancelled = false;

        public volatile String? serverError = null;

        private readonly MainWindow window;

        public MyHttpServer(MainWindow window) => this.window = window;

        private void notifyServerStatus() {
            OtherSettingDialog? d = null;
            window.refSettingDialogOther?.TryGetTarget( out d );
            d?.showServerStatus();
        }

        public void updateListening() => Task.Run( () => {
            try {
                if (lastTask != null && !lastTask.IsCompleted) {
                    Log.d( "updateListening: cancel last task…" );
                    isCancelled = true;
                    lastTask?.Wait();
                    Log.d( "updateListening: last task was cancelled." );
                }
            } catch (Exception ex) {
                Log.e( ex, "closing failed." );
            }

            isCancelled = false;
            serverError = null;
            notifyServerStatus();

            if (!enabled) {
                Log.d( "updateListening: not enabled." );
                return;
            }

            lastTask = Task.Run( async () => {
                try {
                    Log.d( $"updateListening: listen to {listenAddr} port {listenPort}…" );
                    var router = new HttpRouter();

                    router.With( "status", window.onStatus );

                    var addr = IPAddress.Parse( listenAddr );
                    var port = Int32.Parse( listenPort );
                    var tcpListener = new TcpListener( addr, port );

                    using var httpServer = new HttpServer( new HttpRequestProvider() );

                    httpServer.Use( new TcpListenerAdapter( tcpListener ) );
                    httpServer.Use( router );
                    httpServer.Use( (context, next) => {
                        context.Response = new HttpResponse( HttpResponseCode.NotFound, "Not found", closeConnection: true );
                        return Task.Factory.GetCompleted();
                    } );

                    httpServer.Start();
                    while (!isCancelled) {
                        await Task.Delay( 333 );
                    }
                    Log.d( $"updateListening: server cancelled." );
                } catch (Exception ex) {
                    Log.e( ex, "http server error." );
                    serverError = ex.ToString();
                    notifyServerStatus();
                }
            } );
        } );
    }
}
