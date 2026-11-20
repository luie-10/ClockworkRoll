using UnityEditor;
using UnityEngine;

/// <summary>메뉴 [ClockworkRoll → 홍보영상 촬영] — 플레이 모드로 들어가 TrailerDirector가 장면을 연기하며 MP4로 녹화한다.</summary>
public static class TrailerMenu
{
    [MenuItem("ClockworkRoll/홍보영상 촬영 (Recordings 폴더에 MP4)")]
    private static void Record()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("홍보영상 촬영", "플레이 모드를 끈 다음에 다시 눌러 주세요.", "확인");
            return;
        }
        if (!EditorUtility.DisplayDialog("홍보영상 촬영",
                "Game 창 해상도를 1920x1080(Full HD)으로 맞춰 두면 그 크기로 찍힙니다.\n" +
                "촬영이 끝나면 플레이 모드가 저절로 꺼지고 Recordings 폴더가 열립니다 (약 75초 분량, 촬영은 더 오래 걸릴 수 있음).\n\n촬영할까요?",
                "촬영 시작", "취소"))
            return;
        PlayerPrefs.SetInt(TrailerDirector.RequestKey, 1);
        PlayerPrefs.Save();
        EditorApplication.EnterPlaymode();
    }
}
