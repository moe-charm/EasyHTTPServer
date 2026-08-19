# EasyHTTPServer 2 現在のタスク

更新日: 2026-08-19

このファイルは「現在地・次に行うこと・完了条件」だけを扱います。完了済み機能の詳細な記録は
[実装履歴](docs/history/implementation-log.md)へ分離しました。

## 現在地

- EasyHTTPServer 2は、.NET 10 / Kestrel / WPFで新規実装済みです。
- 読み取り専用ファイル共有と、排他的な静的Webサイトモードを利用できます。
- localhost、家庭内LAN HTTPS、明示選択したVPNアダプターへ公開できます。
- LAN/VPN公開では短時間ペアリング、セッション証明書、Host/Origin検証を必須にします。
- Windows x64 self-contained ZIPと、初回説明書を生成できます。
- 製品アイコン、Modern / Classic 2005テーマ、History / Aboutを実装済みです。
- 直近の全テストは160件成功しています。
- 旧版はSHA-256を再確認し、歴史資料として legacy/ へ分離しました。

## 現在の作業

### P14: GitHub公開用の独立リポジトリを準備

- [x] 旧配布物と全履歴を含む完全版を`EasyHTTPServer1.2-backup.git`へmirror保存する。
- [x] 選別済みファイルから、履歴のない独立公開リポジトリを作成して`main`を初回コミットにする。
- [x] 公開版から旧EXE、TTFA DLL、旧配布一式を最初のコミット以前に除外する。
- [x] 新版と`legacy/source-1.2/`の旧ソース、設計、テスト、Guideを公開版へ含める。
- [x] 公開版の全160テスト、追跡対象、秘密情報、ローカルパスを検査する。
- [x] 独立公開リポジトリの`main`へ初回コミットを作成する。GitHubへのpushは別途明示承認後に行う。

完了条件:

- 公開リポジトリの全履歴に旧EXE・DLLが存在しない。
- 完全版mirrorの`main`が元リポジトリの`main`と同じcommitを指す。
- 公開版READMEとライセンスが、実際に含まれる旧ソースの範囲と一致する。
- Git remoteが未設定のまま、公開操作を行わない。

### P13: Release成果物の階層を短縮

- [x] 開発用展開版を`artifacts/release/<version>/app/`へ固定する設計を文書化する。
- [x] ビルドスクリプトの展開先を製品名の長いフォルダーから`app/`へ変更する。
- [x] ZIP名は従来どおり版名とRIDを含め、ZIP内部へ余分な`app/`階層を追加しない。
- [x] 全160テスト、Releaseビルド、ZIP構造、禁止物検査を確認する。

完了条件:

- 開発用Guideは`artifacts/release/<version>/app/Guide/index.html`で開ける。
- ZIPと外側の`SHA256SUMS.txt`は`app/`と同じ版ディレクトリに並ぶ。
- ZIPを展開すると直下に`EasyHTTPServer.exe`と`Guide/`があり、動作や配布内容が変わらない。

### P12: リポジトリ整理

- [x] 旧版配布物を legacy/distribution-1.1/ へ移動する。
- [x] 旧版1.2ソースを legacy/source-1.2/ へ移動する。
- [x] 移動前の主要ファイルSHA-256が既存記録と一致することを確認する。
- [x] legacy/source-1.2/Backup/ の重複を調べ、差分を含むため削除せず保存する。
- [x] 現在タスクと過去の実装履歴を分離する。
- [x] 秘密鍵、証明書、環境ファイルのGit除外規則を強化する。
- [x] 文書リンク、全テスト、Release ZIP、Git追跡対象を最終確認する。

完了条件:

- リポジトリ直下に旧版EXE/DLLや旧ソースが混在しない。
- 旧版ファイルはGit履歴とバイト列を保って移動されている。
- 新版と旧版のライセンス境界が明記されている。
- Git作業ツリーに生成物、ローカル設定、ログ、秘密鍵が追跡されない。
- 全160テストとRelease配布ビルドが成功する。

## 次の候補

- 旧ソースとTTFAライブラリはcharmpic作成と確認済み。旧バイナリは公開版へ含めない。
- 署名サービスを用意できた段階で、Windows配布物のコード署名を追加する。
- 外部トンネル連携は初期リリース後に別設計として検討する。

## 正とする文書

- [README](README.md)
- [アーキテクチャ](docs/architecture.md)
- [セキュリティ設計](docs/security.md)
- [Webサイトモード設計](docs/website-mode.md)
- [Windows配布設計](docs/release-distribution.md)
- [旧版インベントリ](docs/legacy-inventory.md)
- [実装履歴](docs/history/implementation-log.md)
- [GitHub公開方針](docs/publication.md)
