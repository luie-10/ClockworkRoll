using System;
using UnityEngine;

/// <summary>공격 종류별 받는 피해 배율 (내성)</summary>
public enum Resistance
{
    Normal,   // 보통 x1.0
    Weak,     // 약점 x1.5
    Fatal     // 취약 x2.0
}

/// <summary>
/// 캐릭터 1명의 내성 — 참격 / 관통 / 타격 공격을 맞을 때 피해가 몇 배가 되는가.
/// 적은 EnemyData에서, 플레이어는 씬의 Player(Unit) 인스펙터에서 정한다.
/// </summary>
[Serializable]
public class ResistanceSet
{
    public Resistance slash = Resistance.Normal;
    public Resistance thrust = Resistance.Normal;
    public Resistance blunt = Resistance.Normal;

    public ResistanceSet() { }

    public ResistanceSet(Resistance slash, Resistance thrust, Resistance blunt)
    {
        this.slash = slash;
        this.thrust = thrust;
        this.blunt = blunt;
    }

    public ResistanceSet Clone()
    {
        return new ResistanceSet(slash, thrust, blunt);
    }

    /// <summary>공격 종류에 해당하는 내성 (가드/회피는 공격이 아니므로 보통)</summary>
    public Resistance Get(DiceDetail detail)
    {
        switch (detail)
        {
            case DiceDetail.Slash:  return slash;
            case DiceDetail.Thrust: return thrust;
            case DiceDetail.Blunt:  return blunt;
            default:                return Resistance.Normal;
        }
    }

    public static float Multiplier(Resistance r)
    {
        switch (r)
        {
            case Resistance.Weak:  return 1.5f;
            case Resistance.Fatal: return 2.0f;
            default:               return 1.0f;
        }
    }

    public static string Label(Resistance r)
    {
        switch (r)
        {
            case Resistance.Weak:  return "약점";
            case Resistance.Fatal: return "취약";
            default:               return "보통";
        }
    }

    /// <summary>표시용 색 (보통 회색 / 약점 주황 / 취약 빨강)</summary>
    public static string ColorHex(Resistance r)
    {
        switch (r)
        {
            case Resistance.Weak:  return "#ffb04a";
            case Resistance.Fatal: return "#ff4a4a";
            default:               return "#c8c8c8";
        }
    }
}
