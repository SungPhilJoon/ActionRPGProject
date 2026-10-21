using System;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core
{
    public enum DamageType : byte
    {
        Physical = 0,
        Magical = 1,
        True = 2
    }

    /// <summary>아군/적군 판정. 튜토리얼 허수아비는 Neutral 로 두면 아무도 공격하지 않는다.</summary>
    public enum Faction : byte
    {
        Neutral = 0,
        Player = 1,
        Enemy = 2
    }

    public enum WeaponType : byte
    {
        Unarmed = 0,
        Sword = 1,
        Bow = 2
    }

    public enum StatType : byte
    {
        MaxHealth = 0,
        MaxMana = 1,
        ManaRegen = 2,
        AttackPower = 3,
        AbilityPower = 4,
        Defense = 5,
        MoveSpeed = 6,
        AttackSpeed = 7,
        CritChance = 8,
        CritDamage = 9,
        CooldownReduction = 10
    }

    public enum ModifierOp : byte
    {
        /// <summary>합연산. 여러 개가 있으면 모두 더한 뒤 적용.</summary>
        Flat = 0,
        /// <summary>곱연산. 0.2f == +20%.</summary>
        Percent = 1
    }

    public static class FactionExtensions
    {
        public static bool IsHostileTo(this Faction self, Faction other)
        {
            if (self == Faction.Neutral || other == Faction.Neutral) return false;
            return self != other;
        }

        public static bool IsAllyOf(this Faction self, Faction other) => self == other && self != Faction.Neutral;
    }

    /// <summary>피해 1회에 대한 모든 정보. RPC 로 그대로 보낼 수 있게 unmanaged 로 유지한다.</summary>
    public struct DamageInfo : INetworkSerializable
    {
        public ulong SourceObjectId;
        public float Amount;
        public DamageType Type;
        public Faction SourceFaction;
        public Vector3 HitPoint;
        public Vector3 HitDirection;
        public float KnockbackForce;
        public float StaggerDuration;
        public bool IsCritical;

        public static DamageInfo Create(float amount, DamageType type, Faction source, ulong sourceId)
        {
            return new DamageInfo
            {
                Amount = amount,
                Type = type,
                SourceFaction = source,
                SourceObjectId = sourceId,
                HitDirection = Vector3.forward
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SourceObjectId);
            serializer.SerializeValue(ref Amount);
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref SourceFaction);
            serializer.SerializeValue(ref HitPoint);
            serializer.SerializeValue(ref HitDirection);
            serializer.SerializeValue(ref KnockbackForce);
            serializer.SerializeValue(ref StaggerDuration);
            serializer.SerializeValue(ref IsCritical);
        }
    }

    /// <summary>피해를 받을 수 있는 대상. 구현체의 메서드는 서버에서만 호출한다.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        Faction Faction { get; }
        Transform Transform { get; }
        void ApplyDamage(DamageInfo info);
        void ApplyHeal(float amount, ulong sourceObjectId);
    }

    /// <summary>상호작용 가능한 오브젝트(문, 상자, NPC 등).</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public ModifierOp Op;
        public float Value;

        [NonSerialized] public int SourceId;

        public StatModifier(StatType stat, ModifierOp op, float value, int sourceId = 0)
        {
            Stat = stat;
            Op = op;
            Value = value;
            SourceId = sourceId;
        }
    }
}
