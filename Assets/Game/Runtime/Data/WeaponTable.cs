using System;
using Framework;
using UnityEngine;

[Serializable]
public class WeaponRow : ITableRow
{
    public int Index;
    public int Primary;
    public int SkillQ;
    public int SkillE;
    public int Model;
    public float HarvestWood;
    public float HarvestStone;
    public float HarvestPlant;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>무기 → 평타/무기스킬 어빌리티와 채집 배율.</summary>
[CreateAssetMenu(menuName = "Game/Tables/WeaponTable")]
public class WeaponTable : TableAsset<WeaponRow>
{
}
