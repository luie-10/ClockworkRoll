using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 태엽판마다 다른 고유 연출 (정밀 · 걸작 · 금단 등급의 이름 있는 태엽판).
///
/// 전투 흐름의 세 지점에서 부른다 (BattleManager):
///   OnStart   — 태엽판이 시계판에서 시작될 때 (컷인 뒤)
///   BeforeDie — 그 태엽판의 i번째 칸이 굴러가기 직전 (합 · 일방 공격 모두)
///   OnHit     — 그 칸의 공격이 들어갔을 때
///
/// 예) [열두 번의 타종]: 칸이 하나 진행될 때마다 주인 둘레 시계 자리에 종이 3개씩 나타나 울린다 → 4칸 x 3 = 열두 번.
///     열두 번째 종이 울리면 열두 개가 함께 번쩍이며 금빛 충격파.
///
/// 소품(종 · 톱니 · 동전 · 시계판 …)은 직접 그린 텍스처를 카메라를 바라보는 스프라이트로 띄운다.
/// 한 태엽판이 띄운 소품은 마지막 칸이 끝나면 사라진다 (혹시 남아도 몇 초 뒤 스스로 없어진다).
/// </summary>
public static class CardSignatures
{
    private delegate IEnumerator StartFx(Ctx c);
    private delegate IEnumerator DieFx(Ctx c, int index, int hour);
    private delegate void HitFx(Ctx c, int index, Vector3 point);

    private class Sig
    {
        public StartFx start;
        public DieFx die;
        public HitFx hit;
    }

    /// <summary>한 번 쓰인 태엽판의 연출 상태 (주인 + 태엽판마다 하나)</summary>
    private class Ctx
    {
        public MonoBehaviour host;
        public Unit owner, foe;
        public CardData card;
        public readonly List<GameObject> props = new List<GameObject>();
        public int DiceCount { get { return card.diceList != null ? card.diceList.Count : 0; } }
        public Vector3 Chest { get { return owner.transform.position + Vector3.up * 1.0f; } }
        public Vector3 FoeChest { get { return foe != null ? foe.transform.position + Vector3.up * 1.0f : Chest + Side * 2f; } }
        /// <summary>화면에서 주인이 상대 반대쪽으로 비켜설 방향 (소품을 상대와 겹치지 않게)</summary>
        public Vector3 Side
        {
            get
            {
                Vector3 d = foe != null ? owner.transform.position - foe.transform.position : Vector3.left;
                d.y = 0f;
                return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.left;
            }
        }
    }

    private static readonly Dictionary<string, Sig> sigs = new Dictionary<string, Sig>
    {
        { "열두 번의 타종", new Sig { die = TwelveBellsDie, hit = GoldRingHit } },
        { "제야의 종",      new Sig { start = NewYearBellStart, die = NewYearBellDie, hit = GoldRingHit } },
        { "자정의 일격",    new Sig { start = MidnightStart, die = MidnightDie, hit = MidnightHit } },
        { "그믐달 베기",    new Sig { start = CrescentStart, hit = CrescentHit } },
        { "톱니 폭풍",      new Sig { start = c => GearOrbitStart(c, 6, new Color(0.85f, 0.75f, 0.5f)), die = GearVolleyDie, hit = GearShardHit } },
        { "톱니 지옥",      new Sig { start = c => GearOrbitStart(c, 8, new Color(1f, 0.45f, 0.2f)), die = GearVolleyDie, hit = GearShardHit } },
        { "영구 기관",      new Sig { start = c => GearOrbitStart(c, 3, new Color(0.6f, 0.85f, 0.95f)), die = PerpetualDie } },
        { "멈춰 선 시간",   new Sig { start = TimeStopStart, die = TimeStopDie } },
        { "멈춘 심장의 고동", new Sig { die = HeartbeatDie, hit = RedPulseHit } },
        { "멎은 심장 태엽", new Sig { die = HeartbeatDie, hit = RedPulseHit } },
        { "러시안 태엽",    new Sig { die = RevolverDie, hit = MuzzleHit } },
        { "마지막 판돈",    new Sig { die = CoinRainDie, hit = CoinBurstHit } },
        { "올인",           new Sig { start = CoinPileStart, hit = CoinBurstHit } },
        { "이중 내기",      new Sig { die = CoinFlipDie } },
        { "행운의 태엽",    new Sig { die = CoinFlipDie, hit = CoinBurstHit } },
        { "과열 폭주",      new Sig { die = FlameDie, hit = FireHit } },
        { "용광로",         new Sig { die = FlameDie, hit = FireHit } },
        { "장력 해방",      new Sig { start = SpringStart, die = DashDie, hit = GoldRingHit } },
        { "열두 시의 그림자", new Sig { die = ShadowCloneDie, hit = ShadowHit } },
        { "새벽 종",        new Sig { die = DawnBellDie } },
        { "시침과 분침",    new Sig { start = MidnightStart, die = MidnightDie } },
    };

    private static readonly Dictionary<(Unit, CardData), Ctx> live = new Dictionary<(Unit, CardData), Ctx>();

    public static bool Has(CardData card) { return card != null && sigs.ContainsKey(card.cardName); }

    private static Ctx Get(MonoBehaviour host, Unit owner, Unit foe, CardData card, bool fresh)
    {
        var key = (owner, card);
        if (fresh && live.TryGetValue(key, out Ctx old)) Clear(old);
        if (fresh || !live.TryGetValue(key, out Ctx c))
        {
            c = new Ctx { host = host, owner = owner, foe = foe, card = card };
            live[key] = c;
        }
        if (foe != null) c.foe = foe;
        return c;
    }

    /// <summary>태엽판이 시작될 때 (전투가 이 코루틴을 기다린다)</summary>
    public static IEnumerator OnStart(MonoBehaviour host, Unit owner, Unit foe, CardData card)
    {
        if (!Has(card) || owner == null) yield break;
        Ctx c = Get(host, owner, foe, card, true);
        Sig s = sigs[card.cardName];
        if (s.start != null) yield return host.StartCoroutine(Safe(s.start(c)));
    }

    /// <summary>i번째 칸이 굴러가기 직전</summary>
    public static IEnumerator BeforeDie(MonoBehaviour host, Unit owner, Unit foe, CardData card, int index, int hour)
    {
        if (!Has(card) || owner == null) yield break;
        Ctx c = Get(host, owner, foe, card, false);
        Sig s = sigs[card.cardName];
        if (s.die != null) yield return host.StartCoroutine(Safe(s.die(c, index, hour)));
        // 마지막 칸이 끝나면 소품을 치운다 (결과 연출을 본 뒤에)
        if (index >= c.DiceCount - 1) host.StartCoroutine(ClearLater(c, 1.6f));
    }

    public static void OnHit(Unit owner, Unit foe, CardData card, int index, Vector3 point)
    {
        if (!Has(card) || owner == null) return;
        if (!live.TryGetValue((owner, card), out Ctx c)) return;
        Sig s = sigs[card.cardName];
        try { if (s.hit != null) s.hit(c, index, point); } catch (Exception e) { Debug.LogWarning("연출 오류: " + e.Message); }
    }

