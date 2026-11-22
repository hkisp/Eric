@echo off
setlocal
dotnet publish mac\EricAgent.Mac\EricAgent.Mac.csproj -c Release -r osx-arm64 --self-contained true -o publish-mac\osx-arm64
if errorlevel 1 exit /b %errorlevel%
dotnet publish mac\EricAgent.Mac\EricAgent.Mac.csproj -c Release -r osx-x64 --self-contained true -o publish-mac\osx-x64
exit /b %errorlevel%
