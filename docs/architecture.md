# EasyHTTPServer 2 アーキテクチャ

更新日: 2026-08-19

この文書は目標アーキテクチャを示します。ファイル共有、LAN/VPN、配布基盤、`Publication`分離、Webサイトモードの基盤は実装済みです。残る実機・配布QAは[現在のタスク](../current_task.md)を正とします。

## 1. 採用技術

- .NET 10 LTS
- C#
- ASP.NET Core Kestrel
- WPF
- MVVM
- JSON設定
- 許可リスト方式の構造化ログ

初期版はWindows専用です。ただし、サーバーコアはWPFへ依存させません。

## 2. ソリューション構成

```text
EasyHTTPServer.sln
├─ src/
│  ├─ EasyHttpServer.Core/
│  ├─ EasyHttpServer.Server/
│  └─ EasyHttpServer.Desktop.Wpf/
└─ tests/
   ├─ EasyHttpServer.Core.Tests/
   ├─ EasyHttpServer.Server.Tests/
   ├─ EasyHttpServer.IntegrationTests/
   └─ EasyHttpServer.Desktop.Wpf.Tests/
```

### EasyHttpServer.Core

UIやASP.NET Coreに依存しないドメイン層です。

- `ShareDefinition`
- `Publication`（`FileSharePublication`または`WebsitePublication`）
- `WebsiteDefinition`
- `OriginPortHistory`と役割集合の不変条件
- `IOriginPortHistoryStore`
- URLスラッグ検証

### EasyHttpServer.Server

Kestrelをホストし、HTTP要求を処理します。

LAN共有時はloopback HTTPと選択したLAN IPv4のHTTPSを同じホスト内の独立endpointとして構成します。Host許可と認証要否は`HttpContext.Connection.LocalIpAddress`で到着endpointを特定して判定し、LAN資格情報をloopbackへ適用しません。

pair POSTの期待`Origin`はRequest Hostから作らず、開始時に確定したscheme、選択IP、実際のbind portをSSOTとして正規生成します。Request Hostは同じ構成済みauthorityへ独立に完全一致させ、forwarded headerは参照しません。通常はOrigin完全一致を要求し、一部ブラウザーの欠落/opaque Origin互換fallbackは、検証済みendpoint/Hostとsame-origin top-level form navigationを示すFetch Metadataがすべて一致する場合だけ許可します。

- `IServerController`
- `OriginPortAllocator`（候補選択、耐久retire、bindエラー分類、有限再試行）
- `OriginPortHistoryStore`（安定lock、状態検証、flush、原子的置換）
- Kestrelの構成とライフサイクル
- セキュリティミドルウェア
- 共有ルートの解決
- ディレクトリ目次生成
- ファイル共有とWebサイトの排他的なhandler選択
- mode別MIME・ファイル応答・セキュリティヘッダー
- 転送観測
- 転送完了イベント

### EasyHttpServer.Desktop.Wpf

Windows hostのprocess lifecycleと利用者との対話を担当します。

- `SingleInstanceGuard`と`ApplicationStateInitializer`
- MainWindowと各設定View
- ViewModel
- サーバー開始・停止Command
- 共有フォルダー選択
- ファイル共有／Webサイトの目的選択と単一サイトフォルダー選択
- 転送一覧
- URLコピー
- Modern / Classic 2005テーマ
- History / About

## 3. 依存方向

```text
Desktop.Wpf ─────► Server ─────► Core
     │                              ▲
     └──────────────────────────────┘
```

- Coreは他プロジェクトを参照しません。
- ServerはCoreだけを参照します。
- Desktop.WpfはCoreとServerを参照します。
- ServerからWPF型や`Dispatcher`を参照してはいけません。

## 4. 実行時フロー