    /// <summary>전투가 끝나거나 라운드가 끝날 때 남은 소품을 모두 치운다</summary>
    public static void ClearAll()
    {
        foreach (Ctx c in live.Values) Clear(c);
        live.Clear();
    }

    private static IEnumerator ClearLater(Ctx c, float delay)
    {
        yield return new WaitForSeconds(delay);
        Clear(c);
        if (live.TryGetValue((c.owner, c.card), out Ctx now) && now == c) live.Remove((c.owner, c.card));
    }

    private static void Clear(Ctx c)
    {
        foreach (GameObject go in c.props) if (go != null) Prop.FadeOut(go, 0.25f);
        c.props.Clear();
    }

    /// <summary>연출 하나가 틀려도 전투는 멈추지 않게</summary>
    private static IEnumerator Safe(IEnumerator inner)
    {
        while (true)
        {
            object cur;
            try
            {
                if (!inner.MoveNext()) yield break;
                cur = inner.Current;
            }
            catch (Exception e) { Debug.LogWarning("연출 오류: " + e.Message); yield break; }
            yield return cur;
        }
    }

    private static IEnumerator Tween(float duration, Action<float> step)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            step(Mathf.Clamp01(t / duration));
            yield return null;
        }
        step(1f);
    }

    private static float Pop(float t) { return t < 0.6f ? Mathf.Lerp(0f, 1.18f, t / 0.6f) : Mathf.Lerp(1.18f, 1f, (t - 0.6f) / 0.4f); }

    private static Vector3 CamRight { get { Camera cam = Camera.main; return cam != null ? cam.transform.right : Vector3.right; } }
    private static Vector3 CamUp { get { Camera cam = Camera.main; return cam != null ? cam.transform.up : Vector3.up; } }

    private static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
    private static readonly Color Bronze = new Color(0.85f, 0.6f, 0.3f);

    // ═══════════════════════════ 종 ═══════════════════════════

    /// <summary>열두 번의 타종: i번째 칸 → 시계 자리 (3i+1 ~ 3i+3)시에 종이 하나씩 나타나 울린다</summary>
    private static IEnumerator TwelveBellsDie(Ctx c, int index, int hour)
    {
        Vector3 center = c.Chest + Vector3.up * 1.15f + c.Side * 1.0f;
        const float radius = 0.95f;
        for (int k = 0; k < 3; k++)
        {
            int n = index * 3 + k + 1;          // 몇 번째 종인가 (1~12)
            if (n > 12) break;
            float ang = (90f - n * 30f) * Mathf.Deg2Rad;   // 12시 = 위, 시계 방향
            Vector3 pos = center + (CamRight * Mathf.Cos(ang) + CamUp * Mathf.Sin(ang)) * radius;
            GameObject bell = Prop.Spawn(SigArt.Bell(), pos, 0.46f, Color.white, c, null, true);
            c.host.StartCoroutine(Prop.PopIn(bell, 0.16f));
            c.host.StartCoroutine(Prop.Swing(bell, 28f, 0.9f));
            FxQuad.Spawn(ImpactFX.RingTexture(), pos, Gold, 0.45f, Vector2.one * 0.2f, Vector2.one * 1.1f);
            FxQuad.Spawn(ImpactFX.GlowTexture(), pos, Gold, 0.3f, Vector2.one * 0.8f, Vector2.one * 0.3f);
            FxLight.Spawn(pos, Gold, 1.6f, 4f, 0.2f);
            Sfx.Play(SfxId.Chime, 0.75f + n * 0.02f, 0.06f);
            Vector3 outward = (pos - center).normalized;   // 번호는 고리 바깥쪽에 (종과 겹치지 않게)
            DamagePopup.Spawn(pos + outward * 0.42f, n.ToString(), n == 12 ? Color.white : Gold, n == 12 ? 1.1f : 0.7f);
            if (BattleCamera.Instance != null) BattleCamera.Instance.Shake(0.03f + n * 0.006f, 0.12f);
            yield return new WaitForSeconds(0.2f);

            if (n == 12)
            {
                // 열두 번째: 모든 종이 함께 번쩍 + 금빛 충격파
                foreach (GameObject g in c.props) if (g != null) c.host.StartCoroutine(Prop.Flash(g, Color.white, 0.35f));
                FxQuad.Spawn(ImpactFX.RingTexture(), center, Gold, 0.7f, Vector2.one * 0.8f, Vector2.one * 5f);
                FxQuad.Spawn(ImpactFX.RingTexture(), c.owner.transform.position + Vector3.up * 0.05f, Gold, 0.8f, Vector2.one, Vector2.one * 8f, 0f, 0f, true);
                FxQuad.Spawn(ImpactFX.StarTexture(), center, Color.white, 0.4f, Vector2.one * 4f, Vector2.one, 0f, 200f);
                FxLight.Spawn(center, Gold, 5f, 16f, 0.6f);
                ClashSpark.Burst(center, Gold, 60, 7f);
                StagePostFX.Punch(1f);
                StageBackdrop.Impact(1f);
                Sfx.Play(SfxId.Chime, 1f, 0f);
                Sfx.Play(SfxId.HitBig, 0.8f, 0f);
                DamagePopup.Spawn(center + CamUp * 1.35f, "열두 번의 종!", Gold, 1.4f);
                if (BattleCamera.Instance != null) { BattleCamera.Instance.Shake(0.35f, 0.5f); BattleCamera.Instance.Punch(1f); }
                yield return new WaitForSeconds(0.35f);
            }
        }
    }

    /// <summary>제야의 종: 상대 머리 위에 큰 종이 내려온다</summary>
    private static IEnumerator NewYearBellStart(Ctx c)
    {
        Vector3 pos = c.FoeChest + Vector3.up * 2.7f + Vector3.forward * 0.4f;
        GameObject bell = Prop.Spawn(SigArt.Bell(), pos + Vector3.up * 2f, 1.3f, new Color(1f, 0.92f, 0.7f), c, null, true);
        Sfx.Play(SfxId.Chime, 0.7f, 0f);
        yield return c.host.StartCoroutine(Tween(0.35f, t => { if (bell != null) bell.transform.position = Vector3.Lerp(pos + Vector3.up * 2f, pos, 1f - (1f - t) * (1f - t)); }));
        FxQuad.Spawn(ImpactFX.GlowTexture(), pos, Gold, 0.5f, Vector2.one * 2f, Vector2.one * 4f);
    }

    private static IEnumerator NewYearBellDie(Ctx c, int index, int hour)
    {
        GameObject bell = c.props.Count > 0 ? c.props[0] : null;
        if (bell == null) yield break;
        c.host.StartCoroutine(Prop.Swing(bell, 22f, 0.8f));
        Vector3 pos = bell.transform.position;
        Sfx.Play(SfxId.Chime, 1f, 0f);
        Sfx.Play(SfxId.HitBlunt, 0.5f);
        FxQuad.Spawn(ImpactFX.RingTexture(), pos, Gold, 0.6f, Vector2.one * 0.8f, Vector2.one * 5.5f);
        FxQuad.Spawn(ImpactFX.RingTexture(), c.FoeChest, Gold * 0.8f, 0.5f, Vector2.one * 0.5f, Vector2.one * 3.5f);
        FxLight.Spawn(pos, Gold, 3f, 10f, 0.35f);
        if (BattleCamera.Instance != null) BattleCamera.Instance.Shake(0.18f, 0.3f);
        StageBackdrop.Impact(0.6f);
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>새벽 종: 칸마다 작은 종이 주인 위에서 맑게 울린다</summary>
    private static IEnumerator DawnBellDie(Ctx c, int index, int hour)
    {
        Vector3 pos = c.Chest + Vector3.up * 1.0f + c.Side * 0.85f;
        Color dawn = new Color(1f, 0.85f, 0.6f);
        GameObject bell = Prop.Spawn(SigArt.Bell(), pos, 0.45f, dawn, c);
        c.host.StartCoroutine(Prop.PopIn(bell, 0.14f));
        c.host.StartCoroutine(Prop.Swing(bell, 30f, 0.7f));
        FxQuad.Spawn(ImpactFX.RingTexture(), pos, dawn, 0.4f, Vector2.one * 0.3f, Vector2.one * 2f);
        Sfx.Play(SfxId.Chime, 0.8f, 0.04f);
        yield return new WaitForSeconds(0.22f);
        Prop.FadeOut(bell, 0.4f);
        c.props.Remove(bell);
    }

    private static void GoldRingHit(Ctx c, int index, Vector3 p)
    {
        FxQuad.Spawn(ImpactFX.RingTexture(), p, Gold, 0.45f, Vector2.one * 0.5f, Vector2.one * 3.6f);
        FxLight.Spawn(p, Gold, 2.5f, 8f, 0.25f);
    }

    // ═══════════════════════════ 시계 ═══════════════════════════

    /// <summary>자정의 일격 · 시침과 분침: 주인 뒤에 큰 시계판이 떠오르고 바늘이 돈다</summary>
    private static IEnumerator MidnightStart(Ctx c)
    {
        Vector3 pos = c.Chest + Vector3.up * 0.6f + c.Side * 0.9f + Vector3.forward * 0.6f;
        Color face = new Color(0.75f, 0.8f, 1f);
        GameObject clock = Prop.Spawn(SigArt.ClockFace(), pos, 2.2f, UITheme.Alpha(face, 0.9f), c, null, true);
        GameObject hourHand = Prop.Spawn(SigArt.Hand(), pos, 0.85f, Color.white, c, new Vector2(0.5f, 0.06f), true);
        GameObject minHand = Prop.Spawn(SigArt.Hand(), pos, 1.15f, Gold, c, new Vector2(0.5f, 0.06f), true);
        Prop.SetOrder(hourHand, Prop.BehindOrder + 2);
        Prop.SetOrder(minHand, Prop.BehindOrder + 3);
        c.host.StartCoroutine(Prop.PopIn(clock, 0.2f));
        Sfx.Play(SfxId.ClockStart, 0.8f, 0f);
        // 바늘이 빙글빙글 돌다 12시에 멈춘다
        yield return c.host.StartCoroutine(Tween(0.6f, t =>
        {
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            Prop.SetRoll(hourHand, -360f * 1f * e - 90f * (1f - e));
            Prop.SetRoll(minHand, -360f * 3f * e);
        }));
        FxQuad.Spawn(ImpactFX.GlowTexture(), pos, face, 0.4f, Vector2.one * 3f, Vector2.one * 1.5f);
        Sfx.Play(SfxId.Chime, 0.8f, 0f);
    }

    private static IEnumerator MidnightDie(Ctx c, int index, int hour)
    {
        // 칸마다 분침이 한 칸씩 똑딱
        if (c.props.Count >= 3 && c.props[2] != null)
        {
            Prop.SetRoll(c.props[2], -30f * (index + 1));
            FxQuad.Spawn(ImpactFX.RingTexture(), c.props[0].transform.position, Color.white, 0.3f, Vector2.one * 1.5f, Vector2.one * 3.2f);
        }
        Sfx.Play(SfxId.Tock, 1f, 0f);
        yield return new WaitForSeconds(0.12f);
    }

    private static void MidnightHit(Ctx c, int index, Vector3 p)
    {
        // 하늘에서 내리꽂는 세로 일격
        FxQuad.Spawn(ImpactFX.StreakTexture(), p + Vector3.up * 0.6f, Color.white, 0.25f, new Vector2(0.6f, 6f), new Vector2(0.15f, 7f), 90f);
        FxQuad.Spawn(ImpactFX.StreakTexture(), p + Vector3.up * 0.6f, new Color(0.6f, 0.7f, 1f), 0.35f, new Vector2(1.2f, 7f), new Vector2(0.3f, 8f), 90f);
        FxQuad.Spawn(ImpactFX.StarTexture(), p, Color.white, 0.25f, Vector2.one * 3f, Vector2.one * 0.5f);
        FxLight.Spawn(p, new Color(0.7f, 0.8f, 1f), 4f, 12f, 0.3f);
    }

    /// <summary>멈춰 선 시간: 화면이 잿빛으로 가라앉고 시간이 느려진다</summary>
    private static IEnumerator TimeStopStart(Ctx c)
    {
        Overlay.Show(new Color(0.55f, 0.6f, 0.75f, 0.32f), 1.4f);
        Vector3 pos = c.Chest + Vector3.up * 0.5f;
        GameObject clock = Prop.Spawn(SigArt.ClockFace(), pos + Vector3.forward * 0.8f, 3.6f, new Color(0.7f, 0.75f, 0.95f, 0.6f), c, null, true);
        GameObject hand = Prop.Spawn(SigArt.Hand(), pos + Vector3.forward * 0.8f, 1.7f, Color.white, c, new Vector2(0.5f, 0.06f), true);
        Prop.SetOrder(hand, Prop.BehindOrder + 2);
        c.host.StartCoroutine(Prop.PopIn(clock, 0.25f));
        Sfx.Play(SfxId.ClockStart, 1f, 0f);
        float before = Time.timeScale;
        Time.timeScale = 0.25f;
        float t = 0f;
        int ticks = 0;
        while (t < 0.9f)
        {
            t += Time.unscaledDeltaTime;
            int now = Mathf.FloorToInt(t / 0.3f);
            if (now > ticks) { ticks = now; Sfx.Play(SfxId.Tock, 1f, 0f); Prop.SetRoll(hand, -6f * ticks); }
            yield return null;
        }
        if (Mathf.Approximately(Time.timeScale, 0.25f)) Time.timeScale = before > 0.01f ? before : 1f;
        FxQuad.Spawn(ImpactFX.RingTexture(), pos, Color.white, 0.5f, Vector2.one, Vector2.one * 6f);
    }

    private static IEnumerator TimeStopDie(Ctx c, int index, int hour)
    {
        if (c.props.Count >= 2 && c.props[1] != null) Prop.SetRoll(c.props[1], -6f * (index + 4));
        DamagePopup.Spawn(c.Chest + Vector3.up * 1.4f, "째깍", new Color(0.8f, 0.85f, 1f), 0.8f);
        Sfx.Play(SfxId.Tick, 1f, 0f);
        yield return new WaitForSeconds(0.1f);
    }

    // ═══════════════════════════ 달 · 그림자 ═══════════════════════════

    private static IEnumerator CrescentStart(Ctx c)
    {
        Color moon = new Color(0.75f, 0.85f, 1f);
        Vector3 pos = c.Chest + Vector3.up * 1.6f + c.Side * 0.5f + Vector3.forward * 0.5f;
        GameObject m = Prop.Spawn(SigArt.Crescent(), pos, 1.6f, moon, c, null, true);
        c.host.StartCoroutine(Prop.PopIn(m, 0.3f));
        FxQuad.Spawn(ImpactFX.GlowTexture(), pos, moon, 0.8f, Vector2.one * 2f, Vector2.one * 4.5f);
        FxLight.Spawn(pos, moon, 2.5f, 10f, 0.6f);
        yield return new WaitForSeconds(0.25f);
    }

    private static void CrescentHit(Ctx c, int index, Vector3 p)
    {
        Color moon = new Color(0.75f, 0.85f, 1f);
        float side = index % 2 == 0 ? 1f : -1f;
        FxQuad.Spawn(ImpactFX.ArcTexture(), p, moon, 0.4f, new Vector2(3f * side, 3f), new Vector2(5f * side, 5f), 30f * side, 260f * side);
        FxQuad.Spawn(ImpactFX.ArcTexture(), p, Color.white, 0.25f, new Vector2(2.8f * side, 2.8f), new Vector2(4.6f * side, 4.6f), 30f * side, 260f * side);
    }

    /// <summary>열두 시의 그림자: 주인의 그림자 분신이 양옆에서 함께 휘두른다</summary>
    private static IEnumerator ShadowCloneDie(Ctx c, int index, int hour)
    {
        SpriteRenderer body = c.owner.GetComponentInChildren<SpriteRenderer>();
        Color shade = new Color(0.35f, 0.2f, 0.6f, 0.6f);
        for (int k = -1; k <= 1; k += 2)
        {
            Vector3 off = CamRight * (0.7f * k) + Vector3.forward * 0.25f;
            if (body != null && body.sprite != null)
            {
                GameObject clone = new GameObject("ShadowClone");
                clone.transform.position = body.transform.position + off;
                clone.transform.rotation = body.transform.rotation;
                clone.transform.localScale = body.transform.lossyScale;
                SpriteRenderer sr = clone.AddComponent<SpriteRenderer>();
                sr.sprite = body.sprite;
                sr.flipX = body.flipX;
                sr.color = shade;
                sr.sortingOrder = body.sortingOrder - 1;
                c.host.StartCoroutine(Prop.DriftFade(clone, (c.FoeChest - c.Chest).normalized * 0.9f, 0.45f));
            }
            else FxQuad.Spawn(ImpactFX.GlowTexture(), c.Chest + off, shade, 0.45f, Vector2.one * 1.6f, Vector2.one * 0.6f);
        }
        Sfx.Play(SfxId.Evade, 0.8f);
        yield return new WaitForSeconds(0.12f);
    }

    private static void ShadowHit(Ctx c, int index, Vector3 p)
    {
        Color shade = new Color(0.55f, 0.35f, 0.95f);
        FxQuad.Spawn(ImpactFX.ArcTexture(), p, shade, 0.35f, new Vector2(-2.5f, 2.5f), new Vector2(-4f, 4f), -20f, -200f);
        FxQuad.Spawn(ImpactFX.ArcTexture(), p, shade, 0.35f, new Vector2(2.5f, 2.5f), new Vector2(4f, 4f), 20f, 200f);
    }

    // ═══════════════════════════ 톱니 ═══════════════════════════

    private static IEnumerator GearOrbitStart(Ctx c, int count, Color tint)
    {
        GearOrbit orbit = GearOrbit.Create(c.owner.transform, count, tint);
        c.props.Add(orbit.gameObject);
        Sfx.Play(SfxId.Overwind, 0.6f);
        yield return new WaitForSeconds(0.2f);
    }

    /// <summary>톱니 폭풍 · 지옥: 칸마다 도는 톱니 둘이 상대에게 날아간다</summary>
    private static IEnumerator GearVolleyDie(Ctx c, int index, int hour)
    {
        GearOrbit orbit = FindOrbit(c);
        Color tint = orbit != null ? orbit.tint : Bronze;
        for (int k = 0; k < 2; k++)
        {
            Vector3 from = orbit != null ? orbit.TakeGearPosition() : c.Chest;
            GameObject g = Prop.Spawn(SigArt.Gear(), from, 0.55f, tint, null);
            c.host.StartCoroutine(Prop.Fly(g, from, c.FoeChest + UnityEngine.Random.insideUnitSphere * 0.3f, 0.22f, 900f));
            Sfx.Play(SfxId.Slash, 0.5f);
            yield return new WaitForSeconds(0.07f);
        }
        yield return new WaitForSeconds(0.1f);
    }

    /// <summary>영구 기관: 칸이 지날 때마다 도는 톱니가 하나씩 늘어난다</summary>
    private static IEnumerator PerpetualDie(Ctx c, int index, int hour)
    {
        GearOrbit orbit = FindOrbit(c);
        if (orbit != null) orbit.AddGear();
        FxQuad.Spawn(ImpactFX.RingTexture(), c.Chest, orbit != null ? orbit.tint : Bronze, 0.35f, Vector2.one * 0.5f, Vector2.one * 2.8f);
        Sfx.Play(SfxId.Tock, 0.8f);
        yield return new WaitForSeconds(0.12f);
    }

    private static GearOrbit FindOrbit(Ctx c)
    {
        foreach (GameObject g in c.props) if (g != null && g.GetComponent<GearOrbit>() != null) return g.GetComponent<GearOrbit>();
        return null;
    }

    private static void GearShardHit(Ctx c, int index, Vector3 p)
    {
        GearOrbit orbit = FindOrbit(c);
        Color tint = orbit != null ? orbit.tint : Bronze;
        ClashSpark.Burst(p, tint, 26, 6f);
        FxQuad.Spawn(ImpactFX.RingTexture(), p, tint, 0.35f, Vector2.one * 0.5f, Vector2.one * 3f);
    }

    // ═══════════════════════════ 심장 · 불 ═══════════════════════════

    private static IEnumerator HeartbeatDie(Ctx c, int index, int hour)
    {
        Color red = new Color(1f, 0.25f, 0.3f);
        Vector3 pos = c.Chest + Vector3.up * 0.5f + c.Side * 0.75f;
        GameObject heart = Prop.Spawn(SigArt.Heart(), pos, 0.6f, red, null);
        for (int beat = 0; beat < 2; beat++)
        {
            Sfx.Play(SfxId.Tock, 1f, 0f);
            Sfx.Play(SfxId.HitBlunt, 0.35f, 0f);
            FxQuad.Spawn(ImpactFX.RingTexture(), pos, red, 0.4f, Vector2.one * 0.4f, Vector2.one * (2.2f + beat));
            FxLight.Spawn(pos, red, 2f, 6f, 0.15f);
            yield return c.host.StartCoroutine(Tween(0.16f, t => { if (heart != null) heart.transform.localScale = Vector3.one * 0.6f * (1f + 0.45f * Mathf.Sin(t * Mathf.PI)); }));
            if (BattleCamera.Instance != null) BattleCamera.Instance.Shake(0.05f, 0.1f);
        }
        Prop.FadeOut(heart, 0.25f);
    }

    private static void RedPulseHit(Ctx c, int index, Vector3 p)
    {
        Color red = new Color(1f, 0.25f, 0.3f);
        FxQuad.Spawn(ImpactFX.GlowTexture(), p, red, 0.4f, Vector2.one * 3.5f, Vector2.one);
        FxQuad.Spawn(ImpactFX.RingTexture(), p, red, 0.5f, Vector2.one * 0.5f, Vector2.one * 4.5f);
    }

    private static IEnumerator FlameDie(Ctx c, int index, int hour)
    {
        Color fire = new Color(1f, 0.5f, 0.15f);
        for (int k = 0; k < 3; k++)
        {
            Vector3 p = c.owner.transform.position + Vector3.up * (0.2f + k * 0.5f);
            FxQuad.Spawn(ImpactFX.GlowTexture(), p, fire, 0.45f, Vector2.one * (1.8f - k * 0.3f), Vector2.one * 0.4f);
        }
        ClashSpark.Burst(c.Chest, fire, 24, 4f);
        FxLight.Spawn(c.Chest, fire, 3f, 8f, 0.3f);
        Sfx.Play(SfxId.Overwind, 0.6f);
        yield return new WaitForSeconds(0.15f);
    }

    private static void FireHit(Ctx c, int index, Vector3 p)
    {
        Color fire = new Color(1f, 0.5f, 0.15f);
        FxQuad.Spawn(ImpactFX.RingTexture(), p, fire, 0.5f, Vector2.one * 0.6f, Vector2.one * 4.5f);
        FxQuad.Spawn(ImpactFX.RingTexture(), p - Vector3.up * 0.95f, fire * 0.8f, 0.6f, Vector2.one, Vector2.one * 5f, 0f, 0f, true);
        ClashSpark.Burst(p, fire, 40, 6f);
    }

    // ═══════════════════════════ 도박 ═══════════════════════════

    /// <summary>러시안 태엽: 여섯 구멍 실린더가 돌다 멈추고, 철컥</summary>
    private static IEnumerator RevolverDie(Ctx c, int index, int hour)
    {
        Vector3 pos = c.Chest + Vector3.up * 0.7f + c.Side * 0.95f;
        GameObject cyl = Prop.Spawn(SigArt.Cylinder(), pos, 0.75f, new Color(0.8f, 0.82f, 0.88f), null);
        c.host.StartCoroutine(Prop.PopIn(cyl, 0.1f));
        float spin = UnityEngine.Random.Range(540f, 900f);
        int clicks = 0;
        yield return c.host.StartCoroutine(Tween(0.5f, t =>
        {
            float e = 1f - (1f - t) * (1f - t);
            float roll = -spin * e;
            Prop.SetRoll(cyl, roll);
            int now = Mathf.FloorToInt(-roll / 60f);
            if (now > clicks) { clicks = now; Sfx.Play(SfxId.Tick, 0.7f, 0.08f); }
        }));
        Sfx.Play(SfxId.Guard, 0.9f, 0f);   // 철컥
        DamagePopup.Spawn(pos + CamUp * 0.6f, "철컥", Color.white, 0.9f);
        yield return new WaitForSeconds(0.12f);
        Prop.FadeOut(cyl, 0.2f);
    }

    private static void MuzzleHit(Ctx c, int index, Vector3 p)
    {
        FxQuad.Spawn(ImpactFX.StarTexture(), p, new Color(1f, 0.9f, 0.6f), 0.2f, Vector2.one * 4f, Vector2.one * 0.5f, UnityEngine.Random.Range(0f, 90f));
        FxQuad.Spawn(ImpactFX.StreakTexture(), Vector3.Lerp(c.Chest, p, 0.5f), Color.white, 0.12f, new Vector2(4f, 0.25f), new Vector2(6f, 0.05f));
        FxLight.Spawn(p, new Color(1f, 0.85f, 0.5f), 5f, 12f, 0.12f);
    }

    /// <summary>마지막 판돈: 동전이 포물선을 그리며 상대에게 쏟아진다</summary>
    private static IEnumerator CoinRainDie(Ctx c, int index, int hour)
    {
        for (int k = 0; k < 7; k++)
        {
            Vector3 from = c.Chest + UnityEngine.Random.insideUnitSphere * 0.3f;
            Vector3 to = c.FoeChest + UnityEngine.Random.insideUnitSphere * 0.5f;
            GameObject coin = Prop.Spawn(SigArt.Coin(), from, 0.32f, Gold, null);
            c.host.StartCoroutine(Prop.Arc(coin, from, to, 1.4f, 0.32f, 720f));
            if (k % 2 == 0) Sfx.Play(SfxId.Tick, 0.6f, 0.1f);
            yield return new WaitForSeconds(0.03f);
        }
        yield return new WaitForSeconds(0.15f);
    }

    private static IEnumerator CoinPileStart(Ctx c)
    {
        for (int k = 0; k < 10; k++)
        {
            Vector3 p = c.Chest + UnityEngine.Random.insideUnitSphere * 0.4f;
            GameObject coin = Prop.Spawn(SigArt.Coin(), p, 0.3f, Gold, null);
            c.host.StartCoroutine(Prop.Arc(coin, p, p + UnityEngine.Random.insideUnitSphere * 0.8f + Vector3.up * 0.2f, 1.6f, 0.45f, 900f));
        }
        DamagePopup.Spawn(c.Chest + Vector3.up * 1.3f, "올인!", Gold, 1.3f);
        Sfx.Play(SfxId.Treasure, 0.9f, 0f);
        yield return new WaitForSeconds(0.3f);
    }

    /// <summary>이중 내기 · 행운의 태엽: 머리 위로 동전을 튕겨 올린다</summary>
    private static IEnumerator CoinFlipDie(Ctx c, int index, int hour)
    {
        Vector3 from = c.Chest + Vector3.up * 0.5f + c.Side * 0.7f;
        GameObject coin = Prop.Spawn(SigArt.Coin(), from, 0.4f, Gold, null);
        Sfx.Play(SfxId.Tick, 0.8f, 0f);
        yield return c.host.StartCoroutine(Tween(0.38f, t =>
        {
            if (coin == null) return;
            coin.transform.position = from + Vector3.up * (4f * t * (1f - t) * 1.4f);
            coin.transform.localScale = new Vector3(0.4f * Mathf.Abs(Mathf.Cos(t * Mathf.PI * 6f)) + 0.04f, 0.4f, 1f);   // 빙글빙글 뒤집힌다
        }));
        Sfx.Play(SfxId.Guard, 0.6f, 0f);
        FxQuad.Spawn(ImpactFX.GlowTexture(), from, Gold, 0.25f, Vector2.one * 1.2f, Vector2.one * 0.3f);
        Prop.FadeOut(coin, 0.2f);
    }

    private static void CoinBurstHit(Ctx c, int index, Vector3 p)
    {
        ClashSpark.Burst(p, Gold, 30, 6f);
        FxQuad.Spawn(ImpactFX.RingTexture(), p, Gold, 0.4f, Vector2.one * 0.5f, Vector2.one * 3.4f);
    }

    // ═══════════════════════════ 태엽 · 돌진 ═══════════════════════════

    /// <summary>장력 해방: 꽉 감긴 태엽이 눌렸다가 튕겨 나간다</summary>
    private static IEnumerator SpringStart(Ctx c)
    {
        Vector3 pos = c.Chest + c.Side * 0.9f;
        GameObject spring = Prop.Spawn(SigArt.Spring(), pos, 1.0f, new Color(1f, 0.7f, 0.35f), c, null, true);
        Sfx.Play(SfxId.Overwind, 0.9f);
        yield return c.host.StartCoroutine(Tween(0.3f, t => { if (spring != null) spring.transform.localScale = new Vector3(1.1f, 1.1f * Mathf.Lerp(1f, 0.35f, t), 1f); }));
        FxQuad.Spawn(ImpactFX.RingTexture(), pos, new Color(1f, 0.7f, 0.35f), 0.4f, Vector2.one * 0.4f, Vector2.one * 3f);
        yield return c.host.StartCoroutine(Tween(0.12f, t => { if (spring != null) spring.transform.localScale = new Vector3(1.1f, 1.1f * Mathf.Lerp(0.35f, 1.6f, t), 1f); }));
        Prop.FadeOut(spring, 0.2f);
        c.props.Remove(spring);
    }

    private static IEnumerator DashDie(Ctx c, int index, int hour)
    {
        Color hot = new Color(1f, 0.7f, 0.35f);
        Vector3 dir = (c.FoeChest - c.Chest);
        for (int k = 0; k < 4; k++)
        {
            Vector3 p = c.Chest + dir * (0.15f * k) + CamUp * UnityEngine.Random.Range(-0.5f, 0.5f);
            FxQuad.Spawn(ImpactFX.StreakTexture(), p, hot, 0.2f, new Vector2(2.5f, 0.12f), new Vector2(4f, 0.04f));
        }
        Sfx.Play(SfxId.Thrust, 0.6f);
        yield return new WaitForSeconds(0.08f);
    }

    // ═══════════════════════════ 소품 ═══════════════════════════

    /// <summary>카메라를 바라보는 스프라이트 소품</summary>
    private static class Prop
    {
        public const int BehindOrder = -6, FrontOrder = 24;

        /// <summary>
        /// behind = 캐릭터 뒤 (종 고리 · 시계판 · 달처럼 배경 노릇을 하는 소품) / 아니면 캐릭터 앞.
        /// 무대 벽 · 바닥에 파묻히지 않게 깊이 검사를 끈 머티리얼을 쓰고, 밝은 배경에서도 보이게 어두운 테두리를 깐다.
        /// </summary>
        public static GameObject Spawn(Texture2D tex, Vector3 pos, float size, Color color, Ctx owner, Vector2? pivot = null, bool behind = false)
        {
            GameObject go = new GameObject("SigProp " + tex.name);
            go.transform.position = pos;
            Sprite sprite = SigArt.SpriteOf(tex, pivot ?? new Vector2(0.5f, 0.5f));
            int order = behind ? BehindOrder : FrontOrder;

            GameObject shade = new GameObject("Shade");
            shade.transform.SetParent(go.transform, false);
            shade.transform.localScale = Vector3.one * 1.14f;
            SpriteRenderer sh = shade.AddComponent<SpriteRenderer>();
            sh.sprite = sprite;
            sh.sharedMaterial = SigArt.PropMaterial();
            sh.color = new Color(0f, 0f, 0f, 0.5f * color.a);
            sh.sortingOrder = order - 1;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = SigArt.PropMaterial();
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<Billboard>();
            UnityEngine.Object.Destroy(go, 8f);   // 혹시 치우지 못해도 스스로 사라진다
            if (owner != null) owner.props.Add(go);
            return go;
        }

        public static void SetRoll(GameObject go, float roll)
        {
            if (go == null) return;
            Billboard b = go.GetComponent<Billboard>();
            if (b != null) b.roll = roll;
        }

        public static IEnumerator PopIn(GameObject go, float time)
        {
            if (go == null) yield break;
            Vector3 full = go.transform.localScale;
            yield return Tween(time, t => { if (go != null) go.transform.localScale = full * Pop(t); });
        }

        public static IEnumerator Swing(GameObject go, float angle, float time)
        {
            yield return Tween(time, t => SetRoll(go, angle * Mathf.Sin(t * Mathf.PI * 4f) * (1f - t)));
        }

        /// <summary>앞/뒤 층 바꾸기 (테두리도 함께)</summary>
        public static void SetOrder(GameObject go, int order)
        {
            if (go == null) return;
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = order;
            Transform shade = go.transform.Find("Shade");
            if (shade != null) shade.GetComponent<SpriteRenderer>().sortingOrder = order - 1;
        }

        public static IEnumerator Flash(GameObject go, Color flash, float time)
        {
            SpriteRenderer sr = go != null ? go.GetComponent<SpriteRenderer>() : null;
            if (sr == null) yield break;
            Color baseColor = sr.color;
            yield return Tween(time, t => { if (sr != null) sr.color = Color.Lerp(flash, baseColor, t); });
        }

        public static IEnumerator Fly(GameObject go, Vector3 from, Vector3 to, float time, float spin)
        {
            float roll = 0f;
            yield return Tween(time, t =>
            {
                if (go == null) return;
                go.transform.position = Vector3.Lerp(from, to, t * t);
                roll -= spin * Time.deltaTime;
                SetRoll(go, roll);
            });
            if (go != null)
            {
                ClashSpark.Burst(to, go.GetComponent<SpriteRenderer>().color, 8, 3f);
                UnityEngine.Object.Destroy(go);
            }
        }

        public static IEnumerator Arc(GameObject go, Vector3 from, Vector3 to, float height, float time, float spin)
        {
            float roll = 0f;
            yield return Tween(time, t =>
            {
                if (go == null) return;
                go.transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (4f * height * t * (1f - t));
                roll -= spin * Time.deltaTime;
                SetRoll(go, roll);
            });
            if (go != null) FadeOut(go, 0.15f);
        }

        public static IEnumerator DriftFade(GameObject go, Vector3 drift, float time)
        {
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            Vector3 start = go.transform.position;
            Color c0 = sr.color;
            yield return Tween(time, t =>
            {
                if (go == null) return;
                go.transform.position = start + drift * t;
                sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - t));
            });
            if (go != null) UnityEngine.Object.Destroy(go);
        }

        public static void FadeOut(GameObject go, float time)
        {
            if (go == null) return;
            Fader f = go.GetComponent<Fader>();
            if (f == null) f = go.AddComponent<Fader>();
            f.Begin(time);
        }
    }

    /// <summary>카메라를 바라보며 화면 안에서 roll만큼 돈다</summary>
    private class Billboard : MonoBehaviour
    {
        public float roll;
        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, roll);
        }
    }

    private class Fader : MonoBehaviour
    {
        private float time, age;
        private SpriteRenderer[] srs;
        private Color[] starts;
        public void Begin(float t)
        {
            srs = GetComponentsInChildren<SpriteRenderer>();
            starts = new Color[srs.Length];
            for (int i = 0; i < srs.Length; i++) starts[i] = srs[i].color;
            time = Mathf.Max(0.01f, t);
            age = 0f;
        }
        void Update()
        {
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / time);
            for (int i = 0; i < srs.Length; i++)
                if (srs[i] != null) srs[i].color = new Color(starts[i].r, starts[i].g, starts[i].b, starts[i].a * (1f - k));
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>주인 둘레를 도는 톱니 고리 (톱니 폭풍 · 지옥 · 영구 기관)</summary>
    private class GearOrbit : MonoBehaviour
    {
        public Color tint;
        private Transform center;
        private readonly List<Transform> gears = new List<Transform>();
        private float angle;

        public static GearOrbit Create(Transform center, int count, Color tint)
        {
            GameObject go = new GameObject("GearOrbit");
            go.transform.position = center.position;
            GearOrbit o = go.AddComponent<GearOrbit>();
            o.center = center;
            o.tint = tint;
            for (int i = 0; i < count; i++) o.AddGear();
            Destroy(go, 8f);
            return o;
        }

        public void AddGear()
        {
            GameObject g = Prop.Spawn(SigArt.Gear(), transform.position, 0.36f, tint, null);
            g.transform.SetParent(transform, true);
            gears.Add(g.transform);
            StartCoroutine(Prop.PopIn(g, 0.15f));
        }

        /// <summary>톱니 하나를 고리에서 떼어 그 자리를 돌려준다 (날려 보낼 출발점)</summary>
        public Vector3 TakeGearPosition()
        {
            gears.RemoveAll(g => g == null);
            if (gears.Count == 0) return center != null ? center.position + Vector3.up : transform.position;
            Transform g = gears[gears.Count - 1];
            gears.RemoveAt(gears.Count - 1);
            Vector3 p = g.position;
            Destroy(g.gameObject);
            return p;
        }

        void LateUpdate()
        {
            if (center == null) { Destroy(gameObject); return; }
            transform.position = center.position + Vector3.up * 1.0f;
            angle += Time.deltaTime * 140f;
            gears.RemoveAll(g => g == null);
            for (int i = 0; i < gears.Count; i++)
            {
                float a = (angle + 360f * i / Mathf.Max(1, gears.Count)) * Mathf.Deg2Rad;
                gears[i].position = transform.position + CamRight * Mathf.Cos(a) * 1.15f + CamUp * Mathf.Sin(a) * 0.45f + Vector3.forward * Mathf.Sin(a) * 0.3f;
                SetRollOf(gears[i], -angle * 3f + i * 20f);
                Prop.SetOrder(gears[i].gameObject, Mathf.Sin(a) > 0f ? Prop.BehindOrder : Prop.FrontOrder);
            }
        }

        private static void SetRollOf(Transform t, float roll)
        {
            Billboard b = t.GetComponent<Billboard>();
            if (b != null) b.roll = roll;
        }
    }

    /// <summary>화면 전체를 잠깐 물들이는 막 (멈춰 선 시간)</summary>
    private static class Overlay
    {
        private static Image image;
        private static OverlayRunner runner;

        public static void Show(Color color, float time)
        {
            if (image == null)
            {
                GameObject go = new GameObject("SigOverlay");
                Canvas canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 95;   // 무대 위, 전투 UI(100~) 아래
                RectTransform rt = UIBuilder.Rect("Tint", go.transform);
                UIBuilder.Stretch(rt);
                image = rt.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                runner = go.AddComponent<OverlayRunner>();
            }
            runner.Play(image, color, time);
        }
    }

    private class OverlayRunner : MonoBehaviour
    {
        private Image image;
        private Color color;
        private float time, age = 999f;
        public void Play(Image img, Color c, float t) { image = img; color = c; time = t; age = 0f; }
        void Update()
        {
            if (image == null) return;
            age += Time.unscaledDeltaTime;
            float k = age < 0.15f ? age / 0.15f : Mathf.Clamp01((time - age) / 0.4f);
            image.color = new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(k));
        }
    }
}

