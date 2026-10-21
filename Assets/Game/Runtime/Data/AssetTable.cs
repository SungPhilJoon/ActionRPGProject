using System;
using Framework;
using UnityEngine;

[Serializable]
public class AssetRow : ITableRow
{
    public int Index;
    public string Address;
    public EAssetKind Kind;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>Addressables 주소. 다른 테이블은 이 Index 로 에셋을 가리킨다.</summary>
[CreateAssetMenu(menuName = "Game/Tables/AssetTable")]
public class AssetTable : TableAsset<AssetRow>
{
}