```text
App.OnStartup
  │ version非依存のmachine-wide mutexを取得しprocess終了まで保持
  ▼
SingleInstanceGuard
  │ settings / origin履歴を優先順で分類
  ▼
ApplicationStateInitializer
  │ 新規作成または冪等移行（origin履歴を先に耐久保存）
  ▼
利用者
  │ 公開目的・フォルダー選択・開始
  ▼
WPF ViewModel
  │ StartAsync(configuration)
  ▼
IServerController
  ├─ FileShare: port roleを検証・必要なら履歴へ耐久union
  └─ Website: OriginPortAllocator
       │ 安定history lock内でread → validate → candidate選択 → retire → flush → atomic replace
       │ commit後にexact bind。AddressAlreadyInUseだけcleanupして最大32候補を再試行
       ▼
  Kestrel起動
  ▼
HTTP Pipeline
  ├─ Method / 要求ボディ検証
  ├─ LAN・VPN時のHost検証 / 短時間ペアリング / メモリ内Cookie認証
  ├─ Kestrel制限 / レート制限
  ├─ Publicationを1つだけ選択
  │   ├─ FileShareRequestHandler
  │   │   ├─ DirectoryIndexRenderer
  │   │   └─ 能動形式をdownload-onlyで配信
  │   └─ WebsiteRequestHandler
  │       ├─ 末尾slash / index解決
  │       └─ 静的Web素材を通常配信
  ├─ PublishedPathResolver
  ├─ PublishedFileOpener（root保持、component no-follow、handle identity検証）
  └─ 応答種別ごとのセキュリティヘッダー
        │
        ▼
  明示的に選ばれた共有ルートまたは単一サイトルート
```

サーバーは`TransferRecord`イベントをUI非依存の契約として通知します。WPF側はスレッド安全なキューへ蓄積し、250ms間隔でDataGridへ反映します。
同じイベントを有界Channelへ非同期投入し、UIスレッドやHTTP応答をファイルI/Oで待たせずJSON Linesログへ保存します。

## 5. コンテンツモードとURL設計

`Publication`は、無効な混在を型で作れない排他的な契約にします。

```csharp
abstract record Publication;
sealed record FileSharePublication(IReadOnlyList<ShareDefinition> Shares) : Publication;
sealed record WebsitePublication(WebsiteDefinition Website) : Publication;
```

開始要求も`FileShareStartRequest`と`WebsiteStartRequest`へ分けます。前者だけが利用者設定のportを持ち、後者は`FreshOnly` allocatorを要求します。実際のportを含む`BoundServerOptions`は、retired履歴への記録とbind成功後だけ生成します。これにより固定Webサイトportや、モードとport policyの不正な組み合わせを型で作れないようにします。

### ファイル共有モード

```text
/                         トップページ
/s/{shareSlug}/           共有ルートの目次
/s/{shareSlug}/{path}     ディレクトリまたはファイル
```

ファイル共有のディレクトリURLは末尾`/`を正規形とします。route値は解決前に末尾の1文字だけを除いて相対パスへ変換し、生成したサブフォルダーリンク、空フォルダー、多段フォルダー、親リンクを同じ規則で扱います。内部の空segmentを作る二重slash、dot segment、encoded traversal、backslashは正規化で救済せず拒否します。

- `shareSlug`は小文字ASCIIへ正規化し、完全一致で一意にします。
- 表示名からASCIIスラッグを作れない場合は、表示名のSHA-256から短い決定的サフィックスを作ります。同じ日本語名を再追加しても同じ候補になり、乱数でURLを変えません。
- 同一候補が複数ある場合だけ、WPF側で`-2`、`-3`の連番を付けます。
- 共有名と実フォルダー名は分離します。
- ルート選択は完全な1セグメント一致とします。
- ファイル共有内の実`index.html`を見つけても能動形式として添付ダウンロードにし、通常サイトとして実行させません。
- 自動目次はアプリ生成HTMLとして安全にエンコードします。

### Webサイトモード

```text
/                         サイトルートのindex.htmlまたはindex.htm
/{path}                   サイトルート内の静的ファイル
/{directory}/             ディレクトリのindex.htmlまたはindex.htm
/_easyhttp/pair           remoteペアリング用の予約route
```

- 1つのサイトルートだけを`/`へ割り当てます。
- ディレクトリの末尾slashを308で正規化し、相対URLを壊しません。
- indexがないディレクトリは404とし、自動目次を生成しません。
- `/s/...`は公開しません。
- `/_easyhttp`の先頭セグメントはアプリ専用とし、サイトファイルから上書きできません。
- SPAフォールバック、CGI、書き込みrouteは追加しません。

ファイル共有は既定18080を使います。Webサイトは49152〜65535のbrowser-safe候補から開始ごとにfresh portを割り当て、役割付きorigin port履歴へ原子的に記録して、ファイル共有、別サイト、別開始セッションへ意図的に再利用しません。ファイル共有で一度受理したportもWebsite候補から除外します。両モードも同時稼働させません。URLとQRは確定したportから自動生成し、利用者へport選択を要求しません。

