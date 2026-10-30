using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 프리팹으로 만든 창의 공통 부모. 자식 참조(텍스트, 버튼, 목록 루트 …)는 프리팹에 직렬화되어 있고,
    /// UIManager 가 Addressables 로 프리팹을 로드 → Instantiate → <see cref="Initialize"/> 한 번 호출한다.
    /// 런타임에 자식 Transform 을 만들거나 찾아서 연결하지 않는다 (2026-10-29, Documentation/VRising/UI.md).
    /// BlocksGameInput 이 true 인 창이 하나라도 열려 있으면 게임 입력(공격/스킬)을 막는다.
    /// </summary>
    public abstract class UIWindow : UIBehaviour
    {
        private bool Initialized;

        public bool IsOpen
        {
            get { return IsVisible; }
        }

        public virtual bool BlocksGameInput
        {
            get { return true; }
        }

        /// <summary>Instantiate 직후 한 번. 동적 목록(UIItemList) 준비와 코드 리스너 연결만 한다.</summary>
        public void Initialize()
        {
            if (Initialized == true)
            {
                return;
            }

            Initialized = true;
            OnInitialize();
        }

        protected virtual void OnInitialize()
        {
        }

        public void Open()
        {
            SetVisible(true);
            transform.SetAsLastSibling();
            OnOpen();
        }

        public void Close()
        {
            if (IsVisible == false)
            {
                return;
            }

            SetVisible(false);
            OnClose();
        }

        public void Toggle()
        {
            if (IsOpen == true)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        protected virtual void OnOpen()
        {
        }

        protected virtual void OnClose()
        {
        }
    }
}