/// <summary>고유 연출 소품의 그림 (코드로 그린 텍스처 — 한 번 그리면 재사용)</summary>
public static class SigArt
{
    private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<(Texture2D, Vector2), Sprite> sprites = new Dictionary<(Texture2D, Vector2), Sprite>();

    private static Material propMaterial;

    /// <summary>깊이 검사를 끈 소품 머티리얼 (Resources/SigProp.shader — 없으면 기본 스프라이트)</summary>
    public static Material PropMaterial()
    {
        if (propMaterial != null) return propMaterial;
        Shader shader = Shader.Find("Hidden/ClockworkRoll/SigProp");
        if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
        propMaterial = new Material(shader) { name = "SigProp" };
        return propMaterial;
    }

    public static Sprite SpriteOf(Texture2D tex, Vector2 pivot)
    {
        if (sprites.TryGetValue((tex, pivot), out Sprite s) && s != null) return s;
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, tex.width);   // 1 유닛 = 텍스처 한 장
        sprites[(tex, pivot)] = s;
        return s;
    }

    /// <summary>u, v = -1~1 (가운데 0, 위 +). 색을 돌려주면 그 색, null이면 투명</summary>
    private static Texture2D Paint(string name, int size, Func<float, float, Color?> f)
    {
        if (cache.TryGetValue(name, out Texture2D t) && t != null) return t;
        t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.name = name;
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                Color? c = f(u, v);
                px[y * size + x] = c ?? new Color(0f, 0f, 0f, 0f);
            }
        t.SetPixels(px);
        t.Apply();
        cache[name] = t;
        return t;
    }

    private static float Edge(float d, float soft = 0.03f) { return Mathf.Clamp01(0.5f - d / soft); }   // d < 0 이면 안

    public static Texture2D Bell()
    {
        return Paint("SigBell", 128, (u, v) =>
        {
            // 종 몸통: 위는 좁고 아래로 벌어진다 (v: -0.75 ~ 0.6)
            float alpha = 0f;
            Color col = new Color(1f, 0.82f, 0.38f);
            if (v > -0.62f && v < 0.55f)
            {
                float t = (0.55f - v) / 1.17f;                 // 0 = 꼭대기, 1 = 아래 끝
                float half = 0.3f + 0.42f * t * t;
                alpha = Edge(Mathf.Abs(u) - half);
                float shade = 0.75f + 0.35f * (1f - Mathf.Abs(u) / Mathf.Max(0.01f, half)) - 0.25f * Mathf.Max(0f, u / half);
                col *= shade;
            }
            // 입술 (아래 테)
            if (v > -0.75f && v <= -0.58f)
            {
                float a = Edge(Mathf.Abs(u) - 0.8f);
                if (a > alpha) { alpha = a; col = new Color(0.85f, 0.62f, 0.25f); }
            }
            // 꼭지 고리
            float knob = Mathf.Sqrt(u * u + (v - 0.68f) * (v - 0.68f));
            if (knob < 0.16f) { float a = Edge(Mathf.Abs(knob - 0.11f) - 0.05f); if (a > alpha) { alpha = a; col = new Color(0.8f, 0.6f, 0.25f); } }
            // 추
            float clap = Mathf.Sqrt(u * u + (v + 0.82f) * (v + 0.82f));
            if (clap < 0.14f) { float a = Edge(clap - 0.12f); if (a > alpha) { alpha = a; col = new Color(0.55f, 0.4f, 0.2f); } }
            // 띠 장식
            if (alpha > 0f && Mathf.Abs(v + 0.35f) < 0.03f) col *= 0.7f;
            if (alpha <= 0f) return null;
            return new Color(Mathf.Clamp01(col.r), Mathf.Clamp01(col.g), Mathf.Clamp01(col.b), alpha);
        });
    }

    public static Texture2D Gear()
    {
        return Paint("SigGear", 128, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
            float tooth = Mathf.Clamp01(Mathf.Cos(a * 10f) * 3f);
            float outer = 0.7f + 0.2f * tooth;
            float alpha = Edge(r - outer) * (1f - Edge(r - 0.24f));
            if (alpha <= 0f) return null;
            float shade = 0.8f + 0.2f * (v * 0.5f + 0.5f);
            if (Mathf.Abs(r - 0.5f) < 0.04f) shade *= 0.7f;
            return new Color(shade, shade, shade, alpha);
        });
    }

    public static Texture2D Coin()
    {
        return Paint("SigCoin", 64, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float alpha = Edge(r - 0.9f, 0.06f);
            if (alpha <= 0f) return null;
            float shade = r > 0.72f ? 0.75f : 0.95f + 0.1f * (u - v);
            if (Mathf.Abs(u) < 0.08f && Mathf.Abs(v) < 0.45f) shade = 0.7f;   // 가운데 새김
            return new Color(shade, shade, shade, alpha);
        });
    }

    public static Texture2D ClockFace()
    {
        return Paint("SigClock", 256, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float a = 0f;
            a = Mathf.Max(a, Edge(Mathf.Abs(r - 0.9f) - 0.035f, 0.02f));              // 테
            a = Mathf.Max(a, 0.12f * Edge(r - 0.88f, 0.02f));                          // 은은한 판
            float ang = Mathf.Atan2(u, v) * Mathf.Rad2Deg;                             // 12시 = 0
            float step = Mathf.Repeat(ang + 15f, 30f) - 15f;
            if (r > 0.72f && r < 0.84f) a = Mathf.Max(a, Edge(Mathf.Abs(step) - 1.6f, 1f));   // 눈금 12개
            a = Mathf.Max(a, Edge(r - 0.05f, 0.02f));                                   // 가운데 축
            if (a <= 0f) return null;
            return new Color(1f, 1f, 1f, a);
        });
    }

    public static Texture2D Hand()
    {
        return Paint("SigHand", 128, (u, v) =>
        {
            // 아래 끝(v=-0.88)이 축, 위로 뻗는다 — 스프라이트 피벗을 아래쪽으로 잡는다
            float t = (v + 0.88f) / 1.76f;
            if (t < 0f || t > 1f) return null;
            float half = Mathf.Lerp(0.07f, 0.015f, t);
            float a = Edge(Mathf.Abs(u) - half, 0.02f);
            return a > 0f ? new Color(1f, 1f, 1f, a) : (Color?)null;
        });
    }

    public static Texture2D Heart()
    {
        return Paint("SigHeart", 128, (u, v) =>
        {
            float x = u * 1.25f, y = v * 1.25f + 0.25f;
            float f = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
            if (f > 0f) return null;
            float shade = 0.8f + 0.25f * (v - u) * 0.5f;
            return new Color(shade, shade, shade, Mathf.Clamp01(-f * 40f));
        });
    }

    public static Texture2D Cylinder()
    {
        return Paint("SigCylinder", 128, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float alpha = Edge(r - 0.9f, 0.04f);
            if (alpha <= 0f) return null;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + Mathf.PI / 2f;
                float d = Mathf.Sqrt((u - Mathf.Cos(a) * 0.52f) * (u - Mathf.Cos(a) * 0.52f) + (v - Mathf.Sin(a) * 0.52f) * (v - Mathf.Sin(a) * 0.52f));
                if (d < 0.17f) return new Color(0.12f, 0.12f, 0.14f, alpha);   // 구멍
            }
            if (r < 0.1f) return new Color(0.4f, 0.4f, 0.45f, alpha);
            float shade = 0.7f + 0.3f * (v * 0.5f + 0.5f);
            return new Color(shade, shade, shade, alpha);
        });
    }

    public static Texture2D Crescent()
    {
        return Paint("SigCrescent", 128, (u, v) =>
        {
            float outer = Mathf.Sqrt(u * u + v * v) - 0.85f;
            float inner = Mathf.Sqrt((u - 0.36f) * (u - 0.36f) + (v - 0.18f) * (v - 0.18f)) - 0.74f;
            float a = Edge(outer, 0.04f) * (1f - Edge(inner, 0.04f));
            return a > 0f ? new Color(1f, 1f, 1f, a) : (Color?)null;
        });
    }

    public static Texture2D Spring()
    {
        return Paint("SigSpring", 128, (u, v) =>
        {
            // 지그재그 코일 (세로)
            if (Mathf.Abs(v) > 0.9f) return null;
            float wave = Mathf.Abs(Mathf.Repeat((v + 0.9f) * 3.5f, 2f) - 1f) * 2f - 1f;   // -1~1 삼각파
            float x = wave * 0.55f;
            float a = Edge(Mathf.Abs(u - x) - 0.07f, 0.04f);
            return a > 0f ? new Color(1f, 1f, 1f, a) : (Color?)null;
        });
    }
}
