# EasyHTTPServer 2 実装履歴

> この文書は2026-08-19までの完了作業を保存した履歴スナップショットです。
> テスト件数や次タスクなどの現在情報には使用せず、ルートの
> [current_task.md](../../current_task.md)を参照してください。

更新日: 2026-08-19

このファイルを、EasyHTTPServer 2の現在の作業、判断、次のタスクのSSOTとします。作業開始時と終了時に更新します。

## 現在のフェーズ

**P7進行中: 旧版の静的Webサイト公開を安全に復元**

旧版を保全したまま、LAN HTTPS、Windows x64 self-contained配布成果物、オンデマンドのグローバルIPv4確認、localhost/LAN同時待ち受け、短時間ペアリング認証、URLだけのQR、VPNアダプターモードまで実装しました。正式名称はEasyHTTPServer 2、新版はMITライセンス、作者・発行者表記はcharmpicです。

次の最優先は、旧版の中核価値だった「フォルダー内の実`index.html`を普通のホームページとして表示する」機能です。ファイル共有のHTML許可設定にはせず、ファイル共有とWebサイトを排他的なモードにし、Webサイト開始ごとにアプリがfresh portを自動割り当てしてoriginを分離します。外部トンネルは初期リリース後の将来候補へ移しました。

2026-08-19の外部セカンドオピニオンをコードと照合し、次の5項目を採用しました: 非同期Commandの例外境界、決定的な日本語スラッグ、UI Automationラベル、文字拡大可能なAbout画面、WPF ViewModelテスト。グローバルに全例外を握りつぶす案は採用せず、操作単位で安全に通知します。

## 確定した判断

- [x] 旧版のコードと機能を調査した。
- [x] 旧版は現在のネットワークへ公開しない。
- [x] 旧ネットワークコードを直接移植しない。
- [x] 新版はC# / .NET 10 LTSとする。
- [x] HTTPサーバーはASP.NET Core Kestrelとする。
- [x] GUIはWPFとする。
- [x] UI設計はMVVMとする。
- [x] 初期版はWindows向けとする。
- [x] 初期版は読み取り専用とする。
- [x] CGI、アップロード、削除を初期版へ含めない。
- [x] 旧版のClassicテーマとHistoryを新版に残す。
- [x] 静的Webサイト表示を初期リリースの中核機能へ戻す。
- [x] ファイル共有とWebサイトを排他的なコンテンツモードにする。
- [x] Webサイトは単一ルートと開始ごとのfresh originを使い、ファイル共有、別サイト、別開始セッションと混在させない。
- [x] 外部トンネルは初期リリース後の将来候補とする。

## 今回完了した作業

- [x] 新版READMEを作成する。
- [x] 製品方針を文書化する。
- [x] WPF / Kestrel構成を文書化する。
- [x] セキュリティ境界と完了条件を文書化する。
- [x] 段階的な移行計画を文書化する。
- [x] `current_task.md`を作成する。
- [x] .NET 10ソリューション、Kestrelサーバー、WPF画面を実装する。
- [x] パス境界、HTTP、Range、大容量ファイルの自動テストを実装する。
- [x] Modern / Classic 2005、設定、History / About画面を実装する。
- [x] Release版WPFからサーバーを起動し、実際のHTTP配信と停止を確認する。
- [x] 実機QAで判明した起動時バインディング例外とポート競合時の異常終了を修正する。
- [x] Classic 2005テーマの補助文字コントラストを改善する。
- [x] セカンドオピニオンの指摘を精査し、採用項目を設計文書へ反映する。
- [x] 非同期Commandの回復可能な例外を状態表示へ通知する。
- [x] 日本語名の決定的スラッグとWPF ViewModelテストを追加する。
- [x] UI Automationラベルとスクロール可能なHistory / Aboutを実装する。
- [x] Claude-GLMセカンドオピニオンを精査し、CSPを緩めない判断を文書化する。
- [x] JSON設定を許可リスト、原子的保存、安全な復元で実装する。
- [x] 転送ログを有界キュー、JSON Lines、サイズローテーションで実装する。
- [x] 50並列ストリーミング、低速ヘッダー、接続上限回復を統合テストする。
- [x] LAN公開の脅威境界、TLS、アクセスコード、UI、完了条件を文書化する。
- [x] LAN HTTPSとWPF操作を設計どおり実装・検証する。
- [x] 正式名称、MITライセンス、charmpic表記を製品メタデータへ反映する。
- [x] win-x64 self-contained発行プロファイルと検査付きZIP生成を実装する。
- [x] 旧版の実HTML表示と動的目次を再調査し、別機能として記録する。
- [x] `docs/website-mode.md`へWebサイトモードの要件、脅威境界、UI、実装順、完了条件を文書化する。
- [x] 製品方針、アーキテクチャ、セキュリティ、移行、配布文書の矛盾をWebサイトモード設計へ揃える。

