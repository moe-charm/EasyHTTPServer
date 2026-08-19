# 共有セッションと公開範囲

更新日: 2026-08-19

Webサイトモードとmode別Cookieは実装済みです。現時点の検証範囲と残る実機QAは[現在のタスク](../current_task.md)を正とします。

EasyHTTPServer 2は常設のInternet-facingサーバーではなく、利用者が開始・停止する一時的な読み取り専用共有セッションを生成するアプリです。

## 公開範囲

| モード | 待ち受け | 認証 | 位置付け |
|---|---|---|---|
| このPCのみ | loopbackだけ | なし | 既定 |
| LAN | loopbackと明示選択したプライベートIPv4 | LAN側だけHTTPSとセッション限定認証を必須 | 基本機能 |
| VPN | loopbackと明示選択したVPNアダプターのIP | HTTPSとペアリングを維持 | 第一遠隔方式 |
| 外部トンネル | 未実装 | 未確定 | 初期リリース後の将来候補 |
| 直接ポート公開 | 専用機能・自動設定なし | LAN境界以上の保証なし | 技術的にはNAT転送され得るが非推奨・サポート外 |

`0.0.0.0`、`IPAddress.Any`、ワイルドカードHostへ利便性のためにフォールバックしません。選択したネットワーク、アドレスまたはセッションの不変条件が崩れた場合は、新しいアドレスへ自動追従せず共有を停止します。

通常画面では公開範囲を「このPCだけ」と「ほかの端末にも公開」に分けます。後者を選ぶと「同じWi-Fi・家庭内LAN」と「VPN」の排他的なラジオボタンを表示し、どちらかを利用者が明示選択するまで開始できません。Tailscale等のVPNは他用途でも動作し得るため、検出しただけで自動選択・公開しません。実際の接続方法とアダプター名を要約表示し、複数アダプターの変更は設定画面の「ネットワーク」へ置きます。候補がない方式は選択不能にします。別端末公開の有効状態と方式は保存せず、停止・再起動後はこのPCだけへ戻します。

方式未選択や候補なしで開始できない場合は、無効な開始ボタンだけに頼らず理由を同じ開始カード内へ表示します。ネットワーク設定も「このPCだけ、家庭内LAN、VPN」の順に揃えます。グローバルIPv4確認は共有開始に不要な外部診断なので通常画面には置かず、ネットワーク設定から利用者が明示実行します。

## コンテンツモード

公開範囲とは別に、開始セッション全体で次のどちらか1つを選びます。

| モード | 公開対象 | origin | ブラウザー実行 |
|---|---|---|---|
| ファイル共有 | 複数共有と自動目次 | 既定port 18080 | HTML等はattachment |
| Webサイト | 単一サイトルート | 開始ごとのfresh port | 許可した静的Web形式を通常実行 |

両モードを同時稼働させず、Webサイトoriginへファイル共有の`/s/*`を載せません。Webサイトportは利用者に設定させず、開始ごとにアプリがfresh originを割り当て、過去のサイト開始セッションへ意図的に再利用しません。完全な契約は[Webサイトモード設計](website-mode.md)を正とします。

## localhostとLANの同時待ち受け

LAN共有中も、このPCからは`http://127.0.0.1:<port>/`を利用できます。Kestrelにはloopback HTTPと選択LAN IPv4のHTTPSを別endpointとして登録し、`0.0.0.0`では待ち受けません。通常設定では両endpointが同じポートを使います。

- loopback endpointは`localhost`またはloopback IPだけをHostとして許可し、LAN認証を要求しない
- LAN endpointは選択したIPv4だけをHostとして許可し、HTTPSとLAN認証を必須にする
- Hostや認証の判定は、要求が到着したローカルendpointを基準に行う
- GUIの公開URL欄は、LAN共有中はスマートフォンへ渡すHTTPS LAN URLを優先表示する
- LAN endpointの安全停止時は共有セッション全体を停止し、loopbackだけを黙って残さない

これによりLAN共有中も管理PCで内容を確認でき、LAN用資格情報をlocalhostへ不要に送らずに済みます。

## LAN開始時の確認

LAN共有を開始する直前に、少なくとも次を利用者へ表示し、明示的な確認を得ます。

- 選択したネットワークアダプター名とIPv4
- ポートとHTTPS URL
- 公開される全共有、または単一Webサイトの表示名と実パス
- Webサイトの場合はHTML/JavaScriptが閲覧端末で実行されること
- HTTPS自己署名証明書と短時間ペアリングが必須であること
- 開始後に8桁コードとURLだけのQRが表示されること
- Windows Firewallやルーターを自動変更しないこと

LAN公開は設定へ保存せず、起動後は必ずOFFへ戻します。

## fail-closedライフサイクル

次の場合はLAN共有を停止し、自動再開しません。

- 選択したアダプターが消えた
- 選択したIPv4がそのアダプターから消えた
- ネットワークが利用不能になった
- Windowsがスリープへ移行した
- Windowsセッションがロックされた

無関係な別アダプターの変更だけでは停止せず、変更通知を契機に選択対象を再検証します。終了時は、待ち受け停止、セッション秘密の破棄、設定保存、ログ終了の順に処理します。

## 公開状態と操作通知

「停止中」「このPCに公開中」「LANへ公開中」「VPNへ公開中」は常時表示するセキュリティ状態です。URLコピー、グローバルIP診断、設定保存等の一時通知とは別のプロパティと表示領域を使い、操作通知が公開中表示を消さないようにします。

