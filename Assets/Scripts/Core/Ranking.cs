using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 무한 모드 랭킹 서버와 주고받는다 (Server/ranking-server.js).
///
/// 위조 막기: 점수를 숫자로 바로 올리지 않는다. 판을 시작할 때 서버가 판 번호(runId)와 일회용 nonce를 주고,
/// 회차를 넘길 때마다 "다음 회차"만 서명해서 보낸다 → 서버가 순서 · 시간 하한 · 서명을 확인한 뒤에만 기록이 오른다.
///   BeginRun()        — 무한 모드를 시작할 때
///   Checkpoint(loop)  — 보스를 쓰러뜨려 loop회차를 넘겼을 때
///   EndRun(done)      — 쓰러졌을 때 (순위를 돌려받는다)
///   FetchTop(n)       — 상위 n명 + 내 순위 (랭킹 창)
/// 요청은 보낸 순서대로 하나씩 처리한다 (판 시작 응답이 오기 전에 회차 통과가 먼저 가지 않게).
/// 서버가 꺼져 있어도 게임은 그대로 돈다 — 실패하면 콜백에 null이 온다.
/// </summary>
public static class Ranking
{
    [Serializable] public class Entry { public int rank; public string name; public int loop; }
    [Serializable] public class TopResult { public Entry[] entries; public Entry me; public int total; }
    [Serializable] public class EndResult { public int rank; public int best; public int total; }

    [Serializable] private class StartBody { public string id; public string name; public string sig; }
    [Serializable] private class StartReply { public string runId; public string nonce; }
    [Serializable] private class CheckpointBody { public string runId; public int loop; public string sig; }
    [Serializable] private class CheckpointReply { public string nonce; public int best; public int rank; }
    [Serializable] private class EndBody { public string runId; public string sig; }
    [Serializable] private class NameBody { public string id; public string name; public string sig; }

    /// <summary>서버 주소 (끝의 / 없이). GameManager가 시작할 때 넣는다</summary>
    public static string ServerUrl = "https://clockworkroll-ranking.clockworkroll-ranking-worker.workers.dev";

    private const int Timeout = 8;

    // 서버와 같이 쓰는 서명 키 (두 배열을 XOR하면 나온다 — 실행 파일에 글자 그대로 남지 않게)
    private static readonly byte[] keyA = { 147, 195, 70, 47, 102, 230, 123, 77, 43, 203, 115, 97, 217, 177, 229, 63, 149, 175, 51, 81, 226, 19, 38, 139, 173, 254, 153, 135, 196, 41, 134, 243, 71, 223, 255, 105, 91, 106, 39, 118, 217, 34, 210 };
    private static readonly byte[] keyB = { 167, 144, 25, 85, 1, 178, 53, 53, 26, 152, 17, 21, 233, 230, 173, 6, 220, 225, 102, 19, 133, 98, 74, 194, 203, 154, 193, 233, 138, 27, 209, 160, 117, 158, 188, 25, 62, 94, 111, 12, 136, 76, 135 };

    private static string Sign(string message)
    {
        byte[] key = new byte[keyA.Length];
        for (int i = 0; i < key.Length; i++) key[i] = (byte)(keyA[i] ^ keyB[i]);
        using (HMACSHA256 h = new HMACSHA256(key))
        {
            byte[] mac = h.ComputeHash(Encoding.UTF8.GetBytes(message));
            StringBuilder sb = new StringBuilder(mac.Length * 2);
            foreach (byte b in mac) sb.Append(b.ToString("x2"));
            Array.Clear(key, 0, key.Length);
            return sb.ToString();
        }
    }

    // ─────────────────────────── 플레이어 ───────────────────────────

