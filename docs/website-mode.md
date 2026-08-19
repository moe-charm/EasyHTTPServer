# EasyHTTPServer 2 Webサイトモード設計

更新日: 2026-08-19

状態: **基盤実装済み・実機QA進行中**

この文書を、静的Webサイト公開機能の要件、境界、実装順、完了条件のSSOTとします。

## 1. 結論

旧版の「フォルダーへ置いた`index.html`を普通のホームページとして表示できる」機能を、EasyHTTPServer 2の初期リリース必須機能として復元します。

ただし、現在のファイル共有へ「HTMLを表示する」設定を足してはいけません。次の2つを、共有セッション全体で排他的なコンテンツモードとして実装します。

| 項目 | ファイル共有モード | Webサイトモード |
|---|---|---|
| 主目的 | ファイルを安全に渡す | 静的ホームページ・教材を表示する |
| 公開ルート | 1件以上の複数共有 | 1件のサイトルートだけ |
| URL | `/s/{slug}/...` | `/...` |
| 自動目次 | あり | なし |
| HTML、JavaScript、SVG等 | 添付ダウンロード | 通常表示・実行 |
| CGI、書き込み | なし | なし |
| ポート | 既定18080。設定で変更可 | 開始ごとに未使用の高位ポートを自動割り当て |

コンテンツモードと公開範囲は別の軸です。

```text
コンテンツモード       公開範囲
├─ ファイル共有       ├─ このPC
└─ Webサイト          ├─ LAN
                       └─ VPN
```

どちらのモードでも、LANとVPNでは現在のHTTPS、exact bind、Host検証、短時間ペアリング、安全停止を維持します。直接Internet公開、UPnP、外部トンネルはこの機能の対象外です。

## 2. 旧版から復元する価値

旧版には、同じ`index.html`という名前で次の2種類の機能がありました。

1. 利用者が作成した実ファイル
   - `/<公開フォルダー>/index.html`が存在すれば、既定MIME設定では`text/html`で通常配信した。
   - HTMLから参照する画像等も静的ファイルとして配信した。
2. サーバーが生成する仮想ファイル
   - 実`index.html`がないとき、ファイルとフォルダーの目次を動的生成した。
   - トップ目次のURLは`/{RootFileName}`で、既定設定の`RootFileName`が`index.html`だったため、通常は`/index.html`で全公開フォルダーの目次を生成した。

確認根拠:

- 作者のローカル完全版にある旧`readme.txt:19-23`: 簡単に設置でき、動的に`index.html`を作るHTTP Serverと説明
- 同`readme.txt:38-43`: 公開フォルダー追加から待ち受け開始までの短い操作
- `legacy/source-1.2/HTTPServer.cs:628-635`: `/{RootFileName}`でのトップ仮想目次生成
- `legacy/source-1.2/HTTPServer.cs:638-720`: 実ファイル優先と、ハードコードされた`index.html`によるフォルダー目次判定
- `legacy/source-1.2/HTTPServer.cs:729-757,853-866,1125-1158`: 静的ファイル配信、MIME判定、`Content-Type`応答
- `legacy/source-1.2/StartUp.cs:94-99`: 既定MIME設定の`.html`と`.htm`
- `legacy/source-1.2/FormHTTPServer.cs:95-99`: トップ仮想目次の`RootFileName`初期値`index.html`

旧版で学校の授業にも使われた価値は、実HTMLを表示できる前者です。新版では、実サイト表示をWebサイトモードへ、自動目次をファイル共有モードへ分離して両方を残します。

旧版の既定動作は、実サイトを`/<公開フォルダー>/index.html`で開き、`/`自体は404、`/index.html`は全共有の仮想目次でした。新版が単一サイトルートを`/`へ割り当て、`/`で実`index.html`を開くこと、および`index.htm`もfallbackにすることは、旧版そのままの再現ではなく、短い操作性を保つための意図的な改善です。

次の旧実装は復元しません。

- CGI、PHP、Perl、shebangによる外部プロセス起動
- 生成目次の前後へ任意HTMLを文字列連結するテンプレート
- 独自HTTP解析、独自Range解析
- URLとOSパスの文字列連結
- TLSなしBasic認証
- `IPAddress.Any`への無条件な待ち受け

## 3. 利用場面と対象範囲

### 3.1 対象

- HTML/CSSを学ぶ授業や教材の閲覧
- 手作りホームページの同一PC、LAN、VPN内プレビュー
- HTML、CSS、JavaScript、画像、音声、動画、フォントで完結する静的サイト
- ネストしたフォルダーと相対URLを使う既存サイト
- 旧Shift_JIS等、HTML自身が文字コードを宣言する既存ページ

### 3.2 初期版の対象外

- CGI、SSI、PHP、Perl、Python、ASP.NET等のサーバー側実行
- POSTされた内容の保存、アップロード、削除、WebDAV
- SPA向けの全パス`index.html`フォールバック
- 任意の既定文書名
- 複数サイトの同時公開
- ディレクトリ一覧
- Service Worker、PWA、Web Worker、Shared Worker
- 外部CDN、外部API、外部フレーム等を許可するCSP編集
- 自動ポート開放、UPnP、直接Internet公開
- 外部トンネルサービスとの連携

外部リンク、meta refresh、JavaScriptによるトップレベル画面遷移は可能です。初期版では外部subresource、外部へのfetch/XHR/WebSocket、外部form送信、外部frameを許可せず、直接読み込むリソース、programmatic通信、form送信、frameを選択したサイトルートと同じoriginに限定します。サーバー側はpair以外の書込み要求を拒否します。

## 4. ドメインモデルと不変条件

`ShareDefinition`へ`IsWebsite`のようなboolを追加しません。公開対象を型で分離し、複数共有と単一サイトを同時に渡せない構造にします。

```csharp
public abstract record Publication;

public sealed record FileSharePublication(
    IReadOnlyList<ShareDefinition> Shares) : Publication;

public sealed record WebsitePublication(
    WebsiteDefinition Website) : Publication;

public sealed record WebsiteDefinition(
    Guid Id,
    string Name,
    string RootPath);
```

