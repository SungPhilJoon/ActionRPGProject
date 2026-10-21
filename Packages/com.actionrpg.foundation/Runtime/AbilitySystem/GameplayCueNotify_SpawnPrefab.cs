using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 가장 흔한 큐: 프리팹을 띄운다. Executed 는 위치에 한 번, WhileActive 는 대상에 붙여 두었다가 Removed 에서 지운다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Ability System/Gameplay Cue (Spawn Prefab)", fileName = "GCN_New")]
    public class GameplayCueNotify_SpawnPrefab : GameplayCueNotify
    {
        public GameObject Prefab;
        [Tooltip("Executed 로 띄운 프리팹을 지울 시간.")]
        public float BurstLifetime = 3f;
        [Tooltip("WhileActive 프리팹을 대상에 붙일지.")]
        public bool AttachToTarget = true;

        private readonly Dictionary<int, GameObject> _looping = new Dictionary<int, GameObject>();

        public override void OnExecute(GameObject target, in GameplayCueParameters parameters)
        {
            if (Prefab == null) return;
            Vector3 position = parameters.Location != Vector3.zero || target == null ? parameters.Location : target.transform.position;
            var rotation = parameters.Normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(parameters.Normal) : Quaternion.identity;
            var instance = Instantiate(Prefab, position, rotation);
            Destroy(instance, BurstLifetime);
        }

        public override void WhileActive(GameObject target, in GameplayCueParameters parameters)
        {
            if (Prefab == null || target == null) return;
            int key = target.GetInstanceID();
            if (_looping.TryGetValue(key, out var existing) && existing != null) return;
            _looping[key] = Instantiate(Prefab, target.transform.position, target.transform.rotation, AttachToTarget ? target.transform : null);
        }

        public override void OnRemove(GameObject target, in GameplayCueParameters parameters)
        {
            if (target == null) return;
            int key = target.GetInstanceID();
            if (_looping.TryGetValue(key, out var instance) && instance != null) Destroy(instance);
            _looping.Remove(key);
        }
    }
}
