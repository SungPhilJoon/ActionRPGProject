using Framework;
using UnityEngine;

/// <summary>LootTable 드랍을 플레이어 인벤토리에 지급한다.</summary>
public static class LootUtil
{
    public static void Grant(int InLootId, PlayerCharacter InPlayer)
    {
        LootRow loot;
        if (InLootId == 0 || Tables.Root.Loot.TryGet(InLootId, out loot) == false)
        {
            return;
        }

        for (int i = 0; i < loot.Items.Length; ++i)
        {
            float chance = i < loot.Chance.Length ? loot.Chance[i] : 1.0f;
            if (Random.value > chance)
            {
                continue;
            }

            int min = i < loot.Min.Length ? loot.Min[i] : 1;
            int max = i < loot.Max.Length ? loot.Max[i] : min;
            int count = Random.Range(min, max + 1);
            if (count <= 0)
            {
                continue;
            }

            int left = InPlayer.Inventory.Add(loot.Items[i], count);
            // 획득 알림은 ToastWindow 가 ItemObtainedEvent 로 띄운다 (전리품).
            EventBus<ItemObtainedEvent>.Publish(new ItemObtainedEvent { Player = InPlayer, ItemId = loot.Items[i], Count = count - left, Source = EItemSource.Loot });
        }
    }
}
