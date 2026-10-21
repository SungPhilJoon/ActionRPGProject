using System;
using Framework;
using UnityEngine;

[Serializable]
public class ConstRow : ITableRow
{
    public int Index;
    public float Value;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>게임 상수 (EConst 인덱스).</summary>
[CreateAssetMenu(menuName = "Game/Tables/ConstTable")]
public class ConstTable : TableAsset<ConstRow>
{
}