開始要求も判別共用体にし、利用者指定portを持つWebサイト要求を型として作れないようにします。公開範囲は`Publication`と直交させますが、portの決め方はモードごとに固定します。

```text
ServerStartRequest
├─ FileShareStartRequest
│  ├─ FileSharePublication
│  ├─ Exposure / ListenAddress / RemoteSecurity
│  └─ ConfiguredPort
└─ WebsiteStartRequest
   ├─ WebsitePublication
   ├─ Exposure / ListenAddress / RemoteSecurity
   └─ PortAllocation: FreshOnly

BoundServerOptions（origin allocator成功後だけ生成）
├─ Publication
├─ Exposure / bound addresses
├─ ActualPort
└─ RemoteSecurity
```

必須の不変条件:

- 1回の開始で選べる`Publication`は1つだけ。
- ファイル共有は共有を1件以上持つ。
- Webサイトはサイトルートをちょうど1件持つ。
- Webサイトoriginへ`/s/{slug}`を公開しない。
- ファイル共有originへサイトファイルを公開しない。
- Webサイトとファイル共有を同時に待ち受けない。
- Webサイト開始ごとに新しいoriginを割り当て、過去のWebサイト開始セッションやファイル共有に使ったoriginを意図的に再利用しない。
- WebサイトではBasic互換認証を受け付けず、remote認証はペアリングだけにする。
- ルート、公開モード、公開範囲を、サーバー稼働中に差し替えない。

## 5. origin分離

originを次のように分けます。

```text
ファイル共有:
  http://127.0.0.1:18080/
  https://選択したLANまたはVPNのIPv4:18080/

Webサイト（開始ごとにfreshWebsitePortを更新）:
  http://127.0.0.1:<freshWebsitePort>/
  https://選択したLANまたはVPNのIPv4:<freshWebsitePort>/
```

Webサイトのポートは利用者設定にしません。候補集合はdynamic/private rangeの49152〜65535とし、WHATWG Fetchのbad port、製品対応ブラウザーのrestricted port、origin port履歴の`fileShareReserved`と`websiteRetired`を除外します。開始ごとに残りからCSPRNGで選び、候補を`websiteRetired`へ耐久記録してからexact bindを試します。flush済みの新しい正常な履歴が原子的置換によって耐久状態として可視化された時点を、予約のcommit pointとします。listenerやpublication handlerはstoreが成功を返し、再検証した履歴に候補が存在するまで有効にしません。crash後に旧履歴が残ればpre-commitとして未使用候補を再候補化でき、新履歴が残ればstoreのreturn前にcrashしていてもpost-commitとして二度と再利用しません。

bind再試行は、候補port固有の競合と判定できる`AddressAlreadyInUse`だけに限定し、1回の開始につき最大32候補とします。部分的に開いたlistenerをすべて閉じ、予約済み候補は`websiteRetired`へ残して次を選びます。`AccessDenied`、選択IP/NIC消失、証明書、HTTPS、構成不正などportを変えても直らないエラーは、その候補をretiredのままcleanupして即座に開始失敗とします。選択した1ポートを、その開始セッションのloopback HTTPとremote HTTPSで共用します。URLは開始ごとに変わりますが、通常画面は確定したURLとQRを自動表示するため、操作は増やしません。

ファイル共有portは、設定の受理時か開始前のどちらか早い時点で`fileShareReserved`へ記録します。ファイル共有自身は同じreserved portを安定して再利用できますが、Website allocatorは過去分を含めて選びません。`websiteRetired`に入ったportをファイル共有へ設定することは拒否します。これによりFileShare → port変更 → Websiteという時系列でも、能動サイトを過去のファイル共有originへ載せません。

アプリはsettingsやorigin履歴を読む前に、version、配置先、Windows利用者profileに依存しない安定名`Global\charmpic.EasyHTTPServer2.SingleInstance`のmachine-wide named mutexを取得し、process終了まで保持します。取得できない2つ目のプロセスは既存instanceが動作中だと表示し、listener、Cookie token、設定、履歴を一切作らず終了します。abandoned mutexは所有権を取得できた状態として扱いますが、前processの途中終了を前提にsettingsとorigin履歴を最初から再検証します。固定のmode別Cookie名はこの単一起動を不変条件とします。

さらにorigin port履歴はアプリ内部のセキュリティ状態として利用者設定と分けます。置換対象JSON自体ではなく、安定名`Global\charmpic.EasyHTTPServer2.OriginPortHistoryLock`の別kernel objectを使い、read、schema/整合性検証、候補選択または集合union、一時ファイルへの書込み、disk flush、原子的置換の全体をprocess間排他します。lock順序は`SingleInstance` → `OriginPortHistoryLock`に固定し、逆順取得を禁止します。history lockがabandonedなら所有権取得後にdisk上の履歴を再読込・再検証し、lockを解放するまでin-memory snapshotを信用しません。単一起動が異常終了した場合や別versionのプロセスが競合してもlost updateを許しません。mutexやhistory lockを安全に取得できなければserver開始をfail closedにします。

状態初期化は履歴を先に分類し、次の優先順でだけ行います。未列挙の組み合わせはfail closedです。

1. 履歴ファイルが存在して破損している、または未知schemaである場合は、settingsの状態を問わず全publication開始をfail closedにする。空集合で再作成しない。
2. settingsと履歴が両方ない完全新規profileだけは、既定ファイル共有portを`fileShareReserved`へ耐久記録してからschema v2 settingsを作る。
3. 有効なschema v1 settingsと履歴がない場合は、検証済みの旧portを含む履歴を耐久作成してからschema v2 settingsへ置換する。
4. 有効なschema v1 settingsと有効な履歴がある場合は、移行途中のcrashからの再開として、既存集合を消さず旧portを`fileShareReserved`へ冪等unionし、その耐久保存後にschema v2 settingsへ置換する。
5. 有効な履歴がありsettingsだけがない場合は、履歴を維持し、既定ファイル共有portを衝突検証後に`fileShareReserved`へunionしてから新しいschema v2 settingsを作る。
6. 有効なschema v2 settingsと有効な履歴は通常loadし、ファイル共有portと役割集合の整合性を再検証する。
7. schema v2 settingsと履歴欠落、または破損・未知schemaのsettingsと履歴欠落は、過去の履歴を安全に復元できないため全publication開始をfail closedにする。
8. 破損・未知schemaのsettingsでも有効な履歴が残る場合に限り、通常settingsだけの安全な既定値fallbackを使える。元settingsは上書きせず、採用するファイル共有portを履歴へ耐久unionしてから開始を許可する。
9. Website候補だけが枯渇した場合は、ファイル共有を維持できるがWebサイト開始を拒否する。

