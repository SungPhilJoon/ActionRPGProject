using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>액터 ID 발급. 플레이어는 고정 대역, 나머지는 순차 발급.</summary>
public static class ActorKey
{
    public const long PlayerBase = 5000;
    private const long Start = 10000;
    private static long Counter = 0;

    public static long Generate()
    {
        return Start + Counter++;
    }

    public static long Player(int InSlot)
    {
        return PlayerBase + InSlot;
    }
}

/// <summary>
/// 살아 있는 액터 목록과 공간 질의. FindObjectsOfType / Physics 질의 대신 리스트를 순회한다
/// (액터 수가 수십 단위인 데모 규모 기준. 규모가 커지면 공간 분할로 교체할 지점 — Issues.md 참고).
/// </summary>
public class ActorRegistry : IManager
{
    private readonly List<Actor> Actors = new List<Actor>();
    private readonly Dictionary<long, Actor> ById = new Dictionary<long, Actor>();

    public IReadOnlyList<Actor> All
    {
        get { return Actors; }
    }

    void IManager.Initialize()
    {
    }

    void IManager.UnInitialize()
    {
        Actors.Clear();
        ById.Clear();
    }

    void IManager.Update(float InDeltaTime)
    {
    }

    public void Add(Actor InActor)
    {
        if (ById.ContainsKey(InActor.ActorId) == true)
        {
            return;
        }

        Actors.Add(InActor);
        ById.Add(InActor.ActorId, InActor);
        EventBus<ActorSpawnedEvent>.Publish(new ActorSpawnedEvent { Actor = InActor });
    }

    public void Remove(Actor InActor)
    {
        Actors.Remove(InActor);
        ById.Remove(InActor.ActorId);
    }

    public Actor Find(long InActorId)
    {
        Actor actor;
        ById.TryGetValue(InActorId, out actor);
        return actor;
    }

    /// <summary>InSelf 에게 적대적이고 살아 있는 액터 중 InCenter 반경 안 (액터 반지름 포함).</summary>
    public void FindHostiles(Actor InSelf, Vector3 InCenter, float InRadius, List<Actor> OutResults)
    {
        OutResults.Clear();
        for (int i = 0; i < Actors.Count; ++i)
        {
            var actor = Actors[i];
            if (actor == null || actor.IsDead == true || InSelf.IsHostileTo(actor) == false)
            {
                continue;
            }

            float distance = MathUtil.FlatDistance(InCenter, actor.transform.position);
            if (distance <= InRadius + actor.Radius)
            {
                OutResults.Add(actor);
            }
        }
    }

    public Actor FindNearestHostile(Actor InSelf, float InRange)
    {
        Actor best = null;
        float bestDistance = InRange;
        var position = InSelf.transform.position;
        for (int i = 0; i < Actors.Count; ++i)
        {
            var actor = Actors[i];
            if (actor == null || actor.IsDead == true || InSelf.IsHostileTo(actor) == false)
            {
                continue;
            }

            float distance = MathUtil.FlatDistance(position, actor.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = actor;
            }
        }

        return best;
    }
}
