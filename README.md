# SplitExplorer

Windows 標準のフォルダビュー（IExplorerBrowser）を 2×2 の 4 ペインで表示する軽量アプリです。

## ビルドと起動

ビルドには .NET SDK 9.0.200 以降が必要です（実行時は不要です）。

1. `dotnet publish src\SplitExplorer\SplitExplorer.csproj -c Release -o publish` を実行すると `publish\SplitExplorer.exe` ができます。
2. `publish\SplitExplorer.exe` をダブルクリックして起動します。.NET Framework 4.8（Windows 10 22H2 / Windows 11 に標準搭載）以外の追加ランタイムは不要で、exe 単体をコピーしても動作します。
3. 起動時に最大 4 つのパスを引数で渡せます（例: `publish\SplitExplorer.exe C:\Windows D:\Work`）。

開発時は `dotnet build` でビルドできます。ソースは `src\SplitExplorer` 以下にあります。

## 主な操作

- 各ペインの `+` で同じ場所に新しいタブ、`×` または中クリックでタブを閉じます。
- `Ctrl+T` / `Ctrl+W` / `Ctrl+Tab` はタブ操作、`Ctrl+Shift+T` は直前に閉じたタブの復元です（各ペイン最大 20 タブ）。
- `Ctrl+L` または `F6` でアドレスバーに移動します。
- `Alt+1`〜`Alt+4` でペイン移動、`Alt+←` / `Alt+→` / `Alt+↑` で戻る・進む・上へ移動します。
- 4つのペインは常に均等（1/4ずつ）に表示されます。
- 各ペイン内は左側約15%に縦型タブと戻る・進む・親階層ボタン、右側にフルパスとエクスプローラーを固定配置します。タブ欄はマウスホイールでスクロールできます。
- アイコン・タブ・パス欄はExplorer風の配色と形状で表示します。パス欄にはファイルシステムの絶対パスを優先し、仮想フォルダーではShellの絶対解析名を表示します。
- ウィンドウ位置・各ペインのタブは終了時に `SplitExplorer.state` に保存されます。

## ポータブル動作と保存データ

- Windows上でインストールせずに実行できるポータブルアプリです。exeを任意のフォルダーへコピーして起動できます。.NET Framework 4.8とWindows Shellを使用します。
- アプリ独自の処理で環境変数やレジストリの設定を変更したり、Program Filesへインストールしたりしません。環境変数は既定のユーザーフォルダーの取得や入力パスの展開に読み取り使用します。
- ウィンドウ位置とタブ状態を保存するため、終了時にexeと同じフォルダーの `SplitExplorer.state` を作成または更新します。それ以外に、アプリ独自の処理で閲覧対象のファイルを作成・変更・削除しません。
- アプリ独自UIの戻る・進む・親階層・更新・新しいタブ・タブを閉じる・パンくず区切りなどのアイコンには、Windows標準のSegoe Fluent Iconsを使用します。
