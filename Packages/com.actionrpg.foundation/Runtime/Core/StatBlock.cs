using System;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core
{
    /// <summary>
    /// 캐릭터의 전체 스탯 한 벌. unmanaged 구조체라 NetworkVariable 로 그대로 복제할 수 있다.
    /// </summary>
    [Serializable]
    public struct StatBlock : INetworkSerializable, IEquatable<StatBlock>
    {
        public float MaxHealth;
        public float MaxMana;
        public float ManaRegen;
        public float AttackPower;
        public float AbilityPower;
        public float Defense;
        public float MoveSpeed;
        public float AttackSpeed;
        [Range(0f, 1f)] public float CritChance;
        public float CritDamage;
        [Range(0f, 0.8f)] public float CooldownReduction;

        public static StatBlock DefaultPlayer => new StatBlock
        {
            MaxHealth = 120f,
            MaxMana = 100f,
            ManaRegen = 6f,
            AttackPower = 12f,
            AbilityPower = 10f,
            Defense = 5f,
            MoveSpeed = 5.2f,
            AttackSpeed = 1f,
            CritChance = 0.1f,
            CritDamage = 1.6f,
            CooldownReduction = 0f
        };

        public static StatBlock DefaultEnemy => new StatBlock
        {
            MaxHealth = 60f,
            MaxMana = 0f,
            ManaRegen = 0f,
            AttackPower = 8f,
            AbilityPower = 0f,
            Defense = 2f,
            MoveSpeed = 3.4f,
            AttackSpeed = 1f,
            CritChance = 0.05f,
            CritDamage = 1.5f,
            CooldownReduction = 0f
        };

        public float Get(StatType type) => type switch
        {
            StatType.MaxHealth => MaxHealth,
            StatType.MaxMana => MaxMana,
            StatType.ManaRegen => ManaRegen,
            StatType.AttackPower => AttackPower,
            StatType.AbilityPower => AbilityPower,
            StatType.Defense => Defense,
            StatType.MoveSpeed => MoveSpeed,
            StatType.AttackSpeed => AttackSpeed,
            StatType.CritChance => CritChance,
            StatType.CritDamage => CritDamage,
            StatType.CooldownReduction => CooldownReduction,
            _ => 0f
        };

        public void Set(StatType type, float value)
        {
            switch (type)
            {
                case StatType.MaxHealth: MaxHealth = value; break;
                case StatType.MaxMana: MaxMana = value; break;
                case StatType.ManaRegen: ManaRegen = value; break;
                case StatType.AttackPower: AttackPower = value; break;
                case StatType.AbilityPower: AbilityPower = value; break;
                case StatType.Defense: Defense = value; break;
                case StatType.MoveSpeed: MoveSpeed = value; break;
                case StatType.AttackSpeed: AttackSpeed = value; break;
                case StatType.CritChance: CritChance = value; break;
                case StatType.CritDamage: CritDamage = value; break;
                case StatType.CooldownReduction: CooldownReduction = value; break;
            }
        }

        /// <summary>값 범위를 게임 규칙 안으로 강제한다. 버프가 겹쳐도 무한대가 되지 않게.</summary>
        public void Clamp()
        {
            MaxHealth = Mathf.Max(1f, MaxHealth);
            MaxMana = Mathf.Max(0f, MaxMana);
            ManaRegen = Mathf.Max(0f, ManaRegen);
            AttackPower = Mathf.Max(0f, AttackPower);
            AbilityPower = Mathf.Max(0f, AbilityPower);
            Defense = Mathf.Max(0f, Defense);
            MoveSpeed = Mathf.Clamp(MoveSpeed, 0f, 30f);
            AttackSpeed = Mathf.Clamp(AttackSpeed, 0.2f, 3f);
            CritChance = Mathf.Clamp01(CritChance);
            CritDamage = Mathf.Max(1f, CritDamage);
            CooldownReduction = Mathf.Clamp(CooldownReduction, 0f, 0.8f);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref MaxMana);
            serializer.SerializeValue(ref ManaRegen);
            serializer.SerializeValue(ref AttackPower);
            serializer.SerializeValue(ref AbilityPower);
            serializer.SerializeValue(ref Defense);
            serializer.SerializeValue(ref MoveSpeed);
            serializer.SerializeValue(ref AttackSpeed);
            serializer.SerializeValue(ref CritChance);
            serializer.SerializeValue(ref CritDamage);
            serializer.SerializeValue(ref CooldownReduction);
        }

        public bool Equals(StatBlock other)
        {
            return MaxHealth.Equals(other.MaxHealth)
                   && MaxMana.Equals(other.MaxMana)
                   && ManaRegen.Equals(other.ManaRegen)
                   && AttackPower.Equals(other.AttackPower)
                   && AbilityPower.Equals(other.AbilityPower)
                   && Defense.Equals(other.Defense)
                   && MoveSpeed.Equals(other.MoveSpeed)
                   && AttackSpeed.Equals(other.AttackSpeed)
                   && CritChance.Equals(other.CritChance)
                   && CritDamage.Equals(other.CritDamage)
                   && CooldownReduction.Equals(other.CooldownReduction);
        }

        public override bool Equals(object obj) => obj is StatBlock other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(MaxHealth);
            hash.Add(MaxMana);
            hash.Add(AttackPower);
            hash.Add(AbilityPower);
            hash.Add(Defense);
            hash.Add(MoveSpeed);
            return hash.ToHashCode();
        }
    }
}
