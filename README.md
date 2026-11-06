# QuadExplorer

Windows 標準のフォルダビュー（IExplorerBrowser）を 2×2 の 4 ペインで表示する軽量アプリです。

## ビルドと起動

1. `build.cmd` を実行すると `QuadExplorer.exe` ができます。
2. `QuadExplorer.exe` をダブルクリックして起動します。追加ランタイムは不要です。
3. 起動時に最大 4 つのパスを引数で渡せます（例: `QuadExplorer.exe C:\Windows D:\Work`）。

## 主な操作

- 各ペインの `+` で同じ場所に新しいタブ、`×` または中クリックでタブを閉じます。
- `Ctrl+T` / `Ctrl+W` / `Ctrl+Tab` はタブ操作、`Ctrl+Shift+T` は直前に閉じたタブの復元です（各ペイン最大 20 タブ）。
- `Ctrl+L` または `F6` でアドレスバーに移動します。
- `Alt+1`〜`Alt+4` でペイン移動、`Alt+←` / `Alt+→` / `Alt+↑` で戻る・進む・上へ移動します。
- 4つのペインは常に均等（1/4ずつ）に表示されます。
- ウィンドウ位置・各ペインのタブは終了時に `QuadExplorer.state` に保存されます。