履歴だけを手動で消す操作は復旧にならず、schema v2 settingsとの組み合わせではfail closedします。完全初期化が必要な場合は、serverを停止し、過去に閲覧した全端末のサイトデータを削除したことを確認してから、`settings.json`と`origin-port-history.json`を対で削除します。通常GUIにはこの破壊的操作を設けません。同じサイトルートを再度開始する場合も新しいportを割り当てます。この保証の範囲は同じWindows利用者profileでアプリが保持する履歴です。別利用者profile、履歴の手動削除、別ソフトが過去に同じportを使った可能性までは証明できません。

専用originが必要な理由:

- WebサイトのJavaScriptから、ファイル共有の一覧やファイルを同一originとして読ませない。
- Local Storage、IndexedDB、Cache Storage等を、モード間だけでなく別サイト・別開始セッション間でも共有しない。
- 過去のタブやWebサイトoriginへ永続登録されたコードが、後日の別サイトやファイル共有originを制御しない。

Cookieはポートで分離されないため、ポートだけを認証境界にはしません。

- ファイル共有: `__Host-EasyHttpFilesSession`
- Webサイト: `__Host-EasyHttpSiteSession`
- tokenは開始セッション、モード、remote endpointごとに新しく生成する。
- Cookieは`Secure`、`HttpOnly`、`SameSite=Strict`、`Path=/`、`Domain`なしとする。
- 停止時にサーバー側tokenを破棄する。残ったブラウザーCookieは再利用できない。

## 6. URLとルーティング

### 6.1 予約名前空間

`/_easyhttp`を先頭セグメントとするパスは、大文字小文字やエンコード表現を含めてアプリ専用として予約します。

```text
/_easyhttp/pair           remoteペアリング画面
/_easyhttp/{anything}     未定義なら404
```

サイトフォルダー内に`_easyhttp`という実フォルダーがあっても配信しません。利用者コンテンツから予約routeを上書きできません。

ペアリング画面は未認証のremote要求にだけ表示します。認証済みremote要求とloopback要求の`/_easyhttp/pair`は404にし、WebサイトJavaScriptへアプリ画面や管理機能を公開しません。

### 6.2 サイトroute

| 要求 | 動作 |
|---|---|
| `/` | ルートの`index.html`、なければ`index.htm`を配信。`/`への実サイト割り当てと`index.htm` fallbackは新版の拡張 |
| `/index.html` | 実ファイルを配信 |
| `/lesson/` | `lesson/index.html`、なければ`index.htm`を配信 |
| `/lesson`がディレクトリ | path-onlyの`/lesson/`へ308リダイレクト |
| `/assets/site.css` | 実ファイルを配信 |
| indexがないディレクトリ | 404。目次を生成しない |
| `/s/...` | 404。ファイル共有routeを公開しない |
| 存在しないパス | 404。SPAフォールバックしない |

ディレクトリ末尾の`/`は、HTML内の相対URLを正しく解決するための契約です。リダイレクトは同じoriginの正規化済みpathだけを`Location`に使い、外部URLへ変換しません。queryはリダイレクトでは保持しますが、ファイル解決と転送ログには使いません。

サイト開始時はルートの`index.html`または`index.htm`を必須とし、見つからなければ待ち受け前に日本語で通知します。開始後に削除された場合は、一覧へフォールバックせず404にします。

## 7. ファイルシステム境界

現在の安全なパス解決規則をWebサイトでも共通利用します。`WebsiteDefinition`を一時的な`ShareDefinition`へ偽装するのではなく、ルートパスと相対パスを扱う共通のresolverへ責務を抽出します。

resolverとfile openerは次を保証します。

1. URLをフレームワーク解析後のセグメントとして扱う。
2. 空要素、`.`、`..`、バックスラッシュ、NUL、コロンを拒否する。
3. Windowsの末尾ドット・末尾空白、予約デバイス名、NTFS ADSを拒否する。
4. 設定またはUIから受けたroot文字列は、存在確認や通常openより先に分類する。UNC、device namespace、mapped network drive、network filesystemは初期版では拒否し、ローカルvolumeの絶対pathだけを次段へ渡す。
5. 信頼したローカルvolume rootを起点に、選択rootまでの全componentを、検証済み親directory handle基準の単一component no-follow openで順に取得する。Windows nativeのhandle-relative APIまたは同等性を試験できるAPIを使い、絶対pathによる通常openへfallbackしない。
6. 各ancestorと選択rootがreparse pointでないことを確認し、`FILE_SHARE_DELETE`なしのdirectory handleを開始セッション中保持する。これによりroot自身だけでなく祖先directoryのrename/deleteも許さない。
7. 要求時も保持中のroot handleを起点に、各directory componentと最終targetをhandle-relativeかつno-followで順に開く。各親handleは少なくとも子の検証完了まで`FILE_SHARE_DELETE`なしで保持し、検査前にjunctionやsymbolic linkのtargetを追跡しない。
8. rootとtarget handleのfinal pathとvolume identityを同じWindows namespaceへ正規化し、同じvolume・保持rootのtreeであることをseparator境界とhandle identityで確認する。
9. 拒否属性、実ファイル名、拡張子、MIME、長さは、文字列pathの事前情報ではなく、開いたhandleから確定する。
10. 検査済みの同じfile handleだけをGET、HEAD、Range応答へ渡し、検査後にpathを開き直さない。
11. いずれかのno-follow open、属性取得、identity比較、共有状態が期待どおりでなければ、内容を1 byteも返さずfail closedにする。

