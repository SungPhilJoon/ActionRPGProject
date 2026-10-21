using System.Collections.Generic;
using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// V Rising 의 혈액 시스템.
///  - 혈액 풀(BloodPool 속성)은 시간이 지나면 줄고, 0 이 되면 BloodStarve 이펙트로 체력을 잃는다.
///  - 흡혈한 대상의 혈액 종류/품질을 얻고, 품질이 BloodTypeTable.Thresholds 를 넘는 단계의 TierEffects 를 부여한다.
///  - V Blood 보유자를 흡혈하면 CharacterTable.VBloodUnlocks 의 어빌리티를 해금한다.
/// </summary>
public class BloodSystem : MonoBehaviour
{
    private PlayerCharacter Owner;
    private readonly List<int> TierHandles = new List<int>();
    private int StarveHandle;

    public EBloodType BloodType { get; private set; }
    public float Quality { get; private set; }

    public void Initialize(PlayerCharacter InOwner)
    {
        Owner = InOwner;
        float max = 100.0f;
        Owner.Attributes.Set(EAttribute.MaxBloodPool, max);
        Owner.Attributes.Set(EAttribute.BloodPool, max * 0.6f);
        SetBlood(EBloodType.None, 0.0f);
    }

    private void Update()
    {
        if (Owner == null || Owner.IsDead == true)
        {
            return;
        }

        var attributes = Owner.Attributes;
        float pool = attributes.GetBase((int)EAttribute.BloodPool);
        if (pool > 0.0f)
        {
            attributes.Set(EAttribute.BloodPool, pool - Tables.Const(EConst.BloodDrainPerSecond) * Time.deltaTime);
        }

        bool empty = attributes.Get(EAttribute.BloodPool) <= 0.0f;
        if (empty == true && StarveHandle == 0)
        {
            var spec = new GameplayEffectSpec(EffectLibrary.Get(EffectLibrary.BloodStarveEffectId), Owner.Asc, 0);
            spec.SetByCaller((int)ESetByCaller.Magnitude, Tables.Const(EConst.BloodEmptyDamagePercent));
            StarveHandle = Owner.Asc.ApplyGameplayEffectSpecToSelf(spec);
        }
        else if (empty == false && StarveHandle != 0)
        {
            Owner.Asc.RemoveActiveEffect(StarveHandle);
            StarveHandle = 0;
        }
    }

    public void Consume(Actor InVictim)
    {
        var row = InVictim.Row;
        Owner.Attributes.Set(EAttribute.BloodPool, Owner.Attributes.Get(EAttribute.MaxBloodPool));

        // 같은 종류면 높은 품질을 유지, 다른 종류면 교체 (V Rising 과 동일하게 선택 없이 교체).
        if (row.BloodType != EBloodType.None)
        {
            float quality = Mathf.Clamp(row.BloodQuality + Random.Range(-5.0f, 10.0f), 1.0f, 100.0f);
            SetBlood(row.BloodType, quality);
        }

        if (row.IsVBlood == true)
        {
            Owner.UnlockAbilities(row.VBloodUnlocks);
            EventBus<VBloodConsumedEvent>.Publish(new VBloodConsumedEvent { CharacterId = row.Index, Unlocked = row.VBloodUnlocks });
            EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.VBloodConsumed, Arg = Tables.Text(row.NameId), Color = new Color(1.0f, 0.2f, 0.25f) });
        }
    }

    public void SetBlood(EBloodType InType, float InQuality)
    {
        for (int i = 0; i < TierHandles.Count; ++i)
        {
            Owner.Asc.RemoveActiveEffect(TierHandles[i]);
        }

        TierHandles.Clear();
        BloodType = InType;
        Quality = InQuality;

        BloodTypeRow row;
        if (InType != EBloodType.None && Tables.Root.BloodType.TryGet((int)InType, out row) == true)
        {
            for (int i = 0; i < row.Thresholds.Length && i < row.TierEffects.Length; ++i)
            {
                if (InQuality < row.Thresholds[i])
                {
                    continue;
                }

                var def = EffectLibrary.Get(row.TierEffects[i]);
                if (def == null)
                {
                    continue;
                }

                var spec = new GameplayEffectSpec(def, Owner.Asc, 0);
                int handle = Owner.Asc.ApplyGameplayEffectSpecToSelf(spec);
                if (handle != 0)
                {
                    TierHandles.Add(handle);
                }
            }
        }

        EventBus<BloodChangedEvent>.Publish(new BloodChangedEvent { BloodType = InType, Quality = InQuality });
    }

    /// <summary>사망 후 부활 시 Infinite 혈액 이펙트가 RemoveAllEffects 로 지워지므로 다시 적용한다.</summary>
    public void Reapply()
    {
        TierHandles.Clear();
        StarveHandle = 0;
        SetBlood(BloodType, Quality);
    }
}
