using CefSharp;
using CefSharp.Handler;
using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StarGarner {
    public class MyRequestHandler : RequestHandler {
        static readonly Log log = new Log( "MyRequestHandler" );

        // 追跡対象サイトの正規表現
        private static readonly Regex reSiteDomain = new Regex( Config.REGEX_SITE_DOMAIN, RegexOptions.IgnoreCase );

        // URL の ? # 以降を除去する
        private static readonly Regex reRemoveQueryAndFragment = new Regex( @"[?#].+" );

        // ファイル拡張子の正規表現
        private static readonly Regex reFileExtension = new Regex( @"\.([^./]+)\z" );

        // 追跡しないファイル拡張子
        private static readonly HashSet<String> ignoreExtensions = new HashSet<String>() { "png", "jpeg", "jpg", "js", "svg", "css", "gif", "ts", "m3u", "m3u8" };

        private readonly MainWindow window;

        public MyRequestHandler(MainWindow window) => this.window = window;

        protected override IResourceRequestHandler? GetResourceRequestHandler(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            IRequest request,
            Boolean isNavigation,
            Boolean isDownload,
            String requestInitiator,
            ref Boolean disableDefaultHandling
        ) {
            try {
                if (willCheckResource( request.Url ))
                    return new MyResourceRequestHandler( window );
            } catch (Exception ex) {
                log.e( ex, "GetResourceRequestHandler failed." );
            }
            return null;
        }

        private Boolean willCheckResource(String url) {
            // Log( $"{request.Url}" );
            window.lastHttpRequest = UnixTime.now;

            // skip if not site domain. facebook,twitter,google,youtube, etc.
            if (!reSiteDomain.IsMatch( url ))
                return false;

            // skip some image/css/playlist url.
            var urlWithoutQuery = reRemoveQueryAndFragment.Replace( url, "" );
            var ext = reFileExtension.matchOrNull( urlWithoutQuery )?.Groups[ 1 ].Value.ToLower();
            if (ext != null && ignoreExtensions.Contains( ext ))
                return false;

            return true;
        }
    }
}
