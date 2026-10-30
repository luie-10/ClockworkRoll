@echo off
REM 사용법: build.bat "C:\Program Files\Unity\Hub\Editor\6000.0.23f1\Editor\Unity.exe"
set UNITY=%~1
if "%UNITY%"=="" set UNITY=C:\Program Files\Unity\Hub\Editor\6000.0.23f1\Editor\Unity.exe
"%UNITY%" -batchmode -nographics -quit -projectPath "%~dp0." -executeMethod BuildScript.BuildWindows -logFile "%~dp0build.log"
echo 빌드 완료: Builds\Windows\ClockworkRoll.exe  (로그: build.log)
