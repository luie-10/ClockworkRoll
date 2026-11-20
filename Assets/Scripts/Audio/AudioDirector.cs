using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 게임의 소리를 지휘한다 — 직접 붙일 필요 없음 (게임이 시작될 때 알아서 생긴다).
///   - 배경 음악: 평소(시작 화면 · 대사 · 갈래길)엔 느린 오르골, 전투에선 초침이 재촉하는 빠른 곡. 서로 스르륵 넘어간다
///   - 환경음: 지금 무대 배경에 맞춰 — 뒷골목의 비 / 잠긴 거리의 물결 / 하수도의 물방울 / 종탑의 바람과 톱니 / 공방의 시계들
///   - 효과음 재생기 (Sfx.Play가 빌려 쓴다)
/// 음악과 환경음도 에셋 없이 코드로 그린다 (Wave). 길어서 만드는 데 시간이 걸리므로 뒤에서(다른 스레드) 만들고, 다 되면 틀기 시작한다.
/// </summary>
public class AudioDirector : MonoBehaviour
{
    private const string MusicKey = "audio.music";
    private const int LoopRate = 22050;   // 음악 · 환경음은 낮은 표본율로 충분하다 (만드는 시간 절반)

    private static AudioDirector instance;
    private static float musicVolume = -1f;

    private enum Track { None, Calm, Battle }

    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private int nextSfx;

    private AudioSource calmSource, battleSource;
    private readonly Dictionary<BackdropType, AudioSource> ambience = new Dictionary<BackdropType, AudioSource>();
    private Task<Wave> calmTask, battleTask;
    private readonly Dictionary<BackdropType, Task<Wave>> ambienceTasks = new Dictionary<BackdropType, Task<Wave>>();
    private Task<Dictionary<SfxId, Wave>> sfxTask;

    /// <summary>음악 · 환경음 크기 0~1 (PlayerPrefs에 저장)</summary>
    public static float MusicVolume
    {
        get
        {
            if (musicVolume < 0f) musicVolume = PlayerPrefs.GetFloat(MusicKey, 0.6f);
            return musicVolume;
        }
        set
        {
            musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, musicVolume);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        GameObject go = new GameObject("AudioDirector");
        DontDestroyOnLoad(go);   // 씬을 다시 불러도(패배 뒤 재시작) 음악이 끊기지 않는다
        instance = go.AddComponent<AudioDirector>();
    }

    /// <summary>효과음용 재생기 하나 (돌아가며 쓴다)</summary>
    public static AudioSource NextSfxSource()
    {
        if (instance == null || instance.sfxSources.Count == 0) return null;
        instance.nextSfx = (instance.nextSfx + 1) % instance.sfxSources.Count;
        return instance.sfxSources[instance.nextSfx];
    }

    void Awake()
    {
        for (int i = 0; i < 10; i++) sfxSources.Add(NewSource("Sfx " + i, false));
        calmSource = NewSource("Music Calm", true);
        battleSource = NewSource("Music Battle", true);

        // 긴 소리는 뒤에서 만든다 (순수 계산이라 다른 스레드에서 해도 된다)
        sfxTask = Task.Run(() => Sfx.BuildAll());   // 효과음부터 (그 전에 필요해지면 Sfx가 그 자리에서 하나만 만든다)
        calmTask = Task.Run(() => CalmMusic());
        battleTask = Task.Run(() => BattleMusic());
        foreach (BackdropType type in System.Enum.GetValues(typeof(BackdropType)))
        {
            BackdropType t = type;
            ambience[t] = NewSource("Ambience " + t, true);
            ambienceTasks[t] = Task.Run(() => Ambience(t));
        }
    }

    private AudioSource NewSource(string name, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;   // 2D — 카메라 위치와 상관없이 같은 크기
        s.volume = loop ? 0f : 1f;
        return s;
    }

