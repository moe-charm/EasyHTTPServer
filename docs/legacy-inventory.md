# 旧版インベントリ

調査日: 2026-08-19

この文書は、EasyHTTPServer 2の実装前に存在した主要な旧版成果物と来歴を記録します。主要ファイルのSHA-256は作者のローカル完全版で再確認済みです。公開リポジトリには`legacy/source-1.2/`のソース資料だけを収録し、旧EXE、DLL、旧ビルド出力、配布READMEは含めません。

## 来歴

### 配布版1.1

- ファイル: `EasyHTTPServer.exe`（ローカル完全版だけで保存、公開版には非収録）
- ファイルバージョン: `1.1.2159.17014`
- CLRイメージバージョン: `v1.0.3705`
- サイズ: 98,304 bytes
- 最終更新: 2005-11-29 09:27:09
- 同梱README上の公開日: 2005-11-29

この実行ファイルとルートのDLL群が、雑誌掲載時期の配布物と考えられます。

### ソース版1.2

- ソリューション: Visual C# Express 2005形式
- `AssemblyVersion`: `1.2.*`
- リリース実行ファイル: `Source/bin/Release/EasyHTTPServer.exe`（ローカル完全版だけで保存、公開版には非収録）
- ファイルバージョン: `1.2.2208.29246`
- CLRイメージバージョン: `v2.0.50727`
- サイズ: 94,208 bytes
- 最終更新: 2006-01-17 16:14:53

`legacy/source-1.2/UpgradeLog.XML`には2005-12-30のVisual Studio 2005変換記録があります。公開版は1.2ソースだけを`legacy/`へ保存し、配布版1.1は含めません。

## ローカル完全版で確認した主要ファイルのSHA-256

次の一覧は来歴証拠として記録しています。公開リポジトリに存在しない旧配布物と旧ビルド出力も含みます。

```text
F132F7971933AC89A396D73F55AB27F7142AAD19E1E17D7F9F97D8B573B42FDA  legacy/distribution-1.1/EasyHTTPServer.exe
D3E182D70E6E30D6A7E093EE3408D63B1864884CBC2344D3E270CE5AD90F75A8  legacy/distribution-1.1/readme.txt
CE7582CE2E59493E96875C922B839AE7A17FF7E476D791378CF9743656410405  legacy/distribution-1.1/TTFA.CommonClass.dll
CF5966211D0DA4A20B77F1CDB2CE8DA0742F52A6477B3D465DE5344C10E9DF35  legacy/distribution-1.1/TTFA.CommonControl.dll
4CF4A7823A9F965EEF48CB2285207DDF11C45BF46333D6B2CD9D5485DAD68E7C  legacy/distribution-1.1/TTFA.Compress.dll
31D5CA2B53C0E5D55DDB76FD751BE2AE2718788F90F842730E416295AC5EBE98  legacy/distribution-1.1/TTFA.FilePathWithArchiveFunction.dll
EE0BEB896C05D7E107264F0132E6214F9C4DFB416FC88DCC3BEF832E2EF88E84  legacy/distribution-1.1/TTFA.NET.dll
1105B05D6A570CE30B21B49227B53D18C4EF7D135AAEC47F3C875C9B325CF121  legacy/distribution-1.1/TTFA.SusieControl.dll
21FDE7D73671702E6D87E101F8188F6503B76390FA613D272028AA4E7A5B1C1C  legacy/distribution-1.1/TTFA.Win32.dll
0E23C51F5429D0A2A8FF9963AB3E9B054E7EB182C84B27A07CF50DCE9A186C50  legacy/source-1.2/EasyHTTPServer.sln
88EA6F873E72B2D3E7E94FADE2915C056E80BEDE17FE4200F7D6A556A5F647A4  legacy/source-1.2/EasyHTTPServer.csproj
3E21B92111C735628E8F72D2C33E44BCC29953AEDF2872BF8BB7AD382646BF97  legacy/source-1.2/bin/Release/EasyHTTPServer.exe
53714D9666A942CF19B4FB4C5D2B216577955F88961DB66C6713A3978335F166  legacy/source-1.2/Prog.ico
```

## 文字コード

