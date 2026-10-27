# Orbitals

Windows 11向けのリングランチャー・マウスジェスチャー。現行版は **1.0.1** です。

[最新版のダウンロード](https://github.com/kagami-act1/orbitals/releases/latest)

| 配布形式 | ファイル名 | 使い方 |
| --- | --- | --- |
| インストーラー版 | `orbitals-Setup.exe` | セットアップで保存先を選び、スタートメニューから起動 |
| ポータブル版（ZIP・インストール不要） | `orbitals-Portable-Windows11.zip` | ZIPを展開し、フォルダー内の `orbitals.exe` を起動 |

ポータブル版は設定も実行ファイルと同じフォルダーに保存します。フォルダーごと移動できます。公開済みの1.0.0では、ポータブル版のファイル名は `orbitals-Windows11.zip` です。

- planet：リングにアクションを配置。中央のホイールで切替。スタック、キー保持、状態ごとのアイコンに対応。
- マクロ：キー・待機に加え、アプリ起動・文字入力・ウィンドウ操作などを連結。ウィンドウの条件待ち、ステップ単位／途中からのテストに対応。
- orbit：自作ジェスチャー、プリセット、アプリ別の割り当てに対応。
- ファイル変換：画像・PDF・Word・Excel・PowerPointをドラッグしたまま形式を選んで変換。HEIC・AVIFの読み込み、ファイル・フォルダーのZIP圧縮とZIPの展開にも対応。Office文書は対応するMicrosoft Office、またはLibreOfficeを利用。

## フォルダー

| 場所 | 内容 |
| --- | --- |
| `work/current/source` | 現行ソース、テスト、ビルド・パッケージ作成スクリプト |
| `work/current/conversion` | ファイル変換に必要な実行ファイル・DLL・ライセンス |
| `work/current/plugin-examples` | 自作プラグインのサンプル |
| `work/current` の各文書 | 使い方、アイコン分類、プラグイン作成方法、ライセンス |
| `work/build-tools/nsis-3.12` | インストーラーのコンパイラー |
| `work/dependencies` | 変換ライブラリの再生成に使うパッケージと検証用ハッシュ |
| `outputs` | 最新のインストーラー版、ポータブル版（ZIP）、SHA-256チェックサム |

ソースは担当ごとに配置し、テストと素材を分けています。

| `work/current/source` 内 | 担当 |
| --- | --- |
| `app` | 起動・常駐・設定モデル・保存・更新・アプリの状態アイコン |
| `actions` | アクション実行・マクロ・キー入力・音声・プラグイン |
| `planet` | リングの表示・配置編集・スタック・切替・呼び出し |
| `orbit` | ジェスチャーの入力監視・認識・補正・描画 |
| `conversion` | ファイルの判定・変換・変換画面・外部処理の管理 |
| `ui` | 設定画面・共通コントロール・アクション用アイコンの選択と表示 |
| `tests` | 自動テスト |
| `assets/app` | アプリ用の実行中・停止・エラーICO。`source` 内は通常／小サイズ用の編集元SVG |
| `icons` | アクション用Material Symbolsのカタログ・分類・生成処理 |
| `installer` | インストーラーの定義・作成処理 |
| `image-codec` | Windowsが読み込めない画像形式を補う別プロセスのソース |

アプリアイコンは `assets/app/running.ico` を実行ファイル・ウィンドウ・セットアップに共用します。通知領域は `running.ico`・`stopped.ico`・`error.ico` を状態に応じて切り替えます。ICOは16・20・24・32・48・64・128・256pxを含み、16・20pxは小サイズ用の描画です。PNGの重複や旧生成スクリプトは置いていません。

通常の利用では、スタートメニューの **Orbitals** から設定画面を開きます。常駐中でも、同じ操作で画面を開けます。インストール済みアプリの設定はこの作業フォルダー外の `%LOCALAPPDATA%\Orbitals` に保存します。

## 開発

バージョン番号はGitHubの公開版を基準にします。未公開の変更は次回版へまとめ、公開するまでは修正ごとに番号を増やしません。

Windows 11・64bitの.NET Framework 4.8でビルドします。変換用のDLL・実行ファイルと各ライセンスはソースと一緒に管理しています。

Gitから取得した場合、インストーラーの作成時は[NSIS](https://nsis.sourceforge.io/Download) 3.12を `work/build-tools/nsis-3.12` に展開してください。依存パッケージの再展開用キャッシュはローカル専用です。

このフォルダーをカレントディレクトリとして実行します。

```powershell
.\work\current\source\build.ps1
.\work\current\orbitals.exe --self-test "$env:TEMP\orbitals-self-test.txt"
.\work\current\source\package.ps1 -Repository kagami-act1/orbitals -OutputDirectory .\work\current\distribution
.\work\current\source\installer\build-installer.ps1 -Package .\work\current\distribution\orbitals-Portable-Windows11.zip -Compiler .\work\build-tools\nsis-3.12\makensis.exe -Output .\work\current\distribution\orbitals-Setup.exe
```

`work/prepare-conversion.ps1` は、保存済みパッケージのハッシュを確認して変換ライブラリを再展開します。

変換はプラットフォームやアプリの公式機能を優先します。追加部品は、標準機能が不足する形式・処理を担当します。

| 担当 | ソース | 呼び出す機能 |
| --- | --- | --- |
| 文書の判定・検証・保存 | `conversion/DocumentConversion.cs` | 変換アプリとの共通インターフェース |
| Microsoft Office | `conversion/OfficeDocumentConverter.cs` | Word・Excelの保存／PDF出力、PowerPointのスライド画像出力 |
| LibreOffice | `conversion/LibreOfficeConverter.cs` | インストール済みアプリの公式コマンドライン変換 |
| 画像の読み込み | `conversion/RasterImageDecoder.cs`・`conversion/WindowsImageDecoder.cs` | Windows画像API・WPFを優先し、不足する形式は専用部品で補完 |
| PDFの画像化・OCR・ZIP | `conversion/WindowsPdf.cs`・`conversion/WindowsOcr.cs`・`conversion/ArchiveConversion.cs` | Windows API・.NET標準ライブラリ |
| 外部処理の管理 | `conversion/ConversionProcesses.cs`・`conversion/ChildProcessJob.cs` | 中止・制限・後始末を共通化 |

PDFの作成・結合・分割にはPDFsharp、WebPの書き出しにはGoogle libwebp、SVGにはresvgを使用します。HEIC・AVIF用の同梱部品は、Windows側で読み込めない場合に使用します。これらは変換ワーカー内で動作し、常駐側に画像ライブラリを読み込みません。

インストーラーはWindowsの通常のデスクトップ起動経路から実行し、設定が別アプリの仮想保存先へ振り分けられていないことを確認します。GitHubへの公開・更新は、明示的な依頼があった場合だけ行います。
