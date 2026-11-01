# ClockworkRoll — 시계판 카드 전투 (기력 · 장력 · 흡수 · 침식) — Unity 셋팅 가이드

한 씬 안에서 **스토리(대사) → 시계판 전투 → 승리/흡수 → 다음 스토리**가 반복되는 오리지널 게임의 뼈대입니다.
물에 잠긴 시계 마을이 배경이고, 전투도 **시계판(1시~12시)** 위에서 벌어집니다.

## 이 게임만의 규칙

| 시스템 | 규칙 |
|---|---|
| **시계판** | 한 라운드 = 시곗바늘 한 바퀴(1~12시). **시계판 한 칸 = 주사위 하나.** 적의 행동이 미리 시계판에 보이고, 나는 태엽판을 **원하는 시각**에 놓는다. 태엽판의 주사위가 그 시각부터 한 칸씩 이어서 배정된다 (3개면 3칸) |
| **합** | 칸마다 따로 겨룬다. **같은 칸에 내 주사위와 적 주사위가 있으면 그 둘만 합**(먼저 시작한 행동이 공격자). 상대가 없는 칸의 공격 주사위는 **일방 공격**, 수비 주사위는 아무 일 없음 — 어느 칸에 무엇을 맞댈지가 수싸움의 핵심 |
| **기력** | 태엽판 비용. 라운드마다 회복되고, **공격 주사위로 합을 이기면 +1 추가 회복** |
| **장력** | 적중할수록 이안의 태엽이 감긴다(10pt당 1단계, 최대 5). 높은 단계가 필요한 태엽판이 있다. **최대 장력 = 모든 주사위 +1**, 하지만 최대인 채로 라운드를 넘기면 **태엽이 끊어져** 피해를 입고 3단계로 풀린다 |
| **녹** | 출혈 대신. 굴리는 주사위의 **최댓값이 깎이고**, 주사위를 하나 굴릴 때마다 1씩 벗겨진다 |
| **타입 상성** | **참격 > 관통 > 타격 > 참격** 순환. 우위한 쪽은 굴림 **+2** (`BattleManager.typeAdvantageBonus`) |
| **회피 보정** | 회피 주사위는 상대가 공격 주사위일 때 굴림 **+2** |
| **동점 규칙** | 합에서 같은 값이 나오면 **양쪽 모두 무효** (팽팽하게 밀림) |
| **태엽판 흡수** | 승리하면 **VICTORY** 후 쓰러뜨린 적의 태엽판이 펼쳐지고, **전투가 끝났을 때의 장력 단계만큼** 골라 흡수한다 (1단계 = 1장, 2단계 = 2장 … 0단계 = 0장). ◈ 표시, 이미 가진 것을 또 고르면 침식 +1 |
| **침식** | 흡수 태엽판이 발동할 때 확률적으로 사용자가 피해. 중복 흡수할수록 침식이 깊어진다 |
| **등급** | 주석 → 황동 → 강철 → 금박 → 정지된 시간 |

```
게임 시작
  ▼
[스토리]  대사 클릭/Space로 진행 (타이핑 효과)
  ▼
[전투]    계획: 적 행동이 시계판에 표시됨
          태엽판 클릭 → 시계판의 시각 클릭 = 배치 (적 클릭 = 그 적의 다음 행동에 맞부딪히게 자동 배치)
          배치한 칸을 다시 클릭 = 회수 (기력 반환)
          적 클릭 / 숫자 키 1~9 = 일방 공격 대상 지정 (적 머리 위 [번호]) · 내 칸에 마우스를 올리고 숫자 키 = 그 행동만 대상 변경
          [덱 보기] 버튼 / Tab = 뽑을 더미 · 손패 · 시계판 위 · 버림패 확인
          '시계 진행' 버튼 → 바늘이 1시부터 한 칸씩: 같은 칸의 주사위끼리 합, 혼자인 공격 주사위는 일방 공격
  ├─ 승리 → VICTORY → 장력 단계만큼 적 태엽판 골라 흡수(◈/침식) + 보상 → 아웃트로 → 다음 챕터
  └─ 패배 → GameOver → 씬 재시작
```

---

## 1. 파일 구성

