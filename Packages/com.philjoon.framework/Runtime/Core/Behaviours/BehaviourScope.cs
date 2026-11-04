using UnityEngine;

namespace Framework
{
    /// <summary>
    /// StaticBehaviour / DynamicBehaviour 가 맡는 범위 판정. 노드에서 위로 올라가며 처음 만나는 Static/Dynamic Behaviour 가 그 노드의 주인이다
    /// → 정적 물체 아래 붙은 동적 물체(또는 그 반대)는 각자 자기 규칙을 따른다.
    /// 캐싱 · 에디터 플래그 적용 때만 부른다 (매 프레임 호출 금지).
    /// </summary>
    internal static class BehaviourScope
    {
        public static bool IsOwnedBy(Transform InNode, BaseBehaviour InOwner)
        {
            var node = InNode;
            while (node != null)
            {
                var owner = ownerOn(node);
                if (owner != null)
                {
                    return ReferenceEquals(owner, InOwner) == true;
                }

                node = node.parent;
            }

            return false;
        }

        private static BaseBehaviour ownerOn(Transform InNode)
        {
            var staticOwner = InNode.GetComponent<StaticBehaviour>();
            if (staticOwner != null)
            {
                return staticOwner;
            }

            return InNode.GetComponent<DynamicBehaviour>();
        }
    }
}
