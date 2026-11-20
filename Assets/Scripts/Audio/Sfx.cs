using System.Collections.Generic;
using UnityEngine;

/// <summary>효과음 종류 — 새 소리는 여기에 추가하고 Sfx.Build에 그리는 법을 넣는다.</summary>
public enum SfxId
{
    // 화면
    Click, Hover, CardHover, CardPick, CardPlace, CardReturn, Type, MapStep, Treasure,
    // 시계
    ClockStart, Tick, Tock, Chime,
    // 합
    DiceRoll, Clash, Slash, Thrust, Blunt, Guard, Evade, Tie,
    HitSlash, HitThrust, HitBlunt, HitBig,
    // 효과
    Heal, Buff, Debuff, Rust, Overwind, Break, Corrupt, Death,
    // 결과
    Victory, Defeat
}

/// <summary>
/// 효과음. 에셋 없이 전부 코드로 그린다 (Wave) — 태엽 · 쇠 · 종의 소리.
///   Sfx.Play(SfxId.Tick);            어디서든 한 줄로
///   Sfx.Volume = 0.8f;               효과음 크기 (저장됨)
/// 소리는 처음 쓸 때 만들어 두고 다시 쓴다. 재생기는 AudioDirector(자동 생성)가 갖고 있다.
/// </summary>
public static class Sfx
{
    private const string VolumeKey = "audio.sfx";
    private static readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
    private static readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
    private static float volume = -1f;