## 次に行うタスク

### P0: リポジトリを安全に初期化する

- [x] 旧版ファイルの一覧とSHA-256を記録する。
- [x] 旧版1.1とソース側1.2の来歴を`docs/legacy-inventory.md`へ記録する。
- [x] `Save/`、`log/`、`bin/`、`obj/`、`.suo`、`.pwd`を除外する`.gitignore`を作る。
- [x] Git追跡予定ファイルを確認し、秘密情報がないことを確認する。
- [x] 新版をMITライセンス、旧版歴史資料を従来条件として分離する。
- [x] 正式な製品名をEasyHTTPServer 2とする。

完了条件:

- 旧版ファイルの同一性をチェックサムで確認できる。
- 実使用設定や認証情報がGit追跡対象にならない。
- 新版の名称とライセンスが文書化されている。

### P1: .NET 10ソリューションを作る

- [x] 新しいソースを配置するディレクトリを確定する。
- [x] `EasyHttpServer.Core`を作る。
- [x] `EasyHttpServer.Server`を作る。
- [x] `EasyHttpServer.Desktop.Wpf`を作る。
- [x] Core、Server、Integrationのテストプロジェクトを作る。
- [x] Nullable、WarningsAsErrors、解析ルールを有効にする。
- [x] プロジェクト参照を設計どおりに設定する。
- [x] 最小のWPFウィンドウと、起動・停止可能なサーバーホストを作る。
- [x] buildとtestを成功させる。

完了条件:

- クリーンな環境で`dotnet build`と`dotnet test`が成功する。
- ServerからWPFアセンブリへの参照がない。
- UIを閉じるとホストが正常終了する。

### P2: 安全なパス境界を先に実装する

- [x] `ShareDefinition`を定義する。
- [x] URLスラッグの検証を実装する。
- [x] `ISharePathResolver`を実装する。
- [x] `..`、エンコード表現、バックスラッシュ、ADS、予約名をテストする。
- [x] junction / symbolic link脱出をWindows統合テストで確認する。
- [x] 共有名の前方一致で兄弟パスへ出られないことを確認する。

完了条件:

- `docs/security.md`のパス攻撃ケースがすべてテスト化される。
- 共有外ファイルを解決する入力がすべて失敗する。

### P3: 読み取り専用HTTP機能を実装する

- [x] ループバック限定Kestrelホストを実装する。
- [x] GETとHEADを実装する。
- [x] RangeをASP.NET Coreのファイル応答で有効化する。
- [x] 自動目次を安全なHTML生成で実装する。
- [x] MIMEと未知形式のダウンロード方針を実装する。
- [x] Kestrelの接続・ヘッダー・タイムアウト制限を設定する。
- [x] Graceful Shutdownを実装する。
- [x] 10GB級の疎ファイルを使う大容量試験を実装する。

完了条件:

- GET、HEAD、通常Range、suffix Range、不正Rangeの統合テストが通る。
- ファイルサイズに比例してメモリ使用量が増えない。
- POST等は405となる。

### P4: WPFの主要操作を実装する

- [x] メイン画面のワイヤーフレームを決める。
- [x] 共有フォルダーの追加・削除を実装する。
- [x] サーバー開始・停止Commandを実装する。
- [x] 公開URL表示とコピーを実装する。
- [x] 転送状態イベントとDataGridを接続する。
- [x] UI更新を250ms間隔で集約する。
- [x] 設定画面を一般、共有、ネットワーク、セキュリティ、ログに分ける。
- [x] Modernテーマを作る。
- [x] Classic 2005テーマを作る。
- [x] History / About画面を作る。

完了条件:

