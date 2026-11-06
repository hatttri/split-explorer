@echo off
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /platform:anycpu /out:QuadExplorer.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll QuadExplorer\Program.cs QuadExplorer\Forms\*.cs QuadExplorer\Controls\*.cs QuadExplorer\Models\*.cs QuadExplorer\State\*.cs QuadExplorer\Interop\*.cs
