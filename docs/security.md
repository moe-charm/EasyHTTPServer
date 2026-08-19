# EasyHTTPServer 2 セキュリティ設計

更新日: 2026-08-19

Webサイトモードに関する項目は実装済みの必須境界です。検証済み範囲と残る実機・配布QAは[現在のタスク](../current_task.md)を正とします。

## 1. セキュリティ目標

1. 明示的に選択された共有ルートまたは単一サイトルート外のファイルを公開しない。
2. 読み取り専用という境界を維持する。
3. 遅い接続や不正要求で、メモリ・スレッド・CPUを無制限に消費しない。
4. 認証情報や個人情報をログへ残さない。
5. 初期設定のままインターネットへ露出しない。
6. 古い設定ファイルをコードとしてデシリアライズしない。
7. Webサイトの能動コンテンツを、ファイル共有や別サイトと同じoriginへ混在させない。

## 2. 信頼境界

### 信頼するもの

- アプリ本体と署名済み配布物
- 現在の利用者がGUIから選択した共有ルートまたは単一サイトルートという公開境界
- アプリが生成し、スキーマ検証に成功した設定

### 信頼しないもの

- HTTP要求のすべての値
- URL、Host、ヘッダー、Cookie、Range
- ファイル名とディレクトリ名
- junction、symbolic link、reparse pointのリンク先
- 旧SOAP設定の型情報
- 外部ネットワークから得たIP情報
- ブラウザーから返される表示値
- Webサイト内のコードが安全・無害であるという仮定
- ブラウザーに残るStorage、Cache、Service Worker

## 3. 旧版で確認された主な危険

旧版を修正して継続利用するのではなく、新版の回帰テストへ変換します。

| 分類 | 旧版の問題 | 新版の方針 |
|---|---|---|
| パス | URLデコード後の文字列を共有パスへ連結 | 専用リゾルバーとルート包含検証 |
| CGI | 共有ファイルを外部プロセスとして実行 | 初期版から削除 |
| Range | suffix範囲とファイル長検証が不正 | ASP.NET Coreへ委譲 |
| HTTP解析 | 1バイト読み、行・ヘッダー上限なし | Kestrelへ委譲し制限を設定 |
| 並行処理 | 1接続1スレッドと共有Hashtable | 非同期処理とスレッド安全な状態管理 |
| 認証 | TLSなしBasic認証、平文保存 | HTTPS前提、秘密情報を別管理 |
| ログ | Authorizationを含む全ヘッダーを保存可能 | 許可リスト方式の構造化ログ |
| 設定 | SoapFormatterで型を復元 | JSONと安全な旧XMLインポーター |
| HTML | URI等をHTMLへ直接連結 | HTMLエンコードとCSP |
| origin | 実サイト、動的目次、複数共有を同じURL空間へ混在 | ファイル共有とWebサイトを別モード・別originへ分離 |

## 4. 既定の公開範囲

- 既定は`127.0.0.1`だけです。明示操作時だけ、選択したLANまたはVPN IPv4にも同時exact bindします。IPv6ループバック`::1`は待ち受け方式とテストを追加してから有効化します。
- ファイル共有の既定ポートは80ではなく18080です。Webサイトは開始ごとにアプリがfresh portを自動割り当てし、過去のサイト開始セッションやファイル共有へ意図的に再利用しません。
- LAN公開では待ち受けアドレスを明示的に選択し、管理PC用loopbackと同時にexact bindします。
- `IPAddress.Any`相当の全インターフェース公開は、既定にしません。
- インターネットへの直接公開、UPnP、ルーターの自動設定は行いません。
- LAN公開前に選択NIC、IPv4、ポート、公開共有、認証方式を確認し、選択対象の消失時は自動再bindせず停止します。
- VPN公開はOSが仮想・Tunnel・PPPとして報告するNICのRFC 1918またはCGNAT IPv4へexact bindし、VPNソフト自体は操作しません。
- コンテンツモードと公開範囲は直交させますが、ファイル共有とWebサイトを同時には待ち受けません。

## 5. HTTP制限

初期値は実装時に負荷試験で確定します。少なくとも次を明示的に設定します。

- 最大同時接続数
- リクエスト行の最大長
- ヘッダー総サイズ
- ヘッダー数
- ヘッダー受信タイムアウト
- Keep-Aliveタイムアウト
- 最低要求データ速度
- 最低応答データ速度
- 読み取り専用エンドポイントの要求ボディ拒否
- IP単位の短時間レート制限
- loopbackでは`localhost`またはループバックIP以外、LANでは選択IPv4以外のHostヘッダー拒否

これらは資源枯渇を抑えるものであり、インターネット規模のDDoS防御ではありません。

## 6. パスとファイルシステム

この節の入力拒否、handle-based opener、Webサイト固有のローカルroot制約は実装済みです。検証済み範囲は[現在のタスク](../current_task.md)を正とします。

次の入力をテストケースとして拒否します。

