using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 장비. 무기는 평타/Q/E 어빌리티와 무기 모델을 교체하고, 방어구는 ItemTable.Effects(Infinite 이펙트)로 능력치를 올린다.
/// 기어 레벨 = 장착 슬롯 GearLevel 평균 → Level 속성 (피해 계산의 레벨 보정에 사용).
/// </summary>
public class Equipment : BaseBehaviour
{
    private readonly int[] Equipped = new int[(int)EEquipSlot.Count];
    private readonly int[][] EffectHandles = new int[(int)EEquipSlot.Count][];

    private PlayerCharacter Owner;
    private Inventory Inventory;

    public float GearLevel { get; private set; }

    public void Initialize(PlayerCharacter InOwner)
    {
        Owner = InOwner;
        Inventory = GetComponent<Inventory>();
    }

    public int GetEquipped(EEquipSlot InSlot)
    {
        return Equipped[(int)InSlot];
    }

    /// <summary>인벤토리 슬롯의 아이템을 장착한다. 기존 장비는 인벤토리로 돌아간다.</summary>
    public bool EquipFromInventory(int InInventorySlot)
    {
        var stack = Inventory.Slots[InInventorySlot];
        ItemRow row;
        if (stack.IsEmpty == true || Tables.Root.Item.TryGet(stack.ItemId, out row) == false || row.EquipSlot == EEquipSlot.None)
        {
            return false;
        }

        Inventory.TakeSlot(InInventorySlot);
        int previous = Equipped[(int)row.EquipSlot];
        Equip(stack.ItemId);
        // 벗은 장비는 방금 비운 칸으로 (드래그 앤 드롭에서 두 아이템이 자리를 바꾼 것처럼 보이게).
        if (previous != 0 && Inventory.Put(InInventorySlot, previous, 1) == false)
        {
            Inventory.Add(previous, 1);
        }

        return true;
    }

    /// <summary>인벤토리와 무관하게 바로 장착 (시작 장비/치트).</summary>
    public void Equip(int InItemId)
    {
        ItemRow row;
        if (Tables.Root.Item.TryGet(InItemId, out row) == false || row.EquipSlot == EEquipSlot.None)
        {
            return;
        }

        int slot = (int)row.EquipSlot;
        unequipInternal(slot);
        Equipped[slot] = InItemId;

        if (row.EquipSlot == EEquipSlot.Weapon)
        {
            applyWeapon(row.Weapon);
        }

        var effects = row.Effects;
        EffectHandles[slot] = new int[effects.Length];
        for (int i = 0; i < effects.Length; ++i)
        {
            EffectHandles[slot][i] = Owner.ApplyEffectToSelf(EffectLibrary.Get(effects[i]), 0);
        }

        recalculateGearLevel();
        EventBus<EquipmentChangedEvent>.Publish(new EquipmentChangedEvent { Equipment = this });
    }

    public void Unequip(EEquipSlot InSlot)
    {
        int slot = (int)InSlot;
        int itemId = Equipped[slot];
        if (itemId == 0)
        {
            return;
        }

        if (Inventory.Add(itemId, 1) > 0)
        {
            return;
        }

        removeEquipped(InSlot);
    }

    /// <summary>
    /// 드래그 앤 드롭: InSlot 장비를 인벤토리 InInventorySlot 칸으로 벗는다. 그 칸이 비었으면 그대로, 같은 부위 장비가 있으면 서로 바꾼다.
    /// </summary>
    public bool UnequipTo(EEquipSlot InSlot, int InInventorySlot)
    {
        int itemId = Equipped[(int)InSlot];
        if (itemId == 0)
        {
            return false;
        }

        var target = Inventory.Slots[InInventorySlot];
        if (target.IsEmpty == true)
        {
            Inventory.Put(InInventorySlot, itemId, 1);
            removeEquipped(InSlot);
            return true;
        }

        ItemRow row;
        bool sameSlot = Tables.Root.Item.TryGet(target.ItemId, out row) == true && row.EquipSlot == InSlot;
        return sameSlot == true && EquipFromInventory(InInventorySlot) == true;
    }

    private void removeEquipped(EEquipSlot InSlot)
    {
        unequipInternal((int)InSlot);
        if (InSlot == EEquipSlot.Weapon)
        {
            applyWeapon(Tables.ConstInt(EConst.UnarmedWeaponId));
        }

        recalculateGearLevel();
        EventBus<EquipmentChangedEvent>.Publish(new EquipmentChangedEvent { Equipment = this });
    }

    private void unequipInternal(int InSlot)
    {
        var handles = EffectHandles[InSlot];
        if (handles != null)
        {
            for (int i = 0; i < handles.Length; ++i)
            {
                Owner.RemoveEffect(handles[i]);
            }
        }

        EffectHandles[InSlot] = null;
        Equipped[InSlot] = 0;
    }

    private void applyWeapon(int InWeaponId)
    {
        WeaponRow weapon;
        if (Tables.Root.Weapon.TryGet(InWeaponId, out weapon) == false)
        {
            return;
        }

        Owner.CurrentWeapon = weapon;
        Owner.GiveAbility(weapon.Primary);
        if (weapon.SkillQ != 0)
        {
            Owner.GiveAbility(weapon.SkillQ);
        }
        else
        {
            Owner.ClearSlot(EAbilitySlot.WeaponQ);
        }

        if (weapon.SkillE != 0)
        {
            Owner.GiveAbility(weapon.SkillE);
        }
        else
        {
            Owner.ClearSlot(EAbilitySlot.WeaponE);
        }

        if (Owner.Anim != null)
        {
            string address = Tables.Address(weapon.Model);
            var prefab = string.IsNullOrEmpty(address) == true ? null : Managers.Get<AssetLoader>().LoadSync<GameObject>(address);
            Owner.Anim.SetWeaponModel(prefab);
        }
    }

    private void recalculateGearLevel()
    {
        float sum = 0.0f;
        int slots = (int)EEquipSlot.Count;
        for (int i = 0; i < slots; ++i)
        {
            ItemRow row;
            if (Equipped[i] != 0 && Tables.Root.Item.TryGet(Equipped[i], out row) == true)
            {
                sum += row.GearLevel;
            }
        }

        GearLevel = sum / slots;
        Owner.Attributes.Set(EAttribute.Level, GearLevel);
        Owner.Attributes.Set(EAttribute.GearLevel, GearLevel);
    }
}
