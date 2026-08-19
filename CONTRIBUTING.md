# Contributing to EasyHTTPServer 2

バグ報告、改善案、ドキュメント修正、コードへの貢献を歓迎します。

## Issueを作成する前に

- セキュリティ上の問題は公開Issueへ書かず、[SECURITY.md](SECURITY.md)の手順で報告してください。
- 旧版`legacy/`の脆弱性は既知です。旧版を修正して実運用するのではなく、新版の改善として提案してください。
- 再現手順、期待した結果、実際の結果、Windowsとアプリのバージョンを記載してください。
- ログにはローカルパス、IPアドレス、共有名などが含まれ得ます。公開前に個人情報を除いてください。

## 開発

Windowsと.NET 10 SDKが必要です。

```powershell
dotnet restore EasyHTTPServer.sln
dotnet test EasyHTTPServer.sln
dotnet build EasyHTTPServer.sln -c Release --no-restore
```

変更時は、利用者から見える仕様を対応する`docs/`または`Guide/`にも反映してください。セキュリティ境界を緩める変更、書き込み機能、CGI、認証方式の追加は、実装より先に脅威モデルと設計の合意が必要です。

## Pull Request

- 1つのPRでは、できるだけ1つの目的に絞ってください。
- 既存の命名、nullable、警告エラー化を維持してください。
- 関連テストを追加し、全テスト成功を確認してください。
- 自動生成物、配布ZIP、秘密鍵、証明書、ローカル設定をコミットしないでください。

コントリビューションを送信した時点で、その変更をプロジェクトと同じ適用可能なライセンスで提供することに同意したものとします。