    void Update()
    {
        // 다 만들어진 소리부터 재생기에 올린다
        if (sfxTask != null && sfxTask.IsCompleted)
        {
            if (sfxTask.Status == TaskStatus.RanToCompletion) Sfx.Adopt(sfxTask.Result);
            else Debug.LogWarning("효과음을 미리 만들지 못했다 — " + sfxTask.Exception);
            sfxTask = null;
        }
        Adopt(calmSource, ref calmTask, "Music Calm");
        Adopt(battleSource, ref battleTask, "Music Battle");
        foreach (BackdropType type in new List<BackdropType>(ambienceTasks.Keys))
        {
            Task<Wave> task = ambienceTasks[type];
            Adopt(ambience[type], ref task, "Ambience " + type);
            if (task == null) ambienceTasks.Remove(type);
        }

        Track track = WantedTrack(out float level);
        float dt = Time.unscaledDeltaTime;
        Fade(calmSource, track == Track.Calm ? 0.5f * level : 0f, dt, 0.9f);
        Fade(battleSource, track == Track.Battle ? 0.5f * level : 0f, dt, 0.9f);

        BackdropType? here = StageBackdrop.Active != null ? StageBackdrop.Active.Type : (BackdropType?)null;
        bool quiet = TitleScreen.IsOpen || LoadingScreen.IsOpen;   // 시작 화면에서는 환경음을 낮춘다
        foreach (KeyValuePair<BackdropType, AudioSource> pair in ambience)
            Fade(pair.Value, here.HasValue && pair.Key == here.Value ? (quiet ? 0.2f : 0.55f) : 0f, dt, 1.2f);
    }

    /// <summary>지금 틀 음악과 크기(0~1). 승리 화면에서는 종소리가 들리게 낮추고, 패배하면 끈다.</summary>
    private static Track WantedTrack(out float level)
    {
        level = 1f;
        GameManager gm = GameManager.Instance;
        if (gm == null || TitleScreen.IsOpen || LoadingScreen.IsOpen) return Track.Calm;

        switch (gm.State)
        {
            case GameManager.GameState.Battle:
                if (VictoryScreen.IsOpen) { level = 0.45f; return Track.Calm; }
                BattleManager bm = BattleManager.Instance;
                if (bm != null && bm.Phase == BattleManager.BattlePhase.End) level = 0.35f;
                return Track.Battle;
            case GameManager.GameState.GameOver:
                return Track.None;
            default:
                return Track.Calm;
        }
    }

    private void Adopt(AudioSource source, ref Task<Wave> task, string name)
    {
        if (task == null || !task.IsCompleted) return;
        if (task.Status == TaskStatus.RanToCompletion && task.Result != null)
        {
            source.clip = task.Result.ToClip(name);
            source.volume = 0f;
            source.Play();
        }
        else if (task.IsFaulted) Debug.LogWarning("소리를 만들지 못했다: " + name + " — " + task.Exception);
        task = null;
    }

    private static void Fade(AudioSource source, float target, float dt, float seconds)
    {
        if (source == null || source.clip == null) return;
        target *= MusicVolume;
        source.volume = Mathf.MoveTowards(source.volume, target, dt / Mathf.Max(0.05f, seconds));
    }

    // ─────────────────────────── 음악 ───────────────────────────
    // 두 곡 모두 라단조. 화음 하나 = 근음(미디 번호) + 3음 · 5음까지의 간격.

    private struct Chord
    {
        public int root, third, fifth;
        public Chord(int root, int third, int fifth) { this.root = root; this.third = third; this.fifth = fifth; }
    }

    private static readonly Chord Dm = new Chord(62, 3, 7), Bb = new Chord(58, 4, 7), F = new Chord(65, 4, 7),
                                  C = new Chord(60, 4, 7), Gm = new Chord(55, 3, 7), A = new Chord(57, 4, 7);