このTOCTOU契約を`PublishedFileOpener`へ隔離します。通常openしてから最終pathだけを確認する方式や、親を検査した後に子を絶対pathで開く方式は、確認前にroot祖先・途中componentのreparse targetへ接続し得るため採用しません。実装するWindows API、relative open、no-follow option、handle共有modeを統合テストで固定し、この保証を満たせないfilesystemは公開対象へ広げません。WPFのindex検出も`File.Exists`で先回りせず、この安全なroot分類とopenerを使います。

Webサイトモードでは、次も404として扱います。

- `.`から始まる任意のpath segment
- HiddenまたはSystem属性を持つファイル・ディレクトリ
- `.cgi`、`.pl`、`.php`、`.phtml`、`.asp`、`.aspx`、`.cshtml`、`.razor`
- `.pfx`、`.p12`、`.key`、`.pem`
- `web.config`、`appsettings.json`、`appsettings.*.json`

これは秘密検出機能ではありません。サイトルート内の到達可能な内容は公開物として扱うため、開始確認で「公開専用フォルダーを選ぶ」「秘密や個人情報を置かない」と明示します。`.well-known`等のdot path対応は、具体的な用途と試験を追加するまで将来機能とします。

## 8. MIMEとファイル応答

Webサイトモードは拡張子の明示マップだけでMIMEを決定し、内容から推測しません。少なくとも次を正しく通常配信します。

- HTML、XHTML、CSS
- JavaScript、ES modules、JSON、source map、Web Manifest
- SVG、PNG、JPEG、GIF、WebP、AVIF、ICO
- WOFF、WOFF2、TTF、OTF
- MP3、WAV、Ogg、MP4、WebM
- WebAssembly
- PDF、プレーンテキスト、XML

応答規則:

- 既知のサイト形式は`Content-Disposition: attachment`を付けない。
- 未知の拡張子は`application/octet-stream`とし、添付ダウンロードにする。
- `.svgz`等、MIME以外に`Content-Encoding`も必要な形式は、正しく実装するまで未知形式として扱う。
- ファイル内容を書き換えず、byte-for-byteで配信する。
- HTMLへ一律に`charset=utf-8`を追加しない。BOMやHTML内のcharset宣言を尊重する。
- ファイル全体をメモリへ読み込まない。
- GET、HEAD、単一Range、suffix Range、416をASP.NET Coreのファイル応答へ委譲する。
- `Content-Length`、`Last-Modified`、HEAD、Rangeの整合性を維持する。
- サイト応答は編集後の再読込と一時公開を優先し、`Cache-Control: private, no-store`とする。

## 9. 応答セキュリティプロファイル

現在の全応答一律ヘッダーを、応答の所有者ごとに3種類へ分離します。route解決後、bodyを書き始める前に`ResponseKind`を確定し、status codeだけで別profileへ切り替えません。

### 9.1 アプリ生成ページ

対象: ファイル共有トップ、生成目次、ペアリング画面とpairへのredirect、認証・rate limit応答、存在しないpath等のアプリ生成error/control応答。

```text
Content-Security-Policy:
  default-src 'none';
  style-src 'unsafe-inline';
  form-action 'self';
  base-uri 'none';
  frame-ancestors 'none'
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
X-Frame-Options: DENY
Cross-Origin-Resource-Policy: same-origin
Cache-Control: no-store
```

### 9.2 ファイル共有の実ファイル

対象: FileShare routeで安全に解決済みの実ファイル。GET、HEAD、206、Range不成立の416でもこのprofileを維持します。

```text
Content-Security-Policy:
  default-src 'none';
  form-action 'none';
  base-uri 'none';
  frame-ancestors 'none'
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
X-Frame-Options: DENY
Cross-Origin-Resource-Policy: same-origin
Cache-Control: private, no-store
```

- `Access-Control-Allow-Origin`は送信しない。
- HTML、SVG、JavaScript、XML等の能動形式と未知形式は、成功した200、HEAD、206で`Content-Disposition: attachment`にする。
- 画像等の既存inline-safe形式へ、一律にattachmentを追加しない。
- bodyのない416へattachmentの有無を契約せず、上記security headersだけを固定する。
- Webサイトモード追加を理由に、既存のdownload-only回帰を緩めない。

### 9.3 Webサイト

フォルダー内で完結する既存教材を動かしつつ、外部subresource、外部へのprogrammatic fetch、外部form送信、外部frame、永続実行を初期状態で抑えます。同一originのframe、fetch、formはCSP上許可しますが、サーバーはGET、HEADと限定されたpair POST以外を拒否します。

対象: Website routeで安全に解決済みの実ファイル、ディレクトリ末尾slashの同一origin 308、解決済みfileに対する416。存在しないpath、認証error、pairはアプリ生成profileを使います。

```text
Content-Security-Policy:
  default-src 'self';
  base-uri 'self';
  object-src 'none';
  frame-ancestors 'self';
  frame-src 'self';
  form-action 'self';
  script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval';
  style-src 'self' 'unsafe-inline';
  img-src 'self' data: blob:;
  font-src 'self' data:;
  media-src 'self' blob:;
  connect-src 'self';
  manifest-src 'self';
  worker-src 'none'
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
X-Frame-Options: SAMEORIGIN
Cross-Origin-Resource-Policy: same-origin
Cache-Control: private, no-store
```

`Access-Control-Allow-Origin`は送信しません。未知形式だけはWebsite profileを維持したまま`application/octet-stream`とattachmentを追加します。

| ResponseKind | 主な応答 | 適用profile |
|---|---|---|
| `AppGenerated` | FileShareトップ/目次、pair 200、pair redirect 302、400/401/403/404/405/413/429 | 9.1 |
| `FileShareFile` | FileShare実ファイルの200/HEAD/206/416 | 9.2。attachmentは形式と成功statusに応じる |
| `WebsiteContent` | Website実ファイルの200/HEAD/206/416、末尾slash 308 | 9.3。未知形式だけattachment |

このCSPの意味:

