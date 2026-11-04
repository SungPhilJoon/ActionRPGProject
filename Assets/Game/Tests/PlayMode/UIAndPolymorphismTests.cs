using System;
using System.Collections;
using Framework;
using Framework.Ability;
using Framework.StateTree;
using Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 2026-11-01 리팩터링 검증 (Documentation/VRising/Refactor_Polymorphism.md):
///  - UI 레이어: 모든 창이 자기 GetLayer() 레이어 캔버스 아래에 있고, 레이어 정책(입력 차단 · ESC)이 맞다
///  - 토스트: 아이템을 얻으면 토스트가 뜨고, 같은 아이템은 한 줄로 합쳐진다
///  - 드래그 앤 드롭: 칸 바인딩 더블 디스패치 (옮기기 · 합치기 · 장착 · 벗기 · 맞지 않는 부위 거절)
///  - 다형성: 데이터 enum 의 모든 값에 [Kind] 클래스가 있다 (switch 를 없앤 대신 빠진 클래스가 없게)
/// </summary>
public class UIAndPolymorphismTests : GamePlayTestBase
{
    private const int Lumber = 1;
    private const int ClothRobe = 20;
    private const int CopperSword = 11;

    [Test]
    public void UILayers_EveryWindowUnderItsLayer()
    {
        var ui = Managers.Get<UIManager>();
        int checkedWindows = 0;
        foreach (var window in ui.AllWindows)
        {
            var layer = window.GetLayer();
            Assert.AreEqual(ui.GetCanvas(layer).transform, window.transform.parent, $"{window.name} 는 {layer.Name} 레이어 아래");
            checkedWindows += 1;
        }

        Assert.Greater(checkedWindows, 5);
        for (int i = 1; i < UILayer.All.Count; ++i)
        {
            Assert.Greater(UILayer.All[i].SortingOrder, UILayer.All[i - 1].SortingOrder, "레이어는 아래 → 위 순서");
        }

        Assert.IsFalse(ui.Get<HudWindow>().BlocksGameInput, "HUD 레이어는 입력을 막지 않음");
        Assert.IsTrue(ui.Get<InventoryWindow>().BlocksGameInput, "창 레이어는 입력을 막음");
        Assert.IsFalse(ui.Get<ToastWindow>().BlocksGameInput, "토스트 레이어는 입력을 막지 않음");
        Assert.IsFalse(UILayer.System.ClosesOnCancel, "시스템 레이어(타이틀)는 ESC 로 닫히지 않음");
    }

    [UnityTest]
    public IEnumerator Toast_ItemObtained_MergesSameItem()
    {
        var toast = Managers.Get<UIManager>().Get<ToastWindow>();
        Assert.IsTrue(toast.IsOpen, "토스트 창은 항상 열려 있음");
        yield return WaitFor(() => toast.Count == 0, 6.0f, "이전 토스트가 사라짐");

        publish(Lumber, 3, EItemSource.Harvest);
        publish(Lumber, 2, EItemSource.Harvest);
        yield return null;
        Assert.AreEqual(1, toast.Count, "같은 아이템 · 같은 경로는 한 줄로 합침");

        publish(Lumber, 1, EItemSource.Loot);
        yield return null;
        Assert.AreEqual(2, toast.Count, "경로가 다르면 새 줄");
    }

    [Test]
    public void DragDrop_SlotBindings()
    {
        var inventory = Player.Inventory;
        var equipment = Player.Equipment;
        clear(inventory);

        inventory.Put(0, Lumber, 10);
        inventory.Put(1, Lumber, 5);
        Assert.IsTrue(new InventorySlotBinding(1).DropOnto(new InventorySlotBinding(0), Player), "같은 아이템 합치기");
        Assert.AreEqual(15, inventory.Slots[0].Count);
        Assert.IsTrue(inventory.Slots[1].IsEmpty);

        Assert.IsTrue(new InventorySlotBinding(0).DropOnto(new InventorySlotBinding(7), Player), "빈 칸으로 옮기기");
        Assert.AreEqual(Lumber, inventory.Slots[7].ItemId);

        inventory.Put(2, ClothRobe, 1);
        Assert.IsFalse(new InventorySlotBinding(2).DropOnto(new EquipSlotBinding(EEquipSlot.Weapon), Player), "흉갑은 무기 칸에 못 넣음");
        Assert.IsTrue(new InventorySlotBinding(2).DropOnto(new EquipSlotBinding(EEquipSlot.Chest), Player), "흉갑 칸에 장착");
        Assert.AreEqual(ClothRobe, equipment.GetEquipped(EEquipSlot.Chest));

        Assert.IsTrue(new EquipSlotBinding(EEquipSlot.Chest).DropOnto(new InventorySlotBinding(9), Player), "장비 → 지정한 칸으로 벗기");
        Assert.AreEqual(ClothRobe, inventory.Slots[9].ItemId);
        Assert.AreEqual(0, equipment.GetEquipped(EEquipSlot.Chest));

        int weapon = equipment.GetEquipped(EEquipSlot.Weapon);
        inventory.Put(3, CopperSword, 1);
        Assert.IsTrue(new InventorySlotBinding(3).DropOnto(new EquipSlotBinding(EEquipSlot.Weapon), Player), "무기 교체");
        Assert.AreEqual(weapon, inventory.Slots[3].ItemId, "벗은 무기는 끌어온 칸으로 (자리 바꿈)");

        // 원래 무기로 되돌린다 (다른 테스트 영향 방지).
        new InventorySlotBinding(3).DropOnto(new EquipSlotBinding(EEquipSlot.Weapon), Player);
        clear(inventory);
    }

    [Test]
    public void KindRegistry_EveryDataEnumHasClass()
    {
        assertCovered<GameAbility, EAbilityType>(EAbilityType.None);
        assertCovered<QuestObjective, EQuestObjective>(EQuestObjective.None);
        assertCovered<SpawnHandler, ESpawnKind>();
        assertCovered<StructureKind, EStructureType>();
        assertCovered<HarvestKind, EHarvestType>(EHarvestType.None);
        assertCovered<AssetPreloader, EAssetKind>();
        assertCovered<AnimationParameterOp, EAnimParamType>();
        assertCovered<ModifierOperation, EModifierOp>();
        assertCovered<MagnitudeCalculation, EMagnitudeType>();
        assertCovered<CompareOperation, ECompareOp>();
        assertCovered<TransitionTrigger, ETransitionTrigger>();
        assertCovered<TransitionTarget, ETransitionType>();
        assertCovered<AssetEventAccumulator, EAssetEvent>();
        assertCovered<ItemUse, EItemType>(EItemType.Resource);
    }

    private static void assertCovered<TBase, TEnum>(params TEnum[] InSkip) where TBase : class where TEnum : struct, Enum
    {
        foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
        {
            if (Array.IndexOf(InSkip, value) >= 0)
            {
                continue;
            }

            Assert.IsTrue(KindRegistry<TBase>.Has(Convert.ToInt32(value)), $"{typeof(TEnum).Name}.{value} → {typeof(TBase).Name} 하위 클래스 없음");
        }
    }

    private void publish(int InItemId, int InCount, EItemSource InSource)
    {
        EventBus<ItemObtainedEvent>.Publish(new ItemObtainedEvent { Player = Player, ItemId = InItemId, Count = InCount, Source = InSource });
    }

    private static void clear(Inventory InInventory)
    {
        for (int i = 0; i < InInventory.Size; ++i)
        {
            InInventory.TakeSlot(i);
        }
    }
}