    /// <summary>평소의 곡: 느린 오르골 (라단조, 두 마디씩 Dm – B♭ – F – C). 박마다 멀리서 초침이 간다.</summary>
    public static Wave CalmMusic()
    {
        const float beat = 60f / 72f;
        const int bars = 8;
        Wave w = new Wave(bars * 4 * beat, 4101, true, LoopRate);

        int[][] melody =
        {
            new[] { 62, 69, 74, 77, 76, 74, 69, 65 }, new[] { 62, 69, 74, 77, 81, 77, 76, 74 },
            new[] { 58, 65, 70, 74, 72, 70, 65, 62 }, new[] { 58, 65, 70, 74, 77, 74, 72, 70 },
            new[] { 65, 69, 72, 77, 76, 72, 69, 65 }, new[] { 65, 69, 72, 77, 81, 77, 76, 72 },
            new[] { 60, 67, 72, 76, 74, 72, 67, 64 }, new[] { 60, 67, 72, 76, 79, 76, 72, 69 },
        };
        Chord[] chords = { Dm, Dm, Bb, Bb, F, F, C, C };

        for (int bar = 0; bar < bars; bar++)
        {
            float t0 = bar * 4 * beat;
            Chord ch = chords[bar];

            // 오르골: 8분음표로 화음을 오르내린다 (마디 첫 음은 조금 세게, 나머지는 살짝 들쭉날쭉하게)
            for (int i = 0; i < 8; i++)
            {
                float human = w.Rand(-0.006f, 0.006f);
                w.MusicBox(t0 + i * beat * 0.5f + human, Wave.Note(melody[bar][i]), (i == 0 ? 0.5f : 0.34f) * w.Rand(0.85f, 1.05f));
            }

            // 낮은 음: 두 마디마다 근음을 길게
            if (bar % 2 == 0)
            {
                float low = Wave.Note(ch.root - 12);
                w.Tone(t0, beat * 7.5f, low, low, 0.3f, beat * 3f, 0.03f, Wave.Triangle, 0.4f);
                w.Tone(t0, beat * 7.5f, low * 0.5f, low * 0.5f, 0.2f, beat * 3f, 0.03f, Wave.Sine, 0.4f);
            }

            // 깔리는 화음: 천천히 부풀었다 사라진다
            foreach (int step in new[] { 0, ch.third, ch.fifth })
            {
                float f = Wave.Note(ch.root + step);
                w.Tone(t0, beat * 4f, f, f, 0.055f, 0f, beat * 1.5f, Wave.Sine, beat * 1.5f);
                w.Tone(t0, beat * 4f, f * 1.004f, f * 1.004f, 0.04f, 0f, beat * 1.5f, Wave.Sine, beat * 1.5f);
            }

            // 멀리서 가는 초침 (틱 — 톡)
            for (int b = 0; b < 4; b++) w.Click(t0 + b * beat, b % 2 == 0 ? 2400f : 1700f, 0.05f);
        }
        return w.Normalize(0.7f);
    }

