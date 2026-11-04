#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Framework
{
    /// <summary>
    /// (에디터 전용) StaticBehaviour / DynamicBehaviour 가 맡는 오브젝트의 Static 플래그를 맞춘다.
    ///  - StaticBehaviour: Batching Static (+ Occluder/Occludee, Contribute GI 옵션) 을 켠다. 이미 켜진 다른 플래그는 건드리지 않는다.
    ///  - DynamicBehaviour: Batching Static · Occluder Static 을 끈다 (Occludee 는 유지 — 사라져도 가려질 수는 있다).
    /// OnValidate · Reset(인스펙터에서 붙일 때) 은 delayCall 로 미룬다 — 그 안에서 다른 오브젝트를 바꾸면 안 된다.
    /// 스크립트 AddComponent 는 Reset/OnValidate 가 불리지 않으므로 Framework.Editor 의 StaticBehaviourFlagsHook 이 씬 · 프리팹 저장 때 다시 적용한다.
    /// 바뀔 때만 기록해 불필요한 dirty 를 만들지 않는다.
    /// </summary>
    public static class StaticEditorFlagsUtil
    {
        private const StaticEditorFlags OcclusionFlags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

        public static void ApplyDeferred(BaseBehaviour InOwner)
        {
            if (Application.isPlaying == true || InOwner == null)
            {
                return;
            }

            EditorApplication.delayCall += () => Apply(InOwner);
        }

        /// <summary>지금 바로 적용한다 (StaticBehaviour / DynamicBehaviour 가 아니면 무시). 씬 · 프리팹 저장 훅과 메뉴가 쓴다.</summary>
        public static void Apply(BaseBehaviour InOwner)
        {
            if ((InOwner is StaticBehaviour) == false && (InOwner is DynamicBehaviour) == false)
            {
                return;
            }

            if (InOwner == null || Application.isPlaying == true)
            {
                return;
            }

            var staticOwner = InOwner as StaticBehaviour;
            bool includeChildren = staticOwner == null || staticOwner.IncludeChildren == true;
            var nodes = includeChildren == true ? InOwner.GetComponentsInChildren<Transform>(true) : new[] { InOwner.transform };
            foreach (var node in nodes)
            {
                if (BehaviourScope.IsOwnedBy(node, InOwner) == false)
                {
                    continue;
                }

                var go = node.gameObject;
                var before = GameObjectUtility.GetStaticEditorFlags(go);
                var after = staticOwner != null ? staticFlags(staticOwner, before) : dynamicFlags(before);
                if (after != before)
                {
                    Undo.RecordObject(go, "Static Flags");
                    GameObjectUtility.SetStaticEditorFlags(go, after);
                    EditorUtility.SetDirty(go);
                }
            }
        }

        private static StaticEditorFlags staticFlags(StaticBehaviour InOwner, StaticEditorFlags InFlags)
        {
            var flags = InFlags | StaticEditorFlags.BatchingStatic;
            if (InOwner.OcclusionStatic == true)
            {
                flags |= OcclusionFlags;
            }

            if (InOwner.ContributeGI == true)
            {
                flags |= StaticEditorFlags.ContributeGI;
            }

            return flags;
        }

        private static StaticEditorFlags dynamicFlags(StaticEditorFlags InFlags)
        {
            return InFlags & ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        }
    }
}
#endif
