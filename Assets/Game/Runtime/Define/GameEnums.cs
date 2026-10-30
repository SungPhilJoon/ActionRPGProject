/// <summary>
/// 게임 전역 열거형. CSV 에는 열거형 "이름" 으로 적고, 임포트 시 int 로 변환되어 런타임은 int 로만 비교한다.
/// 값은 데이터에 저장되므로 중간 삽입 없이 뒤에 추가한다.
/// </summary>

/// <summary>게임플레이 태그 (0~127). 계층은 TagTable.csv 의 Parent 로 정의한다.</summary>
public enum ETag
{
    None = -1,

    State = 0,
    State_Dead = 1,
    State_Debuff = 2,
    State_Debuff_Stun = 3,
    State_Debuff_Slow = 4,
    State_Debuff_Bleed = 5,
    State_Debuff_Ignite = 6,
    State_Debuff_SunBurn = 7,
    State_Buff = 8,
    State_Buff_Shield = 9,
    State_Buff_Empower = 10,
    State_Invulnerable = 11,
    State_Casting = 12,
    State_Attacking = 13,
    State_Dashing = 14,
    State_InSunlight = 15,
    State_Feeding = 16,
    State_Buff_Haste = 17,
    State_Debuff_Silence = 18,
    State_Buff_SunResist = 19,

    Ability = 20,
    Ability_Primary = 21,
    Ability_Weapon = 22,
    Ability_Spell = 23,
    Ability_Travel = 24,
    Ability_Ultimate = 25,
    Ability_Feed = 26,
    Ability_Monster = 27,

    Event = 40,
    Event_Hit = 41,
    Event_Kill = 42,
    Event_Damaged = 43,

    Cheat = 60,
    Cheat_God = 61,
    Cheat_NoCooldown = 62,

    // V Rising 상태이상 (TagTable.csv 계층: Frozen 은 Stun 의 자식이라 기절과 같은 행동 불가를 자동으로 공유한다)
    /// <summary>Immaterial: 지정형 공격(근접 부채꼴, 발사체, 대시 관통)이 통과한다. 범위(원형) 공격은 맞는다.</summary>
    State_Immaterial = 70,
    /// <summary>Leech: 물리 피해를 받으면 공격자가 피해의 LeechHealPercent% 회복.</summary>
    State_Debuff_Leech = 71,
    /// <summary>Chill: 이동속도 감소. Ice Nova 등이 소모하면 Freeze.</summary>
    State_Debuff_Chill = 72,
    /// <summary>Freeze: 이동·행동 불가 (State_Debuff_Stun 의 자식). 체력이 깎이면 풀린다.</summary>
    State_Debuff_Stun_Frozen = 73,
    /// <summary>Static: 물리 피해를 받을 때 StaticBonusPercent% 추가 피해. Ball Lightning 등이 소모하면 기절.</summary>
    State_Debuff_Static = 74,
    /// <summary>Condemn: 받는 피해 +CondemnDamageTakenPercent%.</summary>
    State_Debuff_Condemn = 75,
    /// <summary>Weaken: 주는 피해 −WeakenDamageDealtPercent%.</summary>
    State_Debuff_Weaken = 76,
    /// <summary>Guard: 방어 어빌리티 시전 중 (GAS Tool 방어 템플릿의 GE 가 부여). StateTree 조건/다른 어빌리티의 차단에 사용.</summary>
    State_Buff_Guard = 77,
}

/// <summary>속성 인덱스. AttributeSet 배열 인덱스로 그대로 쓴다.</summary>
public enum EAttribute
{
    Health = 0,
    MaxHealth = 1,
    PhysicalPower = 2,
    SpellPower = 3,
    PhysicalResist = 4,
    SpellResist = 5,
    MoveSpeed = 6,
    AttackSpeed = 7,
    CritChance = 8,
    CritDamage = 9,
    Shield = 10,
    SunResist = 11,
    GearLevel = 12,
    CooldownReduction = 13,
    HealthRegen = 14,
    LifeLeech = 15,
    Level = 16,
    BloodPool = 17,
    MaxBloodPool = 18,
    ResourceYield = 19,
    /// <summary>메타 속성: 피해 계산 결과를 담아 PostGameplayEffectExecute 에서 Shield/Health 로 분배.</summary>
    IncomingDamage = 20,
    /// <summary>메타 속성: 회복량.</summary>
    IncomingHeal = 21,
    Count = 22,
}

