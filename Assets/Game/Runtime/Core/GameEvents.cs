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
