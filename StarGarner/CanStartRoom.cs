using System;
using System.Collections.Generic;
using System.Windows.Documents;

namespace StarGarner {

    // 部屋を開くかどうか判断する。
    internal class CanStartRoom {

        private readonly MainWindow window;

        // 星か種か。外部から参照される
        internal readonly Garner garner;

        // 判定時刻
        private readonly Int64 now;

        // 状況テキストの出力先
        private readonly StatusCollection status;

        // 部屋を閉じる判定なら真
        private readonly Boolean checkClosing;

        private readonly List<Room>? rooms;

        private readonly Int64 expectedReset;

        // 外部から参照される。星と種のどちらを優先するかに影響する
        internal readonly Int32 historyCount;

        // 検討結果。 0以下なら部屋を開く。1以上なら待機時間目安を示す。
        internal readonly Int64 remainStartRoom;

        // 部屋を開いた後に行う処理。forceOpenフラグの設定など
        private event Action? afterOpen;

        // 部屋を開く理由
        private String? openReason = null;

        // 検討結果を見て部屋を開く
        internal Int64 openRoom() {
            if (remainStartRoom > 0L)
                return remainStartRoom;

            if (window.openAnyRoom( garner, rooms, now, openReason ?? "????" )) {
                afterOpen?.Invoke();
                return 0L;
            }

            return Int64.MaxValue;
        }

        internal CanStartRoom(
            MainWindow window,
            StatusCollection status,
            Int64 now,
            Garner garner,
            Boolean checkClosing = false,
            List<Room>? rooms = null
            ) {

            this.window = window;
            this.status = status;
            this.now = now;
            this.garner = garner;
            this.checkClosing = checkClosing;
            this.rooms = rooms;

            // giftHistory.count() を呼ぶ前に必ずexpectedResetを呼ばないといけない
            this.expectedReset = garner.giftHistory.expectedReset( now );

            // historyCount は外部から参照されるreadonly変数なのでこのタイミングで初期化したい
            this.historyCount = garner.giftHistory.count();

            // 初期化時に検討までやってしまう
            this.remainStartRoom = check();
        }