- フォルダー内の外部CSS/JSと、古い教材に多いinline CSS/JSは動作する。
- JavaScriptの`eval`は許可せず、WebAssemblyコンパイルだけを`wasm-unsafe-eval`で許可する。
- 同一originの古いframe/iframeは動作する。
- 外部CDN、外部APIへのfetch/XHR/WebSocket、外部画像、外部frame、外部form送信は初期版では動作しない。
- 外部サイトへの通常リンク、meta refresh、JavaScriptによるトップレベルnavigationは動作し得る。
- サイト作者が追加したCSPは、このヘッダーより緩くできない。
- CSPはサイトコードを無害化しない。選択したHTML/JavaScriptは閲覧端末で実行される。

現行CSPには一般的なトップレベルnavigationを全面禁止する契約を置けないため、Webサイトモードを「通信不能なsandbox」とは説明しません。サイトJavaScriptは、URLへデータを載せて外部へ遷移させることができます。したがって公開専用フォルダーを選ぶこと、サイトコードとその内容を信頼することが前提です。`Referrer-Policy: no-referrer`は通常のReferer漏えいを抑えますが、悪意あるコードによる明示送信の防止策ではありません。

サイト互換性のために`unsafe-inline`とWebAssembly専用の`wasm-unsafe-eval`は許可しますが、JavaScriptの`unsafe-eval`は許可しません。任意origin入力欄や「CSPを無効化」は初期版へ設けません。

### 9.4 両モード共通のFetch MetadataとCORS

- `Access-Control-Allow-Origin`を送信しない。
- `Sec-Fetch-Site: same-origin`の要求を許可する。
- `Sec-Fetch-Site: none`は、ヘッダーが揃う場合に`Mode: navigate`かつ`Dest: document`であるトップレベル遷移を許可する。
- `same-site`または`cross-site`は、`Mode: navigate`、`Dest: document`、`User: ?1`が揃う利用者操作のトップレベル遷移だけを許可する。
- `same-site`または`cross-site`のsubresource要求は403にする。
- Fetch Metadataを送らないクライアントは、Hostと認証を満たせばGET/HEADを利用できる。仕様にない値は将来互換のためヘッダーなしとして扱い、認証を迂回する信号には使わない。
- ペアリングPOSTの期待`Origin`はRequest Hostから組み立てず、構成済みendpointのscheme、選択IP、実際にbindしたportからURIとして生成する。Request Hostは別途、その構成済みauthorityへ完全一致させる。通常はOriginを完全一致させ、欠落/`null`時の互換fallbackは検証済みremote endpoint/Hostとsame-origin top-level form navigationのFetch Metadataが揃う場合だけ許可する。
- scheme違い、別port、別Host、cross-site Fetch Metadata、OriginとFetch Metadataを共に欠く要求は403とし、コード試行回数を消費しない。
- port 443の省略、IPv4 authority、将来のIPv6 bracketはURIの正規serializationで扱い、`X-Forwarded-Host`、`X-Forwarded-Proto`等は参照しない。
- すでに認証済みのブラウザーからのペアリングPOSTは拒否する。
- 未認証のトップレベルGET navigationだけをペアリング画面へ誘導し、CSS、JS、画像等のsubresource要求は401にする。

この判定はWebサイトだけでなくファイル共有originにも適用し、別portのサイトや外部ページからファイル共有をsubresourceとして埋め込ませません。

## 10. Service Worker

初期版ではService Workerを明示的に非対応とします。localhostもsecure contextとして扱われるため、HTTPのloopbackだから登録できないとは限りません。

対策:

- 開始ごとに新しいWebサイトoriginを割り当て、アプリが記録した過去のサイトoriginを別サイト、別セッション、ファイル共有へ再利用しない。
- `worker-src 'none'`でService Worker、Dedicated Worker、Shared Workerを禁止する。
- Service Worker登録失敗を実ブラウザーテストする。

`worker-src 'none'`は、このポリシー下からの新規登録を止めるもので、同じoriginへ過去に登録済みのService Workerを遡って削除するものではありません。`Service-Worker-Allowed`を送らないこと自体は登録禁止策にならず、ルートのscriptは既定で`/` scopeを取得できます。そのためfresh originを併用します。ただし別ソフトがそのportを過去に使っていなかったことまでは証明できません。表示異常や履歴消失時は、閲覧端末のサイトデータを削除して新しいoriginを発行します。アプリ停止だけで閲覧端末のStorageや登録を消せるとは説明しません。

将来PWAを支援する場合は、originの長期固定、登録解除不能時の扱い、別サイトへの切替、キャッシュ、更新、管理画面との分離を改めて脅威分析します。

## 11. 認証とネットワーク

### このPC

- 開始時に割り当てたfresh portのloopbackだけへHTTPでexact bindする。
- Hostは`localhost`またはloopback表現へ限定する。
- 現在どおり認証は要求しない。

### LAN / VPN

- loopback HTTPと、選択したLANまたはVPN IPv4のHTTPSへ同時exact bindする。
- remoteのHostは、選択IPv4と開始時に実際にbindしたfresh portに完全一致させる。
- remoteでは8桁・5分・初回成功まで有効なペアリングを必須にする。
- 未認証では、アプリ生成のペアリング画面を除き、利用者サイトのHTML、CSS、JS、画像等を1 byteも返さない。
- WebサイトpublicationではBasic互換認証を拒否し、ペアリングだけを受け付ける。
- QRとURLへコード、token、Cookieを含めない。
- 選択NIC/IP消失、ネットワーク停止、スリープ、Windowsロックで停止する。
- 証明書警告、指紋確認、公開範囲の表示は現在のLAN/VPN設計を使う。

開始確認には、少なくとも次を表示します。

- Webサイトフォルダー
- 公開範囲、NIC、IPv4、サイトポート
- `index.html`または`index.htm`の検出結果
- HTML/JavaScriptが閲覧端末で実行されること
- 外部fetch等は制限するが、リンクやscriptの外部画面遷移まで止める通信不能sandboxではないこと
- サーバー停止後も、閲覧・保存された内容を回収できないこと
- 公開専用フォルダーを使い、秘密情報を置かないこと

## 12. WPF操作設計

通常画面では、最初に目的を選びます。

```text
公開するもの
● ファイルを共有
○ Webサイトを表示

Webサイトフォルダー  C:\授業\ホームページ  [選択...]
状態                  index.html が見つかりました

公開範囲
● このPCのみ  ○ LAN  ○ VPN

[開始]  [URLをコピー]
```