    /// <summary>전투의 곡: 초침이 재촉하는 빠른 곡 (라단조, Dm – Dm – B♭ – C – Dm – F – Gm – A).</summary>
    public static Wave BattleMusic()
    {
        const float beat = 60f / 126f;
        const int bars = 8;
        Wave w = new Wave(bars * 4 * beat, 4202, true, LoopRate);
        Chord[] chords = { Dm, Dm, Bb, C, Dm, F, Gm, A };
        int[] shape = { 0, 1, 2, 3, 2, 1, 2, 1 };   // 화음을 오르내리는 모양 (0 = 근음, 1 = 3음, 2 = 5음, 3 = 한 옥타브 위 근음)

        for (int bar = 0; bar < bars; bar++)
        {
            float t0 = bar * 4 * beat;
            Chord ch = chords[bar];
            int[] tones = { ch.root, ch.root + ch.third, ch.root + ch.fifth, ch.root + 12 };

            for (int i = 0; i < 8; i++)
            {
                float t = t0 + i * beat * 0.5f;

                // 낮은 음: 8분음표로 근음을 두드린다 (2박 · 4박의 뒷박은 5음으로)
                int bassNote = (i == 3 || i == 7) ? ch.root + ch.fifth - 24 : ch.root - 24;
                float low = Wave.Note(bassNote + 12);
                w.Tone(t, beat * 0.45f, low, low, i % 2 == 0 ? 0.34f : 0.24f, beat * 0.22f, 0.004f, Wave.Triangle, 0.02f);
                w.Tone(t, beat * 0.45f, low * 0.5f, low * 0.5f, 0.2f, beat * 0.22f, 0.004f, Wave.Sine, 0.02f);

                // 오르골: 한 옥타브 위에서 화음을 오르내린다. 뒤쪽 네 마디는 한 번 더 높여 고조시킨다
                int lift = bar >= 4 ? 24 : 12;
                w.MusicBox(t, Wave.Note(tones[shape[i]] + lift), (i == 0 ? 0.36f : 0.25f) * w.Rand(0.9f, 1.05f));

                // 초침: 8분음표마다
                w.Click(t, i % 2 == 0 ? 3000f : 2300f, i % 2 == 0 ? 0.1f : 0.06f);
            }

            // 북: 1 · 3박은 낮게 쿵, 2 · 4박은 톱니가 물리는 탁
            for (int b = 0; b < 4; b++)
            {
                float t = t0 + b * beat;
                if (b % 2 == 0)
                {
                    w.Tone(t, 0.16f, 120f, 48f, 0.5f, 0.05f, 0.002f);
                }
                else
                {
                    w.Noise(t, 0.07f, 1800f, 900f, 1.2f, 0.3f, 0.02f, 0.001f);
                    w.Click(t, 1300f, 0.14f);
                }
            }

            // 깔리는 화음 (낮게, 살짝 거친 음색으로 긴장감)
            foreach (int step in new[] { 0, ch.fifth })
            {
                float f = Wave.Note(ch.root + step - 12);
                w.Tone(t0, beat * 4f, f, f, 0.05f, 0f, beat * 0.5f, Wave.Triangle, beat * 0.5f);
            }
        }
        return w.Normalize(0.7f);
    }

    // ─────────────────────────── 환경음 (8초 반복) ───────────────────────────

    private const float TwoPi = 6.2831853f;

    /// <summary>한 바퀴(u = 0~1)에 cycles번 출렁이는 값: mid ± depth (반복 이음매가 맞는다)</summary>
    private static float Swell(float u, float cycles, float mid, float depth, float phase = 0f)
    {
        return mid + depth * (float)System.Math.Sin(TwoPi * (u * cycles + phase));
    }

