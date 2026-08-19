# Security Policy

## Supported version

EasyHTTPServer 2の最新公開版のみをセキュリティ修正の対象とします。`legacy/`の2005年版ソースは歴史資料であり、実運用をサポートしません。

## Reporting a vulnerability

脆弱性の詳細、再現コード、未公開の攻撃手順を公開Issueへ投稿しないでください。

GitHubリポジトリの **Security** タブに **Report a vulnerability** が表示される場合は、Private Vulnerability Reportingから報告してください。利用できない場合は、機密情報を含まないIssueで「非公開の連絡方法が必要」とだけ知らせてください。

報告には、影響するバージョン、前提となる公開モード、再現条件、想定される影響を含めてください。受領後、内容を確認し、修正と公開方法を調整します。

## Scope notes

- 自己署名証明書の警告自体は既知の配布上の制約です。
- ルーターのポート転送やUPnPによるインターネット直接公開は推奨・サポートしません。
- 旧版のCGI、Basic認証、パス解決などの問題は既知であり、旧版は使用しないでください。
