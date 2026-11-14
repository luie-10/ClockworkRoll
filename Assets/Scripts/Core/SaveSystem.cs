using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>덱의 태엽판 한 장 (에셋은 이름으로 다시 찾는다)</summary>
[Serializable]
public class SavedCard
{
    public string card;         // 에셋 이름 (흡수한 태엽판은 원본의 카드 이름)
    public bool absorbed;
    public int corruption;
    public string sourceName;
}

/// <summary>
/// 이어하기용 저장 내용 — '어디까지 왔는가'와 '무엇을 들고 있는가'.
/// 전투 한복판은 저장하지 않는다: 이어하면 그 전투를 시작하기 전 지점(챕터의 첫 장면 / 갈래길 지도)으로 돌아간다.
/// </summary>
[Serializable]
public class SaveData
{
    public int version = SaveSystem.Version;
    public string story;                // 어느 이야기의 저장인가 (다른 이야기면 무시)
    public string savedAt;

    public int chapterIndex;
    public string chapterTitle;         // 시작 화면에 보여 줄 이름

    public bool onRoute;                // true = 갈래길 지도 위
    public int routeSeed;               // 같은 지도를 다시 만드는 씨앗
    public List<int> routePath = new List<int>();   // 지나온 칸 (층 * 100 + 열)

    public int hp, maxHp, maxKi;
    public List<string> items = new List<string>();
    public List<SavedCard> deck = new List<SavedCard>();
}

/// <summary>
/// 저장 파일 읽고 쓰기 (Application.persistentDataPath/save.json 한 개).
/// 저장 시점은 GameManager가 정한다: 챕터가 시작될 때, 갈래길 지도에서 칸 하나를 마칠 때마다.
/// 패배하거나 끝까지 깨면 지운다.
/// </summary>
public static class SaveSystem
{
    public const int Version = 1;

    private static string FilePath { get { return Path.Combine(Application.persistentDataPath, "save.json"); } }

    /// <summary>story 이야기의 저장이 있으면 읽어 온다 (없거나 못 읽으면 null)</summary>
    public static SaveData Load(string story)
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            if (data == null || data.version != Version || data.story != story) return null;
            if (data.deck == null || data.deck.Count == 0 || data.maxHp <= 0) return null;
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning("저장 파일을 읽지 못했습니다: " + e.Message);
            return null;
        }
    }

    public static void Write(SaveData data)
    {
        try
        {
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("저장하지 못했습니다: " + e.Message);
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("저장 파일을 지우지 못했습니다: " + e.Message);
        }
    }
}