    public static Wave Ambience(BackdropType type)
    {
        const float seconds = 8f;
        Wave w = new Wave(seconds, 5300 + (int)type * 11, true, LoopRate);

        switch (type)
        {
            case BackdropType.Alley:   // 뒷골목: 쏟아지는 비 + 처마에서 떨어지는 굵은 방울
                w.Bed(u => 2600f, 0.7f, u => Swell(u, 2f, 0.5f, 0.07f), Wave.HighPass);
                w.Bed(u => Swell(u, 1f, 900f, 150f), 0.8f, u => 0.3f);
                w.Bed(u => 220f, 0.7f, u => 0.35f, Wave.LowPass);
                for (int i = 0; i < 70; i++) w.Click(w.Rand(0f, seconds), w.Rand(2200f, 3400f), w.Rand(0.03f, 0.1f));
                for (int i = 0; i < 9; i++) Drip(w, w.Rand(0f, seconds), w.Rand(700f, 1100f), 0.12f);
                break;

            case BackdropType.FloodedStreet:   // 물에 잠긴 거리: 찰랑이는 물결 + 가랑비
                w.Bed(u => Swell(u, 3f, 520f, 180f), 0.9f, u => Swell(u, 3f, 0.55f, 0.3f, 0.2f), Wave.LowPass);
                w.Bed(u => Swell(u, 2f, 1300f, 300f, 0.4f), 1.2f, u => Swell(u, 2f, 0.14f, 0.1f, 0.6f));
                w.Bed(u => 3000f, 0.7f, u => 0.16f, Wave.HighPass);
                for (int i = 0; i < 12; i++) Drip(w, w.Rand(0f, seconds), w.Rand(500f, 950f), 0.16f);
                break;

            case BackdropType.Sewer:   // 하수도: 낮게 웅웅대는 굴 + 메아리치는 물방울 + 멀리 흐르는 물
                w.Bed(u => 130f, 0.8f, u => Swell(u, 1f, 0.6f, 0.12f), Wave.LowPass);
                w.Bed(u => Swell(u, 2f, 700f, 120f), 1.5f, u => 0.12f);
                w.Tone(0f, seconds, 55f, 55f, 0.1f, 0f, 1f, Wave.Sine, 1f);
                for (int i = 0; i < 10; i++)
                {
                    float t = w.Rand(0f, seconds), f = w.Rand(900f, 1900f);
                    Drip(w, t, f, 0.26f);
                    Drip(w, t + 0.19f, f, 0.1f);    // 굴에 울리는 메아리
                    Drip(w, t + 0.38f, f, 0.045f);
                }
                break;

            case BackdropType.ClockTower:   // 태엽 장치: 탑 사이를 지나는 바람 + 1초마다 도는 큰 톱니 + 가끔 삐걱
                w.Bed(u => Swell(u, 1f, 520f, 220f), 2.2f, u => Swell(u, 1f, 0.5f, 0.28f, 0.25f));
                w.Bed(u => Swell(u, 2f, 1100f, 300f, 0.3f), 3f, u => Swell(u, 2f, 0.14f, 0.1f, 0.1f));
                w.Bed(u => 150f, 0.7f, u => 0.28f, Wave.LowPass);
                for (int i = 0; i < 8; i++)
                {
                    w.Click(i, 900f, 0.22f);
                    w.Tone(i, 0.12f, 180f, 130f, 0.22f, 0.04f);
                    w.Click(i + 0.5f, 1300f, 0.1f);
                }
                for (int i = 0; i < 3; i++)
                {
                    float t = w.Rand(0f, seconds), f = w.Rand(380f, 620f);
                    w.Tone(t, 0.5f, f, f * 1.12f, 0.035f, 0f, 0.15f, Wave.Saw, 0.2f);
                }
                break;

            case BackdropType.Rooftops:   // 지붕 위: 탁 트인 높은 바람 + 지붕을 두드리는 비 + 멀리 삐걱이는 바람개비
                w.Bed(u => Swell(u, 1f, 600f, 260f), 2.4f, u => Swell(u, 1f, 0.5f, 0.3f, 0.1f));
                w.Bed(u => 2400f, 0.7f, u => Swell(u, 2f, 0.3f, 0.08f), Wave.HighPass);
                for (int i = 0; i < 40; i++) w.Click(w.Rand(0f, seconds), w.Rand(1500f, 2600f), w.Rand(0.03f, 0.08f));
                for (int i = 0; i < 2; i++)
                {
                    float t = w.Rand(0f, seconds), f = w.Rand(500f, 700f);
                    w.Tone(t, 0.7f, f, f * 1.2f, 0.03f, 0f, 0.2f, Wave.Saw, 0.2f);
                }
                break;

            case BackdropType.Foundry:   // 주조소: 낮게 끓는 화로 + 쿵 하는 망치질 + 타닥이는 불티
                w.Bed(u => 90f, 0.7f, u => Swell(u, 2f, 0.6f, 0.15f), Wave.LowPass);
                w.Bed(u => Swell(u, 4f, 400f, 80f), 1.5f, u => 0.18f, Wave.LowPass);
                for (int i = 0; i < 4; i++) { w.Click(i * 2f + 0.3f, 300f, 0.3f); w.Tone(i * 2f + 0.3f, 0.35f, 110f, 70f, 0.3f, 0.002f); }
                for (int i = 0; i < 50; i++)
                    w.Noise(w.Rand(0f, seconds), w.Rand(0.006f, 0.02f), w.Rand(1500f, 3500f), w.Rand(800f, 2000f), 1.5f, w.Rand(0.05f, 0.18f), 0.005f, 0.0005f);
                break;

            case BackdropType.Station:   // 멈춘 역: 텅 빈 승강장의 울림 + 째깍이는 역 시계 + 가끔 새는 김
                w.Bed(u => 180f, 0.8f, u => Swell(u, 1f, 0.35f, 0.08f), Wave.LowPass);
                w.Bed(u => Swell(u, 1f, 800f, 200f), 2f, u => Swell(u, 1f, 0.12f, 0.08f, 0.3f));
                for (int i = 0; i < 8; i++) w.Click(i, 1800f, 0.16f);
                for (int i = 0; i < 2; i++)
                {
                    float t = w.Rand(0.5f, seconds - 1.5f);
                    w.Noise(t, 1.2f, 3000f, 2500f, 0.8f, 0.12f, 0f, 0.3f, Wave.BandPass, 0.5f);
                }
                break;

            case BackdropType.Garden:   // 태엽 정원: 부드러운 바람 + 졸졸 흐르는 분수 + 작게 감기는 태엽 꽃
                w.Bed(u => Swell(u, 1f, 450f, 120f), 1.8f, u => Swell(u, 1f, 0.3f, 0.12f, 0.2f));
                w.Bed(u => Swell(u, 6f, 1600f, 400f), 1.2f, u => 0.12f);
                for (int i = 0; i < 18; i++) Drip(w, w.Rand(0f, seconds), w.Rand(1200f, 2200f), 0.06f);
                for (int i = 0; i < 32; i++) w.Click(i * 0.25f, 4200f, i % 4 == 0 ? 0.06f : 0.03f);
                break;

            case BackdropType.Market:   // 밤 장터: 웅성이는 먼 소리 + 흔들리는 등롱 줄 + 띄엄띄엄 나무 삐걱
                w.Bed(u => Swell(u, 2f, 350f, 90f), 1.2f, u => Swell(u, 3f, 0.3f, 0.1f, 0.4f), Wave.LowPass);
                w.Bed(u => Swell(u, 1f, 700f, 150f), 2.2f, u => Swell(u, 1f, 0.16f, 0.08f));
                for (int i = 0; i < 4; i++)
                {
                    float t = w.Rand(0f, seconds), f = w.Rand(250f, 420f);
                    w.Tone(t, 0.3f, f, f * 0.9f, 0.05f, 0.02f, 0.1f, Wave.Saw, 0.2f);
                }
                for (int i = 0; i < 6; i++) w.Click(w.Rand(0f, seconds), w.Rand(600f, 1100f), 0.08f);
                break;

            default:   // Workshop — 시계공의 공방: 서로 박자가 다른 시계들 + 화로가 타닥이는 소리
                w.Bed(u => 260f, 0.7f, u => Swell(u, 2f, 0.32f, 0.08f), Wave.LowPass);
                for (int i = 0; i < 16; i++) w.Click(i * 0.5f, 2600f, i % 2 == 0 ? 0.2f : 0.13f);             // 1초에 두 번
                for (int i = 0; i < 24; i++) w.Click(i / 3f + 0.11f, 3400f, 0.08f);                               // 1초에 세 번 (작은 시계)
                for (int i = 0; i < 8; i++) { w.Click(i + 0.27f, 1200f, 0.14f); w.Tone(i + 0.27f, 0.1f, 300f, 240f, 0.1f, 0.03f); }   // 괘종
                for (int i = 0; i < 46; i++)
                    w.Noise(w.Rand(0f, seconds), w.Rand(0.008f, 0.03f), w.Rand(900f, 2600f), w.Rand(500f, 1500f), 1.5f, w.Rand(0.05f, 0.22f), 0.008f, 0.0005f);
                break;
        }
        return w.Normalize(0.6f);
    }

    /// <summary>물방울 하나: 짧게 떨어지는 음 + 튀는 소리</summary>
    private static void Drip(Wave w, float t, float freq, float amp)
    {
        w.Tone(t, 0.09f, freq * 1.5f, freq, amp, 0.025f, 0.001f);
        w.Tone(t + 0.02f, 0.06f, freq * 2.3f, freq * 2.1f, amp * 0.3f, 0.015f, 0.001f);
    }
}
