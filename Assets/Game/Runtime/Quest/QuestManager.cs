using Framework;
using UnityEngine;

/// <summary>
/// 퀘스트 진행 (QuestTable 의 한 줄짜리 체인: 튜토리얼 → 사냥 → 보스).
///  - 첫 플레이어가 스폰되면 EConst.FirstQuestId 부터 시작한다.
///  - 게임 이벤트(EventBus)를 구독해 현재 퀘스트의 목표만 센다. 이동 거리 · 기어 레벨처럼 연속 값은 Update 에서 본다.
///  - 장비 · 기어 레벨 목표는 "지금 상태" 를 보므로 시작할 때 이미 만족하면 바로 완료된다.
///  - 완료 → 보상 지급 → NextDelay 뒤 다음 퀘스트. Next 가 0 이면 QuestsCompletedEvent.
/// 게임플레이 코드는 이 매니저를 모른다 (이벤트만 발행). UI 는 QuestChangedEvent 를 받는다.
/// </summary>
public class QuestManager : IManager
{
    private const float NextDelay = 2.5f;
    /// <summary>한 프레임에 이보다 많이 움직이면 순간 이동(리스폰 · tp)으로 보고 이동 거리에 넣지 않는다.</summary>
    private const float TeleportThreshold = 3.0f;

    private Vector3 LastPosition;
    private bool HasLastPosition;
    /// <summary>이동 목표 누적 거리. 진행도는 정수 m 지만 프레임당 이동은 1m 미만이라 따로 둔다.</summary>
    private float MoveAccumulated;
    private float NextRemaining;

    public QuestRow Current { get; private set; }
    public int Progress { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsAllCompleted { get; private set; }

    public int Goal
    {
        get { return Current != null ? Mathf.Max(1, Current.Count) : 0; }
    }

    void IManager.Initialize()
    {
        EventBus<PlayerSpawnedEvent>.Subscribe(onPlayerSpawned);
        EventBus<PlayerAbilityUsedEvent>.Subscribe(onAbilityUsed);
        EventBus<ItemObtainedEvent>.Subscribe(onItemObtained);
        EventBus<EquipmentChangedEvent>.Subscribe(onEquipmentChanged);
        EventBus<StructurePlacedEvent>.Subscribe(onStructurePlaced);
        EventBus<ActorDiedEvent>.Subscribe(onActorDied);
        EventBus<PlayerFedEvent>.Subscribe(onFed);
    }

    void IManager.UnInitialize()
    {
        EventBus<PlayerSpawnedEvent>.Unsubscribe(onPlayerSpawned);
        EventBus<PlayerAbilityUsedEvent>.Unsubscribe(onAbilityUsed);
        EventBus<ItemObtainedEvent>.Unsubscribe(onItemObtained);
        EventBus<EquipmentChangedEvent>.Unsubscribe(onEquipmentChanged);
        EventBus<StructurePlacedEvent>.Unsubscribe(onStructurePlaced);
        EventBus<ActorDiedEvent>.Unsubscribe(onActorDied);
        EventBus<PlayerFedEvent>.Unsubscribe(onFed);
        Current = null;
    }

    void IManager.Update(float InDeltaTime)
    {
        if (Current == null)
        {
            return;
        }

        if (IsCompleted == true)
        {
            NextRemaining -= InDeltaTime;
            if (NextRemaining <= 0.0f)
            {
                startNext();
            }

            return;
        }

        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        switch (Current.Objective)
        {
            case EQuestObjective.Move:
                trackMove(player);
                break;
            case EQuestObjective.GearLevel:
                checkState(player);
                break;
        }
    }

    /// <summary>InQuestId 퀘스트를 처음부터 시작한다 (치트 · 테스트용으로도 쓴다). 없는 Index 면 false.</summary>
    public bool Begin(int InQuestId)
    {
        QuestRow row;
        if (InQuestId == 0 || Tables.Root.Quest == null || Tables.Root.Quest.TryGet(InQuestId, out row) == false)
        {
            return false;
        }

        Current = row;
        Progress = 0;
        IsCompleted = false;
        IsAllCompleted = false;
        HasLastPosition = false;
        MoveAccumulated = 0.0f;
        EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.QuestNew, Arg = Tables.Text(row.TitleId), Color = UIUtil.GoldColor });
        publishChanged();

