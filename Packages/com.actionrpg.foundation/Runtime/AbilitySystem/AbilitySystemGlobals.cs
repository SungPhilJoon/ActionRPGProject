using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 언리얼 UAbilitySystemGlobals + 게임플레이 태그 설정에 해당한다. Resources/AbilitySystemGlobals.asset 하나를 둔다.
    ///
    /// 네트워크로는 이펙트 · 어빌리티를 에셋 ID(이름 해시)로만 주고받는다. 클라이언트가 그 ID 를 에셋으로 되돌리려면
    /// 에셋이 메모리에 올라와 있어야 하므로, 여기 목록에 모든 이펙트 · 어빌리티 · 큐를 등록해 둔다.
    /// (에디터 메뉴 "ActionRPG/Ability System/Globals 갱신" 이 프로젝트를 훑어 자동으로 채운다.)
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Ability System/Ability System Globals", fileName = "AbilitySystemGlobals")]
    public class AbilitySystemGlobals : ScriptableObject
    {
        public const string ResourcePath = "AbilitySystemGlobals";

        public List<GameplayEffect> GameplayEffects = new List<GameplayEffect>();
        public List<GameplayAbility> GameplayAbilities = new List<GameplayAbility>();
        public List<GameplayCueNotify> GameplayCueNotifies = new List<GameplayCueNotify>();
        [Tooltip("미리 등록해 둘 태그 이름. 계층 판정을 하려면 클라이언트도 태그 이름을 알아야 한다.")]
        public List<string> GameplayTagList = new List<string>();

        private static AbilitySystemGlobals _instance;
        private static bool _initialized;
        private static readonly Dictionary<int, Object> AssetsById = new Dictionary<int, Object>(128);

        /// <summary>복제본을 만드는 동안 OnEnable 이 자신을 등록하지 않도록 막는다.</summary>
        internal static bool SuppressRegistration;

        /// <summary>Resources 의 글로벌 에셋을 읽어 등록을 끝낸다. 여러 번 불러도 한 번만 동작한다.</summary>
        public static AbilitySystemGlobals Get()
        {
            if (_initialized) return _instance;
            _initialized = true;
            _instance = Resources.Load<AbilitySystemGlobals>(ResourcePath);
            if (_instance == null)
            {
                Debug.LogWarning($"[GAS] Resources/{ResourcePath}.asset 이 없습니다. 클라이언트가 이펙트 ID 를 해석하지 못할 수 있습니다. " +
                                 "ActionRPG > Ability System > Globals 갱신 메뉴로 만드세요.");
                return null;
            }
            _instance.InitGlobalData();
            return _instance;
        }

        /// <summary>언리얼 InitGlobalData.</summary>
        public void InitGlobalData()
        {
            for (int i = 0; i < GameplayTagList.Count; i++) GameplayTag.Request(GameplayTagList[i]);
            for (int i = 0; i < GameplayEffects.Count; i++) RegisterAsset(GameplayEffects[i]);
            for (int i = 0; i < GameplayAbilities.Count; i++) RegisterAsset(GameplayAbilities[i]);
            for (int i = 0; i < GameplayCueNotifies.Count; i++) GameplayCueManager.RegisterNotify(GameplayCueNotifies[i]);
        }

        /// <summary>에셋을 ID 표에 등록한다. GameplayEffect / GameplayAbility 의 OnEnable 에서도 호출된다.</summary>
        public static void RegisterAsset(Object asset)
        {
            if (asset == null || SuppressRegistration || string.IsNullOrEmpty(asset.name)) return;
            if (asset is GameplayAbility ability && ability.IsInstantiated) return;

            int id = ComputeId(asset);
            if (AssetsById.TryGetValue(id, out var existing) && existing != null && existing != asset)
            {
                // 같은 타입 · 이름의 에셋이 둘이면 먼저 등록된 것을 유지한다.
                Debug.LogError($"[GAS] 에셋 ID 충돌: '{asset.name}' 이 두 개 있습니다. 같은 타입의 에셋 이름은 겹치면 안 됩니다.", asset);
                return;
            }
            AssetsById[id] = asset;
        }

        /// <summary>
        /// 네트워크 식별자. "타입이름:에셋이름" 의 해시라서 모든 피어가 같은 값을 얻는다.
        /// 복제본(어빌리티 인스턴스)은 원본 에셋의 ID 를 쓴다.
        /// </summary>
        public static int GetAssetId(Object asset)
        {
            if (asset == null) return 0;
            if (asset is GameplayAbility ability && ability.SourceAsset != null) asset = ability.SourceAsset;
            int id = ComputeId(asset);
            if (!AssetsById.TryGetValue(id, out var existing) || existing == null) RegisterAsset(asset);
            return id;
        }

        private static int ComputeId(Object asset) => GameplayTagManager.ComputeHash(asset.GetType().Name + ":" + asset.name);

        public static T FindAssetById<T>(int id) where T : Object
        {
            if (id == 0) return null;
            if (!_initialized) Get();
            return AssetsById.TryGetValue(id, out var asset) ? asset as T : null;
        }

        public static GameplayEffect FindEffectById(int id) => FindAssetById<GameplayEffect>(id);
        public static GameplayAbility FindAbilityById(int id) => FindAssetById<GameplayAbility>(id);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // 에셋 ID 표는 에셋 수명과 같으므로 지우지 않는다. 글로벌 에셋만 다시 읽게 한다.
            _initialized = false;
            _instance = null;
            SuppressRegistration = false;
        }
    }

    /// <summary>언리얼 UAbilitySystemBlueprintLibrary 의 자주 쓰는 함수들.</summary>
    public static class AbilitySystemStatics
    {
        /// <summary>GameObject 에서 ASC 를 찾는다. IAbilitySystemInterface → 같은 오브젝트 → 부모 순.</summary>
        public static AbilitySystemComponent GetAbilitySystemComponent(GameObject actor)
        {
            if (actor == null) return null;
            if (actor.TryGetComponent<IAbilitySystemInterface>(out var provider))
            {
                var fromInterface = provider.GetAbilitySystemComponent();
                if (fromInterface != null) return fromInterface;
            }
            if (actor.TryGetComponent<AbilitySystemComponent>(out var asc)) return asc;
            return actor.GetComponentInParent<AbilitySystemComponent>();
        }

        public static AbilitySystemComponent GetAbilitySystemComponent(Component component) =>
            component != null ? GetAbilitySystemComponent(component.gameObject) : null;

        /// <summary>언리얼 SendGameplayEventToActor. 이벤트 트리거 어빌리티와 WaitGameplayEvent 태스크가 반응한다.</summary>
        public static void SendGameplayEventToActor(GameObject actor, GameplayTag eventTag, GameplayEventData payload)
        {
            var asc = GetAbilitySystemComponent(actor);
            if (asc == null) return;
            payload.EventTag = eventTag;
            asc.HandleGameplayEvent(eventTag, payload);
        }

        /// <summary>쿨다운처럼 태그로 찾은 활성 이펙트의 남은 시간 중 가장 긴 값.</summary>
        public static float GetTimeRemaining(AbilitySystemComponent asc, GameplayTagContainer tags)
        {
            if (asc == null || tags == null || tags.IsEmpty) return 0f;
            asc.GetActiveEffectsTimeRemainingAndDuration(GameplayEffectQuery.MakeQuery_MatchAnyOwningTags(tags), out float remaining, out _);
            return remaining;
        }
    }

}
