using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json.Linq;
using StarGarner.Dialog;
using StarGarner.Model;
using StarGarner.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using uhttpsharp;

namespace StarGarner {

    // main window and main logic.
    public partial class MainWindow : Window {

        // ギフトの取得状態を保持するクラス。星と種の2つ用意する
        private readonly Garner starGarner = new Garner( false );
        private readonly Garner seedGarner = new Garner( true );

        // タイマー
        private readonly DispatcherTimer timer;

        // オンライブ部屋チェッカー
        private readonly OnliveChecker onLiveChecker;

        internal readonly NotificationSound notificationSound = new NotificationSound();

        private readonly ScreenKeeper screenKeeper = new ScreenKeeper();

        internal readonly MyHttpServer httpServer;

        // ウィンドウを閉じたか
        private Boolean isClosed = false;

        // ログイン状態。bool? をvolatileにすることはできないのでInt32で持つ
        // false=0, true=1, unknown = -1
        internal volatile Int32 isLogin = -1;
        private Int64 lastLoginChanged;

        // 現在開いている部屋
        private Room? currentRoom = null;

        // 部屋を開いた時刻のリスト。自主的なRate Limitに使う
        private readonly List<Int64> lastOpenList = new List<Int64>();

        // 最後に部屋を開いた時刻
        internal Int64 lastOpenRoom;

        // ステータスが変化した場合だけUIを更新する
        private StatusCollection? lastStatus = null;

        //###################################################
        // クラス外部から参照される

        // ブラウザがHTTPリクエストを投げた時刻
        private Int64 _lastHttpRequest;
        public Int64 lastHttpRequest {
            get => Interlocked.Read( ref _lastHttpRequest );
            set => Interlocked.Exchange( ref _lastHttpRequest, value );
        }

        internal Task onStatus(IHttpContext httpContext)
            => Dispatcher.RunAsync( () =>
            httpContext.Response = new HttpResponse(
                HttpResponseCode.Ok,
                $"日本語ABC!!",
                closeConnection: true
        ) );



        //###################################################

        // 部屋の種類にマッチするGarner
        private Garner garnerFor(Room room) => room.isSeed ? seedGarner : starGarner;

        // 部屋を閉じる
        private void closeRoom(String reason, Boolean dontResetForceOpen = false) {
            var room = currentRoom;
            Log.d( $"部屋を閉じる理由: {reason} {room?.roomUrlKey}" );

            if (room != null && !dontResetForceOpen)
                garnerFor( room ).forceOpenReason = null;

            currentRoom = null;
            cefBrowser.Address = Config.URL_TOP;
            StatusCollection.textOrGone( tbCloseReason, Config.PREFIX_CLOSE_REASON + reason );
        }

        // 部屋を開く
        internal Boolean openAnyRoom(Garner garner, List<Room>? rooms, Int64 now, String openReason) {

            void openRoomDetail(Room room, Int64 now, String openReason) {
                Log.d( $"部屋を開く理由: {openReason} {room.roomUrlKey}" );

                lastOpenRoom = now;
                lock (lastOpenList) {
                    lastOpenList.Add( now );
                }
                currentRoom = room;
                cefBrowser.Address = Config.URL_TOP + room.roomUrlKey;
                StatusCollection.textOrGone( tbOpenReason, Config.PREFIX_OPEN_REASON + openReason );
                StatusCollection.textOrGone( tbCloseReason, "" );
            }

            if (rooms != null) {
                var lastRooms = garner.lastRooms;
                lock (lastRooms) {
                    for (var i = 0; i < 2; ++i) {
                        foreach (var room in rooms) {
                            if (lastRooms.Contains( room.roomUrlKey ))
                                continue;
                            openRoomDetail( room, now, openReason );
                            return true;
                        }
                        lastRooms.Clear(); // 2周目はLRUを無視する
                    }
                }
            }
            return false;
        }

