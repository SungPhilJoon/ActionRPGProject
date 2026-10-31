using System;
using Framework;
using UnityEngine;

[Serializable]
public class MapLabelRow : ITableRow
{
    public int Index;
    public int NameId;
    /// <summary>월드 좌표 (x;z).</summary>
    public Vector2 Position;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>월드맵에 쓰는 지역 이름.</summary>
[CreateAssetMenu(menuName = "Game/Tables/MapLabelTable")]
public class MapLabelTable : TableAsset<MapLabelRow>
{
}
