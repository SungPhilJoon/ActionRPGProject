using System.Collections;
using System.Collections.Generic;
using ActionRPG.Abilities;
using ActionRPG.Character;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Skills.Effects
{
    /// <summary>네트워크 프리팹을 소환한다. 설치형 함정, 소환수, 지속 장판 등.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/Spawn Networked Prefab", fileName = "EF_Spawn")]
    public class SpawnNetworkedPrefabEffect : SkillEffect
    {
        public enum Anchor : byte { CasterPosition, TargetPoint, ForwardOffset }

        public GameObject Prefab;
        public Anchor SpawnAt = Anchor.TargetPoint;
        public float ForwardDistance = 3f;
        public float LifetimeSeconds = 8f;
        [Tooltip("시전자가 소유권을 갖게 할지. 소환수처럼 조작이 필요하면 켠다.")]
        public bool GiveOwnershipToCaster;

        public override void Execute(in SkillCastContext ctx)
        {
            if (Prefab == null || ctx.SourceASC == null) return;
            Vector3 position = SpawnAt switch
            {
                Anchor.CasterPosition => ctx.CasterTransform.position,
                Anchor.ForwardOffset => ctx.CasterTransform.position + ctx.Direction * ForwardDistance,
                _ => ctx.TargetPoint
            };

            var instance = Instantiate(Prefab, position, Quaternion.LookRotation(ctx.Direction, Vector3.up));
            var netObject = instance.GetComponent<NetworkObject>();
            if (netObject == null)
            {
                Debug.LogError($"[SpawnNetworkedPrefabEffect] {Prefab.name} 에 NetworkObject 가 없어 스폰할 수 없습니다.", Prefab);
                Destroy(instance);
                return;
            }

            var casterNetObject = ctx.CasterObject != null ? ctx.CasterObject.GetComponent<NetworkObject>() : null;
            if (GiveOwnershipToCaster && casterNetObject != null) netObject.SpawnWithOwnership(casterNetObject.OwnerClientId, true);
            else netObject.Spawn(true);

            if (LifetimeSeconds > 0f) ctx.SourceASC.StartCoroutine(DespawnAfter(netObject, LifetimeSeconds));
        }

        private static IEnumerator DespawnAfter(NetworkObject netObject, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (netObject != null && netObject.IsSpawned) netObject.Despawn();
        }
    }
}
