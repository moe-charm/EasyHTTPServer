# EasyHTTPServer

EasyHTTPServer は、2005年に公開された Windows 向けの簡易HTTPファイル／Webサーバーです。

この公開リポジトリには、Visual C# Express 2005へ移行した1.2系の旧ソースと、安全な現代版 **EasyHTTPServer 2** を保存しています。雑誌掲載時期の旧配布EXE・DLLは、安全性と公開履歴の明確さを優先して含めていません。

## 現在の状態

- `legacy/source-1.2/` は主に2005～2006年の C# / Windows Formsソースです。旧DLLとビルド出力は含みません。
- `src/` と `tests/` に .NET 10 / Kestrel / WPF版の動作する基礎実装があります。
- 新版は読み取り専用です。ファイル共有モードに加え、旧版の静的ホームページ表示を安全に復元したWebサイトモードを利用できます。
- 既定はループバック限定で、明示的なLAN HTTPS公開ではセッションごとの証明書と短時間ペアリングを必須にします。
- 現在の作業と次のタスクは [`current_task.md`](current_task.md) を正とします。

## 重要な注意

旧版は、現在のLANやインターネットへ公開して使用しないでください。パス解決、CGI、Basic認証、HTTP解析、Range処理などに、現代の基準では危険な実装があります。

また、`Save/SaveFormHTTPServer.xml` は旧版の実使用設定です。平文の認証情報を含む可能性があるため、公開リポジトリや配布物へ含めてはいけません。

## 新版の基本方針

正式名称を **EasyHTTPServer 2** とし、次の構成で新規実装します。新版のコードはMITライセンスです。2005年版の歴史資料は同梱された従来条件を維持します。

- 言語・ランタイム: C# / .NET 10 LTS
- HTTPサーバー: ASP.NET Core Kestrel
- GUI: WPF
- UI設計: MVVM
- 初期対応OS: Windows
- 初期リリース対象: 読み取り専用のファイル共有と静的Webサイト表示

旧版のネットワークコードを行単位で移植することはしません。旧版は製品仕様と歴史資料として参照し、HTTP処理は保守されているフレームワークへ委譲します。

## 新版を試す

必要環境は Windows と .NET 10 SDK です。

```powershell
dotnet restore EasyHTTPServer.sln
dotnet test EasyHTTPServer.sln
dotnet run --project src/EasyHttpServer.Desktop.Wpf
```

配布版の完全な初回起動では「EasyHTTPServer の説明書」が共有フォルダーへ登録されています。そのまま「開始」を押すと、このPCのブラウザーでファイル共有を試せます。完全版の説明書は、配布フォルダーの`Guide/index.html`を直接開いてください。説明書共有を削除した後は自動で復活しません。

自分のファイルを共有するときは「追加…」でフォルダーを選び、「開始」を押します。表示されたURLは既定ではこのPC内からだけアクセスできます。終了時は稼働中のKestrelホストを正常停止します。

GUIの「公開目的」でファイル共有とWebサイトを切り替えます。Webサイトモードは、選択した単一フォルダーの実`index.html`または`index.htm`、CSS、JavaScript、画像等を通常表示します。ファイル共有と同時には公開せず、ディレクトリ一覧、CGI、アップロードは有効にしません。

別端末へ一時共有する場合は、停止中に「ほかの端末にも公開」を選び、続けて「同じWi-Fi・家庭内LAN」または「VPN」を明示選択してから開始します。アプリがTailscale等のVPNを勝手に選ぶことはありません。複数アダプターの選択は設定画面の「ネットワーク」で変更できます。別端末ではURLだけを含むQRまたは表示URLを開き、PCに表示された8桁コードでペアリングします。コードは5分または初回成功まで有効です。自己署名証明書のSHA-256指紋もこのPCの表示と照合してください。別端末へ公開中も、このPCからは同じポートの`http://127.0.0.1:<port>/`側を認証なしで利用できます。証明書警告を無確認で回避したり、インターネットや公衆Wi-Fiへ公開したりしないでください。

現在実装済みの主な機能:

- 複数の共有フォルダーと共有ごとのURLスラッグ
- GET / HEAD、Range、suffix Range、大容量ファイルのストリーミング
- HTMLエスケープ済みの自動目次
- パストラバーサル、ADS、Windows予約名、reparse pointの拒否
- 接続・ヘッダー・タイムアウト・レート制限
- WPFでの追加、削除、開始、停止、URLコピー、転送一覧
- Modern / Classic 2005テーマ、設定画面、History / About
- 明示的に有効化するLAN HTTPS、8桁短時間ペアリング、URLだけのQR、証明書指紋表示
- LANの待ち受けNIC/IP選択、開始前の公開範囲確認、ネットワーク・スリープ・ロック時の安全停止
- VPN仮想アダプターの明示選択、CGNAT対応、LANとの排他、VPN切断時の安全停止
- localhost HTTPと選択LAN IPv4 HTTPSの同時待ち受け、endpoint別Host検証・認証
- ファイル共有モードで、HTML、SVG、JavaScript、XML等の能動的Web形式を添付ダウンロードとして配信
- AWSへオンデマンドでHTTPS照会するグローバルIPv4確認（外部到達性は保証しません）

実装済みのWebサイトモード:

- 1つのサイトフォルダーを開始ごとのfresh originの`/`へ公開
- 実`index.html`または`index.htm`をホームページとして表示
- HTML、CSS、JavaScript、画像、音声、動画、フォントを通常配信
- ファイル共有とは別originにし、Webサイト開始ごとに新しいポートをアプリが自動割り当てして同時稼働させない
- LAN/VPNでも現在のHTTPS、ペアリング、exact bindを維持
- CGI、アップロード、ディレクトリ一覧、Service Workerは追加しない

## 残す個性

- 「公開フォルダーを追加して開始」の簡潔さ
- 複数フォルダーの同時公開
- 自動生成されるファイル目次
- フォルダーに置いた実`index.html`を普通のホームページとして表示できること
- 学校の授業やHTML学習でも使える単純な操作
- 大容量ファイルとダウンロード再開への対応
- 接続先、転送量、速度、残り時間の可視化
- 共有URLのコピー
- 旧版の配色を再現する Classic 2005 テーマ
- 旧版と雑誌掲載の来歴を残す History / About 画面

## 初期版で廃止するもの

- CGI実行
- アップロード、削除、名前変更などの書き込み操作
- TLSなしのBasic認証
- 旧版の固定getipホストを前提にした起動時自動取得（新版は利用者操作時だけAWSへHTTPS照会）
- 任意HTMLを直接連結する自動目次テンプレート（利用者が作る実ホームページとは別機能）
- `SoapFormatter` による設定保存

## ドキュメント

- [製品方針](docs/product-vision.md)
- [アーキテクチャ](docs/architecture.md)
- [セキュリティ設計](docs/security.md)
- [LAN公開のセキュリティ設計](docs/lan-security.md)
- [ネットワーク診断](docs/network-diagnostics.md)
- [共有セッションと公開範囲](docs/share-session-security.md)
- [Webサイトモード設計](docs/website-mode.md)
- [Windows配布設計](docs/release-distribution.md)
- [ライセンス](LICENSE.md)
- [移行計画](docs/migration-plan.md)
- [旧版インベントリ](docs/legacy-inventory.md)
- [現在のタスク](current_task.md)

## 旧版について

旧版の保存範囲、安全上の注意、権利の境界は[旧版アーカイブの説明](legacy/README.md)を参照してください。旧配布物は作者charmpicのローカル完全版で保存し、この公開リポジトリには含めていません。
