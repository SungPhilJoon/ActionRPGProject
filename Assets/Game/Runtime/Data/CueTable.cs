using System;
using Framework;
using UnityEngine;

[Serializable]
public class CueRow : ITableRow
{
    public int Index;
    public int Asset;
    public float Lifetime;
    public bool Attach;
    public float Scale;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>게임플레이 큐 → 이펙트 프리팹.</summary>
[CreateAssetMenu(menuName = "Game/Tables/CueTable")]
public class CueTable : TableAsset<CueRow>
{
}