- 旧配布`readme.txt`（公開版には非収録）: CP932
- 旧`.cs`: 主にBOMなしCP932
- `.resx`、`UpgradeLog.XML`: XML宣言に従う
- 新版のソースとMarkdown: UTF-8

旧ソースは歴史保存のため、UTF-8へ一括変換しません。必要な場合は、旧版を変更せず別の閲覧用コピーを生成します。

## 確認済みの旧版Web公開仕様

旧版の`index.html`には、利用者が作成した実ファイルと、サーバーが生成する仮想目次の2種類がありました。

| URL・状態 | 確認した挙動 | 根拠 |
|---|---|---|
| `/{RootFileName}`（既定値では`/index.html`） | 全公開フォルダーのトップ目次を動的生成 | `legacy/source-1.2/HTTPServer.cs:628-635,818-850`; `legacy/source-1.2/FormHTTPServer.cs:95-99` |
| `/<フォルダー>/index.html`が実在 | 実HTMLを優先し、既定MIME設定では`text/html`で通常配信 | `legacy/source-1.2/HTTPServer.cs:638-677,1125-1165`; `legacy/source-1.2/StartUp.cs:94-99` |
| 同URLに実indexがない | フォルダー目次を動的生成 | `legacy/source-1.2/HTTPServer.cs:680-720,760-815` |
| `/<フォルダー>/<ファイル>` | 拡張子からMIMEを判定して静的配信 | `legacy/source-1.2/HTTPServer.cs:729-757,853-866,1125-1158` |

`legacy/distribution-1.1/readme.txt:19-23`も「簡単に設置できるHTTPServer」と動的`index.html`生成を説明し、同ファイル38-43行は「公開フォルダー追加、待ち受け開始」という短い操作を示しています。

このうち、実HTMLと関連素材をホームページとして表示する機能は、作者の回想では学校の授業でも利用された旧版の中核価値であり、新版へ復元します。生成目次もファイル共有モードへ残しますが、安全境界を次のように分離します。

旧版の既定動作では実サイトは`/<フォルダー>/index.html`にあり、`/`自体は404、`/index.html`は全共有の仮想目次でした。新版のWebサイトモードが単一サイトルートを`/`へ割り当て、`index.htm`もfallbackにするのは意図的な改善です。

- ファイル共有モード: 複数共有、自動目次、能動形式は添付ダウンロード
- Webサイトモード: 単一サイトルート、実HTML/CSS/JavaScript等を開始ごとのfresh originで通常配信、目次なし

旧版のCGI、任意HTMLを連結する目次テンプレート、独自HTTP/Range、危険なpath連結は製品仕様として移行しません。新版の契約は[Webサイトモード設計](website-mode.md)を正とします。

## 公開対象から除外するもの

- `Save/SaveFormHTTPServer.xml`: 実使用設定。平文の認証情報を含む可能性がある。
- `log/`: Authorization等を含む旧ログが生成され得る。
- `legacy/source-1.2/bin/`、`legacy/source-1.2/obj/`: 旧ビルド生成物。
- `*.suo`、`*.user`: 個人のVisual Studio状態。
- `*.pwd`: 旧版の平文ID/PASS一覧。

これらは`.gitignore`で除外します。公開前には、Gitの追跡予定一覧と配布物の両方を別々に検査します。

## Backupディレクトリの確認

`legacy/source-1.2/Backup/`は旧作業フォルダーに存在した状態を保存しています。親ディレクトリとのSHA-256比較では、14ファイルが同一で、`AssemblyInfo.cs`、`EasyHTTPServer.csproj`、`EasyHTTPServer.sln`、`FormHTTPServer.cs`、`FormVersion.cs`には差分がありました。重複だけを根拠に削除すると当時の差分を失うため、全体を歴史資料として維持します。

## 保存方針

- 旧版ファイルを新版のビルド出力で上書きしない。
- 新版は`src/`と`tests/`へ配置する。
- 旧版は移動前後のSHA-256一致を確認済みで、`legacy/`配下から再移動する場合も同じ確認を行う。
- 旧設定とログは履歴資料として公開しない。
- 旧版のライセンス文言と新版の公開ライセンスを混同しない。
