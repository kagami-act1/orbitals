# Orbitals

Windows 11向けのリングランチャー・マウスジェスチャー。現行版は **1.0.0** です。

[最新版のダウンロード](https://github.com/kagami-act1/orbitals/releases/latest)

- planet：リングにアクションを配置。スタック、キー保持、状態ごとのアイコンに対応。
- orbit：自作ジェスチャー、プリセット、アプリ別の割り当てに対応。
- ファイル変換：画像・PDFをドラッグしたまま形式を選んで変換。

## フォルダー

| 場所 | 内容 |
| --- | --- |
| `work/current/source` | 現行ソース、テスト、ビルド・パッケージ作成スクリプト |
| `work/current/conversion` | ファイル変換に必要な実行ファイル・DLL・ライセンス |
| `work/current/plugin-examples` | 自作プラグインのサンプル |
| `work/current` の各文書 | 使い方、アイコン分類、プラグイン作成方法、ライセンス |
| `work/build-tools/nsis-3.12` | インストーラーのコンパイラー |
| `work/dependencies` | 変換ライブラリの再生成に使うパッケージと検証用ハッシュ |
| `outputs` | 最新のインストーラー、ZIP版、SHA-256チェックサム |

通常の利用では、スタートメニューの **Orbitals** を起動します。インストール済みアプリの設定はこの作業フォルダー外の `%LOCALAPPDATA%\Orbitals` に保存します。

## 開発

Windows 11・64bitの.NET Framework 4.8でビルドします。変換用のDLL・実行ファイルと各ライセンスはソースと一緒に管理しています。

Gitから取得した場合、インストーラーの作成時は[NSIS](https://nsis.sourceforge.io/Download) 3.12を `work/build-tools/nsis-3.12` に展開してください。依存パッケージの再展開用キャッシュはローカル専用です。

このフォルダーをカレントディレクトリとして実行します。

```powershell
.\work\current\source\build.ps1
.\work\current\orbitals.exe --self-test "$env:TEMP\orbitals-self-test.txt"
.\work\current\source\package.ps1 -Repository kagami-act1/orbitals -OutputDirectory .\work\current\distribution
.\work\current\source\installer\build-installer.ps1 -Package .\work\current\distribution\orbitals-Windows11.zip -Compiler .\work\build-tools\nsis-3.12\makensis.exe -Output .\work\current\distribution\orbitals-Setup.exe
```

`work/prepare-conversion.ps1` は、保存済みパッケージのハッシュを確認して変換ライブラリを再展開します。

インストーラーはWindowsの通常のデスクトップ起動経路から実行し、設定が別アプリの仮想保存先へ振り分けられていないことを確認します。GitHubへの公開・更新は、明示的な依頼があった場合だけ行います。
