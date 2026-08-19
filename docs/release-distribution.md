# EasyHTTPServer 2 Windows配布設計

更新日: 2026-08-19

Webサイトfixtureと`website-mode.md`に関する配布要件は、P7実装版へ適用するリリースゲートです。source側の基盤実装は完了し、α版ZIPの再生成と展開版QAは[現在のタスク](../current_task.md)に従って進めます。

## 1. 正式情報

- 製品名: EasyHTTPServer 2
- 初期バージョン: `2.0.0-alpha.1`
- ライセンス: 新版はMIT、2005年版の歴史資料は従来条件を維持
- 初期配布OS/CPU: Windows 10/11 x64
- 発行者表記: charmpic

## 2. 初期成果物

正式なα版成果物はself-contained、非trim、非single-fileのフォルダーをZIP化します。

```text
artifacts/release/2.0.0-alpha.1/
  app/                                    # 開発者が展開版を直接確認する場所
  EasyHTTPServer-2.0.0-alpha.1-win-x64.zip
  SHA256SUMS.txt
```

開発用の展開フォルダー名は常に短い`app/`とし、版名とRIDは親ディレクトリとZIP名だけに持たせます。ZIPの内容は`app/`の中身と同一で、ZIP内へ余分な`app/`階層は追加しません。利用者はZIPを任意の短い場所へ展開して`EasyHTTPServer.exe`を起動します。

- self-containedにより利用者側の.NET 10 Desktop Runtimeを必須にしません。
- WPFとリフレクションを壊す可能性があるため`PublishTrimmed`は無効です。
- DLLを展開する単一EXEの誤解と起動時抽出を避けるため、初版は`PublishSingleFile`を無効にします。
- ReadyToRunは容量増加とクロス環境差を避けるため無効です。
- ARM64は実機でGUI、TLS、ファイルダイアログを検証できる段階で別成果物にします。

## 3. 成果物へ含めるもの

- EasyHTTPServer 2実行ファイルとself-containedランタイム
- 猫型サーバーを表す製品アイコン（EXE・WPFウィンドウへ埋め込み）
- `README.md`
- `LICENSE.md`
- `THIRD-PARTY-NOTICES.txt`
- `docs/lan-security.md`
- `docs/website-mode.md`
- `Guide/index.html`、`Guide/guide.css`、`Guide/README.txt`（初回説明書）
- ビルドごとの`SHA256SUMS.txt`

次は含めません。

- `legacy/`、`Save/`、旧`log/`
- `settings.json`、`origin-port-history.json`、転送ログ、`.pwd`、PFX、SNK、APIキー
- PDB、XML documentation、テスト成果物、開発用設定

## 4. ビルドと検証

`scripts/build-release.ps1`を唯一の配布ビルド入口とします。

1. Release buildと全テストを実行する。
2. `win-x64`のpublish profileで一時ディレクトリへ発行する。
3. 文書・初回説明書・ライセンス・第三者通知を配置する。
4. 禁止ファイル名と禁止ディレクトリがないことを検査する。
5. 配布フォルダー内の全ファイルからSHA-256一覧を作る。
6. ZIPを作り、ZIP自体のSHA-256を外側の`SHA256SUMS.txt`へ記録する。
7. ZIPを別の一時ディレクトリへ展開し、実行ファイル起動、GUI表示、正常終了を確認する。
8. ZIP展開版でファイル共有のdownload-only回帰を確認する。
9. HTML、CSS、JavaScript、画像、ネストした相対URLを含む固定サンプルサイトを、ZIP展開版から実ブラウザーで表示する。

出力先はGit除外済みの`artifacts/release/<version>/`です。開発用展開版はその直下の`app/`、配布ZIPと外側のチェックサムは同じ版ディレクトリ直下へ置きます。既存成果物は同じ版を明示的に再生成するときだけ、その版専用ディレクトリ内で置換します。

## 5. コード署名とSmartScreen

現在はコード署名証明書がないためα版は未署名です。READMEと配布ページで、発行元警告が表示され得ることとSHA-256確認手順を明示します。Windowsの警告を自動回避する処理や利用者のセキュリティ設定変更は行いません。

正式版前に次を行います。

- 組織または個人名義の信頼されたコード署名証明書を用意する。
- 秘密鍵をリポジトリやスクリプト引数へ保存せず、CIの保護された署名サービスを使う。
- EXEと将来のMSIXへSHA-256 Authenticode署名とRFC 3161 timestampを付ける。
- 署名後にハッシュ一覧とZIPを生成し、`Get-AuthenticodeSignature`で検証する。

## 6. 更新とアンインストール

α版はZIPによる手動更新です。起動中の自動置換、常駐更新サービス、管理者権限を導入しません。

- 更新: 停止・終了後、新版ZIPを別フォルダーへ展開する。
- 設定: `%LocalAppData%\EasyHTTPServer\settings.json`を継続利用する。P7以降はorigin再利用防止用の`origin-port-history.json`も同じ場所で継続利用する。
- アンインストール: 配布フォルダーを削除する。設定・ログ削除は利用者が明示的に選ぶ。origin履歴だけの削除は復旧にならない。完全初期化はserver停止と全閲覧端末のサイトデータ削除を確認後、`settings.json`と`origin-port-history.json`を対で削除する。
- schema移行が必要な版では、旧設定を破壊せずバックアップしてから移行する。

正式版の更新方式は、署名済みMSIXと署名済みZIPのどちらが日常利用に適するかを実機評価して決定します。

## 7. 完了条件

- クリーンなRelease buildと全テストが成功する。
- self-contained ZIP内に秘密情報、旧版、ログ、設定、PDBがない。
- ZIP展開先から.NET SDKに依存せずWPFが起動・終了する。
- 成果物のSHA-256が再計算値と一致する。
- 実行ファイルの製品名、バージョン、著作権が正しい。
- ZIP展開版から、ファイル共有とWebサイトの両モードを3操作以内で開始できる。
- WebサイトfixtureがEdge/Chromeで表示され、Service Worker、外部subresource、外部へのfetch/XHR/WebSocket、外部form送信、外部frameが設計どおり拒否される。同一originのframe/fetch/formは動作し、トップレベル外部navigationは保証対象外とする。
- LAN/VPNでは、アプリ生成pair画面を除き、ペアリング前に利用者サイトのHTMLとsubresourceが配信されない。
