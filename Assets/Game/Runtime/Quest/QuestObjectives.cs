using Framework;
using UnityEngine;

/// <summary>
/// 퀘스트 목표 하나 (EQuestObjective 마다 한 클래스, 2026-11-01 — QuestManager 의 switch / if 분기를 옮김).
/// QuestManager 가 게임 이벤트를 받아 지금 목표의 가상 메서드로 넘기고, 목표는 자기에게 해당하는 것만 센다.
///  - OnBegin: 시작할 때 (상태형 목표는 여기서 바로 진행도를 맞춘다)
///  - Tick: 매 프레임 (이동 거리 · 기어 레벨처럼 연속 값)
///  - On*: 게임 이벤트 (기본은 무시)
///  - TryGetGuidePosition: 퀘스트 장소 (지도 · 길 안내). 기본은 장소 없음
/// 새 목표 종류 = EQuestObjective 값 + [Kind] 를 단 하위 클래스 하나.
/// </summary>
public abstract class QuestObjective
{
    protected QuestManager Quests { get; private set; }
    protected QuestRow Quest { get; private set; }

    internal void Bind(QuestManager InQuests, QuestRow InQuest)
    {
        Quests = InQuests;
        Quest = InQuest;
    }

    /// <summary>특정 몬스터를 노리는 목표인가 (HUD 의 가장 가까운 대상 거리).</summary>
    public virtual bool TargetsMonsters
    {
        get { return false; }
    }

    public virtual void OnBegin(PlayerCharacter InPlayer)
    {
    }

    public virtual void Tick(PlayerCharacter InPlayer, float InDeltaTime)
    {
    }

    public virtual void OnAbilityUsed(PlayerAbilityUsedEvent InEvent)
    {
    }

    public virtual void OnItemObtained(ItemObtainedEvent InEvent)
    {
    }

    public virtual void OnEquipmentChanged(PlayerCharacter InPlayer)
    {
    }

    public virtual void OnStructurePlaced(StructurePlacedEvent InEvent)
    {
    }

    public virtual void OnActorDied(ActorDiedEvent InEvent)
    {
    }

    public virtual void OnFed(PlayerFedEvent InEvent)
    {
    }

    public virtual bool TryGetGuidePosition(Vector3 InFrom, out Vector3 OutPosition)
    {
        OutPosition = Vector3.zero;
        return false;
    }

    /// <summary>Target 0 = 아무거나.</summary>
    protected bool MatchesTarget(int InValue)
    {
        return Quest.Target == 0 || Quest.Target == InValue;
    }

    protected void AddProgress(int InAmount)
    {
        Quests.AddProgress(InAmount);
    }

    protected void SetProgress(int InValue)
    {
        Quests.SetProgress(InValue);
    }
}

/// <summary>이동 거리 (Count = m). 한 프레임에 TeleportThreshold 넘게 움직이면 순간 이동(리스폰 · tp)으로 보고 빼고 센다.</summary>
[Kind(EQuestObjective.Move)]
public sealed class MoveObjective : QuestObjective
{
    private const float TeleportThreshold = 3.0f;

    private Vector3 LastPosition;
    private bool HasLastPosition;
    /// <summary>진행도는 정수 m 지만 프레임당 이동은 1m 미만이라 누적값을 따로 둔다.</summary>
    private float Accumulated;

    public override void Tick(PlayerCharacter InPlayer, float InDeltaTime)
    {
        Vector3 position = InPlayer.transform.position;
        if (HasLastPosition == false || InPlayer.IsDead == true)
        {
            LastPosition = position;
            HasLastPosition = true;
            return;
        }

        float distance = MathUtil.Flat(position - LastPosition).magnitude;
        LastPosition = position;
        if (distance > TeleportThreshold)
        {
            return;
        }

        Accumulated += distance;
        SetProgress(Mathf.FloorToInt(Accumulated));
    }
}

