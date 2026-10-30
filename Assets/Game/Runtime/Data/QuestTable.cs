using System;
using Framework;
using UnityEngine;

/// <summary>
/// 퀘스트 한 개 = 목표 한 개. Next 로 다음 퀘스트를 이어 한 줄짜리 체인을 만든다 (0 이면 마지막).
/// Target 의 의미 (0 = 아무거나):
///  - Move: 없음 (Count = 이동 거리 m)
///  - UseAbility: EAbilitySlot
///  - Buy / Gather / Craft: ItemTable Index (Gather 는 얻은 개수, Buy/Craft 는 횟수를 센다)
///  - Equip: EEquipSlot (그 칸에 무엇이든 장착돼 있으면 완료)
///  - Build: StructureTable Index
///  - Kill / Feed: CharacterTable Index
///  - GearLevel: 없음 (Count = 목표 기어 레벨)
/// Key 는 HUD 에 [키] 로 붙여 보여줄 입력 (튜토리얼 안내용, 없으면 Count 로 둔다).
/// </summary>
[Serializable]
public class QuestRow : ITableRow
{
    public int Index;
    public EQuestChapter Chapter;
    public int TitleId;
    public int DescId;
    public EQuestObjective Objective;
    public int Target;
    public int Count;
    public EInputAction Key = EInputAction.Count;
    public int RewardItem;
    public int RewardCount;
    public int Next;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>퀘스트 체인 (튜토리얼 → 사냥 → 보스). QuestManager 가 진행한다.</summary>
[CreateAssetMenu(menuName = "Game/Tables/QuestTable")]
public class QuestTable : TableAsset<QuestRow>
{
}
