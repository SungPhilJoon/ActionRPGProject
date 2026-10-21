using System.Collections.Generic;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 공격 입력 버퍼 (선입선출).
/// 공격 중에 들어온 다른 공격 입력을 버리지 않고 순서대로 쌓아 두었다가, 지금 공격이 끝나면 들어온 순서대로 실행한다.
///  - 지금 실행할 수 있으면 바로 실행 (큐가 비어 있을 때만 — 먼저 들어온 입력을 새치기하지 않는다).
///  - 막힌 이유가 "다른 공격 중(BlockedByTags) / 같은 어빌리티 실행 중(AlreadyActive)" 이면 대기.
///  - 쿨다운·비용·대상 없음처럼 기다려도 안 풀리는 이유면 버린다.
///  - 같은 슬롯은 한 번만 쌓인다 (평타는 누르고 있는 동안 매 프레임 입력되므로).
///  - 맨 앞에서 ExpireSeconds 넘게 기다린 입력은 버린다 (기절 등으로 오래 막혔을 때 늦은 발동 방지).
///    앞 입력이 실행되느라 기다린 시간은 세지 않는다 → 맨 앞이 되는 순간부터 다시 센다.
/// 회피(Travel)는 V Rising 처럼 즉시 실행되어 현재 공격을 끊는다 (어빌리티의 CancelAbilitiesWithTags) — 큐를 거치지 않는다.
/// </summary>
public class AbilityInputBuffer
{
    public const int MaxCount = 3;
    public const float ExpireSeconds = 3.0f;

    private struct Request
    {
        public EAbilitySlot Slot;
        public float Time;
    }

    private readonly List<Request> Queue = new List<Request>(MaxCount);

    public int Count
    {
        get { return Queue.Count; }
    }

    /// <summary>입력 1회. 바로 실행되면 true, 큐에 쌓이거나 버려지면 false.</summary>
    public bool Push(Actor InActor, EAbilitySlot InSlot, Vector3 InAimPoint)
    {
        if (InSlot == EAbilitySlot.Travel)
        {
            Queue.Clear();
            return InActor.TryActivate(InSlot, InAimPoint);
        }

        if (Queue.Count == 0)
        {
            EActivateFailReason reason;
            if (InActor.TryActivate(InSlot, InAimPoint, null, out reason) == true)
            {
                return true;
            }

            if (isWaitable(reason) == false)
            {
                return false;
            }
        }

        enqueue(InSlot);
        return false;
    }

    /// <summary>매 프레임: 맨 앞 입력을 실행해 본다. 조준은 실행 시점의 커서 위치를 쓴다.</summary>
    public void Update(Actor InActor, Vector3 InAimPoint)
    {
        float now = Time.time;
        while (Queue.Count > 0)
        {
            var front = Queue[0];
            if (now - front.Time > ExpireSeconds)
            {
                Queue.RemoveAt(0);
                continue;
            }

            EActivateFailReason reason;
            if (InActor.TryActivate(front.Slot, InAimPoint, null, out reason) == true)
            {
                Queue.RemoveAt(0);
                restartFrontTimer(now);
                return;
            }

            if (isWaitable(reason) == true)
            {
                return;
            }

            Queue.RemoveAt(0);
            restartFrontTimer(now);
        }
    }

    public void Clear()
    {
        Queue.Clear();
    }

    private void restartFrontTimer(float InNow)
    {
        if (Queue.Count == 0)
        {
            return;
        }

        var front = Queue[0];
        front.Time = InNow;
        Queue[0] = front;
    }

    private void enqueue(EAbilitySlot InSlot)
    {
        for (int i = 0; i < Queue.Count; ++i)
        {
            if (Queue[i].Slot == InSlot)
            {
                return;
            }
        }

        if (Queue.Count >= MaxCount)
        {
            return;
        }

        Queue.Add(new Request { Slot = InSlot, Time = Time.time });
    }

    private static bool isWaitable(EActivateFailReason InReason)
    {
        return InReason == EActivateFailReason.BlockedByTags || InReason == EActivateFailReason.AlreadyActive;
    }
}