/// <summary>SetByCaller 키 (언리얼 SetByCaller Tag 대응).</summary>
public enum ESetByCaller
{
    None = 0,
    Damage = 1,
    Coefficient = 2,
    DamageType = 3,
    Heal = 4,
    Cooldown = 5,
    Cost = 6,
    Duration = 7,
    Magnitude = 8,
    /// <summary>DamageExecution 이 치명타 여부를 PostGameplayEffectExecute 로 넘길 때 사용 (1 = 치명타).</summary>
    Critical = 9,
    /// <summary>Execution 이 계산한 경감 전 피해 (전투 로그용).</summary>
    RawDamage = 10,
}

public enum EExecution
{
    None = 0,
    Damage = 1,
    Heal = 2,
    SunDamage = 3,
    PercentHeal = 4,
    /// <summary>최대 체력 비율 고정 피해 (SetByCaller.Magnitude % × Period).</summary>
    PercentDamage = 5,
}

public enum EDamageType
{
    Physical = 0,
    Spell = 1,
    Sun = 2,
    True = 3,
}

public enum ETeam
{
    Neutral = 0,
    Player = 1,
    Monster = 2,
}

public enum EAbilityType
{
    None = 0,
    MeleeCombo = 1,
    Projectile = 2,
    Dash = 3,
    AreaBurst = 4,
    SelfBuff = 5,
    Feed = 6,
    Leap = 7,
    /// <summary>GAS Tool 에서 만든 AbilityGraphAsset 을 실행 (언리얼 Blueprint 어빌리티 방식). AbilityRow.Graph 가 에셋.</summary>
    Graph = 8,
}

public enum EAbilitySlot
{
    None = -1,
    Primary = 0,
    WeaponQ = 1,
    WeaponE = 2,
    Travel = 3,
    Spell1 = 4,
    Spell2 = 5,
    Ultimate = 6,
    Feed = 7,
    Count = 8,
}

public enum EAIType
{
    None = 0,
    Melee = 1,
    Ranged = 2,
    Boss = 3,
    Passive = 4,
}

public enum EBloodType
{
    None = 0,
    Warrior = 1,
    Rogue = 2,
    Scholar = 3,
    Brute = 4,
    Creature = 5,
}

public enum EItemType
{
    Resource = 0,
    Weapon = 1,
    Armor = 2,
    Consumable = 3,
}

public enum EEquipSlot
{
    None = -1,
    Weapon = 0,
    Chest = 1,
    Legs = 2,
    Boots = 3,
    Gloves = 4,
    Count = 5,
}

public enum EStation
{
    None = 0,
    Hand = 1,
    Workbench = 2,
    Smelter = 3,
}

public enum EStructureType
{
    CastleHeart = 0,
    Floor = 1,
    Wall = 2,
    Station = 3,
    Coffin = 4,
    Decoration = 5,
}

public enum EHarvestType
{
    None = 0,
    Wood = 1,
    Stone = 2,
    Plant = 3,
}

public enum ESpawnKind
{
    Monster = 0,
    Resource = 1,
    Merchant = 2,
    PlayerStart = 3,
}

/// <summary>퀘스트 목표 종류 (QuestTable.Objective). Target 의 의미는 종류마다 다르다 — QuestTable.cs 참고.</summary>
public enum EQuestObjective
{
    None = 0,
    Move = 1,
    UseAbility = 2,
    Buy = 3,
    Gather = 4,
    Craft = 5,
    Equip = 6,
    Build = 7,
    Kill = 8,
    Feed = 9,
    GearLevel = 10,
}

/// <summary>퀘스트 묶음 (HUD 추적기 머리말).</summary>
public enum EQuestChapter
{
    Tutorial = 0,
    Hunt = 1,
    Boss = 2,
}

/// <summary>아이템을 얻은 경로 (ItemObtainedEvent). 퀘스트 목표가 채집/제작/상점을 구분한다.</summary>
public enum EItemSource
{
    None = 0,
    Harvest = 1,
    Craft = 2,
    Shop = 3,
    Loot = 4,
    Reward = 5,
}

/// <summary>AnimationTable.Parameters 의 Animator 파라미터 종류.</summary>
public enum EAnimParamType
{
    Trigger = 0,
    Bool = 1,
    Float = 2,
    Int = 3,
}