## 6. ファイル解決

要求された相対パスをOSパスへ単純連結してはいけません。

`PublishedPathResolver`と`PublishedFileOpener`は共有とサイトで責務を分けて再利用し、次を保証します。

1. 選択された`Publication`以外の公開ルートを解決対象にしない。
2. URLをフレームワークが解析した後のセグメントとして扱う。
3. 空要素、`.`、`..`、バックスラッシュ、NUL、コロンを拒否する。
4. Windowsの末尾ドット・末尾空白、予約デバイス名、NTFS ADSを考慮する。
5. filesystem accessより先にroot文字列とdrive種別を分類し、Webサイト初期版ではUNC、device namespace、mapped network drive、network filesystemを拒否する。
6. 信頼したlocal volume rootから選択rootまでの全componentを、検証済み親handle基準で単一componentずつno-follow openし、全ancestor handleを`FILE_SHARE_DELETE`なしでセッション中保持する。
7. 要求時も保持root handleからhandle-relative no-followで走査し、絶対path openへfallbackしない。
8. rootとtarget handleのfinal pathとvolume identityを同じnamespaceへ正規化し、保持rootのtreeであることを確認する。
9. 属性、実ファイル名、MIMEを開いたhandleから確定し、検査済みの同じhandleを応答へ渡す。

Webサイトではさらにdot segment、Hidden/System属性、サーバー側スクリプト形式、秘密鍵形式、既知の設定ファイルを404にします。`/_easyhttp`はファイル解決より先に予約routeとして判定します。

通常openの後で最終pathだけを調べる方式と、検査後に子を絶対pathで開く方式は、検査前にreparse targetを追跡し得るため採用しません。Windows nativeのhandle-relative APIまたは同等保証を使い、応答後まで検査済みfile handleのidentityを維持します。

パス検証は単体テストとWindows統合テストの両方を用意します。

## 7. ファイル応答

### 共通

- KestrelとASP.NET Coreのファイル応答を利用します。
- Range解析を独自実装しません。
- `enableRangeProcessing`を明示的に有効化します。
- HEAD、Content-Length、Last-Modified、Range等はフレームワークの実装を利用します。
- ファイル全体をメモリへ読み込みません。
- 未知の拡張子は`application/octet-stream`とし、既定では添付ダウンロードにします。
- `X-Content-Type-Options: nosniff`を維持します。

### ファイル共有

- 共有内のHTML、SVG、JavaScript、XML等の能動的形式は同一originで実行させず、添付ダウンロードにします。
- アプリ生成ページへ`default-src 'none'`を基準とする厳格なCSPを適用します。
- FileShareの実ファイルにも厳格CSP、frame拒否、CORP、`private, no-store`を適用し、attachmentは能動形式・未知形式の成功応答だけに付けます。

### Webサイト

- HTML、CSS、JavaScript、SVG、画像、音声、動画、フォント、Wasm等を明示MIMEで通常配信します。
- バイト列と文字コードを変換せず、HTMLへ一律のUTF-8 charsetを追加しません。
- フォルダー内で完結する教材向けにself-only CSPを適用し、inline CSS/JSとsame-origin frameを許可します。
- 外部subresource、外部へのfetch/XHR/WebSocket、外部form送信、外部frame、JavaScript `eval`、Service Workerと各種Workerは初期版で許可しません。同一originのframe/fetch/formと、WebAssemblyコンパイルだけは対応するCSP sourceで許可します。通常リンク、meta refresh、JavaScriptによるトップレベル外部navigationは可能なため、通信不能sandboxとは扱いません。
- `Cross-Origin-Resource-Policy: same-origin`、CORSなし、Fetch Metadata検証を適用します。
- `Cache-Control: private, no-store`とします。

ヘッダー、MIME、予約route、認証を含む完全な契約は[Webサイトモード設計](website-mode.md)を正とします。

初期実装の転送観測はHTTP解析やRange計算と分離し、完了時刻、メソッド、相対URL、状態コード、Content-Length、処理時間をイベント化します。送信済み量・速度・切断理由の逐次観測は次フェーズです。

## 8. WPF / MVVM設計

初期実装は`MainWindowViewModel`を中心とし、共有一覧、サーバー状態、転送一覧、テーマ切替の状態を持ちます。画面が成長した段階で責務ごとのViewModelへ分割します。