| 파일 | 역할 |
|---|---|
| `Scripts/Core/GameManager.cs` | 최상위 진행 관리. 챕터 순서, **태엽판 흡수/침식 확률**, 보상, 패배/재시작 |
| `Scripts/Data/ChapterData.cs` | 챕터 1개 = 인트로 대사 + 전투 무대 + 아웃트로 대사 + 보상 카드 (ScriptableObject) |
| `Scripts/Data/BattleStageData.cs` | 무대 1개 = **시작 기력 / 턴당 기력 회복량**, 적 배치, 덱 오버라이드 |
| `Scripts/Data/EnemyData.cs` | 적 1종 = 이름, 체력, 외형 프리팹, 사용 태엽판 목록 |
| `Scripts/Story/DialogueManager.cs` | 대사 재생(타이핑 효과), 좌클릭/Space 진행, 종료 시 콜백 |
| `Scripts/Battle/BattleManager.cs` | 전투 진행. **기력**, **타입 상성**, 동점 무효, **침식 발동**, 턴 순서, 승패 판정 |
| `Scripts/Battle/Unit.cs` | 3D 유닛(공용). 체력, 피격 플래시, **이동 연출(MoveToRoutine)**, 클릭 입력 |
| `Scripts/Battle/DeckEntry.cs` | 덱 카드 1장 단위. **흡수 여부 + 침식 수치** 보관 |
| `Scripts/Battle/HandManager.cs` | 덱 셔플/드로우/손패 UI 생성/버림패 순환 |
| `Scripts/Battle/CardInstance.cs` | 전투 중 카드 인스턴스 (DeckEntry + CardData 묶음) |
| `Scripts/UI/CardView.cs` | 손패 카드 UI. 등급별 배경색, **흡수 카드 ◈/침식 표시**, 클릭 이벤트 |
| `Scripts/Presentation/BattleCamera.cs` | **합 클로즈업 카메라** (Main Camera에 부착) |
| `Scripts/Presentation/StageLighting.cs` | **장력 단계별 조명 변화** (Directional Light에 부착) |
| (기존) `CardData.cs` | **수정 없이 그대로 사용** — `cost` 필드가 기력 소모로 해석됩니다 |

---

## 2. 설치

1. 이전 버전을 이미 넣었다면 `Scripts/` 내용을 **전부 교체** (파일 몇 개는 필드 이름이 바뀌었습니다)
2. 새 프로젝트라면 `Scripts/` 폴더 전체를 `Assets/`로 복사하고 기존 `CardData.cs`를 같은 프로젝트에 둔다
3. 콘솔에 에러가 없는지 확인

> ⚠ 이전 버전에서 만든 Battle Stage 에셋이 있다면 다시 만들어야 합니다 (빛 → 기력으로 필드가 바뀜).

---

## 3. 씬 세팅 (순서대로)

### 3-1. 3D 공간
1. `Ground`: Hierarchy 우클릭 → **3D Object → Plane**
2. `Player`: **3D Object → Cube**, 위치 `(0, 0.5, -4)` 정도
   - **Add Component → Unit**, `Is Player` 체크, `Unit Name`/`Max Hp` 설정
3. Main Camera가 바닥과 유닛을 보도록 회전 (예: 위치 `(0, 6, -10)`, X=35°)

### 3-2. 연출 컴포넌트 (이번 개조의 핵심)
1. **Main Camera** → **Add Component → BattleCamera** (클로즈업 연출)
   - `Focus Offset`으로 클로즈업 시점 조절 (기본 `(0, 1.5, -3.5)`)
   - 연출이 부담스러우면 이 컴포넌트를 빼도 게임은 정상 동작
2. **Directional Light** → **Add Component → StageLighting** (장력 단계별 빛 변화)
   - `Emotion Colors` 6칸으로 단계별 색 조절 (기본: 회청색 → 붉은색)
   - 마찬가지로 빼도 동작에 지장 없음

### 3-3. 매니저 오브젝트 (빈 오브젝트 3개)
| 오브젝트 | 붙일 컴포넌트 |
|---|---|
| `GameManager` | GameManager |
| `DialogueManager` | DialogueManager |
| `BattleManager` | BattleManager + HandManager (같이 붙여도 됨) |

### 3-4. UI (Canvas 하나에 전부)
Hierarchy 우클릭 → **UI → Canvas** (EventSystem이 자동 생성됩니다 — 지우지 마세요)

**① DialoguePanel** (Canvas 자식, 빈 오브젝트)
- `Image` 추가 → 하단에 크게 배치 (앵커 bottom-stretch)
- 자식: `SpeakerNameText`(Text), `BodyText`(Text, TextArea), `PortraitImage`(Image)
- DialogueManager 인스펙터에 네 개 전부 연결

**② BattleHUD** (Canvas 자식, 빈 오브젝트)
- `StageNameText` / `KiText`(기력) / `EmotionText` / `BattleLogText`(왼쪽 세로로 길게) 배치
- `EndTurnButton`(Button) 생성 → **OnClick에 BattleManager 오브젝트를 끌어다 놓고 `BattleManager.EndPlayerTurn` 선택**
- BattleManager 인스펙터의 `Ki Text`, `Emotion Text` 등에 연결

**③ HandPanel** (Canvas 자식, 빈 오브젝트)
- 화면 하단 가로로 배치 (앵커 bottom-stretch)
- **Horizontal Layout Group** 추가 (Child Force Expand 체크 해제 권장)
- HandManager의 `Hand Panel`에 연결

### 3-5. CardView 프리팹 만들기
1. HandPanel 자식으로 **UI → Image** 생성 → 이름 `CardView`, 크기 약 `160×220`
2. 자식으로 `NameText`(상단), `CostText`(모서리), `DiceText`(하단) Text 배치
3. **Add Component → CardView** → 네 개 UI를 인스펙터에 연결
4. Project 창으로 드래그해 **프리팹화** → 씬의 원본은 삭제
5. HandManager의 `Card View Prefab` 슬롯에 프리팹 할당