- `../`、`..\`
- `%2e%2e`、`%252e%252e`
- `%2f`、`%5c`
- ドライブ絶対パス
- UNCパス
- `C:`を含むNTFS ADS表現
- NUL
- 末尾ドット・末尾空白
- ファイル共有ディレクトリの正規な末尾slashは許可するが、内部の二重slashによる空segmentは拒否
- `CON`、`NUL`、`AUX`等の予約名
- 共有名の前方一致を利用した兄弟パス
- 共有配下から外へ向くjunction、symbolic link

ファイル名は表示前にHTMLエンコードし、URLではセグメント単位にエンコードします。

Webサイト初期版はfilesystem accessより先にrootを分類し、UNC、device namespace、mapped network drive、network filesystemを拒否します。信頼したlocal volume rootから選択rootまでの全componentを、検証済み親handle基準でno-follow openし、全ancestor handleを`FILE_SHARE_DELETE`なしで保持します。要求時も保持rootからhandle-relative no-followで走査し、絶対path openへfallbackしません。rootとtarget handleのfinal pathとvolume identityを比較し、属性、実ファイル名、MIMEをhandleから確定します。応答は同じ検証済みhandleから行います。

Webサイトではさらに、予約`/_easyhttp` route、dot path、Hidden/System属性、CGI等のサーバー側スクリプト、秘密鍵形式、既知の設定ファイルを公開しません。詳細な拒否対象は[Webサイトモード設計](website-mode.md)を正とします。

## 7. 認証とHTTPS

認証情報を平文HTTPで送信する機能は提供しません。

現在の契約は次のとおりです。

- ローカル限定: 認証なし
- 信頼できるLAN/VPN: HTTPSと短時間ペアリング

LANの既定認証は、5分・初回成功まで有効な8桁コードとメモリ内opaque Cookieセッションです。失敗は共有セッション全体で10回までとし、Cookieには`Secure`、`HttpOnly`、`SameSite=Strict`、`__Host-`接頭辞を必須にします。Webサイト実装時はファイル共有とサイトでCookie名を分け、tokenを開始セッションとモードへ結び付けます。HTTPS上Basic認証はファイル共有の移行中互換経路だけに残し、Webサイトpublicationでは拒否します。

pair POSTの期待`Origin`は、Request Hostやforwarded headerではなく、開始時に確定したscheme、選択IP、実際のbind portから正規生成します。Request Hostは同じ構成済みauthorityへ別途完全一致させます。Origin欠落/`null`の実ブラウザー互換経路は、検証済みremote endpoint/Hostとsame-origin top-level form navigationを示すFetch Metadataが揃う場合だけに限定し、両方を欠く要求やcross-site要求を拒否します。

URLクエリに永続トークンを埋め込む方式は、ブラウザー履歴、Referer、ログへ漏れるため既定にしません。

LAN/VPN開始ごとに対象IPだけをSANへ持つ自己署名証明書を生成し、秘密鍵を永続ファイルへ保存しません。利用者はPCに表示されたSHA-256指紋を別端末の表示と照合します。

## 8. ログ

ログへ記録してよい項目を許可リストで定義します。

- 時刻
- メソッド
- 秘密情報を含まない相対パス
- ステータスコード
- Content-Length（判明する場合）
- 処理時間

次は記録しません。

- Authorization
- Cookie / Set-Cookie
- パスワード
- アクセストークン
- TLS秘密鍵
- 要求ヘッダーの一括ダンプ
- 設定ファイル全体
- クエリ文字列
- 接続元IPとポート
- 共有の実ファイルシステムパス

転送ログは許可リスト方式のJSON Linesとし、任意のHTTPオブジェクト全体をシリアライズしません。要求パスはURLパスだけを最大2048文字で保存し、制御文字を含む値は`/`へ置換します。既定で1ファイル5 MiB、現行を含め5世代まで保持します。

## 9. ブラウザー応答とorigin

この節のWebサイト、ファイル共有へのCORP / Fetch Metadata、応答プロファイル分離、fresh origin境界は実装済みです。残る実機QAは[現在のタスク](../current_task.md)を正とします。

### 9.1 アプリ生成ページ

- 目次は型付きモデルから生成し、生の要求URIやファイル名をHTMLへ連結しません。
- `default-src 'none'`を基準とする厳格なCSP、`nosniff`、`no-referrer`、frame拒否、CORP `same-origin`、`no-store`を適用します。
- ペアリング以外の管理APIを公開しません。

### 9.2 ファイル共有モード

- 複数共有は同じファイル共有originに存在するため、HTML、SVG、JavaScript、XML等を添付ダウンロードにします。
- 実`index.html`を検出しても通常実行へ切り替えません。
- 実ファイルにも厳格CSP、`DENY`、`nosniff`、`no-referrer`、CORP `same-origin`、`private, no-store`を適用します。
- CORSを有効化せず、CORPとFetch Metadataで別originのsubresource埋め込みを拒否します。
- Webサイト機能追加後も、このdownload-only境界を回帰テストで固定します。

### 9.3 Webサイトモード

- 単一サイトルートだけを、ファイル共有および過去のWebサイト開始セッションと異なるfresh originの`/`へ割り当てます。
- HTML、CSS、JavaScript等を通常配信しますが、ファイル共有routeや別ルートを同一originへ置きません。
- self-only CSPで同一originのCSS/JS/画像/frame/fetch/formを許可し、外部subresource、外部へのfetch/XHR/WebSocket、外部form送信、外部frame、JavaScript `eval`、object、Service Workerと各種Workerを拒否します。WebAssemblyコンパイルだけは専用sourceで許可します。
- `X-Content-Type-Options: nosniff`、`Referrer-Policy: no-referrer`、`X-Frame-Options: SAMEORIGIN`、`Cross-Origin-Resource-Policy: same-origin`、`Cache-Control: private, no-store`を適用します。
- CORSを有効化せず、Fetch Metadataで別originのsubresource要求を拒否します。
- 利用者サイトのHTMLを変換せず、旧文字コードの宣言を尊重します。
- サイトコードは無害とは仮定しません。開始前に、閲覧端末でHTML/JavaScriptが実行されることを表示します。
- 通常リンク、meta refresh、JavaScriptによるトップレベル外部navigationは可能です。Webサイトモードは通信不能sandboxではなく、選択したコードがURLへデータを載せて外部遷移させることまでは防ぎません。

### 9.4 Service Workerと永続状態

localhostもService Workerを登録可能なsecure contextになり得ます。サイトoriginを後日のファイル共有へ使い回すと、永続登録されたコードが要求を横取りする危険があります。

- Webサイト開始ごとに49152〜65535のbrowser-safe集合からfresh portを割り当て、ファイル共有reservedとWebサイトretiredを役割付き履歴へ保存して意図的なorigin再利用を防ぎます。
- settings読込前のmachine-wide単一起動mutexと、origin履歴のprocess間排他read-modify-writeを併用します。
- Webサイトへ`worker-src 'none'`を送信し、初期版ではWorker全般を非対応にします。
- Cookieはポートで分離されないため、mode別Cookie名とセッション限定tokenを併用します。
- 同時稼働を禁止し、Webサイトoriginへ管理APIを追加しません。

完全なヘッダー、CSP、URL、認証契約は[Webサイトモード設計](website-mode.md)を正とします。

## 10. 旧設定の扱い

`Save/SaveFormHTTPServer.xml`を通常実行時に自動読込しません。

移行機能は次の制約を持ちます。

- DTDと外部エンティティを無効化する。
- 最大ファイルサイズを制限する。
- 許可した要素だけを文字列・数値として読む。
- CLR型名を解決しない。
- CGI設定を移行しない。
- パスワードを移行しない。
- 読み取った共有パスは利用者へ表示し、再承認を求める。

## 11. セキュリティ完了条件

- パストラバーサル試験がすべて拒否される。
- junctionを使った共有外アクセスが拒否される。
- root、途中directory、対象fileの差し替えでも、全componentをno-followで検査し、公開外の実体へ接続または配信しない。WebサイトではUNC、device namespace、mapped network drive、network filesystemのrootもfilesystem access前に拒否する。
- 不正RangeでCPUループや不正Content-Lengthが発生しない。
- 低速ヘッダー送信が期限内に切断される。
- 50以上の並行ダウンロードでもメモリ使用量がファイルサイズに比例しない。
- 100本の不完全接続で上限へ到達しても、解放後に正常なHTTP配信へ回復する。
- 認証を含む統合テスト後もログに秘密情報が存在しない。
- 旧SOAPファイルから任意型が生成されない。
- 公開ビルドに`Save/`、`log/`、旧`.pwd`が含まれない。
- 転送ログの許可キー、秘密情報除外、サイズ上限、保持世代数が自動テストで固定される。
- Webサイトとファイル共有を同一`Publication`、同じoriginへ混在できず、別サイト・別開始セッションにもfresh originを割り当てる。
- Webサイトから非選択ルート、`/s/*`、予約routeの実ファイルへ到達できない。
- ファイル共有では能動形式のattachmentが維持され、Webサイトでは許可した静的形式だけが通常表示される。
- Service Worker、外部subresource、外部へのfetch/XHR/WebSocket、外部form送信、外部frameが実ブラウザーで拒否される。同一originのframe/fetch/formは許可し、トップレベル外部navigationは保証対象外とする。
- LAN/VPNの未認証要求では、アプリ生成pair画面を除き、利用者サイトのHTMLと全subresourceを返さない。

LAN公開時の追加境界は[LAN公開のセキュリティ設計](lan-security.md)を正とします。LAN有効状態は永続化せず、HTTPS証明書とペアリングセッションが揃わない限り非loopbackへbindしません。
