using UnityEngine;

namespace Framework
{
    /// <summary>
    /// UI 요소(창, 위젯, 오버레이 레이어) 공통 부모. RectTransform 캐시와 표시/숨김을 제공한다.
    /// UnityEngine.EventSystems.UIBehaviour 와 이름이 같으므로 두 네임스페이스를 함께 using 하는 파일에서는 한정해서 쓴다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class UIBehaviour : BaseBehaviour
    {
        private RectTransform CachedRect;

        public RectTransform Rect
        {
            get
            {
                if (ReferenceEquals(CachedRect, null) == true)
                {
                    CachedRect = transform as RectTransform;
                }

                return CachedRect;
            }
        }

        public bool IsVisible
        {
            get { return gameObject.activeSelf; }
        }

        public void SetVisible(bool InVisible)
        {
            if (gameObject.activeSelf == InVisible)
            {
                return;
            }

            gameObject.SetActive(InVisible);
        }

        protected override void OnUnityDestroy()
        {
            CachedRect = null;
            base.OnUnityDestroy();
        }
    }
}