/// <summary>어빌리티 사용 (Target = EAbilitySlot).</summary>
[Kind(EQuestObjective.UseAbility)]
public sealed class UseAbilityObjective : QuestObjective
{
    public override void OnAbilityUsed(PlayerAbilityUsedEvent InEvent)
    {
        if (Quest.Target == (int)InEvent.Slot)
        {
            AddProgress(1);
        }
    }
}

/// <summary>특정 경로로 아이템 얻기 (Target = 아이템). 하위 클래스가 경로와 세는 양을 정한다.</summary>
public abstract class ItemSourceObjective : QuestObjective
{
    protected abstract EItemSource Source { get; }

    /// <summary>한 번 얻을 때 올릴 진행도 (기본 = 횟수 1).</summary>
    protected virtual int CountOf(ItemObtainedEvent InEvent)
    {
        return 1;
    }

    public override void OnItemObtained(ItemObtainedEvent InEvent)
    {
        if (InEvent.Count > 0 && InEvent.Source == Source && MatchesTarget(InEvent.ItemId) == true)
        {
            AddProgress(CountOf(InEvent));
        }
    }
}

/// <summary>채집 (얻은 개수를 센다). 장소 = 그 아이템을 떨구는 채집 노드 지역.</summary>
[Kind(EQuestObjective.Gather)]
public sealed class GatherObjective : ItemSourceObjective
{
    protected override EItemSource Source
    {
        get { return EItemSource.Harvest; }
    }

    protected override int CountOf(ItemObtainedEvent InEvent)
    {
        return InEvent.Count;
    }

    public override bool TryGetGuidePosition(Vector3 InFrom, out Vector3 OutPosition)
    {
        return QuestGuide.FindNearestSpawn(InFrom, ESpawnKind.Resource, row =>
        {
            ResourceNodeRow node;
            return Tables.Root.ResourceNode.TryGet(row.RefId, out node) == true && MatchesTarget(node.DropItem) == true;
        }, out OutPosition);
    }
}

[Kind(EQuestObjective.Craft)]
public sealed class CraftObjective : ItemSourceObjective
{
    protected override EItemSource Source
    {
        get { return EItemSource.Craft; }
    }
}

/// <summary>상점에서 받기. 장소 = 가장 가까운 상인 (없으면 SpawnTable 상인 지점).</summary>
[Kind(EQuestObjective.Buy)]
public sealed class BuyObjective : ItemSourceObjective
{
    protected override EItemSource Source
    {
        get { return EItemSource.Shop; }
    }

    public override bool TryGetGuidePosition(Vector3 InFrom, out Vector3 OutPosition)
    {
        if (QuestGuide.FindNearestMarker(InFrom, EMapMarker.Merchant, out OutPosition) == true)
        {
            return true;
        }

        return QuestGuide.FindNearestSpawn(InFrom, ESpawnKind.Merchant, row => true, out OutPosition);
    }
}

/// <summary>장비 (Target = EEquipSlot): 그 칸에 무엇이든 장착돼 있으면 완료 — 지금 상태를 본다.</summary>
[Kind(EQuestObjective.Equip)]
public sealed class EquipObjective : QuestObjective
{
    public override void OnBegin(PlayerCharacter InPlayer)
    {
        check(InPlayer);
    }

    public override void OnEquipmentChanged(PlayerCharacter InPlayer)
    {
        check(InPlayer);
    }

    private void check(PlayerCharacter InPlayer)
    {
        bool equipped = InPlayer.Equipment.GetEquipped((EEquipSlot)Quest.Target) != 0;
        SetProgress(equipped == true ? Quests.Goal : 0);
    }
}

/// <summary>기어 레벨 (Count = 목표 레벨). 장비가 바뀔 때와 매 프레임(치트 level 대비) 본다.</summary>
[Kind(EQuestObjective.GearLevel)]
public sealed class GearLevelObjective : QuestObjective
{
    public override void OnBegin(PlayerCharacter InPlayer)
    {
        check(InPlayer);
    }

    public override void Tick(PlayerCharacter InPlayer, float InDeltaTime)
    {
        check(InPlayer);
    }

    public override void OnEquipmentChanged(PlayerCharacter InPlayer)
    {
        check(InPlayer);
    }

