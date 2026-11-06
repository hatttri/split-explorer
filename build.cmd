@echo off
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /platform:anycpu /out:SplitExplorer.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll SplitExplorer\Program.cs SplitExplorer\Forms\*.cs SplitExplorer\Controls\*.cs SplitExplorer\Models\*.cs SplitExplorer\State\*.cs SplitExplorer\Interop\*.cs
