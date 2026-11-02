using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 메뉴 'Ruina/일러스트 굽기' — 그림이 비어 있는 곳을 자동 일러스트(Illustrator)로 그려
/// PNG로 저장하고 에셋에 연결한다. 이미 그림이 들어 있는 곳은 건드리지 않는다.
///
///   태엽판(CardData.cardArt)        → Assets/Art/Generated/Cards/이름.png
///   적(EnemyData.sprite + 포즈 7종)  → Assets/Art/Generated/Characters/이름_포즈.png
///   씬의 대사 초상화 / 플레이어 그림   → Assets/Art/Generated/Portraits, Characters
///
/// 구운 PNG는 그림판 등으로 고쳐 그려도 된다 (연결은 그대로 유지).
/// 굽지 않아도 게임은 실행할 때 같은 그림을 그려서 쓴다 — 굽는 건 파일로 남겨 손보고 싶을 때.
/// </summary>
public static class IllustrationBaker
{
    private const string Root = "Assets/Art/Generated";

    [MenuItem("Ruina/일러스트 굽기 (빈 그림 채우기)")]
    public static void BakeEmpty() { BakeAll(false); }

    /// <summary>
    /// 이미 구운 자동 그림(Generated 폴더에 있는 것)까지 새 그림으로 다시 그린다 — 그리는 방식이 바뀌었을 때.
    /// 같은 파일에 덮어쓰므로 에셋 연결은 그대로. 직접 그려 넣은 그림(Generated 밖)은 건드리지 않는다.
    /// </summary>
    [MenuItem("Ruina/일러스트 다시 굽기 (자동 그림을 새로 그리기)")]
    public static void Rebake() { BakeAll(true); }

    public static void BakeAll(bool redrawGenerated)
    {
        int cards = 0, enemies = 0, portraits = 0, players = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
            if (card == null || !NeedsArt(card.cardArt, redrawGenerated)) continue;
            Texture2D tex = Illustrator.DrawCard(card).ToTexture(card.cardName, FilterMode.Bilinear);
            card.cardArt = SavePng(tex, "Cards", string.IsNullOrEmpty(card.cardName) ? card.name : card.cardName, new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
            EditorUtility.SetDirty(card);
            cards++;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:EnemyData"))
        {
            EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
            if (enemy == null || !NeedsArt(enemy.sprite, redrawGenerated)) continue;
            BakeCharacter(string.IsNullOrEmpty(enemy.enemyName) ? enemy.name : enemy.enemyName, enemy.tintColor, out Sprite idle, out PoseSprites poses);
            enemy.sprite = idle;
            if (enemy.poses == null || enemy.poses.IsEmpty || IsGenerated(enemy.poses.slash)) enemy.poses = poses;
            EditorUtility.SetDirty(enemy);
            enemies++;
        }

        // 열린 씬: 플레이어 그림 + 대사 초상화
        foreach (SpriteCharacter sc in Object.FindObjectsByType<SpriteCharacter>(FindObjectsSortMode.None))
        {
            Unit unit = sc.GetComponent<Unit>();
            if (unit == null || !NeedsArt(sc.sprite, redrawGenerated)) continue;
            Undo.RecordObject(sc, "bake player art");
            BakeCharacter(unit.unitName, sc.tint, out Sprite idle, out PoseSprites poses);
            sc.sprite = idle;
            if (sc.poses == null || sc.poses.IsEmpty || IsGenerated(sc.poses.slash)) sc.poses = poses;
            sc.tint = Color.white;
            EditorUtility.SetDirty(sc);
            players++;
        }

        DialogueManager dm = Object.FindAnyObjectByType<DialogueManager>();
        if (dm != null)
        {
            Undo.RecordObject(dm, "bake portraits");
            foreach (string speaker in AllSpeakers())
            {
                DialogueManager.SpeakerPortrait has = dm.speakerPortraits.Find(p => p != null && p.speaker == speaker && p.portrait != null);
                if (has != null && !NeedsArt(has.portrait, redrawGenerated)) continue;
                Color tint = new Color(0.45f, 0.5f, 0.6f);
                foreach (string g in AssetDatabase.FindAssets("t:EnemyData"))
                {
                    EnemyData e = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g));
                    if (e != null && e.enemyName == speaker) tint = e.tintColor;
                }
                Sprite live = Illustrator.Portrait(speaker, tint);
                if (live == null) continue;
                Sprite baked = SavePng(live.texture, "Portraits", speaker, new Vector2(0.5f, 0.5f));
                dm.speakerPortraits.RemoveAll(p => p == null || p.speaker == speaker);
                dm.speakerPortraits.Add(new DialogueManager.SpeakerPortrait { speaker = speaker, portrait = baked });
                portraits++;
            }
            EditorUtility.SetDirty(dm);
        }

