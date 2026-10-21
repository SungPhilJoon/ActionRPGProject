using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>상호작용 가능한 오브젝트 (상인, 작업대, 관 등).</summary>
public interface IInteractable
{
    Vector3 Position { get; }
    int PromptTextId { get; }
    bool CanInteract(PlayerCharacter InPlayer);
    void Interact(PlayerCharacter InPlayer);
}

/// <summary>상호작용 대상 목록. 가장 가까운 대상을 찾는다.</summary>
public static class InteractableRegistry
{
    private static readonly List<IInteractable> Items = new List<IInteractable>();

    public static void Add(IInteractable InItem)
    {
        if (Items.Contains(InItem) == false)
        {
            Items.Add(InItem);
        }
    }

    public static void Remove(IInteractable InItem)
    {
        Items.Remove(InItem);
    }

    public static void Clear()
    {
        Items.Clear();
    }

    public static IInteractable FindNearest(PlayerCharacter InPlayer, float InRange)
    {
        IInteractable best = null;
        float bestDistance = InRange;
        Vector3 position = InPlayer.transform.position;
        for (int i = 0; i < Items.Count; ++i)
        {
            var item = Items[i];
            if (item == null || item.CanInteract(InPlayer) == false)
            {
                continue;
            }

            float distance = MathUtil.FlatDistance(position, item.Position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = item;
            }
        }

        return best;
    }
}
