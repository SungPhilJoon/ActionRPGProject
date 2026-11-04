using UnityEngine;

namespace Framework
{
    /// <summary>
    /// 프로젝트의 모든 MonoBehaviour 가 상속하는 공통 부모.
    ///  - transform / gameObject 를 캐싱한다 (Unity 프로퍼티의 네이티브 호출 비용 제거).
    ///  - Awake / OnDestroy 를 봉인하고 OnUnityAwake / OnUnityDestroy 로만 확장하게 한다.
    ///    파생 클래스는 Awake/OnDestroy 를 직접 선언하지 않는다.
    ///  - 외부에서 받은 Behaviour 가 Actor / UI 인지는 타입으로 확인한다 (`is ActorBehaviour actor`, `is UIBehaviour`).
    /// </summary>
    public abstract class BaseBehaviour : Behaviour
    {
        private Transform cachedTransform = null;
        private GameObject cachedGameObject = null;

        /// <summary>Awake 전에(비활성 상태로 AddComponent 된 경우 등) 접근해도 안전하도록 지연 캐싱한다.</summary>
        public new Transform transform
        {
            get
            {
                if (ReferenceEquals(cachedTransform, null) == true)
                {
                    cachedTransform = base.transform;
                }

                return cachedTransform;
            }
        }

        public new GameObject gameObject
        {
            get
            {
                if (ReferenceEquals(cachedGameObject, null) == true)
                {
                    cachedGameObject = base.gameObject;
                }

                return cachedGameObject;
            }
        }

        protected sealed override void Awake()
        {
            cachedTransform = base.transform;
            cachedGameObject = cachedTransform.gameObject;

            OnUnityAwake();
        }

        protected sealed override void OnDestroy()
        {
            OnUnityDestroy();

            cachedGameObject = null;
            cachedTransform = null;
        }

        protected virtual void OnUnityAwake()
        {
        }

        protected virtual void OnUnityDestroy()
        {
        }

        public virtual void SetName(string InName)
        {
            gameObject.name = InName;
        }

        public virtual string GetName()
        {
            return gameObject.name;
        }

        /// <summary>UI 는 레이아웃(앵커/로컬 좌표)을 유지하고, 그 외는 월드 위치를 유지한 채 붙인다.</summary>
        public virtual void SetParent(Transform InParent)
        {
            if (transform.parent == InParent)
            {
                return;
            }

            bool isUI = this is UIBehaviour;
            transform.SetParent(InParent, isUI == false);
        }
    }
}