        // タイマーやブラウザイベントから呼ばれる
        // 状況を確認して部屋を開いたり閉じたりする
        public void step(String caller) => Dispatcher.BeginInvoke( () => {
            if (isClosed)
                return;

            switch (isLogin) {
            case -1:
                StatusCollection.textOrGone( tbStatus, "ログイン状態不明" );
                StatusCollection.textOrGone( tbWaitReason, "" );
                return;
            case 0:
                StatusCollection.textOrGone( tbStatus, "ログインしてください。パスワード認証だけ使えます。TwitterやFacebook連携でのログインには対応してません。" );
                StatusCollection.textOrGone( tbWaitReason, "" );
                return;
            }

            if (caller != "timer")
                Log.d( $"caller={caller}" );

            var now = UnixTime.now;

            screenKeeper.suppressMonitorOff( now );

            starGarner.dumpStatus( now ).setTo( tbHistoryStar );
            seedGarner.dumpStatus( now ).setTo( tbHistorySeed );

            var statusA = new StatusCollection();
            var statusB = new StatusCollection();

            try {
                var room = currentRoom;
                if (room == null) {

                    var remain = lastLoginChanged + Config.waitAfterLogin - now;
                    if (remain > 0L) {
                        statusA.add( $"ログイン状態を検出。残り{ remain.formatDuration()}" );
                        statusB.add( $"ログイン直後を検出した後 { Config.waitAfterLogin.formatDuration()}間は部屋を開きません。" );
                        return;
                    }

                    lock (lastOpenList) {
                        for (var i = lastOpenList.Count - 1; i >= 0; --i) {
                            if (now - lastOpenList[ i ] >= Config.openRoomRateLimitPeriod)
                                lastOpenList.RemoveAt( i );
                        }
                        if (lastOpenList.Count >= Config.openRoomRateLimitCount) {
                            statusA.add( $"クールダウン。残り{ ( lastOpenList[ 0 ] + Config.openRoomRateLimitPeriod - now ).formatDuration()}" );
                            statusB.add( $"{UnixTime.formatDuration( Config.openRoomRateLimitPeriod )}間に{ Config.openRoomRateLimitCount}回部屋を開いたのでクールダウンします。" );
                            return;
                        }
                    }

                    // 情報表示の順序を揃えるため、部屋の開始の見通しは常に星→種の順序で行う
                    var envList = new List<CanStartRoom>(){
                        new CanStartRoom( this, statusB, now, starGarner,rooms: onLiveChecker.starRooms),
                        new CanStartRoom( this, statusB, now, seedGarner,rooms: onLiveChecker.seedRooms)
                    };

                    // 種が初回で星が初回でないなら種の取得を優先する
                    if (envList[ 1 ].historyCount == 0 && envList[ 0 ].historyCount > 0) {
                        envList.Reverse();
                    }

                    var rv = envList[ 0 ].openRoom();
                    if (rv <= 0L) {
                        return; // 部屋を開いた時は後者の部屋は開かない
                    } else if (envList[ 1 ].remainStartRoom <= 0L) {
                        if (rv < Config.remainTimeSkipSeedStart) {
                            statusB.add( $"{envList[ 0 ].garner.itemName}の待機時間が残り少ないので{envList[ 1 ].garner.itemName}の部屋を開きません。" );
                        } else {
                            envList[ 1 ].openRoom();
                        }
                    }
                } else {
                    var garner = garnerFor( room );

                    var delta = 0L;

                    var list = new List<String>();

                    var lastOpen = lastOpenRoom;
                    if (lastOpen > 0L) {
                        delta = now - lastOpen;
                        if (delta >= Config.timeoutStayRoom) {
                            garner.addLastRoom( room );
                            closeRoom( $"{garner.itemName} 部屋を開いてから{delta.formatDuration()}が経過。もしかして： 配信終了 or ギフト取得済み", dontResetForceOpen: true );
                            return;
                        }
                        list.Add( $"部屋を開いてから{delta.formatDuration()}" );
                    }

                    delta = now - lastHttpRequest;
                    if (delta >= Config.timeoutLastRequest) {
                        garner.addLastRoom( room );
                        closeRoom( $"{garner.itemName} 最終リクエストから{delta.formatDuration()}が経過", dontResetForceOpen: true );
                        return;
                    }
                    list.Add( $"最終リクエストから{delta.formatDuration()}" );

                    if (list.Count > 0)
                        statusA.add( String.Join( "。", list ) );

                    var closeReason = new StatusCollection();
                    var cse = new CanStartRoom( this, closeReason, now, garner, checkClosing: true );
                    if (cse.remainStartRoom > 0L) {
                        closeRoom( closeReason.ToString().Replace( "\n", " " ) );
                        return;
                    }
                }
            } finally {
                statusA.setTo( tbStatus );

                if (statusB.ToString() != lastStatus?.ToString()) {
                    statusB.setTo( tbWaitReason );
                    lastStatus = statusB;
                    foreach (var line in statusB.ToString().Split( "\n" )) {
                        if (line.Length > 0)
                            Log.d( $"status: {line}" );
                    }
                }
            }
        } );