        if (players + portraits > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        string msg = $"태엽판 {cards}장 · 적 {enemies}종 · 플레이어 {players} · 초상화 {portraits}개를 그렸습니다.\n저장 위치: {Root}" +
                     (players + portraits > 0 ? "\n\n씬이 바뀌었으니 저장(Ctrl+S)하세요." : "");
        EditorUtility.DisplayDialog("일러스트 굽기", msg, "확인");
    }

    /// <summary>그림이 비었거나, '다시 굽기'인데 자동으로 구운 그림이면 새로 그린다</summary>
    private static bool NeedsArt(Object art, bool redrawGenerated)
    {
        return art == null || (redrawGenerated && IsGenerated(art));
    }

    private static bool IsGenerated(Object art)
    {
        return art != null && AssetDatabase.GetAssetPath(art).StartsWith(Root);
    }

    private static void BakeCharacter(string name, Color tint, out Sprite idle, out PoseSprites poses)
    {
        Illustrator.CharacterArt art = Illustrator.Character(name, tint);
        poses = new PoseSprites();
        idle = null;
        foreach (KeyValuePair<Illustrator.Pose, Texture2D> kv in art.textures)
        {
            Sprite s = SavePng(kv.Value, "Characters", $"{name}_{kv.Key}", new Vector2(0.5f, 0f));
            switch (kv.Key)
            {
                case Illustrator.Pose.Idle:   idle = s; break;
                case Illustrator.Pose.Slash:  poses.slash = s; break;
                case Illustrator.Pose.Thrust: poses.thrust = s; break;
                case Illustrator.Pose.Blunt:  poses.blunt = s; break;
                case Illustrator.Pose.Guard:  poses.guard = s; break;
                case Illustrator.Pose.Evade:  poses.evade = s; break;
                case Illustrator.Pose.Hit:    poses.hit = s; break;
            }
        }
    }

    /// <summary>스토리 · 챕터에 나오는 화자 이름 전부 (나레이션 '―'과 '???'는 Illustrator가 걸러냄)</summary>
    private static IEnumerable<string> AllSpeakers()
    {
        HashSet<string> names = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ChapterData"))
        {
            ChapterData ch = AssetDatabase.LoadAssetAtPath<ChapterData>(AssetDatabase.GUIDToAssetPath(guid));
            if (ch == null) continue;
            foreach (List<DialogueLine> lines in new[] { ch.introLines, ch.outroLines })
            {
                if (lines == null) continue;
                foreach (DialogueLine l in lines) if (l != null && !string.IsNullOrEmpty(l.speaker)) names.Add(l.speaker);
            }
        }
        return names;
    }

    /// <summary>텍스처를 PNG로 저장하고 픽셀 아트용 스프라이트로 불러온다 (pivot: 0~1)</summary>
    private static Sprite SavePng(Texture2D tex, string folder, string name, Vector2 pivot, FilterMode filter = FilterMode.Point)
    {
        string dir = $"{Root}/{folder}";
        Directory.CreateDirectory(dir);
        if (string.IsNullOrWhiteSpace(name)) name = "이름없음";
        foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
        string path = $"{dir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"일러스트 굽기: {path}를 가져오지 못해 건너뜁니다.");
            return null;
        }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = filter;   // 캐릭터 = Point (픽셀 그대로), 카드 그림 = Bilinear (부드럽게)
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 32f;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