- フォルダー追加、開始、URLコピーの基本導線が3操作以内で完了する。
- 複数転送中もUI操作が停止しない。

### P5: 実機QAと日常利用向け機能を追加する

- [x] 表示状態でModern / Classic 2005のレイアウトを目視確認する。
- [x] 非同期Commandから想定外例外をDispatcherへ漏らさず状態表示へ通知する。
- [x] 日本語フォルダー名から再現可能なASCIIスラッグを生成する。
- [x] WPF ViewModelのコマンド状態、重複スラッグ、履歴上限、例外経路をテストする。
- [x] キーボード操作、フォーカス、スクリーンリーダー用ラベルを確認する。
- [x] History / Aboutを高DPI・文字拡大時にもスクロール可能にする。
- [x] 同期Commandの回復可能例外を状態表示へ通知する。
- [x] `%`を含む安全なファイル名と多重エンコード攻撃を両立して処理する。
- [x] 転送履歴を1回のReset通知で最新200件へ更新する。
- [x] junctionとsymbolic linkのreparse point脱出テストを明示的に実行する。
- [x] JSON設定の保存と読込を実装する。
- [x] 消えた共有フォルダーは復元・公開せず、状態表示で通知する。
- [x] 許可リスト方式の構造化ファイルログとローテーションを実装する。
- [x] 50並列ダウンロードの負荷試験を実装する。
- [x] 低速ヘッダーと接続枯渇の試験を実装する。
- [x] LAN公開のHTTPS、証明書、アクセスコード方式を設計・実装する。
- [x] 配布用の署名、発行プロファイル、更新方式を設計する。
- [x] AWSへオンデマンドで問い合わせるグローバルIPv4確認機能を実装する。
- [x] グローバルIP確認のHTTPS限定、タイムアウト、応答上限、厳格なIPv4検証をテストする。
- [x] 取得結果を保存・ログ記録せず、外部到達性を保証しない注意をUIへ表示する。
- [ ] 正式版用コード署名証明書を用意し、署名工程を実装する。

### P6: 共有セッション安全化とVPN共有の基盤

- [x] ファイル共有モードの能動的なWeb形式を添付ダウンロードにする。
- [x] LAN開始前にNIC、IP、ポート、共有範囲、認証方式を確認する。
- [x] LANの待ち受けNICとIPv4を明示選択する。
- [x] 公開状態をコピー・診断等の操作通知から分離する。
- [x] 終了時に待ち受け停止と秘密消去を設定保存より先に行う。
- [x] 選択NIC/IP消失、ネットワーク停止、スリープ、WindowsロックでLAN共有を停止する。
- [x] localhostとLANを別endpointとして同時待ち受けし、Host検証と認証を到着endpointごとに分離する。
- [x] URLだけのQRと短時間ペアリング認証を設計・実装する。
- [x] VPNアダプターモードを実装する。

完了条件:

- ネットワークidentityが変化しても別アドレスへ自動公開されない。
- 公開中の範囲が一時通知に隠されない。
- ファイル共有モードでは、共有内の能動的Web形式がアプリoriginで実行されない。
- 再起動後に安全な設定だけが復元される。
- UIの目視・アクセシビリティ・並行負荷試験が通る。
- LAN/VPN公開はHTTPSと認証の完了前には有効化できない。

### P8: GUIレビュー指摘を整理する

- [x] Modern / Classic双方へ案内文と状態表示用の高コントラスト色を定義し、未定義resourceをなくす。
- [x] 開始できない理由を開始ボタン直下とToolTipへ表示する。
- [x] 公開目的、開始・停止、別端末公開、LAN/VPN選択、詳細、主要操作のTab順を視覚順へ統一する。
- [x] メイン画面と設定画面を「このPCだけ→家庭内LAN→VPN」の順に揃える。
- [x] グローバルIPv4診断を通常画面からネットワーク設定へ移し、共有開始に必須ではないことを明示する。
- [x] WebサイトモードではHTML/JavaScript実行を選択時点で説明し、remote開始時のWebサイト確認とLAN/VPN確認を1つへ統合する。
- [x] Release版Modern / Classicの目視、UI Automation、キーボード操作、全154自動テストを確認する。

### P9: 初回説明書を同梱する