### 3-6. GameManager 인스펙터
- `Player Unit` ← 씬의 Player, `Dialogue Manager` ← DialogueManager, `Battle Manager` ← BattleManager 연결
- `Starting Deck` ← 시작 카드(CardData 에셋) 등록
- `Corruption Base Chance`(기본 0.2) / `Corruption Chance Per Point`(기본 0.05) — 침식 발동 확률 조절

---

## 4. 데이터 에셋 만들기

Project 창 우클릭 → **Create → Ruina** 메뉴:

1. **Card Data** — 카드 이름, 코스트(**기력** 소모), 등급, `Dice List`에 주사위 추가
   (예: Element + → `DiceType: Attack`, `DiceDetail: Slash`, `Min 2 / Max 6`)
2. **Enemy Data** — 이름, HP, (선택) 모델 프리팹, 사용할 카드 목록
   → **여기 넣은 카드가 승리 시 플레이어 덱으로 흡수됩니다.** 흡수시키고 싶지 않은 카드는 적 덱에 넣지 마세요.
3. **Battle Stage** — 무대 이름, `Start Ki` 3, `Ki Per Turn` 1,
   `Enemies`에 적 추가 후 `Spawn Position` 지정 (예: `(3, 1, 2)`, Y는 바닥 위로)
4. **Chapter** — 챕터 제목, 인트로 대사 추가, `Battle`에 스테이지 할당, 아웃트로 대사, 보상 카드
5. 만든 챕터들을 GameManager의 `Chapters` 목록에 **진행 순서대로** 등록

> 전투가 없는 챕터(순수 스토리)를 만들려면 Chapter의 `Battle`을 비워두면 됩니다.

---

## 5. 침식(◈) 시스템 이해하기

- 승리하면 그 무대 적들이 쓰던 태엽판이 전부 흡수되어 보유 덱에 들어온다 (◈ + 보라색)
- **같은 이름의 태엽판을 또 흡수하면 침식 수치 +1** (카드에 "침식 n"으로 표시)
- 흡수 카드를 사용하면 `침식 발동 확률 = 0.2 + 0.05 × (침식-1)` 로 판정 (최대 0.8)
- 발동 시 피해 `2 + (침식-1)` — 설정은 GameManager(`corruptionBaseChance` 등)와 BattleManager(`corruptionBaseDamage`)
- 즉, **적이 강할수록 흡수 이득이 크지만 덱이 점점 오염된다** — 서사("삼킨 기억은 누구의 것인가")와 규칙이 한 몸

---

## 6. 확장 포인트 (주석으로 위치 표시해 둠)

| 하고 싶은 것 | 손볼 곳 |
|---|---|
| 상성표 수정 (순환/보정치) | `BattleManager`의 `advantageOver` 딕셔너리 + `typeAdvantageBonus` |
| 동점 규칙 (공격자 우위로 되돌리기 등) | `ResolveClashRoutine`의 `atkRoll == defRoll` 분기 |
| 침식 발동 효과 다양화 (기력 태우기, 덱에서 카드 소멸 등) | `UseCardRoutine`의 침식 판정 블록 |
| 흡수 거부/선택 시스템 (승리 후 "흡수할까 폐기할까" 선택) | `GameManager.AbsorbCards`를 선택지 UI와 연결 |
| 장력 단계 게이지 조정 | `BattleManager.AddEmotion` (현재 10pt당 1레벨, 최대 5) |
| 이동 거리/복귀 속도 | `BattleManager.TryGetStandPosition`의 `2.0f`, `MoveToRoutine` duration |
| 장력 단계별 무대 색 | `StageLighting.emotionColors` |
| TextMeshPro 사용 | 각 매니저의 `Text` → `TMPro.TextMeshProUGUI`, `using TMPro;` |
| 세이브/로드 | `GameManager.OwnedDeck`(DeckEntry 목록) + `ChapterIndex` 직렬화 |

## 7. 문제 해결

- **적을 클릭해도 반응이 없다** → 적 오브젝트에 `Collider`가 있는지 확인 (기본 큐브에는 자동으로 있음), EventSystem이 씬에 있는지 확인
- **손패가 안 보인다** → HandManager의 `Card View Prefab` / `Hand Panel` 연결 확인
- **대사창이 안 나온다** → DialogueManager의 `Dialogue Panel` 연결 확인
- **무대가 바로 클리어된다** → Battle Stage에 적이 배치되지 않은 경우입니다 (적 1마리 이상 배치)
- **카메라가 이상하게 움직인다** → Main Camera의 `BattleCamera`를 제거하면 기본 시점 고정 (연출 전용 컴포넌트)
- **빛이 안 바뀐다** → Directional Light에 `StageLighting`이 붙어 있는지, 장력 단계가 실제로 올랐는지(로그 확인) 점검
