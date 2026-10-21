using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>
/// 플레이어 액터. 혈액/햇빛/인벤토리/장비 컴포넌트를 묶고 사망 → 리스폰을 관리한다.
/// 멀티플레이 확장 시 PlayerSlot(0~3) 으로 구분한다.
/// </summary>
[RequireComponent(typeof(BloodSystem))]
[RequireComponent(typeof(SunExposure))]
[RequireComponent(typeof(Inventory))]
[RequireComponent(typeof(Equipment))]
public class PlayerCharacter : Actor
{
    public int PlayerSlot { get; private set; }
    public BloodSystem Blood { get; private set; }
    public SunExposure Sun { get; private set; }
    public Inventory Inventory { get; private set; }
    public Equipment Equipment { get; private set; }
    public WeaponRow CurrentWeapon;

    /// <summary>리스폰 위치. 관(Coffin)을 지으면 그 위치로 바뀐다.</summary>
    public Vector3 RespawnPoint;
    public float RespawnRemaining { get; private set; }

    private readonly HashSet<int> Unlocked = new HashSet<int>();

    public override bool IsPlayer
    {
        get { return true; }
    }

    public static PlayerCharacter Local { get; private set; }

    public void InitializePlayer(CharacterRow InRow, int InPlayerSlot)
    {
        PlayerSlot = InPlayerSlot;
        Initialize(InRow, ActorKey.Player(InPlayerSlot));
        Local = this;

        Blood = GetComponent<BloodSystem>();
        Sun = GetComponent<SunExposure>();
        Inventory = GetComponent<Inventory>();
        Equipment = GetComponent<Equipment>();

        Inventory.Initialize(Tables.ConstInt(EConst.InventorySize));
        Equipment.Initialize(this);
        Blood.Initialize(this);
        Sun.Initialize(this);

        int startWeapon = Tables.ConstInt(EConst.StartingWeaponItem);
        if (startWeapon != 0)
        {
            Equipment.Equip(startWeapon);
        }

        RespawnPoint = transform.position;
        EventBus<PlayerSpawnedEvent>.Publish(new PlayerSpawnedEvent { Player = this });
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Local == this)
        {
            Local = null;
        }
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead == false)
        {
            return;
        }

        RespawnRemaining -= Time.deltaTime;
        if (RespawnRemaining <= 0.0f)
        {
            Revive(RespawnPoint, 1.0f);
        }
    }

    public bool IsUnlocked(int InAbilityId)
    {
        return Unlocked.Contains(InAbilityId);
    }

    public void UnlockAbilities(int[] InAbilityIds)
    {
        if (InAbilityIds == null)
        {
            return;
        }

        for (int i = 0; i < InAbilityIds.Length; ++i)
        {
            int abilityId = InAbilityIds[i];
            if (Unlocked.Add(abilityId) == false)
            {
                continue;
            }

            GiveAbility(abilityId);
            AbilityRow row;
            if (Tables.Root.Ability.TryGet(abilityId, out row) == true)
            {
                EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.AbilityUnlocked, Arg = Tables.Text(row.NameId), Color = new Color(0.9f, 0.75f, 0.3f) });
            }
        }
    }

    public override void Die(Actor InKiller)
    {
        base.Die(InKiller);
        RespawnRemaining = Tables.Const(EConst.RespawnSeconds);
    }

    public override void Revive(Vector3 InPosition, float InHealthRatio)
    {
        base.Revive(InPosition, InHealthRatio);

        // RemoveAllEffects 로 지워진 장비/혈액 Infinite 이펙트를 복원한다.
        var equipped = new int[(int)EEquipSlot.Count];
        for (int i = 0; i < equipped.Length; ++i)
        {
            equipped[i] = Equipment.GetEquipped((EEquipSlot)i);
        }

        for (int i = 0; i < equipped.Length; ++i)
        {
            if (equipped[i] != 0)
            {
                Equipment.Equip(equipped[i]);
            }
        }

        Attributes.Set(EAttribute.Health, Attributes.Get(EAttribute.MaxHealth) * InHealthRatio);
        Attributes.Set(EAttribute.BloodPool, Attributes.Get(EAttribute.MaxBloodPool) * 0.5f);
        Blood.Reapply();
        ApplyEffect(EffectLibrary.RespawnShieldEffectId, this, 0);
    }

    protected override void applyBaseStats(CharacterRow InRow)
    {
        base.applyBaseStats(InRow);
        Attributes.Set(EAttribute.MaxBloodPool, 100.0f);
    }
}