操作規則:

- 停止中だけモードを変更できる。
- ファイル共有では現在の複数共有一覧を表示する。
- Webサイトでは単一フォルダーとindex検出結果だけを表示する。
- 非選択モードの設定は消さず、表示と公開から外す。
- 主要導線は「Webサイトを選択、フォルダーを選択、開始」の3操作以内とする。
- 開始後は正しいURLと、LAN/VPNではURLだけのQRを表示する。
- ポート分離はアプリが扱い、既定利用では追加操作を要求しない。
- 公開中のモード、ルート、範囲を常時表示し、一時通知で隠さない。
- モード選択、サイトフォルダー、検出状態へUI Automation名を付ける。
- ModernとClassic 2005の両テーマで同じ機能を提供する。

## 13. 設定schema v2

安全な設定だけを保存します。

```json
{
  "schemaVersion": 2,
  "contentMode": "fileSharing",
  "fileSharingPort": 18080,
  "isClassic": false,
  "website": {
    "id": "...",
    "name": "授業ホームページ",
    "rootPath": "C:\\授業\\ホームページ"
  },
  "shares": [
    {
      "id": "...",
      "name": "配布資料",
      "slug": "docs",
      "rootPath": "C:\\配布資料",
      "directoryBrowsingEnabled": true,
      "preferIndexFile": true
    }
  ]
}
```

移行規則:

- schema v1の`port`を`fileSharingPort`へ移す。
- Webサイトportは設定へ保存せず、開始ごとにfresh portを自動割り当てする。
- schema v1は`fileSharing`モードとして復元する。
- 既存共有を失わない。
- Website設定は未設定から開始する。
- Website rootは、存在確認や`Directory.Exists`より先にUNC、device namespace、mapped network drive、network filesystemを分類して拒否する。その後にlocal ancestorのno-follow検証を行い、存在しない、読めない、reparse pointであるrootは復元も公開もしない。
- LAN/VPN有効状態、証明書、コード、token、Cookieは保存しない。
- 未対応schemaの元ファイルを上書きしない現在の方針を維持する。

通常設定とは別に`origin-port-history.json`を保存します。

```json
{
  "schemaVersion": 1,
  "fileShareReserved": [18080],
  "websiteRetired": []
}
```

これはURL設定ではなくorigin再利用を防ぐセキュリティ状態です。設定load、設定保存、`StartAsync`の各境界で、ファイル共有portが`websiteRetired`にないことを検証します。欠落・破損時は空として続行せず、全publication開始を拒否します。

settingsは選択中の`contentMode`に加え、両モードの有効な入力を保持できます。`shares`と`website`が同じJSONに存在しても、非選択側は保存されるだけで`Publication`へ混在しません。モード切替で共有一覧やサイトrootを消さず、開始時は選択modeだけから型付き`ServerStartRequest`を作ります。

## 14. サーバー責務の分割

現在の`ServerController`へmode分岐を足し続けません。最低限、次へ分離します。

- `SingleInstanceGuard`
  - WPF hostの起動直後にmachine-wide mutexを取得し、process終了まで所有する
- `ApplicationStateInitializer`
  - mutex取得後にsettingsとorigin履歴を分類し、順序付き・冪等な新規作成またはschema移行を行う
- `OriginPortHistoryStore`
  - 役割集合の検証、安定lock、read-modify-write、flush、原子的置換を一括して所有する
- `OriginPortAllocator`
  - browser-safe候補の選択、bind前の耐久retire、bindエラー分類、有限再試行を所有する
- `ServerController`
  - ライフサイクル、Kestrel endpoint、共通制限だけ
- `PublicationPipeline`
  - `Publication`に対応するhandlerを1つだけ選ぶ
- `FileShareRequestHandler`
  - 現在のトップ、目次、download-only応答
- `WebsiteRequestHandler`
  - 予約route、末尾slash、index、静的ファイル応答
- `PublishedPathResolver`
  - 共有とサイトで共通のルート包含・reparse検証
- `PublishedFileOpener`
  - rootをrename不可で保持し、全componentをno-follow検査して同じhandleから配信
- `WebsiteContentTypePolicy`
  - 明示MIME、通常表示、拒否拡張子、未知形式
- `ResponseSecurityHeaders`
  - アプリ生成、ファイル共有、Webサイトの3プロファイル
- `RemoteRequestGuard`
  - 構成済みendpoint、Host、ペアリング、Origin、Fetch Metadata

WPF ViewModelはHTTP型やhandlerを参照せず、`Publication`とネットワーク設定を`StartAsync`へ渡します。

## 15. 実装順

1. ファイル共有モードの現行挙動を回帰テストで固定する。
2. `Publication`、`WebsiteDefinition`、machine-wide単一起動、browser-safe allocator、役割付きorigin port履歴の不変条件をCore/Serverへ追加する。
3. 設定schema v2とschema v1からの明示移行を実装する。
4. 共通path resolverとhandle-based file openerを抽出し、追加拒否規則とTOCTOUを先にテストする。
5. Webサイトroute、index探索、末尾slash、MIME、Rangeを実装する。
6. 応答ヘッダーを3プロファイルへ分離する。
7. mode別Cookie、ペアリングOrigin、Fetch Metadataを実装する。
8. WPFへモード選択、単一サイトフォルダー、事前検査、開始確認を追加する。
9. LAN/VPNのexact bind、HTTPS、ペアリング、安全停止を両モードで検証する。
10. 実ブラウザー、スマートフォン、配布ZIPでサイトを確認する。

各段階で既存ファイル共有の全テストを通し、HTML等のdownload-onlyを回帰させません。

## 16. テスト計画

### モデルと設定

