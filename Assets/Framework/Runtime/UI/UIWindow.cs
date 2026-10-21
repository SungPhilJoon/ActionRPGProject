using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 코드로 만든 창의 공통 부모. Build 에서 UIFactory 로 자식을 조립한다.
    /// BlocksGameInput 이 true 인 창이 하나라도 열려 있으면 게임 입력(공격/스킬)을 막는다.
    /// </summary>
    public abstract class UIWindow : MonoBehaviour
    {
        public RectTransform Rect { get; private set; }
        public bool IsOpen
        {
            get { return gameObject.activeSelf; }
        }

        public virtual bool BlocksGameInput
        {
            get { return true; }
        }

        public void Construct()
        {
            Rect = GetComponent<RectTransform>();
            Build();
        }

        protected abstract void Build();

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            OnOpen();
        }

        public void Close()
        {
            if (gameObject.activeSelf == false)
            {
                return;
            }

            gameObject.SetActive(false);
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