    /// <summary>효과음 크기 0~1 (PlayerPrefs에 저장)</summary>
    public static float Volume
    {
        get
        {
            if (volume < 0f) volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
            return volume;
        }
        set
        {
            volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, volume);
        }
    }

    /// <summary>효과음 한 번. gain = 이 소리만의 크기, jitter = 매번 조금씩 다른 음높이 (같은 소리가 기계처럼 반복되지 않게)</summary>
    public static void Play(SfxId id, float gain = 1f, float jitter = 0.04f)
    {
        if (!Application.isPlaying || Volume <= 0.001f || gain <= 0f) return;

        // 같은 소리가 한순간에 겹치면 (효과 여러 개가 동시에 발동) 시끄럽기만 하다 → 아주 짧은 간격은 한 번만
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < 0.035f) return;
        lastPlayed[id] = now;

        AudioSource source = AudioDirector.NextSfxSource();
        if (source == null) return;

        source.pitch = 1f + Random.Range(-jitter, jitter);
        source.PlayOneShot(Get(id), Mathf.Clamp01(gain) * Volume);
    }

    /// <summary>주사위가 부딪히는 순간: 둘 다 있으면 쇠가 맞부딪히고, 한쪽뿐이면 그 종류를 휘두르는 소리</summary>
    public static void PlayClash(DiceData a, DiceData b)
    {
        if (a != null && b != null) { Play(SfxId.Clash); return; }
        DiceData d = a != null ? a : b;
        if (d == null) return;
        switch (d.diceDetail)
        {
            case DiceDetail.Slash:  Play(SfxId.Slash); break;
            case DiceDetail.Thrust: Play(SfxId.Thrust); break;
            case DiceDetail.Blunt:  Play(SfxId.Blunt); break;
            case DiceDetail.Evade:  Play(SfxId.Evade, 0.6f); break;
            default:                Play(SfxId.Guard, 0.5f); break;
        }
    }

    /// <summary>공격이 들어간 순간: 종류에 따라 베임/찔림/둔탁함, 큰 피해면 묵직한 한 방이 겹친다</summary>
    public static void PlayHit(DiceDetail detail, bool big)
    {
        Play(detail == DiceDetail.Slash ? SfxId.HitSlash : detail == DiceDetail.Thrust ? SfxId.HitThrust : SfxId.HitBlunt);
        if (big) Play(SfxId.HitBig);
    }

    public static AudioClip Get(SfxId id)
    {
        if (!clips.TryGetValue(id, out AudioClip clip) || clip == null)
        {
            clip = Build(id).ToClip("Sfx " + id);
            clips[id] = clip;
        }
        return clip;
    }

    /// <summary>모든 효과음의 파형을 만든다 (순수 계산 — 다른 스레드에서 불러도 된다)</summary>
    public static Dictionary<SfxId, Wave> BuildAll()
    {
        Dictionary<SfxId, Wave> all = new Dictionary<SfxId, Wave>();
        foreach (SfxId id in System.Enum.GetValues(typeof(SfxId))) all[id] = Build(id);
        return all;
    }

    /// <summary>뒤에서 만들어 둔 파형을 받아 재생할 수 있게 올린다 (메인 스레드에서)</summary>
    public static void Adopt(Dictionary<SfxId, Wave> waves)
    {
        foreach (KeyValuePair<SfxId, Wave> pair in waves)
        {
            if (!clips.ContainsKey(pair.Key)) clips[pair.Key] = pair.Value.ToClip("Sfx " + pair.Key);
        }
    }

    // ─────────────────────────── 소리 그리기 ───────────────────────────

    public static Wave Build(SfxId id)
    {
        int seed = 1000 + (int)id * 37;
        Wave w;
        switch (id)
        {
            // ── 화면 ──
            case SfxId.Click:   // 작은 쇠단추를 누르는 '딸깍'
                w = new Wave(0.09f, seed);
                w.Click(0f, 1900f, 0.7f);
                w.Tone(0.004f, 0.07f, 620f, 480f, 0.45f, 0.02f);
                return w.Normalize(0.5f).Trim();

            case SfxId.Hover:
                w = new Wave(0.05f, seed);
                w.Tone(0f, 0.045f, 1500f, 1650f, 0.5f, 0.012f);
                return w.Normalize(0.16f).Trim();

            case SfxId.CardHover:   // 얇은 쇠판을 스치는 소리
                w = new Wave(0.07f, seed);
                w.Noise(0f, 0.06f, 3200f, 5200f, 1.5f, 0.6f, 0.02f, 0.008f);
                return w.Normalize(0.13f).Trim();

            case SfxId.CardPick:   // 태엽판을 집어 든다: 스윽 + 딱
                w = new Wave(0.16f, seed);
                w.Noise(0f, 0.11f, 1400f, 4200f, 1.2f, 0.7f, 0.05f, 0.01f);
                w.Click(0.085f, 2600f, 0.5f);
                return w.Normalize(0.42f).Trim();

            case SfxId.CardPlace:   // 시계판에 끼운다: 철컥 (톱니가 물리는 두 번의 딱 + 낮은 울림)
                w = new Wave(0.26f, seed);
                w.Click(0f, 1700f, 0.8f);
                w.Click(0.045f, 2300f, 0.7f);
                w.Tone(0.045f, 0.18f, 190f, 120f, 0.9f, 0.05f);
                w.Bell(0.05f, 1240f, 0.18f, 0.06f);
                return w.Normalize(0.62f).Trim();

            case SfxId.CardReturn:   // 빼낸다: 딱 + 스윽 (내려가는)
                w = new Wave(0.17f, seed);
                w.Click(0f, 2000f, 0.5f);
                w.Noise(0.01f, 0.13f, 4000f, 1300f, 1.2f, 0.65f, 0.05f, 0.01f);
                return w.Normalize(0.36f).Trim();

            case SfxId.Type:   // 대사 한 글자: 타자기보다 부드러운 '톡'
                w = new Wave(0.035f, seed);
                w.Tone(0f, 0.03f, 880f, 760f, 0.6f, 0.009f, 0.001f, Wave.Triangle);
                w.Noise(0f, 0.008f, 3000f, 3000f, 2f, 0.3f, 0.003f, 0.0005f);
                return w.Normalize(0.2f).Trim(0.004f);

            case SfxId.MapStep:   // 지도에서 한 칸: 젖은 길을 딛는 발소리 + 표식에 불이 들어오는 작은 종
                w = new Wave(0.34f, seed);
                w.Tone(0f, 0.09f, 150f, 80f, 0.9f, 0.03f);
                w.Noise(0f, 0.08f, 900f, 500f, 0.9f, 0.5f, 0.025f, 0.001f, Wave.LowPass);
                w.Bell(0.06f, 1320f, 0.25f, 0.09f);
                return w.Normalize(0.5f).Trim();

            case SfxId.Treasure:   // 부품을 얻었다: 반짝이며 올라가는 오르골
                w = new Wave(1.3f, seed);
                int[] up = { 74, 78, 81, 86, 90 };
                for (int i = 0; i < up.Length; i++) w.MusicBox(i * 0.075f, Wave.Note(up[i]), 0.5f);
                w.Bell(0.38f, Wave.Note(98), 0.12f, 0.25f);
                return w.Normalize(0.5f).Trim(0.05f);

            // ── 시계 ──
            case SfxId.ClockStart:   // 태엽을 감았다 놓는다: 빨라지는 톱니 소리 + 풀리는 울림
                w = new Wave(0.62f, seed);
                float t = 0f;
                for (int i = 0; i < 9; i++)
                {
                    w.Click(t, 1500f + i * 130f, 0.35f + i * 0.04f);
                    t += 0.06f - i * 0.004f;
                }
                w.Tone(t, 0.22f, 240f, 150f, 0.6f, 0.07f);
                w.Bell(t, 980f, 0.3f, 0.1f);
                return w.Normalize(0.55f).Trim();

            case SfxId.Tick:   // 빈 시각을 지나는 초침
                w = new Wave(0.06f, seed);
                w.Click(0f, 2500f, 0.8f);
                w.Tone(0.001f, 0.04f, 3100f, 3000f, 0.25f, 0.01f);
                return w.Normalize(0.34f).Trim();

            case SfxId.Tock:   // 행동이 있는 시각: 더 낮고 묵직하게
                w = new Wave(0.12f, seed);
                w.Click(0f, 1300f, 0.9f);
                w.Tone(0f, 0.1f, 330f, 250f, 0.7f, 0.03f);
                return w.Normalize(0.5f).Trim();

            case SfxId.Chime:   // 정각(3 · 6 · 9 · 12시): 작은 종 한 번
                w = new Wave(1.5f, seed);
                w.Bell(0f, Wave.Note(81), 0.6f, 0.38f);
                w.Tone(0f, 1.3f, Wave.Note(69), Wave.Note(69), 0.12f, 0.4f);
                return w.Normalize(0.42f).Trim(0.08f);

            // ── 합 ──
            case SfxId.DiceRoll:   // 칸이 굴러간다: 톱니가 드르륵 돌다가 느려진다
                w = new Wave(0.5f, seed);
                t = 0f;
                for (int i = 0; i < 16 && t < 0.45f; i++)
                {
                    w.Click(t, w.Rand(1700f, 2900f), 0.5f - i * 0.012f);
                    t += 0.016f + i * 0.0024f;
                }
                w.Noise(0f, 0.42f, 900f, 600f, 1.2f, 0.12f, 0.2f, 0.01f);
                return w.Normalize(0.38f).Trim();

            case SfxId.Clash:   // 칸과 칸이 맞부딪힌다: 쇠와 쇠
                w = new Wave(0.55f, seed);
                w.Noise(0f, 0.03f, 4200f, 2500f, 0.9f, 1f, 0.008f, 0.0005f, Wave.HighPass);
                w.Bell(0f, 1480f, 0.55f, 0.11f);
                w.Bell(0.004f, 2090f, 0.4f, 0.08f);
                w.Tone(0f, 0.08f, 210f, 130f, 0.6f, 0.025f);
                return w.Normalize(0.72f).Trim();

            case SfxId.Slash:   // 휘두른다: 넓게 가르는 바람 + 날의 울림
                w = new Wave(0.3f, seed);
                w.Noise(0f, 0.22f, 5200f, 900f, 1.6f, 0.9f, 0.07f, 0.012f);
                w.Bell(0.03f, 2600f, 0.12f, 0.05f);
                return w.Normalize(0.5f).Trim();

            case SfxId.Thrust:   // 찌른다: 짧고 날카로운 바람
                w = new Wave(0.18f, seed);
                w.Noise(0f, 0.11f, 2500f, 6500f, 2.2f, 0.9f, 0.035f, 0.004f);
                w.Click(0.07f, 3300f, 0.4f);
                return w.Normalize(0.46f).Trim();

            case SfxId.Blunt:   // 내려친다: 묵직한 바람
                w = new Wave(0.3f, seed);
                w.Noise(0f, 0.22f, 1200f, 320f, 0.9f, 1f, 0.08f, 0.02f, Wave.LowPass);
                w.Tone(0.05f, 0.14f, 130f, 70f, 0.4f, 0.05f);
                return w.Normalize(0.52f).Trim();

            case SfxId.Guard:   // 막았다: 방패에 튕기는 맑은 쇳소리
                w = new Wave(0.6f, seed);
                w.Noise(0f, 0.02f, 3000f, 2000f, 1f, 0.8f, 0.006f, 0.0005f);
                w.Bell(0f, 760f, 0.7f, 0.16f);
                w.Bell(0.002f, 1140f, 0.3f, 0.1f);
                return w.Normalize(0.62f).Trim();

            case SfxId.Evade:   // 피했다: 스쳐 지나가는 바람 (올라갔다 사라진다)
                w = new Wave(0.32f, seed);
                w.Noise(0f, 0.28f, 700f, 3600f, 1.8f, 0.9f, 0.12f, 0.06f);
                return w.Normalize(0.42f).Trim();

            case SfxId.Tie:   // 동점: 두 쇠가 서로 튕겨 나간다 (어긋난 두 울림)
                w = new Wave(0.7f, seed);
                w.Noise(0f, 0.03f, 3800f, 2200f, 0.9f, 1f, 0.008f, 0.0005f, Wave.HighPass);
                w.Bell(0f, 1180f, 0.55f, 0.17f);
                w.Bell(0.03f, 1250f, 0.5f, 0.17f);
                return w.Normalize(0.66f).Trim();

            case SfxId.HitSlash:   // 베였다
                w = new Wave(0.26f, seed);
                w.Noise(0f, 0.09f, 6000f, 1500f, 1.4f, 1f, 0.03f, 0.001f);
                w.Tone(0f, 0.16f, 170f, 80f, 0.8f, 0.045f);
                w.Noise(0.01f, 0.12f, 700f, 300f, 0.8f, 0.5f, 0.04f, 0.001f, Wave.LowPass);
                return w.Normalize(0.7f).Trim();

            case SfxId.HitThrust:   // 찔렸다
                w = new Wave(0.22f, seed);
                w.Click(0f, 3600f, 0.9f);
                w.Noise(0f, 0.05f, 4500f, 2500f, 2f, 0.7f, 0.015f, 0.0005f);
                w.Tone(0f, 0.14f, 210f, 110f, 0.8f, 0.035f);
                return w.Normalize(0.68f).Trim();

            case SfxId.HitBlunt:   // 맞았다 (둔기)
                w = new Wave(0.3f, seed);
                w.Tone(0f, 0.22f, 150f, 55f, 1f, 0.06f);
                w.Noise(0f, 0.1f, 1100f, 350f, 0.8f, 0.8f, 0.03f, 0.001f, Wave.LowPass);
                return w.Normalize(0.76f).Trim();

            case SfxId.HitBig:   // 큰 피해: 깊은 한 방 + 금 가는 쇳소리
                w = new Wave(0.6f, seed);
                w.Tone(0f, 0.45f, 120f, 38f, 1f, 0.13f);
                w.Noise(0f, 0.2f, 900f, 180f, 0.7f, 0.8f, 0.06f, 0.001f, Wave.LowPass);
                w.Noise(0f, 0.04f, 5000f, 3000f, 0.9f, 0.5f, 0.012f, 0.0005f, Wave.HighPass);
                w.Bell(0.01f, 620f, 0.2f, 0.12f);
                return w.Normalize(0.85f).Trim();

            // ── 효과 ──
            case SfxId.Heal:   // 회복: 부드럽게 올라가는 세 음
                w = new Wave(0.9f, seed);
                w.MusicBox(0f, Wave.Note(74), 0.5f);
                w.MusicBox(0.09f, Wave.Note(78), 0.5f);
                w.MusicBox(0.18f, Wave.Note(81), 0.55f);
                return w.Normalize(0.4f).Trim(0.05f);

            case SfxId.Buff:   // 힘 · 인내 · 철갑: 조여지는 올라가는 두 음
                w = new Wave(0.34f, seed);
                w.Tone(0f, 0.14f, Wave.Note(69), Wave.Note(69), 0.6f, 0.06f, 0.003f, Wave.Triangle);
                w.Tone(0.1f, 0.22f, Wave.Note(76), Wave.Note(76), 0.7f, 0.08f, 0.003f, Wave.Triangle);
                w.Click(0.1f, 2400f, 0.2f);
                return w.Normalize(0.36f).Trim();

            case SfxId.Debuff:   // 허약 · 취약 · 마비: 풀려 내려가는 두 음 (살짝 어긋나게)
                w = new Wave(0.38f, seed);
                w.Tone(0f, 0.15f, Wave.Note(70), Wave.Note(69), 0.6f, 0.07f, 0.003f, Wave.Triangle);
                w.Tone(0.11f, 0.25f, Wave.Note(64), Wave.Note(62), 0.7f, 0.09f, 0.003f, Wave.Triangle);
                w.Tone(0.11f, 0.25f, Wave.Note(64) * 1.03f, Wave.Note(62) * 1.03f, 0.3f, 0.09f, 0.003f, Wave.Triangle);
                return w.Normalize(0.36f).Trim();

            case SfxId.Rust:   // 녹: 쇠가 긁히며 삭는 소리
                w = new Wave(0.34f, seed);
                for (int i = 0; i < 14; i++)
                    w.Noise(w.Rand(0f, 0.26f), w.Rand(0.012f, 0.035f), w.Rand(1400f, 3800f), w.Rand(900f, 2500f), 3f, w.Rand(0.3f, 0.8f), 0.012f, 0.001f);
                w.Noise(0f, 0.3f, 2200f, 1100f, 1f, 0.2f, 0.12f, 0.02f);
                return w.Normalize(0.36f).Trim();

            case SfxId.Overwind:   // 과감기: 태엽을 과하게 감는다 — 빨라지며 올라가는 톱니 + 삐걱
                w = new Wave(0.5f, seed);
                t = 0f;
                for (int i = 0; i < 12; i++)
                {
                    w.Click(t, 1300f + i * 190f, 0.4f + i * 0.04f);
                    t += 0.045f - i * 0.0025f;
                }
                w.Tone(0f, 0.42f, 180f, 520f, 0.35f, 0f, 0.05f, Wave.Saw, 0.05f);
                return w.Normalize(0.5f).Trim();

            case SfxId.Break:   // 칸 파괴: 톱니가 깨져 흩어진다
                w = new Wave(0.5f, seed);
                w.Noise(0f, 0.05f, 5000f, 2500f, 0.8f, 1f, 0.015f, 0.0005f, Wave.HighPass);
                for (int i = 0; i < 6; i++) w.Bell(0.02f + i * w.Rand(0.03f, 0.06f), w.Rand(1800f, 3600f), 0.3f, 0.05f);
                w.Tone(0f, 0.1f, 260f, 120f, 0.5f, 0.03f);
                return w.Normalize(0.6f).Trim();

            case SfxId.Corrupt:   // 침식: 삼킨 기억이 샌다 — 낮고 어긋난 울음
                w = new Wave(0.75f, seed);
                w.Tone(0f, 0.7f, 98f, 82f, 0.6f, 0.3f, 0.03f, Wave.Saw, 0.1f);
                w.Tone(0f, 0.7f, 104f, 85f, 0.5f, 0.3f, 0.03f, Wave.Saw, 0.1f);
                w.Tone(0f, 0.6f, 147f, 139f, 0.3f, 0.25f, 0.03f, Wave.Square, 0.1f);
                w.Noise(0f, 0.5f, 500f, 250f, 1.5f, 0.3f, 0.2f, 0.02f);
                return w.Normalize(0.5f).Trim(0.05f);

            case SfxId.Death:   // 쓰러졌다: 무너지는 몸 + 멈추는 태엽
                w = new Wave(0.8f, seed);
                w.Tone(0f, 0.3f, 140f, 45f, 1f, 0.1f);
                w.Noise(0f, 0.18f, 700f, 200f, 0.8f, 0.7f, 0.06f, 0.001f, Wave.LowPass);
                t = 0.12f;
                for (int i = 0; i < 6; i++)
                {
                    w.Click(t, 1500f - i * 150f, 0.35f - i * 0.04f);
                    t += 0.05f + i * 0.025f;   // 점점 느려지다 멈춘다
                }
                return w.Normalize(0.62f).Trim(0.03f);

            // ── 결과 ──
            case SfxId.Victory:   // 승리: 종탑의 밝은 종 (올라가는 장화음)
                w = new Wave(2.8f, seed);
                int[] win = { 62, 66, 69, 74 };
                for (int i = 0; i < win.Length; i++) w.Bell(i * 0.16f, Wave.Note(win[i] + 12), 0.5f, 0.5f);
                w.Bell(0.64f, Wave.Note(86), 0.6f, 0.75f);
                w.Tone(0.64f, 1.8f, Wave.Note(50), Wave.Note(50), 0.25f, 0.6f, 0.02f, Wave.Triangle, 0.2f);
                w.Tone(0.64f, 1.8f, Wave.Note(57), Wave.Note(57), 0.18f, 0.6f, 0.02f, Wave.Triangle, 0.2f);
                return w.Normalize(0.6f).Trim(0.2f);

            default:   // Defeat — 패배: 내려앉는 단화음 + 멈춰 가는 초침
                w = new Wave(3.2f, seed);
                int[] lose = { 69, 65, 62, 58 };
                for (int i = 0; i < lose.Length; i++) w.Bell(i * 0.42f, Wave.Note(lose[i]), 0.5f, 0.7f);
                w.Tone(1.26f, 1.8f, Wave.Note(38), Wave.Note(37), 0.4f, 0.7f, 0.05f, Wave.Triangle, 0.3f);
                t = 0.2f;
                for (int i = 0; i < 7; i++)
                {
                    w.Click(t, 2200f - i * 120f, 0.3f - i * 0.03f);
                    t += 0.22f + i * 0.06f;
                }
                return w.Normalize(0.55f).Trim(0.3f);
        }
    }
}
