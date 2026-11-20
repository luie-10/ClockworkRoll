using System;
using UnityEngine;

/// <summary>
/// 소리를 코드로 그리는 도화지 (에셋 없이 효과음 · 환경음 · 음악을 만든다 — 그림이 PixelCanvas라면 소리는 Wave).
///   Tone  : 한 음 (사인/삼각/톱니/사각, 음높이가 미끄러질 수 있고 서서히 사라진다)
///   Noise : 걸러낸 잡음 한 줄기 (휙 · 쉭 · 탁 — 걸러내는 높이가 미끄러질 수 있다)
///   Bell  : 종/쇠 울림 (배음이 어긋난 여러 음)
///   Bed   : 끝없이 이어지는 잡음 바닥 (비 · 바람 · 물 — 반복 재생해도 이음매가 없다)
/// loop = true 면 끝을 넘어간 소리가 앞으로 이어 붙는다 → 반복 재생용.
/// UnityEngine을 쓰지 않는 순수 계산이라 다른 스레드에서 만들어도 된다 (ToClip만 메인 스레드에서).
/// </summary>
public sealed class Wave
{
    public readonly int rate;
    public readonly float[] data;
    public readonly bool loop;
    private readonly System.Random rng;

    public const int Sine = 0, Triangle = 1, Saw = 2, Square = 3;
    public const int BandPass = 0, LowPass = 1, HighPass = 2;

    public float Seconds { get { return data.Length / (float)rate; } }

    public Wave(float seconds, int seed, bool loop = false, int rate = 44100)
    {
        this.rate = rate;
        this.loop = loop;
        data = new float[Math.Max(1, (int)(seconds * rate))];
        rng = new System.Random(seed);
    }

    public float Rand() { return (float)rng.NextDouble(); }
    public float Rand(float min, float max) { return min + (max - min) * (float)rng.NextDouble(); }

    private void Add(int i, float v)
    {
        int n = data.Length;
        if (loop)
        {
            i %= n;
            if (i < 0) i += n;
            data[i] += v;
        }
        else if (i >= 0 && i < n) data[i] += v;
    }

    /// <summary>attack 동안 올라오고, decay(시간 상수, 0 이하면 유지)로 사라지고, 끝의 release 동안 0으로</summary>
    private static float Envelope(float t, float dur, float attack, float decay, float release)
    {
        float e = attack > 0f && t < attack ? t / attack : 1f;
        if (decay > 0f) e *= (float)Math.Exp(-t / decay);
        float left = dur - t;
        if (release > 0f && left < release) e *= Math.Max(0f, left / release);
        return e;
    }

    /// <summary>한 음. f0 → f1 로 음높이가 (배율로 고르게) 미끄러진다.</summary>
    public void Tone(float start, float dur, float f0, float f1, float amp, float decay,
                     float attack = 0.002f, int shape = Sine, float release = 0.005f)
    {
        int n = (int)(dur * rate), i0 = (int)(start * rate);
        if (!loop) n = Math.Min(n, data.Length - i0);   // 끝을 넘어가는 부분은 그리지 않는다
        if (n <= 0) return;
        double phase = 0.0;
        double ratio = f1 > 0f && f0 > 0f ? Math.Pow(f1 / f0, 1.0 / n) : 1.0;
        double f = f0;
        for (int i = 0; i < n; i++)
        {
            phase += f / rate;
            f *= ratio;
            double p = phase - Math.Floor(phase);
            double s;
            switch (shape)
            {
                case Triangle: s = 4.0 * Math.Abs(p - 0.5) - 1.0; break;
                case Saw:      s = 2.0 * p - 1.0; break;
                case Square:   s = Math.Tanh(4.0 * Math.Sin(2.0 * Math.PI * p)); break;   // 모서리를 둥글린 사각파
                default:       s = Math.Sin(2.0 * Math.PI * p); break;
            }
            Add(i0 + i, (float)s * amp * Envelope(i / (float)rate, dur, attack, decay, release));
        }
    }

    /// <summary>걸러낸 잡음. 가운데 주파수가 fc0 → fc1 로 미끄러진다. q가 클수록 좁고 '음'에 가깝다.</summary>
    public void Noise(float start, float dur, float fc0, float fc1, float q, float amp, float decay,
                      float attack = 0.002f, int mode = BandPass, float release = 0.005f)
    {
        int n = (int)(dur * rate), i0 = (int)(start * rate);
        if (n <= 0) return;
        double low = 0.0, band = 0.0;
        double damp = 1.0 / Math.Max(0.5f, q);
        double ratio = fc1 > 0f && fc0 > 0f ? Math.Pow(fc1 / fc0, 1.0 / n) : 1.0;
        double fc = fc0;
        double maxFc = rate / 6.5;   // 이 필터가 안정한 한계
        for (int i = 0; i < n; i++)
        {
            double k = 2.0 * Math.Sin(Math.PI * Math.Min(fc, maxFc) / rate);
            fc *= ratio;
            double x = rng.NextDouble() * 2.0 - 1.0;
            low += k * band;
            double high = x - low - damp * band;
            band += k * high;
            double s = mode == LowPass ? low : mode == HighPass ? high : band;
            Add(i0 + i, (float)s * amp * Envelope(i / (float)rate, dur, attack, decay, release));
        }
    }

