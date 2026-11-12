# Orbitalsの配置と作業入口

共通ルールは[../../RULES.md](../../RULES.md)。現在の作業正本は G:\AI_project\OrbitRing。Git履歴はこの直下の.gitに保持しています。

| 役割 | 保存先 |
|---|---|
| C#ソース・テスト・アイコン素材・インストーラー定義 | source/ |
| プラグインサンプルの正本 | source/plugin-examples/ |
| 操作説明・アイコン分類・プラグイン仕様・ライセンス | docs/ |
| 実行環境・製品同梱変換ライブラリ・生成物 | release/packages/current/ |
| インストーラー・ポータブルZIP・ハッシュ | release/packages/ |
| 変換依存の準備手順・要求マニフェスト | tools/ |
| 別作業のブラウザー拡張 | work/browser-extension/ |
| 移転時の照合・旧コピーの保持 | work/2026-11-01-migration/ |

## ビルドと起動

Windows付属.NET Framework 4.8を使用します。プロジェクト直下で source/build.ps1 を実行すると release/packages/current/orbitals.exe を生成します。
Start.ps1 はこの開発版の設定画面を開きます。普段使うインストール版はスタートメニューから開いてください。
ビルド時、docs/の文書とsource/plugin-examples/を実行環境へコピーします。配布側のコピーを直接編集せず、正本を更新してください。
release/packages/current/内の変換ライブラリ・ライセンス・policy.xml・実行設定・更新先設定は、既存の同梱部品としてGitで管理します。orbitals.exe、conversion/image-codec.exe、文書やサンプルの同梱コピー、配布ZIP・セットアップEXEは生成物として除外します。

source/package.ps1 -Repository kagami-act1/orbitals -OutputDirectory <空の出力先> でポータブルZIPを作成します。
source/installer/build-installer.ps1 は -Package <ZIP> -Compiler G:\AI_tools\Orbitals\nsis-3.12\makensis.exe -Output <セットアップEXE> を指定します。
既存パッケージを上書きする前に、バージョンと公開状況を確認してください。

## 依存関係と設定

NSISコンパイラーは G:\AI_tools\Orbitals\nsis-3.12、取得済み変換パッケージのキャッシュは G:\AI_tools\package-cache\Orbitals にあります。tools/prepare-conversion.ps1 は tools/dependencies/manifest.json のハッシュを検証して、製品同梱ライブラリを配置します。ライブラリは実行時に必要なため製品内に保持します。
インストール版の設定は %LOCALAPPDATA%\Orbitals、ポータブル版の設定は実行ファイルの隣に保存します。移転前の旧ポータブル設定は work/2026-11-01-migration/previous-import/release/ に保持しています。



## 互換リンクと移転元

- C:\Users\kille\Documents\Codex\2026-10-15\new-chat → G:\AI_project\OrbitRing
- work/current → release/packages/current

旧パスで登録されたブラウザー拡張・プラグイン等の実行時参照を保つためのジャンクションです。開発時はsource/と新しいビルド手順を使用します。リンクをたどる再帰削除・全体コピーは行わないでください。
移転元の実体は C:\Users\kille\Documents\Codex\2026-10-15\new-chat.pre-migration-20261101 にリネーム退避しました。照合後の削除が自動承認レビューで拒否されたため、復元用として保持しています。現在の正本は新パスです。