- 1回のactive `Publication`へ複数共有とWebサイトを混在できない。永続settingsには両モードの有効な入力を保持できる。
- 0共有のファイル共有、0または複数ルートのWebサイトを拒否する。
- 49152〜65535だけからCSPRNGで割り当て、注入したbrowser restricted portを除外し、`AddressAlreadyInUse`候補はretireして最大32回だけ別候補へ進む。`AccessDenied`、IP/NIC消失、証明書・構成不正は全候補を消費せず即時失敗する。
- Edge/Chromeで実際の割当portへ到達でき、対応ブラウザーのrestricted port snapshotをreleaseごとに公式実装と照合する。
- `fileShareReserved`をWebsiteへ使わず、`websiteRetired`を全modeで再利用しない。旧FileShare portを変更後もWebsite候補へ戻さない。
- 完全新規profile、schema v1＋履歴なし、schema v1＋有効履歴、履歴だけ残った状態を一方向に初期化する。移行途中のcrash後は既存集合へ旧portを冪等unionする。破損/未知schemaのsettings＋有効履歴では元settingsを上書きせず、安全な既定portを履歴へunionして開始できる。schema v2＋履歴欠落、履歴の破損/未知schema、settings破損＋履歴欠落、候補枯渇はfail closedにする。
- 安定名のmachine-wide mutexで2つ目のinstanceを無変更終了させ、mutexまたはhistory lock保持processを強制終了してもabandoned ownership取得後にdisk状態を再検証する。固定lock順序を検査し、2 process更新でもlost updateしない。
- 履歴のtemp書込み・flush・atomic replace・store return前後でcrashを注入する。再起動後に旧JSONが残ればpre-commitとしてlistener未開始・未使用候補の再利用を許せる。候補を含む新JSONが残ればatomic replace済みのpost-commitとして、store return前、bind前、bind失敗、process crashのどの場合も候補が将来集合へ戻らない。
- 手編集settingsを含め、load、save、Startの各境界でファイル共有portと`websiteRetired`の衝突を拒否する。
- schema v1をファイル共有として無損失移行し、FileShare → Website → FileShare後も共有一覧とsite rootの両方を保持する。
- crafted Website rootのUNC、device namespace、mapped network drive、network filesystemをfilesystem access前に拒否し、消失・不正・reparse rootを復元しない。

### URLと表示

- `/`、明示`/index.html`、`index.htm` fallbackが200になる。
- ネストしたindexと相対CSS、JS、画像、frameが動作する。
- `/dir`が同一originの`/dir/`へ308になる。
- indexなしのディレクトリが404で、目次を表示しない。
- `/s/*`が404になる。
- `/_easyhttp/*`を実ファイルで上書きできず、`/_EASYHTTP`、大小文字違い、encoded表現でも予約routeを迂回できない。
- 認証済みremoteとloopbackの`/_easyhttp/pair`が404になる。
- queryがファイル解決とログへ混入しない。
- 日本語、空白、`%`、`#`を含む正常なファイル名をセグメント単位のURL encodingで取得できる。

### MIMEとHTTP

- HTML、CSS、JS、ES module、JSON、SVG、画像、フォント、音声、動画、Wasm、PDFのContent-Typeが正しい。
- サイト形式にattachmentが付かない。
- 未知形式にはoctet-streamとattachmentが付く。
- Shift_JIS fixtureをbyte-for-byte配信し、UTF-8 charsetを強制しない。
- GET、HEAD、Range、suffix Range、416が一致する。
- pair POSTを除くPOST、PUT、DELETE、PATCH、OPTIONSと要求bodyを拒否し、pair bodyの1 KiB上限を維持する。
- 10GB疎ファイルを全読み込みしない。
- 50並列応答後も回復する。

### パスと秘密候補

- `..`、多重encoding、encoded separator、バックスラッシュ、NUL、ADS、末尾空白・ドット、予約名を拒否する。
- junction、symbolic link、実行中のreparse差し替えで外へ出ない。
- local volume rootから選択rootまでのancestor junction、root rename、途中component差し替えを拒否し、no-follow検査前にlocal外やUNCのreparse targetへ接続しない。
- UNC、device namespace、mapped network drive、network filesystemのサイトルートを存在確認前に拒否する。
- 保持親handle基準の単一component openだけを使い、絶対path fallbackを試験用hookで検出して失敗させる。
- 最終実体パスを検証した同じfile handleから応答し、検査後のpath再openを行わない。
- dot segment、Hidden/System、CGI系、秘密鍵、`web.config`、`appsettings*.json`を404にする。

### origin、CSP、認証

- Webサイトoriginからファイル共有originの内容を読めない。
- 連続するWebサイト開始でfresh portが変わり、retired portを別サイト、別セッション、ファイル共有へ再利用しない。
- 非選択モードのポートが待ち受けていない。
- CORSヘッダーがなく、CORPがsame-originである。
- 外部subresource要求をFetch Metadataで拒否する。
- address bar、QR、利用者がクリックしたcross-siteリンクのトップレベルnavigationを許可する。
- 不正・未知のFetch Metadata値だけで認証やHost検証を迂回できない。
- inline/local CSS・JS、同一origin frameが実ブラウザーで動作する。
- WebAssemblyと同一originのframe/fetch/formが動作し、CDN subresource、外部へのfetch/XHR/WebSocket、外部form送信、外部frame、JavaScript `eval`、Worker、Service Workerが実ブラウザーで失敗する。
- 通常リンク、meta refresh、JavaScriptのトップレベル外部navigationが可能であり、通信不能sandboxではないことをUIと文書で確認できる。
- 過去のService Workerが残る試験profileでは、サイトデータ削除または未使用サイトportへの変更後に正常表示できる。
- remote未認証では、アプリ生成pair画面を除き、利用者サイトのHTML、CSS、JS、画像を返さない。
- 未認証のトップレベルnavigationだけがpairへ移動し、subresourceは401になる。
- 正しいペアリング後だけサイトを返す。
- pair POSTのOrigin違いを、失敗回数を消費せず拒否する。
- expected Originを構成済みendpointから生成し、Request Hostとforwarded headerに依存しない。
- WebサイトpublicationでBasic認証を拒否する。
- Webサイトの401はBasic challengeを送らず、ブラウザーのユーザー名・パスワードdialogを出さない。
- mode別Cookieを相互利用できず、停止後tokenが無効になる。
- Host spoof、別scheme、別portを拒否する。
- URL、QR、ログ、設定へコードやtokenが入らない。
- Webサイト転送ログも許可キーだけを使い、実path、query、Cookie、tokenを含めない。
- FileShare → Stop → Website → Stop → FileShareで非選択listenerが残らず、停止後のサイトportが閉じ、次回は別originになる。
- 正常Stop、bind失敗、設定/認証初期化失敗で、listener、証明書、pair token、QRに加えて全ancestor/root/file handleを解放し、元フォルダーをrename/deleteできる。
- 進行中responseは停止猶予中だけ検証済みfile handleを保持でき、猶予終了またはresponse完了後に全handleを解放する。