        var player = PlayerCharacter.Local;
        if (player != null)
        {
            checkState(player);
        }

        return true;
    }

    /// <summary>현재 퀘스트를 바로 완료한다 (치트).</summary>
    public void CompleteCurrent()
    {
        if (Current == null || IsCompleted == true)
        {
            return;
        }

        addProgress(Goal);
    }

    /// <summary>현재 목표가 특정 몬스터를 노릴 때, 가장 가까운 살아 있는 대상까지의 거리 (HUD 안내용).</summary>
    public bool TryGetNearestTarget(Vector3 InFrom, out float OutDistance)
    {
        Vector3 position;
        return TryGetNearestTarget(InFrom, out position, out OutDistance);
    }

    /// <summary>위와 같고 대상 위치도 준다 (지도 퀘스트 표식).</summary>
    public bool TryGetNearestTarget(Vector3 InFrom, out Vector3 OutPosition, out float OutDistance)
    {
        OutDistance = 0.0f;
        OutPosition = Vector3.zero;
        if (Current == null || IsCompleted == true)
        {
            return false;
        }

        bool targetsMonster = Current.Objective == EQuestObjective.Kill || Current.Objective == EQuestObjective.Feed;
        if (targetsMonster == false)
        {
            return false;
        }

        var registry = Managers.Get<ActorRegistry>();
        if (registry == null)
        {
            return false;
        }

        float best = float.MaxValue;
        var actors = registry.All;
        for (int i = 0; i < actors.Count; ++i)
        {
            var actor = actors[i];
            if (actor == null || actor.IsDead == true || isTargetCharacter(actor) == false)
            {
                continue;
            }

            float distance = Vector3.Distance(InFrom, actor.transform.position);
            if (distance < best)
            {
                best = distance;
                OutPosition = actor.transform.position;
            }
        }

        if (best == float.MaxValue)
        {
            return false;
        }

        OutDistance = best;
        return true;
    }

    /// <summary>
    /// 퀘스트를 진행하러 갈 곳 (미니맵 · 월드맵 가이드, HUD 거리). 위치가 의미 없는 목표(이동 · 어빌리티 · 제작 · 장비 · 기어 · 성 심장)는 false.
    ///  - 처치 · 흡혈: 가장 가까운 살아 있는 대상 → 없으면(리스폰 대기) 그 몬스터가 나오는 SpawnTable 지점
    ///  - 상점: 가장 가까운 상인 (MapMarkers) → 없으면 SpawnTable 상인 지점
    ///  - 채집: 목표 아이템을 떨구는 채집 노드의 SpawnTable 지점 중 가장 가까운 곳
    ///  - 관 등 성 건축물(성 심장 제외): 성 심장 근처에만 지을 수 있으므로 지어 둔 성
    /// </summary>
    public bool TryGetGuidePosition(Vector3 InFrom, out Vector3 OutPosition)
    {
        OutPosition = Vector3.zero;
        if (Current == null || IsCompleted == true)
        {
            return false;
        }

        float distance;
        switch (Current.Objective)
        {
            case EQuestObjective.Kill:
            case EQuestObjective.Feed:
                if (TryGetNearestTarget(InFrom, out OutPosition, out distance) == true)
                {
                    return true;
                }

                return findNearestSpawn(InFrom, ESpawnKind.Monster, out OutPosition);
            case EQuestObjective.Buy:
                if (findNearestMarker(InFrom, EMapMarker.Merchant, out OutPosition) == true)
                {
                    return true;
                }

                return findNearestSpawn(InFrom, ESpawnKind.Merchant, out OutPosition);
            case EQuestObjective.Gather:
                return findNearestSpawn(InFrom, ESpawnKind.Resource, out OutPosition);
            case EQuestObjective.Build:
                StructureRow structure;
                bool needsHeart = Tables.Root.Structure.TryGet(Current.Target, out structure) == true && structure.RequiresHeart == true;
                return needsHeart == true && findNearestMarker(InFrom, EMapMarker.Castle, out OutPosition) == true;
            default:
                return false;
        }
    }

    private static bool findNearestMarker(Vector3 InFrom, EMapMarker InKind, out Vector3 OutPosition)
    {
        OutPosition = Vector3.zero;
        float best = float.MaxValue;
        var entries = MapMarkers.All;
        for (int i = 0; i < entries.Count; ++i)
        {
            if (entries[i].Kind != InKind || entries[i].Target == null)
            {
                continue;
            }

            float distance = Vector3.Distance(InFrom, entries[i].Target.position);
            if (distance < best)
            {
                best = distance;
                OutPosition = entries[i].Target.position;
            }
        }

        return best != float.MaxValue;
    }

    /// <summary>InKind 지점 중 현재 목표와 맞는 가장 가까운 곳 (몬스터 = 캐릭터, 채집 = 노드가 떨구는 아이템).</summary>
    private bool findNearestSpawn(Vector3 InFrom, ESpawnKind InKind, out Vector3 OutPosition)
    {
        OutPosition = Vector3.zero;
        float best = float.MaxValue;
        var rows = Tables.Root.Spawn.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            var row = rows[i];
            if (row.Kind != InKind || matchesSpawn(row) == false)
            {
                continue;
            }

            float distance = Vector3.Distance(InFrom, row.Position);
            if (distance < best)
            {
                best = distance;
                OutPosition = row.Position;
            }
        }

        return best != float.MaxValue;
    }

    private bool matchesSpawn(SpawnRow InRow)
    {
        switch (InRow.Kind)
        {
            case ESpawnKind.Monster:
                return matchesTarget(InRow.RefId);
            case ESpawnKind.Resource:
                ResourceNodeRow node;
                return Tables.Root.ResourceNode.TryGet(InRow.RefId, out node) == true && matchesTarget(node.DropItem);
            default:
                return true;
        }
    }

    // ------------------------------------------------------------------ flow

    private void startNext()
    {
        int next = Current.Next;
        if (next != 0 && Begin(next) == true)
        {
            return;
        }

        Current = null;
        IsCompleted = false;
        IsAllCompleted = true;
        publishChanged();
        EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.QuestAllComplete, Color = new Color(1.0f, 0.25f, 0.3f) });
        EventBus<QuestsCompletedEvent>.Publish(new QuestsCompletedEvent());
    }

    private void addProgress(int InAmount)
    {
        if (Current == null || IsCompleted == true || InAmount <= 0)
        {
            return;
        }

        Progress = Mathf.Min(Goal, Progress + InAmount);
        if (Progress >= Goal)
        {
            complete();
            return;
        }

        publishChanged();
    }

    private void setProgress(int InValue)
    {
        int value = Mathf.Clamp(InValue, 0, Goal);
        if (value == Progress)
        {
            return;
        }

        Progress = value;
        if (Progress >= Goal)
        {
            complete();
            return;
        }

        publishChanged();
    }

    private void complete()
    {
        IsCompleted = true;
        NextRemaining = NextDelay;
        EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.QuestComplete, Arg = Tables.Text(Current.TitleId), Color = new Color(0.55f, 0.95f, 0.55f) });
        giveReward(Current);
        publishChanged();
    }

    private void giveReward(QuestRow InQuest)
    {
        var player = PlayerCharacter.Local;
        if (player == null || InQuest.RewardItem == 0 || InQuest.RewardCount <= 0)
        {
            return;
        }

        int left = player.Inventory.Add(InQuest.RewardItem, InQuest.RewardCount);
        EventBus<ItemObtainedEvent>.Publish(new ItemObtainedEvent { Player = player, ItemId = InQuest.RewardItem, Count = InQuest.RewardCount - left, Source = EItemSource.Reward });
    }

    private void publishChanged()
    {
        EventBus<QuestChangedEvent>.Publish(new QuestChangedEvent { Quest = Current, Progress = Progress, Completed = IsCompleted });
    }

    private bool isActive(EQuestObjective InObjective)
    {
        return Current != null && IsCompleted == false && Current.Objective == InObjective;
    }

    /// <summary>Target 0 = 아무거나.</summary>
    private bool matchesTarget(int InValue)
    {
        return Current.Target == 0 || Current.Target == InValue;
    }

    private bool isTargetCharacter(Actor InActor)
    {
        if (InActor.IsPlayer == true || InActor.Team == ETeam.Neutral)
        {
            return false;
        }

        return matchesTarget(InActor.CharacterId);
    }

    // ------------------------------------------------------------------ objectives

    private void trackMove(PlayerCharacter InPlayer)
    {
        Vector3 position = InPlayer.transform.position;
        if (HasLastPosition == false || InPlayer.IsDead == true)
        {
            LastPosition = position;
            HasLastPosition = true;
            return;
        }

        Vector3 delta = position - LastPosition;
        delta.y = 0.0f;
        LastPosition = position;
        float distance = delta.magnitude;
        if (distance > TeleportThreshold)
        {
            return;
        }

        MoveAccumulated += distance;
        setProgress(Mathf.FloorToInt(MoveAccumulated));
    }

    /// <summary>상태형 목표 (장비 · 기어 레벨): 지금 상태로 진행도를 정한다.</summary>
    private void checkState(PlayerCharacter InPlayer)
    {
        if (Current == null || IsCompleted == true)
        {
            return;
        }

        switch (Current.Objective)
        {
            case EQuestObjective.Equip:
                bool equipped = InPlayer.Equipment.GetEquipped((EEquipSlot)Current.Target) != 0;
                setProgress(equipped == true ? Goal : 0);
                break;
            case EQuestObjective.GearLevel:
                setProgress(Mathf.FloorToInt(InPlayer.Attributes.Get(EAttribute.Level) + 0.01f));
                break;
        }
    }

    private void onPlayerSpawned(PlayerSpawnedEvent InEvent)
    {
        if (Current != null || IsAllCompleted == true)
        {
            return;
        }

        Begin(Tables.ConstInt(EConst.FirstQuestId));
    }

    private void onAbilityUsed(PlayerAbilityUsedEvent InEvent)
    {
        if (isActive(EQuestObjective.UseAbility) == true && Current.Target == (int)InEvent.Slot)
        {
            addProgress(1);
        }
    }

    private void onItemObtained(ItemObtainedEvent InEvent)
    {
        if (Current == null || IsCompleted == true || InEvent.Count <= 0 || matchesTarget(InEvent.ItemId) == false)
        {
            return;
        }

        if (Current.Objective == EQuestObjective.Gather && InEvent.Source == EItemSource.Harvest)
        {
            addProgress(InEvent.Count);
        }
        else if (Current.Objective == EQuestObjective.Craft && InEvent.Source == EItemSource.Craft)
        {
            addProgress(1);
        }
        else if (Current.Objective == EQuestObjective.Buy && InEvent.Source == EItemSource.Shop)
        {
            addProgress(1);
        }
    }

    private void onEquipmentChanged(EquipmentChangedEvent InEvent)
    {
        var player = PlayerCharacter.Local;
        if (player == null || InEvent.Equipment != player.Equipment)
        {
            return;
        }

        // 기어 레벨은 장비가 바뀔 때 다시 계산되므로 여기서도 본다 (Update 폴링은 치트 level 대비).
        checkState(player);
    }

    private void onStructurePlaced(StructurePlacedEvent InEvent)
    {
        if (isActive(EQuestObjective.Build) == true && InEvent.Structure != null && matchesTarget(InEvent.Structure.Row.Index) == true)
        {
            addProgress(1);
        }
    }

    private void onActorDied(ActorDiedEvent InEvent)
    {
        if (isActive(EQuestObjective.Kill) == false || InEvent.Victim == null || InEvent.Killer == null)
        {
            return;
        }

        if (InEvent.Killer.IsPlayer == true && isTargetCharacter(InEvent.Victim) == true)
        {
            addProgress(1);
        }
    }

    private void onFed(PlayerFedEvent InEvent)
    {
        if (isActive(EQuestObjective.Feed) == true && matchesTarget(InEvent.CharacterId) == true)
        {
            addProgress(1);
        }
    }
}