        // 検討する。
        // 戻り値：0以下なら部屋を開くべき。1以上なら残りの待機時間の目安。
        private Int64 check() {

            var itemName = garner.itemName;

            var expireExceed = garner.expireExceed;
            var remainExpireExceed = expireExceed - now;
            var remainExpectedReset = expectedReset - now;
            var hasResetTime = remainExpectedReset >= Config.waitBeforeGift;

            // ギフト所持数
            var giftCountInTime = garner.giftCounts.isInTime( now );
            var sumGiftCount = garner.giftCounts.sum();

            // 制限が解除された時に音を出す
            var cleared = remainExpireExceed < Config.waitBeforeGift && remainExpectedReset < Config.waitBeforeGift;
            if (garner.willPlayHistoryClear( now, cleared )) {
                window.notificationSound.play( garner.soundActor, NotificationSound.exceedReset );
            }

            var situation = "?";
            Int64? situationRemain = null;

            // 指定テキスト + "強制的に開く"を status に追加する
            void addForceLink(String line, String? lineClosing = null) {
                if (checkClosing) {
                    status.add( lineClosing ?? line );
                } else {
                    var hyperLink = new Hyperlink() {
                        NavigateUri = new Uri( $"stargarner://{garner.itemNameEn}/force_open" )
                    };
                    hyperLink.Inlines.Add( "強制的に開く" );
                    hyperLink.RequestNavigate += (sender, e) => garner.forceOpenReason = "「強制的に開く」が押された";
                    status.addLink( line, hyperLink );
                }
            }

            // 部屋を開かない。
            Int64 dontOpen(String? reason) {
                addForceLink( $"{itemName}/{situation}/{reason ?? "?" }" );
                return Math.Max( 1, situationRemain ?? UnixTime.hour1 );
            }

            // 部屋を開く。しかし開いた後に再検討したら閉じてしまうかもしれない。
            Int64 willOpen(String reason) {
                openReason = $"{itemName}/{situation}/{reason ?? "?" }";
                return 0L;
            }

            // 部屋を開く。開いた後の再検討を抑止するため、開いた直後にforceOpenReasonをセットする。
            Int64 forceOpen(String msg) {
                if (!checkClosing) {
                    openReason = $"{itemName}/{situation}/{msg}";
                    afterOpen += () => garner.forceOpenReason = $"{situation}/{msg}";
                }
                return 0L;
            }

            // 所持数の調査のため、ときどき部屋を開く。
            Int64 investigateOnly(String line) {

                var lastRead = garner.giftCounts.updatedAt;

                if (checkClosing) {
                    // 閉じる判断

                    var lastOpen = window.lastOpenRoom;
                    if (lastOpen - now >= 15000L) {
                        // 開いた後に最大15秒しか滞在しない
                        return dontOpen( "所持数調査に失敗しました。15秒以上は滞在しません" );
                    } else if (lastRead > lastOpen) {
                        // 開いた後にギフト所持数が更新されたら閉じる

                        return dontOpen( "所持数調査が終わりました" );
                    }
                } else {
                    // 開く判断
                    var remain = situationRemain ?? UnixTime.hour1;

                    // 残り時間に余裕がない、最後に所持数が更新されてからの経過秒数が短いなら部屋を開かない
                    if (remain < Config.remainTimeSkipSeedStart || now - lastRead < Config.giftCountInvestigateInterval) {
                        return dontOpen( line );
                    }
                }

                return willOpen( "所持数の調査" );
            }

            // いくつかの条件を満たせば部屋を開く
            Int64 normalGet(
                 String waitCaption,
                 Int32? maxGiftCount = null,
                 String? preferGetFirstReason = null
                 ) {

                var remain = situationRemain;

                // maxGiftCountが省略された場合、解除予測まで残り僅かなら495を、それ以外なら450を補う
                maxGiftCount ??= hasResetTime &&
                    remainExpectedReset >= Config.waitBeforeGift + UnixTime.second1 * 5 &&
                    remainExpectedReset < Config.waitBeforeGift * 2 + UnixTime.second1 * 10 &&
                    ( remain == null || remain >= 40000L && remain < 80000L )
                    ? 495
                    : 450;

                if (remain != null && remain < 40000L && !checkClosing) {
                    // 次の状況に間に合わないなら開かない。閉じはしない。
                    return dontOpen( "次の状況が近いので所持数調査や取得を行いません" );

                } else if (!hasResetTime && preferGetFirstReason != null) {
                    // preferGetFirstReason が指定されていたら初回取得を狙う。現在の所持数は関係ない。
                    return willOpen( preferGetFirstReason );

                } else if (giftCountInTime && sumGiftCount < maxGiftCount) {
                    // 明らかに所持数が少ないならギフトを取りに行く。
                    return willOpen( "ギフトの取得" );

                } else {
                    // 所持数の情報が古いか、一定以上持っているなら調査目的で開く。
                    return investigateOnly( $"{sumGiftCount}個取得済み。{waitCaption}" );
                }
            }

            // 配信予定がある場合の検討
            Int64 checkLiveStart(LiveStarts.TimeAndOffset st) {

                var remainLiveStart = st.time - now;
                var remainThirdLap = st.time + st.offset - now;
                var remainDropGift = remainThirdLap - UnixTime.hour1;

                if (remainThirdLap < Config.waitBeforeGift) {
                    // 配信開始後(3周目以降)
                    situation = "3周目後";
                    situationRemain = null;

                    if (garner.willPlayThirdLap( st!.time ))
                        window.notificationSound.play( garner.soundActor, NotificationSound.thirdLap );

                    // 次の周が近いなら星が余ってても取らないとタイミングが狂う
                    // 次の配信まで余裕があるのなら、星が余ってれば初回取得しない
                    var next = garner.liveStarts.nextOf( now, st!.time );
                    var thirdLapPreferReason =
                        ( next != null && next.time <= UnixTime.hour1 * 4 )
                            ? "取らないと次の配信でのタイミングが狂う…"
                            : (String?)null;

                    return normalGet( "適当に待機します", preferGetFirstReason: thirdLapPreferReason, maxGiftCount: 450 );

                } else if (remainLiveStart < Config.waitBeforeGift) {
                    // 配信開始後(3周目より前)
                    // 配信開始が実際には遅れるかもしれない
                    // 「450未満なら取得」で良いと思う。投げてなければ取得しないのだし

                    situation = "配信開始後";
                    situationRemain = remainThirdLap;

                    if (garner.willPlayLiveStart( st.time ))
                        window.notificationSound.play( garner.soundActor, NotificationSound.liveStart );

                    return normalGet( "適当に待機します", maxGiftCount: 450 );

                } else if (remainDropGift < Config.waitBeforeGift) {

                    situationRemain = remainLiveStart;

                    // 捨て星以降
                    if (!hasResetTime) {
                        if (remainThirdLap - Config.waitBeforeGift >= UnixTime.minute1 * 57) {
                            situation = "配信前(初回取得済前,3周目まで57分以上)";
                            return willOpen( "捨て星します" );

                        } else if (sumGiftCount >= 450) {
                            // いま50とっても5無駄になる
                            situation = "配信前(初回取得済前,タイミング悪,450所持)";
                            return investigateOnly( "初回取得しません" );

                        } else {
                            // 捨て星タイミングが合わないしギフト所持数も揃わないので、諦めて取れるだけ取る
                            situation = "配信前(初回取得済前,タイミング悪,所持数不足)";
                            return willOpen( "タイミング調整を諦めて初回を取ります" );
                        }
                    } else {
                        if (remainExpectedReset > remainLiveStart) {
                            // 配信開始より後に解除予測がある
                            situation = "配信前(取得1回以上)";
                            return normalGet( "配信開始まで待機します", maxGiftCount: 450 );

                        } else {
                            // 配信開始より後に解除予測がある
                            // 495まで取る。解除予測が来ても初回を取らない場合などに有効
                            situation = "配信前(取得1回以上)";
                            situationRemain = remainExpectedReset;
                            return normalGet( "解除予測まで待機します", maxGiftCount: 495 );
                        }
                    }

                } else if (remainDropGift < Config.waitBeforeGift + UnixTime.hour1) {
                    // 捨て星前1時間以内

                    situationRemain = remainDropGift;

                    if (hasResetTime) {
                        situation = "捨て星前1時間以内(初回取得後)";
                        return normalGet( "捨て星まで待機します" );

                    } else {
                        situation = "捨て星前1時間以内(初回取得前)";

                        Int64 getTimingDelta() {
                            if (remainDropGift <= 0)
                                return Int64.MinValue;
                            var a = remainDropGift - Config.waitBeforeGift;
                            while (a >= UnixTime.hour1 / 2) {
                                a -= UnixTime.hour1 + UnixTime.second1 * 15;
                            }
                            return a;
                        }
                        var timingDelta = getTimingDelta();

                        if (timingDelta > 0L) {
                            return investigateOnly( "初回取得のタイミングを待ちます" );

                        } else if (timingDelta >= UnixTime.minute1 * -3) {
                            return forceOpen( "タイミングが良いので初回を取りたい" );

                        } else if (sumGiftCount >= 450) {
                            return investigateOnly( "所持数450以上なので初回取得を避けます" );

                        } else {
                            return forceOpen( "タイミングを逃したが、所持数が少ないので初回取得する" );
                        }
                    }

                } else {
                    // 捨て星前1時間以上
                    situationRemain = remainDropGift - UnixTime.hour1;
                    var n = ( remainDropGift - Config.waitBeforeGift ) / UnixTime.hour1;
                    situation = $"捨て星前{n}時間以上";
                    return normalGet( "捨て星まで待機します" );
                }
            }

            /////////////////////

            if (!checkClosing && ( rooms?.Count ?? 0 ) == 0) {
                situation = "部屋一覧に情報がない";
                situationRemain = UnixTime.minute1 * 3;
                return dontOpen( "オンライブ部屋一覧がありません" );

            } else if (garner.forceOpenReason != null) {
                situation = "強制的に開くがON";
                return willOpen( garner.forceOpenReason );

            } else if (remainExpireExceed >= Config.waitBeforeGift) {
                situation = "解除制限";
                situationRemain = remainExpireExceed;
                return dontOpen( "今は取得できません" );

            } else if (remainExpectedReset >= Config.waitBeforeGift && historyCount >= 10) {
                situation = "10回取得済み";
                situationRemain = remainExpectedReset;
                return investigateOnly( "解除予測まで待機します" );

            } else {
                var st = garner.liveStarts.current( now );
                if (st == null) {
                    situation = "配信予定なし";
                    return normalGet( "適当に待機します" );

                } else {
                    return checkLiveStart( st );
                }
            }
        }
    }
}
