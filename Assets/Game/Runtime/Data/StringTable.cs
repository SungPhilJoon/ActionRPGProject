using System;
using Framework;
using UnityEngine;

[Serializable]
public class StringRow : ITableRow
{
    public int Index;
    /// <summary>영어 원문. Unity Localization 이 준비되기 전 · 에디터 툴의 미리보기에서 쓴다.</summary>
    public string Text;
    /// <summary>한국어. CSV → Unity Localization(GameText 컬렉션, ko) 동기화의 입력 (LocalizationSync). 런타임은 Localization 테이블에서 읽는다.</summary>
    public string Korean;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>
/// 표시 문자열. 코드는 int 인덱스로만 참조한다.
/// CSV(Index, Text=영어, Korean) 가 원본이고, 에디터가 Unity Localization String Table Collection(GameText) 의 en/ko 테이블로 옮긴다.
/// 키 Id = Index 라서 런타임 조회도 int 다 (GameLocalization).
/// </summary>
[CreateAssetMenu(menuName = "Game/Tables/StringTable")]
public class StringTable : TableAsset<StringRow>
{
}
