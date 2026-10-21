using System;
using Framework;
using UnityEngine;

[Serializable]
public class TagRow : ITableRow
{
    public ETag Index;
    public ETag Parent;

    public int GetKey()
    {
        return (int)Index;
    }
}

/// <summary>게임플레이 태그 계층 (자식 → 부모).</summary>
[CreateAssetMenu(menuName = "Game/Tables/TagTable")]
public class TagTable : TableAsset<TagRow>
{
}
