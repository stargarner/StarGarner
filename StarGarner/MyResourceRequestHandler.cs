using CefSharp;
using CefSharp.Handler;
using CefSharp.ResponseFilter;
using Newtonsoft.Json.Linq;
using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StarGarner {
    public class MyResourceRequestHandler : ResourceRequestHandler {
        private static readonly Regex reFileNameUnsafe = new Regex( @"[\/:*?""<>|]+" );
        private static readonly Regex reExceedError = new Regex( @"無料ギフトの獲得は(\d+):(\d+)まで制限されています" );
        private static readonly Regex reLiveData = new Regex( @"<script id=""js-live-data"" data-json=""([^""]+)", RegexOptions.Singleline );

        // API応答のログをファイルに保存するなら真
        public static volatile Boolean responseLogEnabled = false;

        private readonly MemoryStream memoryStream = new MemoryStream();
        private readonly MainWindow window;

        public MyResourceRequestHandler(MainWindow window)
            => this.window = window;

        protected override IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response) => new StreamResponseFilter( memoryStream );

        protected override void OnResourceLoadComplete(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            IResponse response,
            UrlRequestStatus status,
            Int64 receivedContentLength
        ) {
            try {
                var now = UnixTime.now;
                checkResponse( request.Url, response.Headers, now, Encoding.UTF8.GetString( memoryStream.ToArray() ) );

                var cookie = request.Headers[ "Cookie" ];
                if (cookie != null) {
                    window.onRequestCookie( cookie );
                }

            } catch (Exception ex) {
                Log.e( ex, "OnResourceLoadComplete failed." );
            }
        }


        // CefSharpのハンドラから呼ばれる。傍受したレスポンスを解釈する。
        private void checkResponse(String url, NameValueCollection responseHeaders, Int64 now, String content) {
            var contentType = responseHeaders[ "Content-Type" ];
            if (contentType == null)
                return;

            // Log.d( $"OnResourceLoadComplete {request.Url} {contentType}" );

            responceLog( now, url, content );

            if (contentType.StartsWith( "text/html" )) {
                // var cookie = response.Headers[ "Set-Cookie" ];
                // if (cookie != null) {
                //     Log.d( $"cookie={cookie}" );
                // }

                if (content.Contains( "class=\"side-user-data-id\"" )) {
                    window.onLogin( true );
                } else if (content.Contains( "class=\"side-nonuser-head\">" )) {
                    window.onLogin( false );
                }

                var src = reLiveData.matchOrNull( content )?.Groups[ 1 ].Value.decodeEntity();
                if (src != null) {
                    try {
                        var list = new List<JObject>();
                        foreach (JObject gift in JToken.Parse( src ).Value<JArray>( "gift_list" )) {
                            list.Add( gift );
                        }
                        if (list.Count == 0) {
                            window.onNotLive();
                        } else {
                            window.onGiftCount( now, list, url );
                        }
                    } catch (Exception ex) {
                        Log.e( ex, $"parse error. {url} {src}" );
                    }
                }

            } else if (contentType.StartsWith( "application/json" )) {
                if (url.Contains( "/api/live/current_user?room_id=" )) {
                    handleCurrentUser( window, now, url, content );
                } else if (url.Contains( "/api/live/polling?room_id=" )) {

                    if (content.Contains( "番組視聴ボーナスとして無料アイテムをGETしました!" )) {
                        window.onGiftGet( now );
                        return;
                    }

                    var groups = reExceedError.matchOrNull( content )?.Groups;
                    if (groups != null) {
                        window.onExceedError( now, groups[ 1 ].Value, groups[ 2 ].Value );
                        return;
                    }
                    // 既に取得済みの部屋だと  {"is_login":true,"online_user_num":12,"live_watch_incentive":{}} などが返る
                    // 特に処理はせず、タイムアウト扱いにする
                    Log.e( $"current_user API returns unknown event: {content}" );
                }
            }
        }

        internal static List<JObject>? handleCurrentUser(MainWindow window, Int64 now, String url, String content, Boolean fromChecker = false) {
            try {
                var list = new List<JObject>();
                foreach (JObject gift in JToken.Parse( content ).Value<JObject>( "gift_list" ).Value<JArray>( "normal" )) {
                    list.Add( gift );
                }
                var a = fromChecker ? "fromChecker, " : "";
                window.onGiftCount( now, list, $"{a}{url}" );
                return list;
            } catch (Exception ex) {
                Log.e( ex, $"parse error. {url} {content}" );
                return null;
            }
        }

        internal static void responceLog(Int64 now, String url, String data) {
            if (!responseLogEnabled)
                return;
            Task.Run( () => {
                try {
                    var dir = Config.responceLogDir;
                    Directory.CreateDirectory( dir );
                    using var writer = new StreamWriter( $"{dir}/{ now.formatFileTime()}-{reFileNameUnsafe.Replace( url, "-" )}", false, Encoding.UTF8 );
                    writer.Write( data );
                } catch (Exception ex) {
                    Log.e( ex, "responceLog() failed." );
                }
            } );
        }
    }
}