- [x] 配布物へローカルで開けるレスポンシブHTML説明書と、安全に共有できるテキスト版を同梱する。
- [x] 初回起動で同梱説明書が存在するときだけ「EasyHTTPServer の説明書」をファイル共有へ登録する。
- [x] 利用者が説明書共有を削除した後や、設定が壊れて既定値へ退避した場合は自動復活させない。
- [x] 開発環境や利用者固有の絶対パスを説明書へ含めず、配布先の実行ファイル基準で初回だけ解決する。
- [x] 説明書欠落時も起動を妨げず、従来どおり空のファイル共有で開始する。
- [x] Release ZIPへGuide一式を含め、禁止物検査、ハッシュ生成、PC・スマートフォン幅の実ブラウザー表示まで確認する。

### P10: ファイル共有のサブフォルダー403を修正する

- [x] ルート目次が生成した末尾`/`付きサブフォルダーURLを、ファイル共有routeで正規化して解決する。
- [x] 日本語名、空フォルダー、多段フォルダー、親リンクを実HTTPテストで固定する。
- [x] 二重slash、dot segment、encoded traversal、backslash、reparse pointの拒否を維持する。
- [x] LAN/VPNとloopbackで同じ共有routeを使い、認証後の挙動に差を作らない。
- [x] 全160テスト、実HTTP、Release ZIP再生成で回帰確認する。

### P11: 製品アイコンを追加する

- [x] 猫型サーバーと共有フォルダーを一体化した、文字なし・透明背景の製品アイコンを作る。
- [x] 16、24、32、48、64、128、256pxを含むWindows ICOへ変換し、小サイズでも輪郭を保つ。
- [x] WPFウィンドウ、タスクバー、配布EXEのApplicationIconへ同じICOを反映する。
- [x] 元PNGとICOを製品アセットとして管理し、配布物には実行に必要なICOだけを埋め込む。
- [x] Releaseビルド、全テスト、実画面、EXE icon resource、配布ZIPを確認する。

### P7: 静的Webサイトモードを復元する

- [x] 旧版の実`index.html`表示、自動目次、MIME、CGIをコードとREADMEから再調査する。
- [x] Webサイトモードの製品要件、origin、URL、MIME、CSP、認証、UI、設定移行を設計する。
- [x] 外部トンネルよりWebサイトモードを優先し、関連文書を更新する。
- [x] 現行ファイル共有のURL、目次、能動形式attachmentを回帰テストで固定する。
- [x] `Publication`、`FileSharePublication`、`WebsitePublication`、`WebsiteDefinition`を実装する。
- [x] `SingleInstanceGuard`、`ApplicationStateInitializer`、`OriginPortAllocator`、`OriginPortHistoryStore`を分離し、machine-wide単一起動、49152〜65535のbrowser-safe fresh port割り当て、役割付きorigin port履歴、process間排他更新を実装する。
- [x] schema v2とschema v1からの無損失移行を実装し、非選択modeの共有一覧とsite rootも保持する。
- [x] filesystem access前のlocal root分類、Webサイト用拒否規則、UNC/device namespace/mapped network drive/network filesystem拒否を実装する。
- [x] local volume rootから全祖先をno-followで開いてrename不可に保持し、要求もroot基準で各componentを検証して同じ検証済みhandleから配信する`PublishedFileOpener`を実装する。
- [x] 単一サイトのroute、index探索、末尾slash、404を実装する。
- [x] Web用MIME、byte-for-byte応答、未知形式attachment、Rangeを実装する。
- [x] アプリ生成、ファイル共有、Webサイトの応答ヘッダープロファイルを分離する。
- [x] mode別Cookie、構成済みendpoint基準のpair POST Origin検証、Fetch Metadata、CORP、WebsiteでのBasic拒否を実装する。
- [x] WPFへモード選択、単一サイトフォルダー、index検出、開始確認を追加する。
- [x] LAN/VPNのHTTPS、ペアリング、exact bind、安全停止をWebサイトでも自動テストする。
- [x] 通常画面の公開範囲を「このPCだけ／ほかの端末にも公開」へ簡略化し、接続方法の要約、設定画面の排他的なLAN/VPN詳細選択を実装・検証する。
- [x] VPN自動選択を廃止し、「ほかの端末にも公開」の展開後に家庭内LAN / VPNを明示選択するまで開始不可とする。停止後はこのPCだけへ戻し、方式を保存しない。
- [x] Tailscale実ブラウザーでpair画面後のPOSTが403になる不具合を、Origin欠落/opaque OriginとFetch Metadataの組合せを含む統合テストで再現し、安全な互換fallbackで修正する。
- [x] 修正版をTailscale実ブラウザーで再確認し、pair POST→Cookie発行→共有目次200まで到達することを確認する。
- [ ] 読み取りmethod/body制限、予約routeの大小文字・encoding、正常な日本語・記号ファイル名を試験する。
- [ ] FileShare→Website→FileShare切替、設定保持、開始失敗cleanup、port閉鎖を試験し、正常Stop・開始失敗・停止猶予終了後に全ancestor/root/file handleが解放され元フォルダーをrename/deleteできることを確認する。
- [ ] 完全新規、v1＋履歴なし、v1＋有効履歴の冪等merge、破損/未知settings＋有効履歴の安全なfallback、v2履歴欠落、履歴破損/未知schema、`fileShareReserved`/`websiteRetired`、2 process更新、mutex/lock保持中crash後のabandoned取得、atomic replace前後のcrash、bindエラー分類、allocator競合・枯渇を試験する。
- [ ] 3種の応答profileをpair/目次/200/HEAD/206/308/401/404/416で直接固定する。
- [ ] Webサイト転送ログとURL/QRに、秘密や実pathを含めず、実際のfresh portを使うことを試験する。
- [ ] 実ブラウザーとスマートフォンでHTML/CSS/JS/画像/相対URLを確認する。
- [ ] self-contained ZIPを再生成し、展開版からサンプル教材サイトを確認する。

