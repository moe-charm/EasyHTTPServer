# LAN公開のセキュリティ設計

更新日: 2026-08-19

## 1. 目的と境界

LAN公開は、信頼できる家庭内・社内・学校内LANで、一時的にファイルを渡す、または静的Webサイトを表示する機能です。インターネット、ポート転送、公衆Wi-Fi、ゼロトラスト認証の代替にはしません。

既定動作は従来どおり`127.0.0.1`のHTTPです。LAN公開は利用者が開始前に明示的に有効化したセッションだけで使え、設定へ保存しません。再起動後は必ずOFFへ戻ります。

## 2. 待ち受け

- 全NICの`0.0.0.0`ではなく、利用可能なRFC 1918プライベートIPv4を1つ選んでbindします。
- loopback、リンクローカル、APIPA、パブリックIPにはLANモードでbindしません。
- 要求のHostはbindしたIPアドレスとの完全一致だけを許可し、DNS rebindingや別名Hostを拒否します。
- Windows Firewallの許可はアプリが自動変更しません。必要な場合だけ利用者がOS側で許可します。

## 3. TLS証明書

- LAN開始ごとにECDSA P-256の自己署名証明書をメモリ上で生成します。
- SANにはbind対象IPだけを含め、Server Authentication EKU、Digital Signature、CA=falseを設定します。
- 有効期間は生成直前から7日間ですが、証明書ストアへ登録せず、PFXファイルも作りません。Windows Schannelは完全なephemeral鍵をサーバー証明書に利用できないため、セッション中だけCurrent User鍵プロバイダーへ一時ロードし、停止時に証明書オブジェクトと秘密鍵を破棄します。
- SHA-256指紋をGUIへ表示します。別端末で警告を例外登録する前に、このPCの表示と一致することを確認します。

自己署名証明書はOSやブラウザーから自動的には信頼されません。指紋を確認しない場合、能動的な中間者攻撃を完全には防げません。将来、ローカルCA・利用者指定証明書・OS証明書ストア連携を別機能として設計します。

## 4. ペアリング認証

- LAN開始ごとに暗号学的乱数から8桁コードを生成し、5分または最初の成功まで有効にします。
- 失敗10回で現在のコードをロックし、GUIからの明示操作だけで再発行します。
- 成功時は256-bitのopaque tokenを発行し、SHA-256ハッシュだけを最大16セッション分メモリへ保持します。
- Webサイトモード実装後はCookie名を`__Host-EasyHttpFilesSession`と`__Host-EasyHttpSiteSession`に分けます。どちらもSecure、HttpOnly、SameSite=Strict、Path=/、Domainなしです。
- コード、token、Cookieは設定・転送ログ・URL・QRへ保存しません。QRのpayloadはHTTPS URLだけです。
- 既存の接続元別レート制限を認証より前に適用し、総当たりと接続占有を制限します。
- pair POSTの期待`Origin`は開始時のscheme、選択IPv4、実際のbind portから生成し、Request Hostとforwarded headerからは組み立てません。Hostは構成済みauthorityへ別途完全一致させます。通常はOrigin完全一致を要求し、証明書警告後にOriginが欠落または`null`になる実ブラウザーだけ、同じremote endpoint/Hostと`Sec-Fetch-Site: same-origin`、`Mode: navigate`、`Dest: document`の組を互換fallbackとして受け付けます。
- HTTPS Basic認証はファイル共有の互換経路としてサーバー層に残しますが、通常GUIでは使用しません。Webサイトpublicationでは拒否します。

## 5. UIとライフサイクル

- 通常画面の「ほかの端末にも公開」と、設定画面のLAN / VPN詳細選択は停止中だけ変更できます。
- LANアドレスが見つからなければ開始せず、状態表示へ理由を返します。
- 開始後はHTTPS URL、URLだけのQR、8桁コード、証明書SHA-256指紋を表示します。
- 停止・開始失敗・アプリ終了時に証明書秘密鍵、コード、全Cookieセッション、QR表示を破棄します。
- URLコピーへ資格情報を埋め込みません。
- Webサイト開始確認では、選択した単一ルートと、HTML/JavaScriptが閲覧端末で実行されることを表示します。
- Webサイトでは、アプリ生成のペアリング画面を除き、利用者サイトのHTML、CSS、JavaScript、画像をペアリング完了前に1 byteも配信しません。

## 6. 完了条件

- loopback HTTPを含む既存テストが変わらず通る。
- LAN用ServerOptionsがTLS証明書や認証セッションなしで生成・起動できない。
- LAN HTTPSは未認証GETをペアリング画面へ誘導し、誤コードを401、正しいコード後を200、異なるHostを400にする。
- 証明書のSAN、EKU、鍵用途、CA=false、有効期間、指紋をテストする。
- コードの形式、期限、初回失効、10回ロック、再発行をテストする。
- ログにコード、Cookie、tokenが含まれない。
- ファイル共有とWebサイトの両方で、同じHTTPS、Host、ペアリング、安全停止の境界が通る。

## 7. 対象外

- インターネット公開とルーター設定
- HTTPからHTTPSへのLANリダイレクト
- 証明書警告の自動回避やルート証明書の自動インストール
- 永続ユーザー、権限、監査ログ
- UPnP、NAT traversal、外部IP取得