/// <summary>
/// 에디터 AnimatorController 빌더의 액션 상태/트리거 슬롯 (이름 = 트리거 이름).
/// 런타임 재생은 이 값이 아니라 AnimationTable Index 로 한다.
/// </summary>
public enum EAnim
{
    None = 0,
    Attack1 = 1,
    Attack2 = 2,
    Cast = 3,
    Interact = 4,
    PickUp = 5,
    Kick = 6,
    Attack3 = 7,
    /// <summary>몬스터 특수 공격 (돌진/도약 등).</summary>
    Special1 = 8,
    Dodge = 9,
    Hit = 10,
    /// <summary>건축 (Crafting 팩 망치질).</summary>
    Build = 11,
    /// <summary>채집: 나무 베기.</summary>
    Chop = 12,
    /// <summary>채집: 돌 캐기.</summary>
    Mine = 13,
    /// <summary>채집: 식물 줍기.</summary>
    Gather = 14,
}

/// <summary>AssetTable 의 에셋 종류. 프리로드 시 어떤 타입으로 로드할지 결정한다.</summary>
public enum EAssetKind
{
    Prefab = 0,
    Sprite = 1,
    Material = 2,
    /// <summary>ScriptableObject 데이터 (StateTree 등).</summary>
    Data = 3,
}

/// <summary>
/// UI 프리팹의 AssetTable Index (주소 = Content/UI 아래 경로). UI 프리팹 빌더(Game > Setup > 3b)가 만들고,
/// GameBootstrap 이 Addressables 로 로드해 Instantiate 한다. 값은 AssetTable.csv 와 같아야 한다.
/// </summary>
public enum EUIAsset
{
    LayerCanvas = 400,
    HudWindow = 401,
    InventoryWindow = 402,
    CraftWindow = 403,
    BuildWindow = 404,
    ShopWindow = 405,
    BalanceOverlayWindow = 406,
    CheatConsoleWindow = 407,
    TitleWindow = 408,
    WorldHealthBarLayer = 409,
    DamageNumberLayer = 410,
    ListRow = 411,
    ItemSlot = 412,
    WorldHealthBar = 413,
    DamageNumber = 414,
}

/// <summary>몬스터 StateTree 블랙보드 키 (MonsterPerceptionEvaluator 가 채운다).</summary>
[Framework.StateTree.StateTreeBlackboardEnum]
public enum EBBKey
{
    HasTarget = 1,
    TargetDistance = 2,
    HomeDistance = 3,
    HealthRatio = 4,
    TargetHealthRatio = 5,
    TimeInCombat = 6,
    IsInvulnerable = 7,
}

/// <summary>몬스터 StateTree 이벤트 (OnEvent 트랜지션에서 선택).</summary>
[Framework.StateTree.StateTreeEventEnum]
public enum EAIEvent
{
    None = 0,
    Damaged = 1,
    TargetAcquired = 2,
    TargetLost = 3,
    AbilityEnded = 4,
}

/// <summary>입력 액션 인덱스. 바인딩 문자열은 InputTable.csv 에 있다.</summary>
public enum EInputAction
{
    Move = 0,
    Primary = 1,
    WeaponQ = 2,
    WeaponE = 3,
    Travel = 4,
    Spell1 = 5,
    Spell2 = 6,
    Ultimate = 7,
    Feed = 8,
    Interact = 9,
    Inventory = 10,
    Build = 11,
    Craft = 12,
    Shop = 13,
    Cheat = 14,
    Cancel = 15,
    RotateCamera = 16,
    Zoom = 17,
    Point = 18,
    BalanceOverlay = 19,
    RotateStructure = 20,
    Submit = 21,
    Count = 22,
}

