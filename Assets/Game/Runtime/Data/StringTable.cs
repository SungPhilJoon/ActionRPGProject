using System;
using Framework;
using UnityEngine;

[Serializable]
public class StringRow : ITableRow
{
    public int Index;
    public string Text;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>표시 문자열. 코드는 int 인덱스로만 참조한다.</summary>
[CreateAssetMenu(menuName = "Game/Tables/StringTable")]
public class StringTable : TableAsset<StringRow>
{
}