### 応答ヘッダープロファイル

- `AppGenerated`のpair 200/302、目次200、400/401/403/404/405/413/429へ、9.1の厳格CSP、`DENY`、`nosniff`、CORP、`no-store`を完全一致で適用する。
- `FileShareFile`の200、HEAD、206、416へ9.2を適用し、能動/未知形式の成功応答だけをattachmentにしてinline-safe形式と416へ強制しない。
- `WebsiteContent`の200、HEAD、206、416、308へ9.3のself CSP、`SAMEORIGIN`、`nosniff`、CORP、`private, no-store`を完全一致で適用する。
- 同じURLでもresponse kindとstatusに対応する1つのprofileだけが選ばれ、Website追加でFileShareやpair/errorへself CSPを一律適用しない。

### WPFと配布物

- 3操作以内でWebサイトを開始できる。
- index不在と無効rootを開始前に説明し、秘密を自動検出できるとは表示せず、公開専用フォルダーを使う一般警告を出す。
- 設定UIにはファイル共有portだけを置き、Webサイトport入力欄を設けず、実際に割り当てたportをURLとQRへ反映する。
- FileShare → Website → FileShareと切り替えても、共有一覧とsite rootを失わず、active modeだけを公開する。
- Modern / Classic 2005、キーボード、Narrator、文字拡大で操作できる。
- 公開中のモード、ルート、範囲が常時分かる。
- Release版とself-contained ZIP展開版から、HTML/CSS/JS/画像を含む授業用fixtureをEdge/Chromeで表示できる。
- LAN/VPNではスマートフォンからペアリングし、同じfixtureを表示できる。

## 17. 完了条件

- Webサイトモードは、選択した1フォルダー以外を公開しない。
- `/`で実在する`index.html`または`index.htm`を200、正しいMIME、attachmentなしで表示する。
- HTML、CSS、JavaScript、画像、音声、動画、フォントの相対参照が実ブラウザーで動く。
- ネストしたディレクトリの末尾slashと相対URLが正しく動く。
- indexなしのディレクトリは一覧を生成せず404になる。
- ファイル共有の複数共有、自動目次、download-only、安全なpath境界を変更しない。
- Webサイトとファイル共有が同一originや同じ開始セッションへ混在しない。
- machine-wide単一起動と役割付き履歴を維持し、別サイト・別開始セッションへbrowser-safe fresh originを割り当て、FileShare reservedとWebsite retiredを誤用しない。
- 予約route、秘密候補、path traversal、reparse point、ancestor/root/component/file差し替えから公開外へ出られない。UNC、device namespace、mapped network drive、network filesystemのrootをfilesystem access前に拒否し、handle-relative no-followから絶対pathへfallbackしない。
- Service Worker、Worker、外部subresource、外部へのfetch/XHR/WebSocket、外部form送信、外部frameが初期CSPの契約どおり失敗し、同一originのframe/fetch/formが動作する。トップレベル外部navigationはこの保証に含めない。
- LAN/VPNではHTTPSとペアリング完了前に、アプリ生成pair画面を除く利用者サイト内容を返さない。
- CGI、アップロード、削除、サーバー側実行を追加しない。
- 基本導線が3操作以内で完了する。
- 全自動テスト、実ブラウザー、スマートフォン、配布ZIPのQAが成功する。

## 18. 将来候補

初期版を完成させたあと、必要性と脅威モデルを再確認して検討します。

- SPAフォールバック
- 任意の既定文書名
- `.well-known`の限定許可
- 外部リソースoriginの完全一致許可リスト
- キャッシュポリシーの選択
- Service Worker / PWA
- 複数サイトの同時公開と、サイトごとの独立origin
- 外部トンネル連携

## 19. 標準・公式資料

- [RFC 6265 section 8.5](https://www.rfc-editor.org/rfc/rfc6265.html#section-8.5): Cookieは同一hostのport間を分離しないため、専用portだけを認証境界にしない。
- [W3C Content Security Policy Level 3](https://www.w3.org/TR/CSP/): `worker-src`はWorker、SharedWorker、ServiceWorkerを制御し、`wasm-unsafe-eval`はJavaScript `unsafe-eval`と分離できる。
- [W3C Secure Contexts](https://www.w3.org/TR/secure-contexts/): `127.0.0.0/8`と条件を満たすlocalhostはpotentially trustworthyになり得るため、loopback HTTPでもService Worker対策が必要。
- [W3C Fetch Metadata Request Headers](https://www.w3.org/TR/fetch-metadata/): `Sec-Fetch-Site`、`Sec-Fetch-Mode`、`Sec-Fetch-Dest`で、利用者navigationとcross-origin subresourceを区別する。
- [IANA Service Name and Transport Protocol Port Number Registry](https://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.xhtml): 49152〜65535をdynamic/private rangeの候補母集団として使う。
- [WHATWG Fetch Standard: Port blocking](https://fetch.spec.whatwg.org/#port-blocking): HTTP(S) fetchがbad portでブラウザーに拒否され得るため、allocator候補をbrowser-safe集合へ限定する。
- [Chromium `net/base/port_util.cc`](https://chromium.googlesource.com/chromium/src/+/refs/heads/main/net/base/port_util.cc): Edge/Chrome系のrestricted port実装をrelease時の互換性照合に使う。
- [Microsoft Learn: Minimal API responses](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0#file-result-types): `TypedResults.File`は`Stream`を受け取れる。
- [Microsoft Learn: FileStreamHttpResult.EnableRangeProcessing](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpresults.filestreamhttpresult.enablerangeprocessing?view=aspnetcore-10.0): 検査済みstreamからのRange応答をフレームワークへ委譲できる。