    private void check(PlayerCharacter InPlayer)
    {
        SetProgress(Mathf.FloorToInt(InPlayer.Attributes.Get(EAttribute.Level) + 0.01f));
    }
}

/// <summary>건축 (Target = StructureTable Index). 성 심장 근처에만 짓는 건축물이면 장소 = 지어 둔 성.</summary>
[Kind(EQuestObjective.Build)]
public sealed class BuildObjective : QuestObjective
{
    public override void OnStructurePlaced(StructurePlacedEvent InEvent)
    {
        if (InEvent.Structure != null && MatchesTarget(InEvent.Structure.Row.Index) == true)
        {
            AddProgress(1);
        }
    }

    public override bool TryGetGuidePosition(Vector3 InFrom, out Vector3 OutPosition)
    {
        OutPosition = Vector3.zero;
        StructureRow structure;
        bool needsHeart = Tables.Root.Structure.TryGet(Quest.Target, out structure) == true && structure.RequiresHeart == true;
        return needsHeart == true && QuestGuide.FindNearestMarker(InFrom, EMapMarker.Castle, out OutPosition) == true;
    }
}

/// <summary>몬스터를 노리는 목표 (Target = CharacterTable Index). 장소 = 가장 가까운 살아 있는 대상 → 없으면(리스폰 대기) 그 몬스터 지역.</summary>
public abstract class MonsterObjective : QuestObjective
{
    public override bool TargetsMonsters
    {
        get { return true; }
    }

    public bool IsTargetCharacter(Actor InActor)
    {
        return InActor.IsPlayer == false && InActor.Team != ETeam.Neutral && MatchesTarget(InActor.CharacterId) == true;
    }

    public override bool TryGetGuidePosition(Vector3 InFrom, out Vector3 OutPosition)
    {
        float distance;
        if (QuestGuide.FindNearestActor(InFrom, IsTargetCharacter, out OutPosition, out distance) == true)
        {
            return true;
        }

        return QuestGuide.FindNearestSpawn(InFrom, ESpawnKind.Monster, row => MatchesTarget(row.RefId), out OutPosition);
    }
}

/// <summary>처치 (플레이어가 마지막 타격 — 흡혈로 처치해도 센다).</summary>
[Kind(EQuestObjective.Kill)]
public sealed class KillObjective : MonsterObjective
{
    public override void OnActorDied(ActorDiedEvent InEvent)
    {
        if (InEvent.Victim != null && InEvent.Killer != null && InEvent.Killer.IsPlayer == true && IsTargetCharacter(InEvent.Victim) == true)
        {
            AddProgress(1);
        }
    }
}

[Kind(EQuestObjective.Feed)]
public sealed class FeedObjective : MonsterObjective
{
    public override void OnFed(PlayerFedEvent InEvent)
    {
        if (MatchesTarget(InEvent.CharacterId) == true)
        {
            AddProgress(1);
        }
    }
}

/// <summary>퀘스트 장소 찾기 도우미 (목표 클래스들이 공유).</summary>
public static class QuestGuide
{
    public static bool FindNearestActor(Vector3 InFrom, System.Func<Actor, bool> InFilter, out Vector3 OutPosition, out float OutDistance)
    {
        OutPosition = Vector3.zero;
        OutDistance = 0.0f;
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
            if (actor == null || actor.IsDead == true || InFilter(actor) == false)
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

        OutDistance = best;
        return best != float.MaxValue;
    }

    public static bool FindNearestMarker(Vector3 InFrom, EMapMarker InKind, out Vector3 OutPosition)
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

    /// <summary>InKind 지점 중 InFilter 를 만족하는 가장 가까운 곳 (SpawnTable).</summary>
    public static bool FindNearestSpawn(Vector3 InFrom, ESpawnKind InKind, System.Func<SpawnRow, bool> InFilter, out Vector3 OutPosition)
    {
        OutPosition = Vector3.zero;
        float best = float.MaxValue;
        var rows = Tables.Root.Spawn.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            var row = rows[i];
            if (row.Kind != InKind || InFilter(row) == false)
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
}