    /// <summary>짧은 '딱' (잡음 한 점 + 높은 음 한 점)</summary>
    public void Click(float start, float freq, float amp)
    {
        Noise(start, 0.012f, freq, freq * 0.8f, 2.5f, amp * 0.9f, 0.004f, 0.0005f);
        Tone(start, 0.03f, freq, freq * 0.92f, amp * 0.5f, 0.008f, 0.0005f);
    }

    /// <summary>쇠 울림 — 배음이 어긋나 있어 종/칼/톱니처럼 들린다. decay = 가장 낮은 음이 사라지는 시간 상수</summary>
    public void Bell(float start, float freq, float amp, float decay)
    {
        float[] ratios = { 1f, 2.0f, 2.76f, 4.07f, 5.4f };
        float[] gains = { 1f, 0.55f, 0.4f, 0.22f, 0.12f };
        for (int i = 0; i < ratios.Length; i++)
        {
            float d = decay / (1f + i * 0.9f);   // 높은 배음일수록 빨리 사라진다
            Tone(start, Math.Min(d * 6f, decay * 6f), freq * ratios[i], freq * ratios[i], amp * gains[i], d, 0.001f);
        }
    }

    /// <summary>오르골 한 음 (맑고 짧은 울림)</summary>
    public void MusicBox(float start, float freq, float amp)
    {
        float d = Math.Max(0.25f, 0.9f * (float)Math.Pow(440.0 / Math.Max(110f, freq), 0.5));
        Tone(start, d * 4f, freq, freq, amp, d, 0.002f, Sine, 0.05f);
        Tone(start, d * 2.5f, freq * 2f, freq * 2f, amp * 0.32f, d * 0.55f, 0.002f, Sine, 0.05f);
        Tone(start, d * 1.5f, freq * 3.01f, freq * 3.01f, amp * 0.1f, d * 0.3f, 0.002f, Sine, 0.03f);
        Tone(start, 0.02f, freq * 5.2f, freq * 5.2f, amp * 0.12f, 0.006f, 0.0005f);   // 빗살을 튕기는 순간
    }

    /// <summary>
    /// 끝없이 이어지는 잡음 바닥 (loop 전용). 같은 잡음을 두 바퀴 돌려 두 번째 바퀴만 쓰므로 이음매가 없다.
    /// fc / gain 은 한 바퀴를 0~1로 본 위치(u)를 받아 값을 돌려준다 — u=0과 u=1에서 같은 값이어야 한다.
    /// </summary>
    public void Bed(Func<float, float> fc, float q, Func<float, float> gain, int mode = BandPass)
    {
        int n = data.Length;
        float[] src = new float[n];
        for (int i = 0; i < n; i++) src[i] = (float)(rng.NextDouble() * 2.0 - 1.0);

        double low = 0.0, band = 0.0;
        double damp = 1.0 / Math.Max(0.5f, q);
        double maxFc = rate / 6.5;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                double k = 2.0 * Math.Sin(Math.PI * Math.Min(Math.Max(20f, fc(u)), maxFc) / rate);
                low += k * band;
                double high = src[i] - low - damp * band;
                band += k * high;
                if (pass == 1)
                {
                    double s = mode == LowPass ? low : mode == HighPass ? high : band;
                    data[i] += (float)s * gain(u);
                }
            }
        }
    }

    /// <summary>가장 큰 값이 peak가 되도록 전체 크기를 맞춘다 (소리가 깨지지 않게)</summary>
    public Wave Normalize(float peak)
    {
        float max = 0f;
        for (int i = 0; i < data.Length; i++) max = Math.Max(max, Math.Abs(data[i]));
        if (max > 0.00001f)
        {
            float g = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= g;
        }
        return this;
    }

    /// <summary>앞뒤를 아주 짧게 다듬어 '틱' 하는 잡음이 나지 않게 (반복용이 아닐 때)</summary>
    public Wave Trim(float fadeOut = 0.01f)
    {
        int n = data.Length, f = Math.Min(n, (int)(fadeOut * rate));
        for (int i = 0; i < f; i++) data[n - 1 - i] *= i / (float)f;
        int a = Math.Min(n, (int)(0.001f * rate));
        for (int i = 0; i < a; i++) data[i] *= i / (float)a;
        return this;
    }

    /// <summary>AudioClip으로 (메인 스레드에서만)</summary>
    public AudioClip ToClip(string name)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>미디 음 번호 → 주파수 (69 = 라 440Hz)</summary>
    public static float Note(int midi)
    {
        return 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);
    }
}
