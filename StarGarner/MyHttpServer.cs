using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using uhttpsharp;
using uhttpsharp.Handlers;
using uhttpsharp.Listeners;
using uhttpsharp.RequestProviders;

namespace StarGarner {

    internal class MyHttpServer {
        static readonly Log log = new Log( "MyHttpServer" );

        public volatile String serverStatus = "?";
        public volatile Boolean enabled = false;
        public volatile String listenAddr = "0.0.0.0";
        public volatile String listenPort = "8485";

        private readonly MainWindow window;
        private readonly HttpRouter router = new HttpRouter();
        private volatile HttpServer? httpServer = null;

        public MyHttpServer(MainWindow window) {
            this.window = window;

            router.With( "status", window.onHttpStatus );
            router.With( "startTime", window.onHttpStartTime );
            router.With( "forceOpen", window.onHttpForceOpen );
        }

        String getMyAddress() {
            var list = new List<String>();
            try {
                var ipentry = Dns.GetHostEntry( Dns.GetHostName() );

                foreach (var ip in ipentry.AddressList) {
                    try {
                        list.Add( $"\n{ip}" );
                    }catch(Exception ex) {
                        log.e( ex, "can't get ip address" );
                    }
                }
            }catch(Exception ex) {
                log.e( ex, "getMyAddress failed." );
            }
            return String.Join( "", list );
        }


        private void setStatus(String s) {
            serverStatus = s;
            window.refSettingDialogOther?.getOrNull()?.showServerStatus();
        }

        public void updateListening() => Task.Run( () => {
            lock (this) {
                try {
                    if (httpServer != null) {
                        log.d( "updateListening: dispose last server…" );
                        httpServer.Dispose();
                    }
                } catch (Exception ex) {
                    log.e( ex, "closing failed." );
                } finally {
                    httpServer = null;
                }

                if (!enabled) {
                    log.d( "updateListening: not enabled." );
                    setStatus( "not enabled." );
                    return;
                }

                IPAddress addr;
                try {
                    addr = IPAddress.Parse( listenAddr );
                } catch (Exception ex) {
                    log.e( ex, "listenAddr parse error." );
                    setStatus( $"待機アドレスを解釈できません。\n{ex}" );
                    return;
                }

                Int32 port;
                try {
                    port = Int32.Parse( listenPort );
                } catch (Exception ex) {
                    log.e( ex, "listenPort parse error." );
                    setStatus( $"待機ポートを解釈できません。\n{ex}" );
                    return;
                }

                try {
                    log.d( $"updateListening: listen to {listenAddr} port {listenPort}…" );
                    setStatus( "initializing…" );

                    var tcpListener = new TcpListener( addr, port );
                    tcpListener.Server.SetSocketOption( SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true );

                    this.httpServer = new HttpServer( new HttpRequestProvider() );
                    httpServer.Use( new TcpListenerAdapter( tcpListener ) );
                    httpServer.Use( router );
                    httpServer.Use( (context, next) => {
                        context.Response = new HttpResponse( HttpResponseCode.NotFound, "Not found", closeConnection: true );
                        return Task.Factory.GetCompleted();
                    } );

                    httpServer.Start();
                    setStatus( $"listening {listenAddr} port {listenPort}\nmaybe your addresses are:{getMyAddress()}" );
                } catch (Exception ex) {
                    log.e( ex, "can't start http server." );
                    setStatus( ex.ToString() );
                }
            }
        } );
    }
}
