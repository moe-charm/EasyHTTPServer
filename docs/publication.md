# GitHub公開方針

更新日: 2026-08-20

## 目的

作者charmpicが保有する2005年当時の配布物と開発履歴は、独立したbare mirrorリポジトリで完全保存します。GitHubには、利用者が現在の製品を理解・ビルド・監査するために必要な内容だけを、履歴のない独立リポジトリの`main`から公開します。

## 公開版へ含めるもの

- EasyHTTPServer 2の`src/`、`tests/`、ソリューション、ビルド設定
- `README.md`、`LICENSE.md`、`THIRD-PARTY-NOTICES.txt`
- `docs/`、`Guide/`、`scripts/`
- 旧版の設計史を確認できる`legacy/source-1.2/`のソース、resx、プロジェクト資料
- 旧版の来歴と危険性を説明する文書

## 公開版へ含めないもの

- `legacy/distribution-1.1/`の旧EXE、旧DLL、当時の配布一式
- `legacy/source-1.2/DLL/`および旧ビルド出力
- Save、log、設定、転送履歴、パスワード一覧、証明書、秘密鍵
- `bin/`、`obj/`、`artifacts/`、PDB、ローカルIDE状態

旧配布物を`.gitignore`へ追加するだけでは過去コミットから消えません。そのため公開版は完全版のworktreeや通常branchとして残さず、別の`.git`を持つ独立リポジトリとして作成します。公開リポジトリの`main`は親を持たない初回コミットから始め、完全版mirrorはGitHubへpushしません。

## 公開前ゲート

1. 公開リポジトリの全refsから到達可能なオブジェクトに旧EXE・DLLがない。
2. Git追跡対象に秘密情報、個人ディレクトリ、ローカル設定がない。
3. 全160テストとReleaseビルドが成功する。
4. Release ZIPがVector掲載物と同一で、SHA-256を掲載できる。
5. READMEへ未署名、Windows警告、旧ソース非推奨を明記する。
6. pushとGitHub公開は利用者の明示承認後に行う。

## 公開先

- GitHub: `https://github.com/moe-charm/EasyHTTPServer`
- 既定ブランチ: `main`
- 作者表記: `charmpic`

公開版にはGitHub ActionsのWindows CI、脆弱性報告手順、コントリビューション案内を含めます。ルート直下のZIPや7zもGit除外し、配布物はGitHub ReleasesまたはVectorで扱います。
