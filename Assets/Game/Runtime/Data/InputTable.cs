using System;
using Framework;
using UnityEngine;

[Serializable]
public class InputRow : ITableRow
{
    public EInputAction Index;
    public string Binding1;
    public string Binding2;
    public string Composite;

    public int GetKey()
    {
        return (int)Index;
    }
}

/// <summary>입력 바인딩 (Input System 경로). 키 변경은 CSV 수정만으로 끝난다.</summary>
[CreateAssetMenu(menuName = "Game/Tables/InputTable")]
public class InputTable : TableAsset<InputRow>
{
}
