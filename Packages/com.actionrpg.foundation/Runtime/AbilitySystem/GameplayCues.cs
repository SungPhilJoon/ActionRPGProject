using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>언리얼 EGameplayCueEvent.</summary>
    public enum GameplayCueEvent : byte
    {
        /// <summary>지속형 큐가 처음 붙을 때 (늦게 들어온 클라이언트는 받지 않는다).</summary>
        OnActive = 0,
        /// <summary>지속형 큐가 붙어 있음 (늦게 들어온 클라이언트도 받는다).</summary>
        WhileActive = 1,
        /// <summary>한 번 실행 (피격, 폭발 등).</summary>
        Executed = 2,
        /// <summary>지속형 큐가 떨어질 때.</summary>
        Removed = 3
    }

    /// <summary>
    /// 언리얼 FGameplayCueParameters. 큐 재생에 필요한 정보를 네트워크로 보낼 수 있게 담는다.
    /// GameObject 참조 대신 NetworkObjectId 를 보내고, 받는 쪽에서 찾는다.
    /// </summary>
    [Serializable]
    public struct GameplayCueParameters : INetworkSerializable
    {
        public float NormalizedMagnitude;
        public float RawMagnitude;
        public Vector3 Location;
        public Vector3 Normal;
        public ulong InstigatorId;
        public ulong EffectCauserId;
        /// <summary>큐를 일으킨 어빌리티 / 이펙트 에셋의 ID (<see cref="AbilitySystemGlobals.GetAssetId"/>).</summary>
        public int SourceObjectId;
        public int EffectLevel;
        public int AbilityLevel;
        /// <summary>프로젝트 확장 값. 언리얼에는 없는 필드로, 같은 큐에서 하위 연출 번호 등을 넘길 때 쓴다.</summary>
        public int UserData;
        public bool IsCriticalHit;
        /// <summary>예측으로 이미 재생한 소유 클라이언트가 서버 전파분을 건너뛰기 위한 키.</summary>
        public int PredictionKey;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref NormalizedMagnitude);
            serializer.SerializeValue(ref RawMagnitude);
            serializer.SerializeValue(ref Location);
            serializer.SerializeValue(ref Normal);
            serializer.SerializeValue(ref InstigatorId);
            serializer.SerializeValue(ref EffectCauserId);
            serializer.SerializeValue(ref SourceObjectId);
            serializer.SerializeValue(ref EffectLevel);
            serializer.SerializeValue(ref AbilityLevel);
            serializer.SerializeValue(ref UserData);
            serializer.SerializeValue(ref IsCriticalHit);
            serializer.SerializeValue(ref PredictionKey);
        }

        public GameObject GetInstigator() => Resolve(InstigatorId);
        public GameObject GetEffectCauser() => Resolve(EffectCauserId);

        private static GameObject Resolve(ulong id)
        {
            if (id == 0UL || NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) return null;
            return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(id, out var netObject) && netObject != null
                ? netObject.gameObject
                : null;
        }

        /// <summary>이펙트 스펙에서 큐 파라미터를 만든다 (언리얼 InitGameplayCueParametersFromSpec).</summary>
        public static GameplayCueParameters FromSpec(GameplayEffectSpec spec, float rawMagnitude, float normalizedMagnitude)
        {
            var context = spec.Context;
            var parameters = new GameplayCueParameters
            {
                RawMagnitude = rawMagnitude,
                NormalizedMagnitude = normalizedMagnitude,
                InstigatorId = context.InstigatorNetworkObjectId,
                EffectCauserId = context.EffectCauserNetworkObjectId,
                SourceObjectId = spec.Def != null ? AbilitySystemGlobals.GetAssetId(spec.Def) : 0,
                EffectLevel = Mathf.RoundToInt(spec.Level),
                AbilityLevel = context.AbilityLevel,
                IsCriticalHit = context.IsCriticalHit
            };
            if (context.HasHitResult)
            {
                parameters.Location = context.HitLocation;
                parameters.Normal = context.HitNormal;
            }
            else if (context.HasOrigin)
            {
                parameters.Location = context.Origin;
            }
            return parameters;
        }
    }

    /// <summary>
    /// 언리얼 IGameplayCueInterface. 큐 대상 GameObject 의 컴포넌트가 구현하면 매니저보다 먼저 큐를 받는다.
    /// (언리얼에서 액터에 큐 이름의 함수를 만들어 두는 방식에 해당)
    /// </summary>
    public interface IGameplayCueInterface
    {
        void HandleGameplayCue(GameplayTag cueTag, GameplayCueEvent eventType, in GameplayCueParameters parameters);
    }

    /// <summary>코드로 등록하는 큐 핸들러.</summary>
    public delegate void GameplayCueHandler(GameObject target, GameplayTag cueTag, GameplayCueEvent eventType, in GameplayCueParameters parameters);

    /// <summary>
    /// 언리얼 UGameplayCueManager. 큐 태그 → 연출을 찾아 실행한다.
    /// 정확히 일치하는 핸들러가 없으면 부모 태그로 올라가며 찾는다 (언리얼과 같은 규칙).
    /// </summary>
    public static class GameplayCueManager
    {
        private static readonly Dictionary<int, List<GameplayCueNotify>> Notifies = new Dictionary<int, List<GameplayCueNotify>>();
        private static readonly Dictionary<int, List<GameplayCueHandler>> Handlers = new Dictionary<int, List<GameplayCueHandler>>();
        private static readonly List<IGameplayCueInterface> InterfaceBuffer = new List<IGameplayCueInterface>(4);

        public static void RegisterNotify(GameplayCueNotify notify)
        {
            if (notify == null || !notify.GameplayCueTag.IsValid) return;
            int hash = notify.GameplayCueTag.Hash;
            if (!Notifies.TryGetValue(hash, out var list)) Notifies[hash] = list = new List<GameplayCueNotify>(1);
            if (!list.Contains(notify)) list.Add(notify);
        }

        public static void RegisterHandler(GameplayTag cueTag, GameplayCueHandler handler)
        {
            if (!cueTag.IsValid || handler == null) return;
            if (!Handlers.TryGetValue(cueTag.Hash, out var list)) Handlers[cueTag.Hash] = list = new List<GameplayCueHandler>(1);
            if (!list.Contains(handler)) list.Add(handler);
        }

        public static void UnregisterHandler(GameplayTag cueTag, GameplayCueHandler handler)
        {
            if (Handlers.TryGetValue(cueTag.Hash, out var list)) list.Remove(handler);
        }

        /// <summary>이 피어에서 큐를 실제로 재생한다. 네트워크 전파는 ASC 가 담당한다.</summary>
        public static void HandleGameplayCue(GameObject target, GameplayTag cueTag, GameplayCueEvent eventType, in GameplayCueParameters parameters)
        {
            if (!cueTag.IsValid) return;

            if (target != null)
            {
                InterfaceBuffer.Clear();
                target.GetComponents(InterfaceBuffer);
                for (int i = 0; i < InterfaceBuffer.Count; i++)
                {
                    try { InterfaceBuffer[i].HandleGameplayCue(cueTag, eventType, parameters); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }

            // 정확한 태그 → 부모 순으로 첫 번째로 처리하는 쪽을 찾는다.
            if (Dispatch(cueTag.Hash, target, cueTag, eventType, parameters)) return;
            var parents = GameplayTagManager.GetParentHashes(cueTag.Hash);
            for (int i = 0; i < parents.Length; i++)
                if (Dispatch(parents[i], target, cueTag, eventType, parameters)) return;
        }

        private static bool Dispatch(int hash, GameObject target, GameplayTag cueTag, GameplayCueEvent eventType, in GameplayCueParameters parameters)
        {
            bool handled = false;
            if (Notifies.TryGetValue(hash, out var notifies) && notifies.Count > 0)
            {
                for (int i = 0; i < notifies.Count; i++)
                {
                    try { notifies[i].HandleGameplayCue(target, eventType, parameters); }
                    catch (Exception e) { Debug.LogException(e); }
                }
                handled = true;
            }
            if (Handlers.TryGetValue(hash, out var handlers) && handlers.Count > 0)
            {
                for (int i = 0; i < handlers.Count; i++)
                {
                    try { handlers[i](target, cueTag, eventType, parameters); }
                    catch (Exception e) { Debug.LogException(e); }
                }
                handled = true;
            }
            return handled;
        }
    }
}
