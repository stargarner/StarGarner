using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StarGarner {

    internal static class Config {

        private const String AES_IV = @"pf69DL6grWFyZcMK";
        private const String AES_Key = @"9Fix4L4hB4PKeKWY";

        // HTTPリクエストのUser-Agent
        internal static readonly String userAgent = test( @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36" );

        // 初期ページおよび部屋を閉じた時に戻るページ。ログイン状態を判断できること
        internal static readonly String URL_TOP = d( @"COTmJ4DIVJHVfuJtBatjGqIoSUwQQ1isTjszBtk9Y1w=" );

        internal static readonly String REGEX_SITE_DOMAIN = d( @"tPw9gD1b+q73JeHqDL4G2vfEWXVUCH6M7EQ2N7FdcdU=" );

        internal const String ffmpegOptions = "-nostdin -hide_banner -loglevel info -timeout 30000 -fflags +igndts";

        // 星と種のギフトID
        internal static readonly HashSet<Int32> starIds = new HashSet<Int32>() { 1, 2, 1001, 1002, 1003 };
        internal static readonly HashSet<Int32> seedIds = new HashSet<Int32>() { 1501, 1502, 1503, 1504, 1505 };

        // オンライブ部屋一覧を取得する間隔
        internal const Int64 onLiveCheckInterval = UnixTime.minute1 * 2;
        internal const Int64 onLiveCheckGiftInterval = UnixTime.minute1 * 2;

        // UIタイマーの間隔(ミリ秒)
        internal const Double timerInterval = 250;

        // ロジックに関わらず、時間内に一定回数よりも部屋を開かないようにする
        internal const Int64 openRoomRateLimitPeriod = UnixTime.minute1;
        internal const Int32 openRoomRateLimitCount = 3;

        // 部屋を開いて一定時間が経過したら無条件に部屋を閉じる
        internal const Int64 timeoutStayRoom = UnixTime.second1 * 33;

        // 部屋を開いて一定時間HTTPリクエストがなかったら、部屋が配信中でないとみなす
        internal const Int64 timeoutLastRequest = UnixTime.second1 * 3;

        // 予定時刻の何秒前に部屋を開きたいか。
        // サーバ側の判定精度はおそらく秒単位なので、マージンは1秒以上ないといけない。30秒-1秒が前倒しできる限界だろう。
        internal const Int64 waitBeforeGift = UnixTime.second1 * 29;

        // 星の待機時間が一定以内になると種の部屋を開くチェックを行わない
        internal const Int64 remainTimeSkipSeedStart = UnixTime.second1 * 60;

        // 三周目になってから何分経つとその配信に注目するのをやめるか
        internal const Int64 startTimeOldLimit = UnixTime.minute1 * 10;

        // ギフト所持数を再調査する時間間隔
        internal const Int64 giftCountInvestigateInterval = UnixTime.minute1 * 3;

        // ログイン状態を検出した直後は部屋を開かない
        internal const Int64 waitAfterLogin = UnixTime.second1 * 3;

        internal const Double giftHistoryFontSize = 9.0;

        // API応答を保存するフォルダ
        internal const String responceLogDir = "jsonLog";

        // UI設定保存ファイル
        internal const String FILE_UI_JSON = "UI.json";

        // UI設定保存キー
        internal const String KEY_START_TIME_STAR = "TextBoxStartTimeStar";
        internal const String KEY_START_TIME_SEED = "TextBoxStartTimeSeed";
        internal const String KEY_RESPONSE_LOG = "ResponseLog";
        internal const String KEY_LISTEN_ENABLED = "listenEnabled";
        internal const String KEY_LISTEN_ADDR = "listenAddr";
        internal const String KEY_LISTEN_PORT = "listenPort";
        internal const String KEY_RECORDER_HUB = "recorderHub";

        internal const String KEY_SAVE_DIR = "saveDir";
        internal const String KEY_FFMPEG_PATH = "ffmpegPath";
        internal const String KEY_ROOM_NAME = "roomName";
        internal const String KEY_ROOM_ID = "roomId";
        internal const String KEY_ROOM_CAPTION = "roomCaption";
        internal const String KEY_ROOMS = "rooms";

        // garner設定保存キー
        internal const String KEY_EXPIRE_EXCEED = "expireExceed";
        internal const String KEY_HISTORY = "history2";
        internal const String KEY_SOUND_ACTOR = "soundActor";
        internal const String KEY_TIME = "time";
        internal const String KEY_COUNT = "count";
        internal const String KEY_LAST_PLAY_LIVE_START = "lastPlayLiveStart";
        internal const String KEY_LAST_PLAY_THIRD_LAP = "lastPlayThirdLap";
        internal const String KEY_LAST_PLAY_HISTORY_CLEAR = "lastPlayHistoryClear";

        // メッセージ表示のprefix
        internal const String PREFIX_CLOSE_REASON = "部屋を閉じた理由：";
        internal const String PREFIX_OPEN_REASON = "部屋を開いた理由：";

        // コード中に特定の単語を含めないための暗号化
        private static String test(String plainText) {
            var sv = e( plainText );
            Log.d( $"e: {sv}" );
            Log.d( $"d: {d( sv )}" );
            return plainText;
        }

        // コード中に特定の単語を含めないための暗号化
        private static String e(String text) {
            var blockSize = 128;
            using var rijndael = new RijndaelManaged {
                BlockSize = blockSize,
                KeySize = 128,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = Encoding.UTF8.GetBytes( AES_IV ),
                Key = Encoding.UTF8.GetBytes( AES_Key )
            };

            var encryptor = rijndael.CreateEncryptor( rijndael.Key, rijndael.IV );

            var bytes = Encoding.UTF8.GetBytes( text );
            using var mStream = new MemoryStream();
            using (var ctStream = new CryptoStream( mStream, encryptor, CryptoStreamMode.Write )) {
                ctStream.Write( bytes );
                ctStream.FlushFinalBlock();
                ctStream.Close();
            }
            var encrypted = mStream.ToArray();
            return System.Convert.ToBase64String( encrypted );
        }

        // コード中に特定の単語を含めないための暗号化
        private static String d(String cipher) {
            var blockSize = 128;
            using var rijndael = new RijndaelManaged {
                BlockSize = blockSize,
                KeySize = 128,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = Encoding.UTF8.GetBytes( AES_IV ),
                Key = Encoding.UTF8.GetBytes( AES_Key )
            };

            var decryptor = rijndael.CreateDecryptor( rijndael.Key, rijndael.IV );
            var bytes = System.Convert.FromBase64String( cipher );
            using var mStream = new MemoryStream( bytes );
            using var ctStream = new CryptoStream( mStream, decryptor, CryptoStreamMode.Read );
            using var decoded = new MemoryStream();
            var tmp = new Byte[ blockSize ];
            while (true) {
                var delta = ctStream.Read( tmp, 0, tmp.Length );
                if (delta <= 0L)
                    break;
                decoded.Write( tmp, 0, delta );
            }
            var str = Encoding.UTF8.GetString( decoded.ToArray() );
            return new Regex( @"\x00+\z" ).Replace( str, "" );
        }

        private static HttpClient createHttpClient() {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add( "User-Agent", Config.userAgent );
            return client;
        }

        internal static readonly HttpClient httpClient = createHttpClient();
    }
}
