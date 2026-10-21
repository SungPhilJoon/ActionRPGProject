using System;
using Framework;
using UnityEngine;

[Serializable]
public class CharacterRow : ITableRow
{
    public int Index;
    public int NameId;
    public int Prefab;
    public ETeam Team;
    public float MaxHealth;
    public float PhysicalPower;
    public float SpellPower;
    public float PhysicalResist;
    public float SpellResist;
    public float MoveSpeed;
    public float Level;
    public int[] Abilities;
    public EAIType AI;
    public float AggroRange;
    public float AttackRange;
    public EBloodType BloodType;
    public float BloodQuality;
    public bool IsVBlood;
    public int[] VBloodUnlocks;
    public int Loot;
    public float Scale;
    public Color Tint;
    public int Weapon;
    /// <summary>몬스터 AI StateTree 에셋 (AssetTable 인덱스, Kind=Data).</summary>
    public int StateTree;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>캐릭터(플레이어/몬스터) 기본 능력치.</summary>
[CreateAssetMenu(menuName = "Game/Tables/CharacterTable")]
public class CharacterTable : TableAsset<CharacterRow>
{
}
