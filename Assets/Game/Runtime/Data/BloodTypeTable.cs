using System;
using Framework;
using UnityEngine;

[Serializable]
public class BloodTypeRow : ITableRow
{
    public EBloodType Index;
    public int NameId;
    public Color Color;
    public float[] Thresholds;
    public int[] TierEffects;

    public int GetKey()
    {
        return (int)Index;
    }
}

/// <summary>혈액 종류. 품질이 Thresholds[i] 이상이면 TierEffects[i] 를 부여.</summary>
[CreateAssetMenu(menuName = "Game/Tables/BloodTypeTable")]
public class BloodTypeTable : TableAsset<BloodTypeRow>
{
}