/// <summary>ConstTable.csv 의 인덱스.</summary>
public enum EConst
{
    DayLengthSeconds = 1,
    NightRatio = 2,
    StartHour = 3,
    SunDamagePercentPerSec = 4,
    SunDamageRampSeconds = 5,
    SunMinElevation = 6,
    BloodDrainPerSecond = 7,
    BloodEmptyDamagePercent = 8,
    FeedHealthRatio = 9,
    FeedDuration = 10,
    LevelDamageScalePerLevel = 11,
    LevelDamageScaleMin = 12,
    LevelDamageScaleMax = 13,
    CastleHeartRadius = 14,
    BuildGridSize = 15,
    RespawnSeconds = 16,
    InventorySize = 17,
    PlayerCharacterId = 18,
    InteractRange = 19,
    CameraPitch = 20,
    CameraDistance = 21,
    CameraYaw = 22,
    CritBaseMultiplier = 23,
    StartingWeaponItem = 24,
    UnarmedWeaponId = 25,
    MonsterLeashRange = 26,
    MaxPartySize = 27,
    LeechHealPercent = 28,
    StaticBonusPercent = 29,
    CondemnDamageTakenPercent = 30,
    WeakenDamageDealtPercent = 31,
    ConsumeMinAgeSeconds = 32,
    /// <summary>건축 시 재생할 AnimationTable Index.</summary>
    BuildAnim = 33,
    /// <summary>상호작용 시 재생할 AnimationTable Index.</summary>
    InteractAnim = 34,
    /// <summary>게임 시작 시 받는 첫 퀘스트 (QuestTable Index). 0 이면 퀘스트 없음.</summary>
    FirstQuestId = 35,
}

/// <summary>StringTable.csv 의 고정 UI 문자열 인덱스. 데이터 행(아이템 이름 등)은 각 테이블의 NameId 로 참조한다.</summary>
public enum EText
{
    GameTitle = 1,
    SinglePlayer = 2,
    MultiplayerSoon = 3,
    Quit = 4,
    Inventory = 10,
    Equipment = 11,
    Crafting = 12,
    Build = 13,
    Shop = 14,
    Free = 15,
    Buy = 16,
    Craft = 17,
    Close = 18,
    GearLevel = 19,
    Blood = 20,
    Day = 21,
    Night = 22,
    InSunlight = 23,
    InShadow = 24,
    PressToInteract = 25,
    PressToFeed = 26,
    YouDied = 27,
    Respawning = 28,
    NotEnoughMaterials = 29,
    NeedCastleHeart = 30,
    VBloodConsumed = 31,
    AbilityUnlocked = 32,
    InventoryFull = 33,
    CheatTitle = 34,
    CheatHint = 35,
    BalanceTitle = 36,
    Requires = 37,
    Owned = 38,
    Place = 39,
    Rotate = 40,
    BuildHint = 41,
    Merchant = 42,
    BloodTypeNone = 43,
    Quality = 44,
    Controls = 45,
    Loading = 46,
    BalanceDealt = 47,
    BalanceTaken = 48,
    BalanceRecent = 49,
    Quest = 50,
    QuestComplete = 51,
    QuestNew = 52,
    QuestAllComplete = 53,
    QuestNearest = 54,
    QuestVictory = 55,
    QuestReward = 56,
    QuestChapterTutorial = 57,
    QuestChapterHunt = 58,
    QuestChapterBoss = 59,
}

/// <summary>UI 캔버스 정렬 순서 = 레이어 번호.</summary>
public static class UILayerId
{
    public const int World = 0;
    public const int Hud = 10;
    public const int Window = 20;
    public const int Popup = 30;
    public const int System = 40;
}

/// <summary>Physics 레이어 번호 (ProjectSettings/TagManager 와 일치해야 한다. 에디터 셋업이 맞춰 준다).</summary>
public static class LayerId
{
    public const int Default = 0;
    public const int Player = 8;
    public const int Monster = 9;
    public const int Ground = 10;
    public const int Interactable = 11;
    public const int Structure = 12;
    public const int SunBlocker = 13;
    public const int Projectile = 14;

    public const int ActorMask = (1 << Player) | (1 << Monster);
    public const int GroundMask = (1 << Ground) | (1 << Structure);
    public const int SunBlockMask = (1 << Default) | (1 << Ground) | (1 << Structure) | (1 << SunBlocker) | (1 << Interactable);
    public const int ProjectileBlockMask = (1 << Ground) | (1 << Structure) | (1 << Interactable);
    /// <summary>카메라 가림 판정 (나무 그늘 콜라이더 = SunBlocker, 건물/바위 = Default, 건축물, 채집 노드). CameraOccluder 가 붙은 것만 숨긴다.</summary>
    public const int CameraOcclusionMask = (1 << Default) | (1 << Structure) | (1 << SunBlocker) | (1 << Interactable);
}