        //###########################################################################
        // in-app browser event

        public void onLogin(Boolean v) => Dispatcher.BeginInvoke( () => {
            if (isClosed)
                return;
            try {
                var i = v ? 1 : 0;
                if (isLogin != i) {
                    isLogin = i;
                    lastLoginChanged = UnixTime.now;
                    step( $"login {v}" );
                    if (v) {
                        ScreenKeeper.DisableSuspend();
                    } else {
                        ScreenKeeper.EnableSuspend();
                    }
                }
            } catch (Exception ex) {
                Log.e( ex, "onGiftGet failed." );
            }
        } );

        internal void onRequestCookie(String cookie) => Dispatcher.BeginInvoke( () => onLiveChecker.setCookie( cookie ) );

        public void onNotLive() => Dispatcher.BeginInvoke( () => {
            try {
                if (isClosed)
                    return;
                if (currentRoom != null)
                    closeRoom( "配信中ではなかった", dontResetForceOpen: true );
            } catch (Exception ex) {
                Log.e( ex, "onNotLive failed." );
            }
        } );

        public void onGiftCount(Int64 now, List<JObject> gifts, String caller) => Dispatcher.BeginInvoke( () => {
            try {
                if (isClosed)
                    return;

                var tmpStarCounts = new Dictionary<Int32, Int32>();
                var tmpSeedCounts = new Dictionary<Int32, Int32>();
                foreach (var gift in gifts) {
                    var giftId = gift.Value<Int32>( "gift_id" );
                    var freeNum = gift.Value<Int32>( "free_num" );
                    // htmlから読んだ時だけある var giftName = gift.Value<String>( "gift_name" );

                    if (Config.starIds.Contains( giftId )) {
                        tmpStarCounts[ giftId ] = freeNum;
                    } else if (Config.seedIds.Contains( giftId )) {
                        tmpSeedCounts[ giftId ] = freeNum;
                    }
                }
                var changed =
                    starGarner.giftCounts.set( now, tmpStarCounts ) +
                    seedGarner.giftCounts.set( now, tmpSeedCounts );
                if (changed != 0)
                    step( $"onGiftCount {caller}" );
            } catch (Exception ex) {
                Log.e( ex, "onGiftCount failed." );
            }
        } );

        public void onGiftGet(Int64 now) => Dispatcher.BeginInvoke( () => {
            try {
                if (isClosed)
                    return;

                var room = currentRoom;
                if (room == null)
                    return;
                var garner = garnerFor( room );
                Log.d( $"{garner.itemName}を取得。{room.roomUrlKey}" );

                garner.addLastRoom( room );

                var x = garner.increment( now );

                notificationSound.play( garner.soundActor, NotificationSound.counts[ Math.Min( 10, x ) ] );

                Log.d( $"取得にかかった時間 {( now - lastOpenRoom ).formatDuration()}" );
                closeRoom( $"{garner.itemName} ギフトを取得しました" );
                step( "got gifts" );
            } catch (Exception ex) {
                Log.e( ex, "onGiftGet failed." );
            }
        } );