完了条件:

- Webサイトモードでは、選択した1フォルダーだけを開始ごとのfresh originの`/`へ公開する。
- `/`で実`index.html`または`index.htm`を200、正しいMIME、attachmentなしで表示する。
- CSS、JavaScript、画像、音声、動画、フォント、ネストした相対URLが実ブラウザーで動く。
- indexなしのディレクトリは目次を生成せず404になる。
- `/s/*`、非選択共有、予約`/_easyhttp`の実ファイルへ到達できない。
- traversal、多重encoding、ADS、予約名、junction、symbolic link、秘密候補を拒否し、UNC/device namespace/mapped network drive/network filesystemのrootをfilesystem access前に拒否する。
- local volume rootからの全ancestor、root、途中directory、fileの差し替えでも公開外へ接続・配信せず、handle-relative no-followから絶対pathへfallbackしない。
- CGI、アップロード、削除、SPA fallback、Service Workerを追加しない。
- ファイル共有ではHTML、SVG、JavaScript、XMLのattachmentが変わらない。
- machine-wide単一起動で、Webサイトとファイル共有を同時稼働・同じoriginへ混在できない。別サイト・別開始セッションへbrowser-safe fresh originを割り当て、役割付き履歴を失わない。
- LAN/VPNではHTTPSとペアリング完了前に、アプリ生成pair画面を除く利用者サイトのHTMLとsubresourceを1 byteも返さない。
- 外部subresource、外部へのfetch/XHR/WebSocket、外部form送信、外部frame、全Workerを既定で拒否するが、同一originのframe/fetch/formとトップレベル外部navigationは許可され得るため、通信不能sandboxとは説明しない。
- app生成、FileShare、WebsiteのCSP、frame、nosniff、CORP、cache、attachment境界をstatusにかかわらず混同しない。
- FileShareとWebsiteを往復しても両モードの設定を失わず、active modeだけを公開する。
- Modern / Classic 2005、キーボード、Narrator、文字拡大で3操作以内に開始できる。
- 全自動テスト、実ブラウザー、スマートフォン、ZIP展開版QAが成功する。

## 未決定・ユーザー判断が必要な項目

- [x] 正式名称を`EasyHTTPServer 2`とする。
- [x] 新版へMITライセンスを付ける。
- [x] Classicテーマを初期リリースへ含める。
- [x] LAN認証を初期リリースへ含める。HTTPS上のセッション限定認証を必須とする。
- [ ] ポータブルモードを初期リリースへ含めるか。

