# 태엽 굴림 (Clockwork Roll)

공은 스스로 움직이지 못합니다. 바닥의 **태엽 톱니**가 돌면서 공을 굴립니다.
**Space**를 누를 때마다 태엽을 반대로 감아 모든 톱니의 회전 방향이 바뀝니다.

## 조작
| 키 | 동작 |
|---|---|
| Space | 태엽 감기 (회전 방향 전환) |
| R | 재시작 |
| 1~5 | 레벨 선택 |
| Esc | 타이틀 |

## 톱니 종류
- 황동색 **일반**: Space 방향을 따름 (시계 방향 → 위의 공이 오른쪽으로)
- 파란색 **역회전**: 항상 일반 톱니의 반대
- 회색 **고정**: Space에 반응하지 않음

빨간 가시에 닿으면 재시작, 초록 깃발에 닿으면 클리어.
별 3개 = 클리어 + 톱니 조각 전부 수집 + 목표 횟수 이하로 감기.

## Unity CLI로 빌드
Unity 2021.3 이상 / Unity 6 지원. 외부 에셋 없음(스프라이트·사운드 모두 코드로 생성).

```bash
# macOS / Linux
UNITY="/Applications/Unity/Hub/Editor/<버전>/Unity.app/Contents/MacOS/Unity" ./build.sh mac
UNITY="$HOME/Unity/Hub/Editor/<버전>/Editor/Unity" ./build.sh linux   # windows | webgl 도 가능

# Windows
build.bat "C:\Program Files\Unity\Hub\Editor\<버전>\Editor\Unity.exe"
```

직접 실행하려면:
```bash
Unity -batchmode -nographics -quit -projectPath . -executeMethod BuildScript.BuildWindows -logFile build.log
```
결과물은 `Builds/` 폴더에 생성됩니다.

에디터에서 바로 해보려면 프로젝트를 열고 아무 씬에서 Play를 누르세요 (게임이 자동으로 부팅됩니다).
메뉴 `Clockwork > Create Main Scene`으로 씬을 만들 수도 있습니다.

## 레벨 추가
`Assets/Scripts/Levels.cs`에 ASCII 맵을 추가하면 됩니다. 기호 설명은 파일 상단 주석 참고.