        public void onExceedError(Int64 now, String h, String m) => Dispatcher.BeginInvoke( () => {
            try {
                if (isClosed)
                    return;

                // 制限解除時刻を HH:MMからUnixTimeに変換する
                static Int64 parseExceedTime(Int64 now, Int32 h, Int32 m) {
                    var dtNow = now.toDateTime();
                    var t = new DateTime( dtNow.Year, dtNow.Month, dtNow.Day, h, m, 0 ).toUnixTime();
                    var hour12 = UnixTime.hour1 * 12;

                    while (t < now - hour12)
                        t += UnixTime.day1;

                    while (t > now + hour12)
                        t -= UnixTime.day1;

                    return t;
                }

                var room = currentRoom;
                if (room == null)
                    return;
                var garner = garnerFor( room );
                var limit = parseExceedTime( now, Int32.Parse( h ), Int32.Parse( m ) );
                garner.setExceed( now, limit );
                notificationSound.play( garner.soundActor, NotificationSound.exceedError );
                closeRoom( $"{garner.itemName} 制限超過エラー" );
                step( "got exceed error" );
            } catch (Exception ex) {
                Log.e( ex, "onExceedError failed." );
            }
        } );

        //######################################################
        // save/load UI state

        private void saveUI()
            => new JObject() {
                { Config.KEY_START_TIME_STAR ,tbStartTimeStar.Text},
                { Config.KEY_START_TIME_SEED ,tbStartTimeSeed.Text},
                { Config.KEY_RESPONSE_LOG, MyResourceRequestHandler.responseLogEnabled },
                { Config.KEY_LISTEN_ENABLED, httpServer.enabled },
                { Config.KEY_LISTEN_ADDR, httpServer.listenAddr },
                { Config.KEY_LISTEN_PORT, httpServer.listenPort },
            }.saveTo( Config.FILE_UI_JSON );

        private void loadUI() {
            try {
                if (File.Exists( Config.FILE_UI_JSON )) {
                    var root = Utils.loadJson( Config.FILE_UI_JSON );

                    var sv = root.Value<String?>( Config.KEY_START_TIME_STAR );
                    if (sv != null)
                        tbStartTimeStar.Text = sv;

                    sv = root.Value<String?>( Config.KEY_START_TIME_SEED );
                    if (sv != null)
                        tbStartTimeSeed.Text = sv;

                    sv = root.Value<String?>( Config.KEY_LISTEN_ADDR );
                    if (sv != null)
                        httpServer.listenAddr = sv;

                    sv = root.Value<String?>( Config.KEY_LISTEN_PORT );
                    if (sv != null)
                        httpServer.listenPort = sv;

                    var bv = root.Value<Boolean?>( Config.KEY_LISTEN_ENABLED );
                    httpServer.enabled = bv ?? false;

                    httpServer.updateListening();

                    bv = root.Value<Boolean?>( Config.KEY_RESPONSE_LOG );
                    MyResourceRequestHandler.responseLogEnabled = bv ?? false;
                }
            } catch (Exception ex) {
                Log.e( ex, "loadUI failed." );
            }
        }

        //######################################################
        // garner setting dialog
        // 星と種の設定ウィンドウ

        private WeakReference<GarnerSettingDialog>? refSettingDialogStar;
        private WeakReference<GarnerSettingDialog>? refSettingDialogSeed;
        internal WeakReference<OtherSettingDialog>? refSettingDialogOther;

