# Keiba JV-Link Bridge

Windows向けの小さなJV-Link COMクライアントです。利用者が契約・導入したJV-Linkから取得したレコードをJSON Linesで標準出力へ渡します。競馬予想、投票、購入、購入推奨を行う機能はありません。

## 現在の状態

初期ソース公開版です。コード署名サービスには未採用で、信頼された署名済みバイナリはまだありません。署名が必要なWindows環境では未署名のビルドを実行できません。保護機能を無効にしたり、証明書を自己登録したりして実行する手順は提供しません。

## 必要なもの

- Windowsと.NET Framework 4系のC#コンパイラー。
- ネットワーク取得には、対象ビット数で登録済みのJRA-VAN JV-Link、利用資格と各サービス規約への同意が必要です。取得可能範囲は契約・提供元の仕様に従います。
- JV-Link本体、SDK、ベンダーDLL、利用キー、レースデータはこのリポジトリには含まれません。別途公式提供元から導入してください。このMITライセンスはそれらの権利を許諾するものではありません。

## ビルド

リポジトリのルートでPowerShellから実行します。

```powershell
./build.ps1
```

`unsigned-build/JvLinkProbe-x64.exe` と `unsigned-build/JvLinkProbe-x86.exe` を生成し、SHA-256を表示します。既存の出力フォルダーは上書きしません。ビルドスクリプトは生成物の起動・署名・セキュリティ設定の変更を行いません。Windowsがスクリプトや実行ファイルを拒否する場合は、保護を変更せず中断してください。

## インターフェース

以下はコマンドの仕様です。実行は、通常のWindows検証で許可されるビルドが用意できてから行ってください。

| 引数 | 動作 |
| --- | --- |
| `--probe` | 32/64bitのCOM登録情報のみを確認。通信やCOMの起動なし |
| `--connect --allow-network YYYYMMDD` | 0B15の接続確認 |
| `--read --allow-network YYYYMMDD` | 0B15のレコード取得 |
| `--odds --allow-network RACEKEY` | 0B30のオッズ取得。RACEKEYは16桁 |
| `--changes --allow-network YYYYMMDD` | 0B14の変更情報取得 |
| `--history --allow-network FROM RACEKEY HORSEIDS` | RCVNの過去資料を走査。FROMは14桁日時、HORSEIDSは10桁IDをカンマ区切りで1〜18件 |

通信操作では明示的な `--allow-network` が必要です。`--probe` の登録確認は契約の有効性や通信成功を証明しません。診断は標準エラー、機械処理用JSON Linesは標準出力に出します。生レコードはBase64で格納します。呼び出し側が終了コードと取得件数を確認してください。時間・件数・サイズの上限により途中終了する場合があります。完全取得を保証するAPIではありません。

`observedAt` はこのツールの観測時刻です。公式の公開時刻や結果確定時刻を意味しません。過去資料の取得が、発走前にその情報を知り得た証拠になるわけではありません。

## 構成とライセンス

- `JvLinkProbe.cs`: COM呼び出しとJSON Lines出力。
- `AssemblyInfo.cs`: 製品名とバージョン。
- `build.ps1`: x64/x86のローカルビルド。
- [コード署名方針](SIGNING.md) / [プライバシー](PRIVACY.md)。

自作コードとこの文書は [MIT License](LICENSE) です。JRAおよびJRA-VANとの提携や、同社による承認を示すものではありません。

## 自動ビルド

GitHub Actionsの標準Windowsランナーで同じスクリプトを実行し、コミットID・SHA-256・製品情報をログに残します。取得ツール自体の実行、JV-Link通信、署名、実環境の動作検証は行いません。ビルド成功はWindows上の起動許可やデータ取得成功を意味しません。成果物ストレージへのアップロード・キャッシュ保存は設定していません。
