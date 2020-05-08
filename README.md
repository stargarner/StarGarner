# StarGarner

あのサイトのアレを集めるアプリです。

----
## 免責事項

アプリを利用した事によるいかなる損害も作者は一切の責任を負いません。
自己の責任の上で使用して下さい。

## 動作環境

- Windows 10 64bit
- .NET Core 3.1 Desktop Runtime (v3.1.3) (Windows x64)

## 注意事項

- このアプリはときどき音が出ます。OSの音量ミキサーでボリュームを適時調整してください。
- このアプリはOSのスリープやディスプレイ消灯やスクリーンセーバー起動を抑止します。

----
## インストール手順

- Windows 10 64bit のPCを用意します。
- .NET Core 3.1 Desktop Runtime (v3.1.3) (Windows x64) をインストールします。
https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.3-windows-x64-installer
- StarGarnwerのリリースページ https://github.com/stargarner/StarGarner/releases から StarGarner-YYYYMMDDHHMMSS.zip をダウンロードします。
- PCのドライブのどこかにStarGarnerフォルダを作って、上記zipファイルの中身を展開します。
- 展開したファイルから StarGarner.exe を探して起動します。

## ログイン

起動するとあのサイトが表示されるのでログインしてください。
パスワード認証のみ対応してます。TwitterやFacebookでのログインはできません。
まだの人は普段お使いのブラウザであのサイトに行ってパスワードを設定してからこのアプリでログインを試してください。

ログインが終わったら自動で星集めを開始します。

## 配信開始時刻の設定

画面上部の「配信開始時刻と3周目までの分」のところに 
配信開始の時と分と、3周目開始までの分の数を指定できます。

たとえば `12:30+15` と書くと、配信開始が12時30分、その15分後に3周目開始というふうに解釈されます。


一日に配信が複数ある場合はカンマ区切りで複数書けます。例:
```
0:30+15, 3:30+15, 6:30+15, 9:30+15, 12:30+15, 15:30+15, 18:30+15, 21:30+15
```

----
## 取得ロジック

250ミリ秒ごとに部屋を開く/閉じるの判断を行います。

条件と行動を優先度の高い順に書くとこんな感じです。

### 部屋を開く条件

<dl>
<dt>「最近1分間に3回以上部屋を開いた」</dt>
<dd>→マッチする間は部屋を開きません。
<br>実装ミスによる過度なアクセスを防ぐための安全装置です。<br>&nbsp;</dd>
<dt>「オンライブ部屋一覧の情報がない」</dt>
<dd>→マッチする間は部屋を開きません。<br>&nbsp;</dd>

<dt>「強制的に開くが押された」</dt>
<dd>→部屋を開きます。部屋を閉じた時にフラグ解除されます。<br>&nbsp;</dd>

<dt>「制限エラーに遭遇した」</dt>
<dd>→制限解除まで取得しません。所持数調査もしません。<br>&nbsp;</dd>

<dt>「10回取得済み」</dt>
<dd>→解除予測まで取得しません。<br>&nbsp;</dd>

<dt>「配信予定がない」</dt>
<dd>→所持数450未満ならギフトを取得します。<br>&nbsp;</dd>

<dt>「配信開始後(3周目後)」</dt>
<dd>→所持数450未満ならギフトを取得します。</dd>
<dd>→または次の配信が4時間以内ならなるべく早く初回取得を狙います。<br>&nbsp;</dd>

<dt>「配信開始後(3周目前)」</dt>
<dd>→所持数450未満ならギフトを取得します。
<br>(配信が始まってギフトを投げた後、「強制的に開く」を押すとすぐに再取得できます。)
<br>(押さなくても最大3分まてばギフト所持数の調査と取得が行われます。)<br>&nbsp;</dd>

<dt>「配信前1時間以内(初回取得前)」</dt>
<dd>→3周目開始まで57分以上あるなら初回取得します。(捨て星)</dd>
<dd>→上記以外で、所持数450以上なら初回取得しません。</dd>
<dd>→上記以外の場合、初回取得します。タイミングも所持数も悪いので戦略がありません。<br>&nbsp;</dd>

<dt>「配信前1時間以内(初回取得後)」</dt>
<dd>→配信開始より次の解除予測が前なら、495まで取ります。</dd>
<dd>→配信開始より次の解除予測が後なら、450まで取ります。
<br>(最大99まで取るより、90で止めて実際の配信開始後に10投げる方が1だけ得します。)<br>&nbsp;</dd>

<dt>「配信前1時間以上2時間未満(初回取得後)」</dt>
<dd>→所持数450未満ならギフトを取得します。<br>&nbsp;</dd>

<dt>「配信前1時間以上2時間未満(初回取得前)」</dt>
<dd>→タイミングが前に0-29分までずれているなら少し待って微調整してから初回取得します。</dd>
<dd>→上記以外ならタイミングが後に3分までずれているなら初回取得します。</dd>
<dd>→上記以外なら所持数450以上なら初回取得しません。</dd>
<dd>→上記以外ならタイミング調整を諦めて初回取得します。<br>&nbsp;</dd>

<dt>「配信前2時間以上」</dt>
<dd>→所持数450未満ならギフトを取得します。<br>&nbsp;</dd>
</dl>

#### 補足説明
- ※実際には予定時刻より28秒前倒しで部屋を開きます。
- ※3周目開始から10分でフォーカスが次の配信に移ります。
- ※上記のどの状況でも、たまにギフト所持数を調べるために部屋を開きます。調査だけが目的の場合はすぐ閉じます。

### 部屋を閉じる条件
- ギフトを取得した
- 制限エラーに遭遇した
- 3秒以上アプリ内ブラウザからのHTTPリクエストがなかった(配信してない)
- 33秒経過した(取得済みの部屋を開いてしまった、その他)
- 「強制的に開く」がオフで、なおかつ部屋を開くべきでない状況だった。
　(たとえば部屋を開いたらその時得られた情報で495取得済みが判明した、等。)

----
## その他

アプリ内ブラウザとは別にバックグラウンドで定期的に以下のAPIを呼び出します。

- /api/live/onlives ライブ中の部屋の一覧
- /api/live/current_user ギフト所持数の調査


起動したフォルダの下に以下のファイル/フォルダを作ります。

<dl>
<dt>UI.json</dt>
<dd>画面上部の設定項目が保存されます。<br>&nbsp;</dd>

<dt>star.json</dt>
<dt>seed.json</dt>
<dd>制限解除時刻や取得履歴が保存されます。<br>&nbsp;</dd>

<dt>StarGarner.log</dt>
<dd>もし動作に問題があったら、このファイルを作者に提供いただけると問題解決の参考になるかもしれません。
削除しても特に動作には影響ありません。<br>&nbsp;</dd>

<dt>cache/</dt>
<dd>アプリ内ブラウザがクッキーやキャッシュデータを保存します。
削除すると再ログインが必要になります。<br>&nbsp;</dd>

<dt>jsonLog/</dt>
<dd>設定の「API応答をファイルに保存」を有効にすると、API応答が記録されます。
開発者以外には必要ないと思います。<br>&nbsp;</dd>

<dt>debug.log</dt>
<dd>アプリ内ブラウザがログを出力します。
削除して構いません。<br>&nbsp;</dd>
</dl>