これらはP1開始を妨げないものを除き、必要になる直前に確認します。

## 初期リリース後のバックログ

- [ ] 専用loopback originによる外部トンネル連携を再評価する。
- [ ] WebサイトのSPA fallback、外部origin許可リスト、PWAを個別の脅威モデルで再評価する。

## 最新の検証結果

2026-08-19に次を確認しました。

### 現在のsource・文書

- `dotnet build EasyHTTPServer.sln -c Release --no-restore`: 成功、警告0、エラー0
- `dotnet test EasyHTTPServer.sln -c Release --no-restore`: 153件成功、失敗0
- Webサイトモード文書を含むローカルMarkdownリンク: すべて有効
- `git diff --check`: 空白エラーなし
- 旧READMEのCP932本文と、実HTML優先・動的目次・MIME処理の旧コード行を再照合
- Cookieのport非分離、CSP Worker制御、Fetch Metadata、browser bad-port、Stream型FileResult/RangeをRFC・WHATWG・W3C・Chromium・Microsoft公式資料で照合
- TailscaleをVPN候補へ分離し、実VPN IPv4へのHTTPS exact bindと未認証ペアリング誘導を統合テストで確認
- VPN候補のTunnel・PPP・仮想NIC分類、RFC 1918・CGNAT境界、LAN/VPN排他、安全監視対象を自動テストで確認
- Release版WPFでLAN・VPN別選択欄とUI Automation名を確認
- 8桁コード、5分失効、初回成功失効、10回ロック、再発行、最大16 Cookieセッション: 自動テスト成功
- ペアリング画面、Secure/HttpOnly/SameSite=Strict Cookie、URL・QRへの秘密非混入: 統合テスト成功
- Release版WPFで8桁コード、URLだけのQR、証明書指紋の同時表示とUI Automation名を確認
- Release版WPFでWebサイトモードを選択し、fixtureをfresh loopback portで開始、HTML/CSS/JavaScript/SVGの200配信、転送履歴、停止、ファイル共有モードへの復帰を確認
- Webサイトの実index、MIME、CSP、SAMEORIGIN、相対asset、Range、pairing前の本文非開示、mode別Cookieを統合テストで確認
- Release版WPFで「ほかの端末にも公開」展開直後は開始不可、家庭内LAN / VPNの明示選択後だけ開始可能、選択アダプター要約、ペアリング詳細の表示切替を目視・UI Automation確認
- pair POSTは通常の構成済みOrigin完全一致に加え、Origin欠落/`null`時だけsame-origin top-level form navigationのFetch Metadataを要求する互換fallbackを統合テストで確認。別Originとブラウザー証拠なしは403を維持
- Tailscale実ブラウザーで修正版のpair POST、Cookie発行、共有目次表示まで成功
- QRCoder 1.8.0と全推移依存のNuGet脆弱性監査: 既知の脆弱なパッケージなし

### P7設計前の配布・各実装時点の履歴

以下の120件、93件、個別project件数は、それぞれ当時の配布または機能実装時点のsnapshotです。この節を記録した時点の全source試験総数は135件でした。現在の件数はルートの`current_task.md`を正とします。

