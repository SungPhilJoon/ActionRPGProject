using Framework;
using UnityEngine;

/// <summary>
/// 퀘스트 진행 (QuestTable 의 한 줄짜리 체인: 튜토리얼 → 사냥 → 보스).
///  - 첫 플레이어가 스폰되면 EConst.FirstQuestId 부터 시작한다.
///  - 게임 이벤트(EventBus)를 구독해 지금 목표 객체(QuestObjective)의 가상 메서드로 넘긴다 — 무엇을 셀지는 목표 클래스가 안다 (QuestObjectives.cs).
///    2026-11-01: 예전에는 EQuestObjective 를 switch / if 로 나눠 여기서 직접 셌다.
///  - 완료 → 보상 지급 → NextDelay 뒤 다음 퀘스트. Next 가 0 이면 QuestsCompletedEvent.
/// 게임플레이 코드는 이 매니저를 모른다 (이벤트만 발행). UI 는 QuestChangedEvent 를 받는다.
/// </summary>
public class QuestManager : IManager
{
    private const float NextDelay = 2.5f;

    private float NextRemaining;

    public QuestRow Current { get; private set; }
    /// <summary>지금 퀘스트의 목표 객체 (Current 의 Objective 종류).</summary>
    public QuestObjective Objective { get; private set; }
    public int Progress { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsAllCompleted { get; private set; }

    public int Goal
    {
        get { return Current != null ? Mathf.Max(1, Current.Count) : 0; }
    }

    /// <summary>진행 중(완료 전)인 목표. 없으면 null.</summary>
    private QuestObjective active
    {
        get { return Current != null && IsCompleted == false ? Objective : null; }
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
        Objective = null;
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
        if (player != null && Objective != null)
        {
            Objective.Tick(player, InDeltaTime);
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

        var objective = KindRegistry<QuestObjective>.Create((int)row.Objective);
        if (objective == null)
        {
            Log.Error($"QuestManager: quest {row.Index} objective {row.Objective} has no QuestObjective class");
            return false;
        }

        objective.Bind(this, row);
        Current = row;
        Objective = objective;
        Progress = 0;
        IsCompleted = false;
        IsAllCompleted = false;
        EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.QuestNew, Arg = Tables.Text(row.TitleId), Color = UIUtil.GoldColor });
        publishChanged();

        var player = PlayerCharacter.Local;
        if (player != null)
        {
            Objective.OnBegin(player);
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

        AddProgress(Goal);
    }

    /// <summary>현재 목표가 특정 몬스터를 노릴 때, 가장 가까운 살아 있는 대상까지의 거리 (HUD 안내용).</summary>
    public bool TryGetNearestTarget(Vector3 InFrom, out float OutDistance)
    {
        Vector3 position;
        return TryGetNearestTarget(InFrom, out position, out OutDistance);
    }

    /// <summary>위와 같고 대상 위치도 준다.</summary>
    public bool TryGetNearestTarget(Vector3 InFrom, out Vector3 OutPosition, out float OutDistance)
    {
        OutPosition = Vector3.zero;
        OutDistance = 0.0f;
        var objective = active as MonsterObjective;
        return objective != null && QuestGuide.FindNearestActor(InFrom, objective.IsTargetCharacter, out OutPosition, out OutDistance) == true;
    }

    /// <summary>퀘스트를 진행하러 갈 곳 (미니맵 · 월드맵 길 안내, HUD 거리). 어디인지는 목표 클래스가 정한다 — 장소가 없으면 false.</summary>
    public bool TryGetGuidePosition(Vector3 InFrom, out Vector3 OutPosition)
    {
        OutPosition = Vector3.zero;
        var objective = active;
        return objective != null && objective.TryGetGuidePosition(InFrom, out OutPosition) == true;
    }

    /// <summary>진행도를 올린다 (목표 클래스가 부른다).</summary>
    internal void AddProgress(int InAmount)
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

    /// <summary>진행도를 지금 상태로 맞춘다 (상태형 · 누적형 목표).</summary>
    internal void SetProgress(int InValue)
    {
        if (Current == null || IsCompleted == true)
        {
            return;
        }

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

    // ------------------------------------------------------------------ flow

    private void startNext()
    {
        int next = Current.Next;
        if (next != 0 && Begin(next) == true)
        {
            return;
        }

        Current = null;
        Objective = null;
        IsCompleted = false;
        IsAllCompleted = true;
        publishChanged();
        EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.QuestAllComplete, Color = new Color(1.0f, 0.25f, 0.3f) });
        EventBus<QuestsCompletedEvent>.Publish(new QuestsCompletedEvent());
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

    // ------------------------------------------------------------------ events → 지금 목표

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
        active?.OnAbilityUsed(InEvent);
    }

    private void onItemObtained(ItemObtainedEvent InEvent)
    {
        active?.OnItemObtained(InEvent);
    }

    private void onEquipmentChanged(EquipmentChangedEvent InEvent)
    {
        var player = PlayerCharacter.Local;
        if (player != null && InEvent.Equipment == player.Equipment)
        {
            active?.OnEquipmentChanged(player);
        }
    }

    private void onStructurePlaced(StructurePlacedEvent InEvent)
    {
        active?.OnStructurePlaced(InEvent);
    }

    private void onActorDied(ActorDiedEvent InEvent)
    {
        active?.OnActorDied(InEvent);
    }

    private void onFed(PlayerFedEvent InEvent)
    {
        active?.OnFed(InEvent);
    }
}
