using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RuinaTest
{
    // ═══════════════════════════════════════════════════════════════════
    // [단독 테스트 부트스트래퍼 — 단일 파일, 외부 의존성 0]
    //
    // 프로젝트의 다른 스크립트가 하나도 없어도 이 파일 하나만으로
    // 핵심 시스템 전체(기력 / 타입 상성 / 동점 무효 / 흡수 / 침식 / 감정 단계)
    // 가 콘솔 로그 + 3D 큐브 연출로 돌아간다.
    //
    // 사용법:
    //   1. 새 씬 생성 (기본 템플릿: Main Camera + Directional Light 유지)
    //   2. 빈 오브젝트 → 이 스크립트(TestBootStandalone) 부착
    //   3. Play → Console 창(Ctrl+Shift+C? 에디터 하단 Console)에서 관전
    //
    // - Auto Play 체크(기본): 클릭 없이 끝까지 자동 진행
    // - Auto Play 해제: 대사마다 좌클릭/Space로 진행
    //
    // 주의:
    //   - 기존 TestBoot.cs(전체 시스템용)는 시스템 스크립트들이 모두
    //     프로젝트에 있어야 컴파일된다. 시스템 파일이 다 차 있지 않다면
    //     일단 씬/프로젝트에서 빼고 이 파일만으로 테스트할 것.
    //   - 모든 클래스가 RuinaTest 네임스페이스 안에 있어서
    //     기존 스크립트(Unit.cs 등)와 이름이 겹쳐도 충돌하지 않는다.
    // ═══════════════════════════════════════════════════════════════════

    public class TestBootStandalone : MonoBehaviour
    {
        [Header("테스트 모드")]
        [Tooltip("체크: 클릭 없이 끝까지 자동 진행 / 해제: 대사마다 클릭")]
        public bool autoPlay = true;

        [Header("테스트 설정")]
        public int playerMaxHp = 60;
        public int handLimit = 5;
        public int startKi = 3;
        public int kiPerTurn = 1;
        public int typeBonus = 2;               // 상성 우위 굴림 보정
        public float corruptionBaseChance = 0.2f; // 침식 발동 기본 확률
        public int corruptionBaseDamage = 2;      // 침식 발동 기본 피해

        private TUnit player;
        private readonly List<TUnit> enemies = new List<TUnit>();

        private List<TCard> owned = new List<TCard>();    // 보유 덱 (흡수 반영)
        private readonly List<TCard> draw = new List<TCard>();
        private readonly List<TCard> discard = new List<TCard>();
        private readonly List<TCard> hand = new List<TCard>();

        private int ki;
        private int emotionLevel;
        private int emotionPoints;

        void Start()
        {
            BuildStage();
            StartCoroutine(StoryRoutine());
        }

        // ─────────────────────── 전체 흐름 ───────────────────────

        private IEnumerator StoryRoutine()
        {
            Log("=== 인트로 대사 ===");
            yield return Say("안내", "단독 테스트다. 기력·타입 상성·동점 무효·흡수·침식을 여기서 확인한다.");
            yield return Say("안내", "전투에서 승리하면 적의 책장이 ◈ 흡수되고, 사용 시 침식이 발동할 수 있다.");

            yield return BattleRoutine();

            if (!player.IsAlive)
            {
                Log("=== 패배 — 테스트 종료 (Play를 다시 눌러 재시작) ===");
                yield break;
            }

            Log("=== 승리! 적의 책장을 흡수한다 ===");
            AbsorbSpoils();

            yield return Say("안내", "아웃트로. 흡수된 책장(◈)은 다음 전투의 손패에 등장한다.");

            Log("=== 클리어! 테스트 완료 ===");
        }

        private IEnumerator Say(string speaker, string text)
        {
            if (!autoPlay)
            {
                Log($"[대사] {speaker}: {text}   ← 클릭/Space하면 계속");
                while (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space))
                    yield return null;
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
                Log($"[대사] {speaker}: {text}");
            }
        }

        // ─────────────────────── 전투 ───────────────────────

        private IEnumerator BattleRoutine()
        {
            ki = startKi;
            emotionLevel = 0;
            emotionPoints = 0;

            draw.Clear();
            discard.Clear();
            hand.Clear();
            foreach (TCard card in owned) draw.Add(card);
            Shuffle(draw);

            Log($"=== 전투 시작 (시작 기력 {ki}, 덱 {draw.Count}장) ===");

            while (true)
            {
                // ── 내 턴 ──
                ki = Mathf.Min(ki + kiPerTurn, 20);
                Refill();
                Log($"── 내 턴 (기력 {ki}, 손패 {hand.Count}장) ──");

                int actions = 3;
                while (actions > 0)
                {
                    TCard card = PickPlayable();
                    TUnit target = RandomAlive(enemies);

                    if (card == null || target == null) break;

                    yield return UseCard(card, target);
                    actions--;

                    if (!player.IsAlive || !target.IsAlive) break;
                }

                if (!player.IsAlive) yield break;
                if (AllDead(enemies)) yield break;

                // ── 적의 턴 ──
                Log("── 적의 턴 ──");
                foreach (TUnit enemy in enemies)
                {
                    if (!enemy.IsAlive) continue;

                    yield return new WaitForSeconds(0.4f);

                    TCard card = enemy.personalDeck.Count > 0
                        ? enemy.personalDeck[UnityEngine.Random.Range(0, enemy.personalDeck.Count)]
                        : null;

                    if (card == null)
                    {
                        Log($"{enemy.unitName}: 사용할 책장이 없다.");
                        continue;
                    }

                    yield return Clash(enemy, card, player, RandomHandCard());

                    if (!player.IsAlive) yield break;
                }

                yield return new WaitForSeconds(0.4f);
            }
        }

        private IEnumerator UseCard(TCard card, TUnit target)
        {
            ki -= card.cost;
            hand.Remove(card);
            discard.Add(card);
            Log($"사용: [{card.name}] (기력 -{card.cost} → {ki}) → 대상: {target.unitName}");

            // 흡수 카드(◈)의 침식 판정
            if (card.absorbed)
            {
                float chance = Mathf.Min(0.8f, corruptionBaseChance + 0.05f * Mathf.Max(0, card.corruption - 1));
                if (UnityEngine.Random.value < chance)
                {
                    int dmg = corruptionBaseDamage + Mathf.Max(0, card.corruption - 1);
                    Log($"  기억이 침식했다! [{card.name}]에 깃든 원한이 샌다 → 자해 피해 {dmg}");
                    player.Hit(dmg);
                    if (!player.IsAlive) yield break;
                }
            }

            TCard response = target.personalDeck.Count > 0
                ? target.personalDeck[UnityEngine.Random.Range(0, target.personalDeck.Count)]
                : null;

            yield return Clash(player, card, target, response);
        }

        // 양쪽 주사위를 앞에서부터 한 칸씩 비교 (동점 = 양쪽 무효)
        private IEnumerator Clash(TUnit attacker, TCard atkCard, TUnit defender, TCard defCard)
        {
            string defName = defCard != null ? defCard.name : "없음";
            Log($"합: {attacker.unitName}[{atkCard.name}] vs {defender.unitName}[{defName}]");

            List<TDie> a = atkCard.dice;
            List<TDie> d = defCard != null ? defCard.dice : null;
            int count = Mathf.Max(a != null ? a.Count : 0, d != null ? d.Count : 0);

            for (int i = 0; i < count; i++)
            {
                if (!attacker.IsAlive || !defender.IsAlive) break;

                yield return new WaitForSeconds(0.3f);

                TDie ad = (a != null && i < a.Count) ? a[i] : null;
                TDie dd = (d != null && i < d.Count) ? d[i] : null;

                int ar = ad != null ? RollWithAdvantage(ad, dd) : -1;
                int dr = dd != null ? RollWithAdvantage(dd, ad) : -1;

                Log($"  주사위 {i + 1}: {attacker.unitName} [{Kind(ad)} {ar}] vs {defender.unitName} [{Kind(dd)} {dr}]");

                if (ad == null && dd == null) break;

                if (ar == dr)
                {
                    Log("  팽팽하게 밀렸다 — 양쪽 주사위가 함께 무효화된다!");
                }
                else if (ar > dr)
                {
                    Apply(attacker, defender, ad, ar);
                }
                else
                {
                    Apply(defender, attacker, dd, dr);
                }
            }
        }

        private int RollWithAdvantage(TDie mine, TDie theirs)
        {
            int roll = mine.Roll();
            if (theirs == null) return Mathf.Max(roll, 0);

            bool theirsAttack = IsAttack(theirs.kind);

            // 공격 대 공격: 참격 > 관통 > 타격 > 참격 순환 우위 보정
            if (IsAttack(mine.kind) && theirsAttack && Beats(mine.kind, theirs.kind))
            {
                roll += typeBonus;
                Log($"  → {mine.kind}은(는) {theirs.kind}에 강하다! 굴림 +{typeBonus}");
            }

            // 회피: 상대 공격 주사위를 상대로 굴림 보정
            if (mine.kind == TDiceKind.Evade && theirsAttack)
            {
                roll += typeBonus;
                Log($"  → 회피가 공격을 겨냥한다! 굴림 +{typeBonus}");
            }

            return Mathf.Max(roll, 0);
        }

        private void Apply(TUnit source, TUnit target, TDie die, int roll)
        {
            if (IsAttack(die.kind))
            {
                target.Hit(roll);
                Log($"  {source.unitName}의 {die.kind} 주사위 → {target.unitName}에게 피해 {roll}!");

                emotionPoints += 2;
                while (emotionLevel < 5 && emotionPoints >= (emotionLevel + 1) * 10)
                {
                    emotionLevel++;
                    Log($"  감정 단계 상승! LV.{emotionLevel}");
                }

                // 공격 주사위로 합을 이기면 기력 +1 (밀어붙일수록 몰아친다)
                if (source.isPlayer)
                {
                    ki = Mathf.Min(ki + 1, 20);
                    Log($"  기력 +1 (→ {ki})");
                }
            }
            else
            {
                Log($"  {source.unitName}의 {die.kind} 주사위가 상대 주사위를 막았다!");
            }
        }

        // ─────────────────────── 흡수 ───────────────────────

        private void AbsorbSpoils()
        {
            foreach (TUnit enemy in enemies)
            {
                foreach (TCard card in enemy.personalDeck)
                {
                    TCard same = owned.Find(c => c.absorbed && c.name == card.name);

                    if (same != null)
                    {
                        same.corruption++;
                        Log($"흡수: [{card.name}] 재흡수 — 침식이 깊어진다 ({same.corruption})");
                    }
                    else
                    {
                        TCard copy = card.Copy();
                        copy.absorbed = true;
                        copy.corruption = 1;
                        owned.Add(copy);
                        Log($"흡수: ◈ [{card.name}] → 내 덱에 합류 (침식 1)");
                    }
                }
            }
        }

        // ─────────────────────── 스테이지 구성 ───────────────────────

        private void BuildStage()
        {
            player = SpawnUnit("테스트 수헬자", playerMaxHp, new Color(0.3f, 0.55f, 0.9f), true, new Vector3(0f, 0.5f, -4f));

            owned = new List<TCard>
            {
                MakeTCard("기본 참격", 1, TDiceKind.Slash, 2, 6),
                MakeTCard("기본 참격", 1, TDiceKind.Slash, 2, 6),
                MakeTCard("기본 가드", 1, TDiceKind.Guard, 3, 7),
                MakeTCard("기본 가드", 1, TDiceKind.Guard, 3, 7),
                MakeTCard("기본 회피", 1, TDiceKind.Evade, 2, 6),
                MakeTCard("기본 관통", 1, TDiceKind.Thrust, 1, 5),
                MakeTCard("기본 타격", 2, TDiceKind.Blunt, 3, 8),
            };

            TUnit a = SpawnUnit("적 A", 30, new Color(0.8f, 0.35f, 0.3f), false, new Vector3(3f, 1f, 2f));
            a.personalDeck.Add(MakeTCard("야생의 참격", 1, TDiceKind.Slash, 2, 5));
            a.personalDeck.Add(MakeTCard("떼어낸 가죽", 1, TDiceKind.Guard, 1, 4));

            TUnit b = SpawnUnit("적 B", 45, new Color(0.4f, 0.5f, 0.85f), false, new Vector3(-3f, 1f, 2f));
            b.personalDeck.Add(MakeTCard("무거운 타격", 2, TDiceKind.Blunt, 3, 7));
            b.personalDeck.Add(MakeTCard("빠른 몸놀림", 1, TDiceKind.Evade, 2, 5));

            enemies.Add(a);
            enemies.Add(b);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
        }

        private TUnit SpawnUnit(string unitName, int hp, Color tint, bool isPlayer, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = unitName;
            go.transform.position = pos;

            TUnit unit = go.AddComponent<TUnit>();
            unit.Setup(unitName, hp, tint, isPlayer);
            return unit;
        }

        // ─────────────────────── 덱/손패 유틸 ───────────────────────

        private void Refill()
        {
            if (draw.Count == 0 && discard.Count > 0)
            {
                draw.AddRange(discard);
                discard.Clear();
                Shuffle(draw);
            }

            while (hand.Count < handLimit && draw.Count > 0)
            {
                TCard card = draw[0];
                draw.RemoveAt(0);
                hand.Add(card);
            }
        }

        private TCard PickPlayable()
        {
            List<TCard> playable = new List<TCard>();
            foreach (TCard c in hand)
            {
                if (c.cost <= ki) playable.Add(c);
            }

            // 공격 카드 우선 (수비밖에 없으면 그것을 사용)
            foreach (TCard c in playable)
            {
                if (HasAttackDie(c)) return c;
            }
            return playable.Count > 0 ? playable[0] : null;
        }

        private TCard RandomHandCard()
        {
            return hand.Count > 0 ? hand[UnityEngine.Random.Range(0, hand.Count)] : null;
        }

        private TUnit RandomAlive(List<TUnit> units)
        {
            List<TUnit> alive = new List<TUnit>();
            foreach (TUnit u in units)
            {
                if (u.IsAlive) alive.Add(u);
            }
            return alive.Count > 0 ? alive[UnityEngine.Random.Range(0, alive.Count)] : null;
        }

        private bool AllDead(List<TUnit> units)
        {
            foreach (TUnit u in units)
            {
                if (u.IsAlive) return false;
            }
            return true;
        }

        private void Shuffle(List<TCard> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                TCard tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static bool IsAttack(TDiceKind kind)
        {
            return kind == TDiceKind.Slash || kind == TDiceKind.Thrust || kind == TDiceKind.Blunt;
        }

        private static bool HasAttackDie(TCard card)
        {
            foreach (TDie die in card.dice)
            {
                if (IsAttack(die.kind)) return true;
            }
            return false;
        }

        private static bool Beats(TDiceKind a, TDiceKind b)
        {
            return (a == TDiceKind.Slash && b == TDiceKind.Thrust) ||
                   (a == TDiceKind.Thrust && b == TDiceKind.Blunt) ||
                   (a == TDiceKind.Blunt && b == TDiceKind.Slash);
        }

        private static string Kind(TDie die)
        {
            return die != null ? die.kind.ToString() : "-";
        }

        private static TCard MakeTCard(string name, int cost, TDiceKind kind, int min, int max)
        {
            TCard card = new TCard(name, cost);
            card.dice.Add(new TDie(kind, min, max));
            return card;
        }

        private void Log(string message)
        {
            Debug.Log("[단독테스트] " + message);
        }
    }

    // ═════════════════════ 테스트 전용 미니 데이터/유닛 ═════════════════════

    public enum TDiceKind
    {
        Slash,   // 참격
        Thrust,  // 관통
        Blunt,   // 타격
        Guard,   // 가드
        Evade    // 회피
    }

    public class TDie
    {
        public TDiceKind kind;
        public int min;
        public int max;

        public TDie(TDiceKind kind, int min, int max)
        {
            this.kind = kind;
            this.min = min;
            this.max = max;
        }

        public int Roll()
        {
            return UnityEngine.Random.Range(min, max + 1);
        }
    }

    public class TCard
    {
        public string name;
        public int cost;
        public bool absorbed;      // 적에게서 흡수한 카드인가
        public int corruption;     // 침식 수치
        public List<TDie> dice = new List<TDie>();

        public TCard(string name, int cost)
        {
            this.name = name;
            this.cost = cost;
        }

        public TCard Copy()
        {
            TCard copy = new TCard(name, cost);
            copy.absorbed = absorbed;
            copy.corruption = corruption;
            copy.dice.AddRange(dice);
            return copy;
        }
    }

    public class TUnit : MonoBehaviour
    {
        public string unitName = "유닛";
        public bool isPlayer;

        public int maxHp = 50;
        public int hp;

        public List<TCard> personalDeck = new List<TCard>();

        public bool IsAlive { get { return hp > 0; } }

        public event Action<TUnit> Died;

        private Renderer rend;
        private Color baseColor = Color.white;

        void Awake()
        {
            hp = maxHp;
            rend = GetComponentInChildren<Renderer>();
            if (rend != null) baseColor = rend.material.color;
        }

        public void Setup(string newName, int newHp, Color tint, bool newIsPlayer)
        {
            unitName = newName;
            maxHp = newHp;
            hp = newHp;
            isPlayer = newIsPlayer;

            rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                baseColor = tint;
                rend.material.color = tint;
            }
        }

        public void Hit(int damage)
        {
            if (!IsAlive || damage <= 0) return;

            hp = Mathf.Max(0, hp - damage);
            StartCoroutine(FlashRoutine());

            if (hp <= 0)
            {
                if (rend != null) rend.material.color = new Color(0.25f, 0.25f, 0.25f);
                if (Died != null) Died(this);
            }
        }

        private IEnumerator FlashRoutine()
        {
            if (rend == null) yield break;

            rend.material.color = new Color(0.9f, 0.2f, 0.2f);
            yield return new WaitForSeconds(0.12f);

            if (IsAlive && rend != null)
                rend.material.color = baseColor;
        }
    }
}