## ブラウザー実行

### ファイル共有モード

ファイル名と内容は信頼しません。共有内のHTML、SVG、JavaScript、XML等の能動的なWeb形式は、アプリの目次・認証UIと同じoriginで実行させず、`Content-Disposition: attachment`でダウンロードさせます。`nosniff`と厳格なCSPは防御層として維持します。

初期対象は次です。

- `.html`、`.htm`、`.xhtml`
- `.svg`、`.svgz`
- `.js`、`.mjs`
- `.xml`、`.xsl`、`.xslt`

### Webサイトモード

利用者が明示的に選択した単一サイトルートだけを開始ごとのfresh originへ割り当て、HTML、CSS、JavaScript、画像等を通常配信します。これは信頼できないサイトコードを安全化する機能ではありません。開始確認でコード実行を説明し、公開専用フォルダーを要求します。

- ファイル共有および過去のWebサイト開始セッションと異なるfresh originにする。
- 自動目次と`/s/*`を公開しない。
- self-only CSP、`nosniff`、CORP、CORSなし、Fetch Metadata検証を使う。
- Service Workerと各種Workerを初期版では禁止する。
- CGI、アップロード、削除、管理APIを追加しない。

## 認証の段階移行

LAN共有の既定認証は短時間ペアリングとします。Basic認証はファイル共有の移行中互換経路としてサーバー層に残しますが、通常GUIからは選択しません。WebサイトpublicationではBasicを拒否し、ペアリングだけを受け付けます。

### ペアリング契約

- コードは暗号学的乱数から作る8桁の数字とし、先頭の0も表示する
- コードは発行から5分、または最初の成功まで有効とする
- 共有セッション全体で失敗10回に達したら現在のコードを失効させる
- 入力はHTTPSの`POST /_easyhttp/pair`だけで受け、本文を1 KiB以下に制限する
- 期待`Origin`はRequest Hostやforwarded headerから作らず、開始時に固定したscheme、選択IPv4、実際のbind portから正規生成し、通常は完全一致させる
- 証明書警告を経た一部ブラウザーがpair form POSTの`Origin`を欠落または`null`にする場合だけ、検証済みremote endpointとHostに加え、ブラウザー管理の`Sec-Fetch-Site: same-origin`、`Mode: navigate`、`Dest: document`がすべて揃うことを互換fallbackとして要求する。OriginとFetch Metadataの両方がないクライアントは拒否する
- Request Hostは期待`Origin`とは独立に、同じ構成済みauthorityへ完全一致させる
- 成功時は128-bit以上のopaque tokenを発行し、tokenのハッシュだけをメモリへ保持する
- Cookieはモード別の`__Host-EasyHttpFilesSession`または`__Host-EasyHttpSiteSession`とし、`Secure`、`HttpOnly`、`SameSite=Strict`、`Path=/`、Domainなしとする
- Cookie、コード、tokenはログ、設定、URL、QRへ記録しない
- GUIの「新しいコード」で既存セッションを維持したまま未使用コードだけを再発行できる
- LAN共有停止時はコード、失敗回数、全tokenをメモリから破棄する

現在のファイル共有では未認証のLAN GETをペアリング画面へ移動し、HEAD等は資格情報を要求する応答とします。Webサイトモードでは、未認証のトップレベルGET navigationだけをペアリングへ誘導し、CSS、JavaScript、画像等のsubresourceは401にします。loopback endpointは従来どおり認証不要です。

2026-08-19のTailscale実ブラウザー確認で、pair画面GETは200でもform POSTが403となり、Cookie発行前に拒否される互換性不具合を確認しました。上記fallbackはこの実ブラウザー経路だけを復旧し、cross-site POST、任意Host、HTTP、Origin/Fetch Metadataを共に欠くPOSTは引き続き拒否します。

### QR契約

QRに含めるのはGUIへ表示しているHTTPS LAN URLだけです。コード、Cookie、証明書指紋、クエリ文字列、fragmentを含めません。QRはLAN共有中だけメモリ上で生成し、停止時に画面から除去します。

## 遠隔共有

### VPNアダプターモード

VPNでは選択IPへexact bindし、VPN内でもHTTPS、証明書指紋、短時間ペアリングを維持します。

- WindowsがTunnel、PPPまたは仮想インターフェースとして報告する稼働中NICだけを候補にする
- IPv4はRFC 1918またはCGNAT `100.64.0.0/10`だけを許可する
- 製品名やアダプター表示名を信頼境界に使わない
- Hyper-V等の通常の仮想EthernetはVPN候補にしない
- 選択したVPN NIC/IPの消失、ネットワーク停止、スリープ、ロックで共有全体を停止する
- VPNソフトの導入、ログイン、接続、ACL、端末承認をEasyHTTPServerから操作しない
- LAN、VPN、直接Internet公開を同時に有効化しない

VPNサービス側のACLで到達端末を絞ることを推奨しますが、VPN参加端末を無条件には信頼せずアプリ認証を残します。

### 外部トンネル

外部トンネルは初期リリースの完了を妨げない将来候補です。着手する場合は、通常loopback listenerと分離した専用origin、完全一致の公開Host、origin secret、限定されたforwarded-header処理を必須とします。外部サービスが公開TLSとInternet edgeを担当し、EasyHTTPServerはクラウドアカウントやAPIトークンを管理しません。