    /// <summary>이 기기의 플레이어 id (처음 부를 때 만든다)</summary>
    public static string PlayerId
    {
        get
        {
            string id = "";
            try { id = PlayerPrefs.GetString("rank.id", ""); } catch { }
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                try { PlayerPrefs.SetString("rank.id", id); PlayerPrefs.Save(); } catch { }
            }
            return id;
        }
    }

    /// <summary>랭킹에 보일 이름 (1~16자, 서버와 같은 규칙으로 정리)</summary>
    public static string PlayerName
    {
        get
        {
            string n = "";
            try { n = PlayerPrefs.GetString("rank.name", ""); } catch { }
            n = CleanName(n);
            return string.IsNullOrEmpty(n) ? "태엽공-" + PlayerId.Substring(0, 4).ToUpperInvariant() : n;
        }
        set
        {
            string n = CleanName(value);
            if (string.IsNullOrEmpty(n)) return;
            try { PlayerPrefs.SetString("rank.name", n); PlayerPrefs.Save(); } catch { }
        }
    }

    /// <summary>서버의 cleanName과 같게: 제어 문자 · &lt; &gt; 빼고, 앞뒤 공백 자르고, 16자까지 (서명할 글자가 서버와 똑같아야 한다)</summary>
    private static string CleanName(string s)
    {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder();
        foreach (char c in s) if (c >= 0x20 && c != 0x7f && c != '<' && c != '>') sb.Append(c);
        string n = sb.ToString().Trim();
        return n.Length > 16 ? n.Substring(0, 16) : n;
    }

    // ─────────────────────────── 판 ───────────────────────────

    private static string runId, nonce;

    public static void BeginRun()
    {
        runId = null;
        nonce = null;
        string id = PlayerId, name = PlayerName;
        Enqueue(() => Post("/api/endless/run/start", JsonUtility.ToJson(new StartBody { id = id, name = name, sig = Sign($"start|{id}|{name}") }), text =>
        {
            StartReply r = Parse<StartReply>(text);
            if (r != null && !string.IsNullOrEmpty(r.runId)) { runId = r.runId; nonce = r.nonce; }
        }));
    }

    public static void Checkpoint(int loop)
    {
        Enqueue(() =>
        {
            if (runId == null) return null;   // 판을 열지 못했다 (서버가 꺼져 있었다)
            string rid = runId;
            return Post("/api/endless/run/checkpoint", JsonUtility.ToJson(new CheckpointBody { runId = rid, loop = loop, sig = Sign($"checkpoint|{rid}|{loop}|{nonce}") }), text =>
            {
                CheckpointReply r = Parse<CheckpointReply>(text);
                if (r != null && !string.IsNullOrEmpty(r.nonce)) nonce = r.nonce;
            });
        });
    }

    public static void EndRun(Action<EndResult> done)
    {
        Enqueue(() =>
        {
            if (runId == null) { if (done != null) done(null); return null; }
            string rid = runId;
            runId = null;
            return Post("/api/endless/run/end", JsonUtility.ToJson(new EndBody { runId = rid, sig = Sign($"end|{rid}|{nonce}") }), text =>
            {
                EndResult r = Parse<EndResult>(text);
                if (done != null) done(r != null && r.total >= 0 && text != null ? r : null);
            });
        });
    }

    /// <summary>이름만 바꾼다 (기록은 그대로)</summary>
    public static void Rename(string newName, Action done)
    {
        PlayerName = newName;
        string id = PlayerId, name = PlayerName;
        Enqueue(() => Post("/api/player/name", JsonUtility.ToJson(new NameBody { id = id, name = name, sig = Sign($"name|{id}|{name}") }), _ => { if (done != null) done(); }));
    }

    public static void FetchTop(int limit, Action<TopResult> done)
    {
        string url = $"/api/endless/top?limit={Mathf.Clamp(limit, 1, 100)}&id={UnityWebRequest.EscapeURL(PlayerId)}";
        Enqueue(() => Get(url, text => done(Parse<TopResult>(text))));
    }

    // ─────────────────────────── 전송 ───────────────────────────

    private static T Parse<T>(string text) where T : class
    {
        if (string.IsNullOrEmpty(text)) return null;
        try { return JsonUtility.FromJson<T>(text); } catch { return null; }
    }

    private static IEnumerator Get(string path, Action<string> done)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(ServerUrl.TrimEnd('/') + path))
        {
            req.timeout = Timeout;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok) Debug.Log($"랭킹 서버에 닿지 못했다: {req.error}");
            done(ok ? req.downloadHandler.text : null);
        }
    }

    private static IEnumerator Post(string path, string json, Action<string> done)
    {
        using (UnityWebRequest req = new UnityWebRequest(ServerUrl.TrimEnd('/') + path, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Timeout;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (!ok) Debug.Log($"랭킹 서버 {path}: {req.error} {req.downloadHandler?.text}");
            done(ok ? req.downloadHandler.text : null);
        }
    }

    // 요청 줄: 앞의 요청이 끝나야 다음 요청을 만든다 (runId · nonce가 앞 응답에 달려 있다)
    private static readonly Queue<Func<IEnumerator>> queue = new Queue<Func<IEnumerator>>();
    private static bool pumping;

    private static void Enqueue(Func<IEnumerator> make)
    {
        queue.Enqueue(make);
        if (!pumping) Runner.Run(Pump());
    }

    private static IEnumerator Pump()
    {
        pumping = true;
        while (queue.Count > 0)
        {
            IEnumerator step = null;
            try { step = queue.Dequeue()(); } catch (Exception e) { Debug.LogWarning("랭킹 요청 실패: " + e.Message); }
            if (step != null) yield return step;
        }
        pumping = false;
    }

    /// <summary>요청을 돌릴 숨은 오브젝트 (씬이 바뀌어도 남는다 — 나가는 순간 보낸 요청도 끝까지 간다)</summary>
    private class Runner : MonoBehaviour
    {
        private static Runner instance;

        public static void Run(IEnumerator routine)
        {
            if (instance == null)
            {
                GameObject go = new GameObject("RankingRunner");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideInHierarchy;
                instance = go.AddComponent<Runner>();
            }
            instance.StartCoroutine(routine);
        }
    }
}
