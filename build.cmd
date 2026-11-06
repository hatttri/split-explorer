@echo off
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize+ /platform:anycpu /out:QuadExplorer.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll QuadExplorer.cs