        private void openGarnerSetting(Garner garner, ref WeakReference<GarnerSettingDialog>? refSettingDialog) {
            GarnerSettingDialog? dialog = null;
            refSettingDialog?.TryGetTarget( out dialog );
            if (dialog != null && dialog.isClosed == false) {
                dialog.Activate();
                return;
            }

            // Instantiate the dialog box
            var dlg = new GarnerSettingDialog( garner ) {
                Owner = this
            };
            dlg.Show();
            refSettingDialog = new WeakReference<GarnerSettingDialog>( dlg );
        }


        internal void saveGarnerSetting(Garner garner) => Dispatcher.BeginInvoke( () => {
            try {
                if (isClosed)
                    return;

                garner.save();
            } catch (Exception ex) {
                Log.e( ex, "saveGarnerSetting failed." );
            }
        } );

        private void openOtherSetting() {
            OtherSettingDialog? dialog = null;
            refSettingDialogOther?.TryGetTarget( out dialog );
            if (dialog != null && dialog.isClosed == false) {
                dialog.Activate();
                return;
            }

            // Instantiate the dialog box
            var dlg = new OtherSettingDialog( this );
            dlg.Show();
            refSettingDialogOther = new WeakReference<OtherSettingDialog>( dlg );
        }

        internal void saveOtherSetting() => Dispatcher.BeginInvoke( () => {
            try {
                if (isClosed)
                    return;
                saveUI();
            } catch (Exception ex) {
                Log.e( ex, "saveGarnerSetting failed." );
            }
        } );

        //######################################################
        // window lifecycle event

        protected override void OnClosed(EventArgs e) {
            isClosed = true;
            timer.Stop();
            cefBrowser.Dispose();
            base.OnClosed( e );
            Utils.singleTask.complete();
            notificationSound.Dispose();
        }

        public MainWindow() {
            this.onLiveChecker = new OnliveChecker( this );
            this.httpServer = new MyHttpServer( this );

            /// Specifying a CachePath is required for persistence of cookies, saving of passwords, etc
            /// an In-Memory cache is used by default( similar to Incogneto).
            using var settings = new CefSettings() {
                WindowlessRenderingEnabled = true,
                LogSeverity = LogSeverity.Default,
                Locale = "ja",
                AcceptLanguageList = "ja-JP",
                CachePath = "cache",
                PersistSessionCookies = true,
                UserAgent = Config.userAgent
            };

            Cef.Initialize( settings );

            InitializeComponent();

            loadUI();
            starGarner.liveStarts.set( tbStartTimeStar.Text );
            seedGarner.liveStarts.set( tbStartTimeSeed.Text );


            var isInInitialize = true;

            btnStarSetting.Click += (sender, e) => openGarnerSetting( starGarner, ref refSettingDialogStar );
            btnSeedSetting.Click += (sender, e) => openGarnerSetting( seedGarner, ref refSettingDialogSeed );
            btnOtherSetting.Click += (sender, e) => openOtherSetting();
            tbStartTimeStar.TextChanged += (sender, e) => {
                if (isInInitialize)
                    return;
                saveUI();
                starGarner.liveStarts.set( tbStartTimeStar.Text );
                step( "TextBoxStartTimeStar.TextChanged" );
            };

            tbStartTimeSeed.TextChanged += (sender, e) => {
                if (isInInitialize)
                    return;
                saveUI();
                seedGarner.liveStarts.set( tbStartTimeSeed.Text );
                step( "TextBoxStartTimeSeed.TextChanged" );
            };

            cefBrowser.BrowserSettings = new BrowserSettings {
                FileAccessFromFileUrls = CefState.Disabled,
                UniversalAccessFromFileUrls = CefState.Disabled
            };

            cefBrowser.RequestHandler = new MyRequestHandler( this );
            cefBrowser.Address = Config.URL_TOP;

            timer = new DispatcherTimer( DispatcherPriority.Normal, Dispatcher ) {
                Interval = TimeSpan.FromMilliseconds( Config.timerInterval ),
            };

            timer.Tick += (sender, e) => {
                onLiveChecker.run();
                step( "timer" );
            };

            timer.Start();

            isInInitialize = false;
        }

    }
}