- `scripts/build-release.ps1`: 120テスト、win-x64 self-contained発行、禁止物検査、ZIP生成に成功
- 最新配布ZIP: 74.17 MiB、546エントリ、VPNアダプターモードとQRCoder本体・第三者通知を収録、SHA-256 `CEBD6A5A4FE378D6EEE1328660B473643AB44400620AB07284CA0E4A35200121`
- LAN共有時の同一固定ポートによるloopback HTTPと選択IPv4 HTTPSの同時bind: 3回連続成功
- endpoint別境界: localhostは認証なし200、LANは未認証401、両側のHost偽装400を統合テストで確認
- HTML、SVG、JavaScript、XMLのContent-Disposition attachmentとnosniff: 統合テスト成功
- LAN開始キャンセル、選択NIC利用、安全停止、状態/通知分離、終了順序: WPFテスト成功
- Release版WPFでNIC/IP選択、Basicユーザー名表示、公開共有を含む開始前確認、Escapeキャンセルを確認
- Modern / Classic 2005両テーマでP0-A追加行のレイアウトとUI Automation名を確認
- AWS実サービスへのオンデマンドHTTPS照会、IPv4表示、コピー有効化、注意表示: WPF実機確認成功
- グローバルIPv4行とLAN証明書行が重ならないこと、UI Automation名と状態通知: WPF実機確認成功
- Serverテスト: 35件成功（LANプライベートIPv4境界、証明書プロファイル、128-bitコード、弱い構成の拒否を含む）
- WPFテスト: 26件成功（LAN開始時のHTTPS構成、コード・指紋表示、停止時秘密消去を含む）
- Integrationテスト: 20件成功（実LAN IPv4へのTLS bind、証明書指紋照合、未認証・誤コード401、正コード200、Host偽装400を含む）
- WPFテスト: 25件成功（Command状態、同期・非同期例外通知、設定保存・復元、ログ許可キー・秘密情報除外・並行投入・ローテーション・故障分離）
- JSON設定の正常往復、破損JSON保全、未対応schema、範囲外ポート、消失・重複共有の非復元: 成功
- JSON Lines転送ログの許可キー、クエリ除去、制御文字置換、200並行投入、3世代ローテーション、書込不能時の安全停止: 成功
- 1 MiBファイルの50並列ストリーミング取得と全SHA-256一致、転送完了50件: 成功
- 不完全ヘッダーの15秒以内切断、100接続上限超過の拒否、解放後5秒以内の正常配信回復: 3回連続成功
- Release版WPFでLAN行のレイアウト、UI Automation名、LANスイッチ、正常終了を実機確認
- `scripts/build-release.ps1`: Release build、93テスト、win-x64 self-contained発行、禁止物検査、ZIP生成に成功
- 配布ZIP: 74.05 MiB、545ファイル、PDB・設定・ログ・旧版・秘密鍵なし
- 配布メタデータ: `EasyHTTPServer 2` / `2.0.0-alpha.1` / `charmpic`、未署名α版
- ZIP外側SHA-256と内部544ファイルのSHA-256: 全件一致
- ZIPを別フォルダーへ展開し、self-contained版WPFの表示と正常終了を確認
- `%`入りファイル名の配信、多重エンコード攻撃拒否、Start→Stop→Start: 統合テスト成功
- Windows junctionとsymbolic linkによる共有外脱出: ReparsePointとして拒否
- 10GB疎ファイルのsuffix Range: 成功
- Release版WPFプロセスの起動スモーク: 成功
- Release版WPFで共有追加、開始、`/`と`/s/docs/`のHTTP 200応答、転送履歴、停止を確認
- 設定、History / About、Modern / Classic 2005の目視確認: 成功
- UI Automationツリーでポート、公開URL、共有一覧、転送一覧、設定カテゴリ、履歴の日本語名を確認
- Tabフォーカス順、Alt+M/E/Hショートカット、設定/AboutのEscape終了: 成功
- HistoryのScrollViewerへTabで到達し、PageDown後もフォーカスを維持: 成功
- 停止後の`127.0.0.1:18080` TCP接続不可: 確認
- ローカルMarkdownリンク: すべて有効
- 旧EXE、readme、旧solution、旧projectのSHA-256: インベントリ記録値と一致
- `Save/`、旧`bin/`、旧`obj/`、`.suo`: Git除外を確認

## 作業ルール

- 旧ファイルを上書き、文字コード変換、移動、削除しない。
- `Save/`と`log/`の内容を公開しない。
- HTTP解析とRange解析を独自実装しない。
- 共有ルート境界のテストを、機能実装より先に作る。
- ファイル共有とWebサイトを同じ`Publication`、同じoriginへ混在させず、Webサイトoriginを別サイトや別開始セッションへ再利用しない。
- 設計済み・未実装・検証済みを文書で明確に区別する。
- UIからHTTPオブジェクトを直接操作しない。
- 実装変更ごとにbuildと関連テストを実行する。
- 完了していない項目へ`[x]`を付けない。

## 関連文書

- [README](../../README.md)
- [製品方針](../product-vision.md)
- [アーキテクチャ](../architecture.md)
- [セキュリティ設計](../security.md)
- [ネットワーク診断](../network-diagnostics.md)
- [共有セッションと公開範囲](../share-session-security.md)
- [Webサイトモード設計](../website-mode.md)
- [移行計画](../migration-plan.md)
