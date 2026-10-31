using Framework;
using UnityEngine;

/// <summary>
/// 게임 이벤트 페이로드 모음. EventBus&lt;T&gt; 로 발행/구독한다.
/// 게임플레이 → UI/연출 방향으로만 흐르며, 게임플레이 코드는 UI 를 모른다.
/// </summary>
public struct DamageEvent : IGameEvent
{
    public Actor Source;
    public Actor Target;
    public int AbilityId;
    public float Amount;
    public float Absorbed;
    public bool IsCritical;
    public EDamageType DamageType;
    public Vector3 Position;
}

public struct HealEvent : IGameEvent
{
    public Actor Target;
    public float Amount;
    public Vector3 Position;
}

/// <summary>건축물 설치 완료. Bounds = 설치된 건축물의 월드 바운드 (식생 제거 등에 쓴다).</summary>
public struct StructurePlacedEvent : IGameEvent
{
    public Structure Structure;
    public Bounds Bounds;
}

public struct ActorDiedEvent : IGameEvent
{
    public Actor Victim;
    public Actor Killer;
}

public struct ActorSpawnedEvent : IGameEvent
{
    public Actor Actor;
}

public struct PlayerSpawnedEvent : IGameEvent
{
    public PlayerCharacter Player;
}

public struct InventoryChangedEvent : IGameEvent
{
    public Inventory Inventory;
}

public struct EquipmentChangedEvent : IGameEvent
{
    public Equipment Equipment;
}

public struct AbilitySlotsChangedEvent : IGameEvent
{
    public Actor Actor;
}

/// <summary>화면 중앙 토스트 메시지. TextId 는 StringTable 인덱스, Arg 는 부가 문자열(아이템 이름 등).</summary>
public struct NotifyEvent : IGameEvent
{
    public int TextId;
    public string Arg;
    public Color Color;
}

public struct BloodChangedEvent : IGameEvent
{
    public EBloodType BloodType;
    public float Quality;
}

public struct VBloodConsumedEvent : IGameEvent
{
    public int CharacterId;
    public int[] Unlocked;
}

public struct BossEngagedEvent : IGameEvent
{
    public Actor Boss;
    public bool Engaged;
}

public struct InteractTargetChangedEvent : IGameEvent
{
    public IInteractable Target;
}

/// <summary>플레이어가 아이템을 얻음 (채집 · 제작 · 상점 · 보상). 인벤토리 변경과 달리 "어디서 몇 개" 를 알려 준다 (퀘스트 목표용).</summary>
public struct ItemObtainedEvent : IGameEvent
{
    public PlayerCharacter Player;
    public int ItemId;
    public int Count;
    public EItemSource Source;
}

/// <summary>플레이어가 어빌리티를 발동함 (콤보 다음 단계 입력은 새 발동이 아니라서 세지 않는다).</summary>
public struct PlayerAbilityUsedEvent : IGameEvent
{
    public PlayerCharacter Player;
    public EAbilitySlot Slot;
    public int AbilityId;
}

/// <summary>플레이어가 흡혈을 마침 (대상은 곧 처치된다).</summary>
public struct PlayerFedEvent : IGameEvent
{
    public PlayerCharacter Player;
    public int CharacterId;
}

/// <summary>현재 퀘스트가 바뀌거나 진행도가 올라감. Quest 가 null 이면 진행 중인 퀘스트 없음.</summary>
public struct QuestChangedEvent : IGameEvent
{
    public QuestRow Quest;
    public int Progress;
    public bool Completed;
}

/// <summary>언어가 바뀌고 새 문자열 테이블이 준비됨 (Unity Localization). 문구를 캐시한 UI 는 다시 채운다.</summary>
public struct LanguageChangedEvent : IGameEvent
{
}

/// <summary>마지막 퀘스트(보스)까지 모두 끝남.</summary>
public struct QuestsCompletedEvent : IGameEvent
{
}
