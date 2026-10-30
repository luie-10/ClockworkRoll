#!/usr/bin/env bash
# 사용법: UNITY=/path/to/Unity ./build.sh [windows|mac|linux|webgl]
# 예) macOS:  UNITY="/Applications/Unity/Hub/Editor/6000.0.23f1/Unity.app/Contents/MacOS/Unity" ./build.sh mac
#     Linux:  UNITY="$HOME/Unity/Hub/Editor/6000.0.23f1/Editor/Unity" ./build.sh linux
set -euo pipefail
cd "$(dirname "$0")"
TARGET="${1:-windows}"
case "$TARGET" in
  windows) METHOD=BuildScript.BuildWindows ;;
  mac)     METHOD=BuildScript.BuildMac ;;
  linux)   METHOD=BuildScript.BuildLinux ;;
  webgl)   METHOD=BuildScript.BuildWebGL ;;
  *) echo "unknown target: $TARGET"; exit 1 ;;
esac
: "${UNITY:?UNITY 환경변수에 Unity 에디터 실행 파일 경로를 지정하세요}"
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD" -executeMethod "$METHOD" -logFile build.log
echo "빌드 완료 → Builds/ (로그: build.log)"
