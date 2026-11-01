using System;
using UnityEngine;

/// <summary>
/// 합 중에 바뀌는 캐릭터 그림 (포즈) 묶음.
/// 비워둔 칸은 기본 그림(Sprite)을 그대로 쓰되, 몸을 기울이고 늘이는 임시 동작으로 대신 보여준다.
/// 그림은 기본 그림과 같은 Pixels Per Unit · 같은 방향(오른쪽을 보는 그림)으로 만들면 크기가 맞는다.
///
/// 쓰이는 곳: Player의 SpriteCharacter > Poses, 적 에셋(EnemyData) > Poses
/// </summary>
[Serializable]
public class PoseSprites
{
    [Tooltip("참격 주사위를 굴릴 때 (베는 자세)")]
    public Sprite slash;
    [Tooltip("관통 주사위를 굴릴 때 (찌르는 자세)")]
    public Sprite thrust;
    [Tooltip("타격 주사위를 굴릴 때 (내려치는 자세)")]
    public Sprite blunt;
    [Tooltip("가드 주사위를 굴릴 때 (막는 자세)")]
    public Sprite guard;
    [Tooltip("회피 주사위를 굴릴 때 (피하는 자세)")]
    public Sprite evade;
    [Tooltip("공격을 맞았을 때")]
    public Sprite hit;
    [Tooltip("쓰러졌을 때")]
    public Sprite down;

    /// <summary>포즈 이름(Slash/Thrust/Blunt/Guard/Evade/Hit/Down)에 해당하는 그림. 없으면 null.</summary>
    public Sprite Get(string pose)
    {
        switch (pose)
        {
            case "Slash":  return slash;
            case "Thrust": return thrust;
            case "Blunt":  return blunt;
            case "Guard":  return guard;
            case "Evade":  return evade;
            case "Hit":    return hit;
            case "Down":   return down;
            default:       return null;
        }
    }
}
