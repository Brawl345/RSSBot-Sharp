@echo off
rd /s /q bin\Release\
dotnet publish -c release -r win-x64 /p:PublishSingleFile=true /p:PublishReadyToRun=true
dotnet publish -c release -r linux-x64 /p:PublishSingleFile=true
dotnet publish -c release -r linux-arm /p:PublishSingleFile=true
pause
exit