転送一覧は`ObservableCollection`へ直接高頻度で追加・更新せず、バックグラウンドで状態を集約して毎秒4～10回程度UIへ反映します。DataGridの行仮想化を有効にします。

設定画面は旧版のPropertyGridを再現せず、一般、共有、ネットワーク、セキュリティ、ログ、詳細設定に分けます。通常画面の先頭で「ファイルを共有」「Webサイトを表示」を選び、前者では複数共有、後者では単一サイトルートだけを表示します。公開範囲は通常画面で「このPCだけ／ほかの端末にも公開」に分け、後者では「同じWi-Fi・家庭内LAN／VPN」を明示選択する排他的ラジオボタンを展開します。方式未選択では開始不可とし、VPNを自動選択しません。メインと設定は「このPCだけ→家庭内LAN→VPN」の概念順へ統一し、複数アダプターの選択と公開に必須でないグローバルIPv4診断はネットワーク設定へ収納します。

開始ボタンを無効化するだけで理由を隠さず、共有またはサイトルート未選択、別端末公開の方式未選択、利用可能アダプターなしをボタン直下とToolTipへ表示します。Webサイト選択時はHTML/JavaScriptが閲覧端末で実行されることを画面内で予告し、remote Webサイト開始ではサイトとネットワークの確認を1つのダイアログへ統合します。Tab順は視覚順と一致させ、状態色と補助文字色はModern / Classic双方で読める明示定義だけを使います。

テーマはResourceDictionaryへ分離します。

### 初回説明書

配布物の`Guide/`には、ZIPから直接開くレスポンシブHTML説明書と、ファイル共有でも能動コンテンツを実行せず読める`README.txt`を置きます。設定ファイルが存在しない完全な初回起動に限り、実行ファイル基準で`Guide/`を解決し、「EasyHTTPServer の説明書」としてファイル共有へ追加します。保存済み設定が空の場合、設定が破損・未対応の場合、同梱Guideが欠落した場合には追加しません。これにより利用者が削除した選択を尊重し、開発環境の絶対パスを配布設定へ埋め込みません。

```text
Themes/
├─ Modern.xaml
└─ Classic.xaml
```

同期・非同期Commandは、クリップボード競合等の回復可能な例外をDispatcherへ漏らしません。Command境界で例外を捕捉し、ViewModelが状態表示へ変換できる通知経路を持たせます。プロセス継続が安全でない致命的例外を無条件に握りつぶすグローバルハンドラーは設けません。

入力欄、一覧、状態表示には`AutomationProperties.Name`等のUI Automation情報を付けます。固定サイズの補助画面は文字拡大・高DPIでも内容へ到達できるようスクロール可能にします。これらはXAMLの存在確認に加え、Release版のキーボード操作とWindows Narratorで手動確認します。

WPFのViewModelはサーバー、フォルダー選択、クリップボード、ダイアログ、テーマを注入可能にします。Dispatcherタイマーは本番実装のまま、転送キューのフラッシュ処理を内部テストシームから検証します。コマンド状態、スラッグ重複、転送履歴上限、同期・非同期例外を自動テストします。

転送履歴のUI反映は250msごとに行いますが、1件ずつ`CollectionChanged`を発火しません。キューを最新200件のスナップショットへまとめ、1回のReset通知でDataGridへ反映します。

## 9. 設定保存

既定保存先は次とします。

```text
%LocalAppData%\EasyHTTPServer\settings.json
%LocalAppData%\EasyHTTPServer\origin-port-history.json
%LocalAppData%\EasyHTTPServer\logs\
```

- 設定には`schemaVersion`を持たせます。
- 一時ファイルへ書いてから置換し、途中終了で破損しないようにします。
- schema v2ではコンテンツモード、ファイル共有ポート、テーマ、共有一覧、単一サイト定義だけを許可リストで保存します。Webサイトportは保存せず、開始ごとに割り当てます。
- `origin-port-history.json`は`fileShareReserved`と`websiteRetired`を役割付きで保存します。version非依存のmachine-wide単一起動mutexをsettings読込前からprocess終了まで保持し、履歴更新は置換対象JSONと別の安定したprocess間排他lock内でread/validateからflush済み一時ファイルの原子的置換まで行います。lock順序はinstance mutex、history lockの順に固定し、abandoned取得後はdisk状態を再検証します。
- 履歴を先に分類します。完全新規profileと有効なschema v1＋履歴なしだけ新規作成を許し、schema v1＋有効履歴は既存集合へ旧portを冪等unionします。履歴の破損・未知schemaはsettings状態を問わず、schema v2または破損settings＋履歴欠落も全publication開始をfail closedにします。
- settingsは両モードの入力を保持できますが、開始時は`contentMode`で選んだ片方だけを`Publication`にします。非選択側を削除しません。
- schema v1の単一`port`はファイル共有ポートへ明示移行し、既存共有を失わず、初期モードをファイル共有とします。
- 起動時に存在しない共有ルートは復元も公開もせず、状態表示で利用者へ通知します。
- Website rootは存在確認より先に文字列とdrive種別を分類し、UNC、device namespace、mapped network drive、network filesystemならfilesystemへ触れず復元対象から外します。local rootだけをhandle-relative no-follow検証へ進めます。
- 通常の`settings.json`に限り、不正なJSON、未対応のschema、範囲外ポート、重複共有は安全な既定値へフォールバックします。元の設定ファイルは調査用に上書きしません。このfallbackをorigin履歴へ適用せず、履歴の欠落・破損・未知schemaは上記状態機械で扱います。
- パスワード、秘密鍵、トークンは通常設定へ保存しません。
- ポータブルモードを追加する場合も、秘密情報はDPAPI等で保護します。

## 10. ライフサイクル

- settingsとorigin履歴を読む前にversion非依存のmachine-wide mutexを取得してprocess終了まで保持し、2つ目のinstanceは状態を変更せず終了します。abandoned取得時は全永続状態を再検証します。
- `StartAsync`は構成検証後にだけ待ち受けを開始します。
- `StopAsync`は新規接続を停止し、猶予時間内で既存応答を終了させます。
- アプリ終了時は設定保存より先に`StopAsync`を待ち、秘密を破棄します。
- LANでは選択したアダプターとIPをセッションへ固定し、消失、ネットワーク停止、スリープ、Windowsロックでfail-closedします。
- 公開状態と一時的な操作通知を別の状態として保持します。
- UIから同期的にネットワーク停止を待ってフリーズさせてはいけません。
- グローバルIP確認は`IPublicIpResolver`へ分離し、ViewModelからHTTPクライアントや問い合わせ先を直接操作しません。
- 起動失敗、ポート競合、権限不足はUIの状態メッセージへ返します。型付きエラーへの整理は次フェーズです。

## 11. 構造化ログ

既定では`%LocalAppData%\EasyHTTPServer\logs\transfers.jsonl`へ保存します。

- 1行1JSONとし、許可するキーは`timestamp`、`method`、`path`、`statusCode`、`contentLength`、`durationMs`だけです。
- クエリ文字列、IPアドレス、HTTPヘッダー、Cookie、Authorization、実ファイルシステムパスは記録しません。
- ログ投入は有界キューで行い、満杯時は古い未書込レコードを破棄してサーバーとUIを停止させません。
- 既定で1ファイル5 MiB、現行を含め5世代までサイズローテーションします。
- ログ書込不能はプロセスを終了させず、そのセッションのファイルログを停止します。
- 終了時はサーバー停止後にキューを完了し、残件を書き出してから閉じます。

## 12. 負荷・耐枯渇試験

- 1 MiB以上の同一ファイルを50クライアントが同時にストリーミング取得し、全応答の内容ハッシュと完了イベント数を確認します。
- 各取得は`ResponseHeadersRead`で読み、クライアント側の全応答一括バッファリングを避けます。
- 低速ヘッダー試験は生TCPで不完全なHTTPヘッダーを送り、Kestrelの10秒制限後、15秒以内に408応答または切断されることを確認します。
- 接続枯渇試験は100本の不完全接続で設定上限を占有し、超過接続が受理され続けないことと、接続解放後5秒以内に正常配信へ回復することを確認します。
- 時間上限は性能スコアではなく、ハングや回復不能を検出する安全弁とします。

## 13. 将来拡張

コアを維持したまま、次を別ホストとして追加できる構造にします。

- コマンドライン版
- Windowsサービス版
- ブラウザー管理画面
- Windows ARM64配布

アップロードやCGI互換機能は、読み取り専用サーバーへ追加せず、別製品または明確に分離されたモジュールとして再設計します。

公開モードと認証の段階移行は[共有セッションと公開範囲](share-session-security.md)を正とします。
静的サイト公開の詳細は[Webサイトモード設計](website-mode.md)を正とします。
