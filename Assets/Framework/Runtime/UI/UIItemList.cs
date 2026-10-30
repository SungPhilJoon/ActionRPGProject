using System.Collections.Generic;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 같은 위젯 프리팹을 여러 개 띄우는 목록 (목록 행, 인벤토리 칸, 월드 체력바 …).
    /// 템플릿은 프리팹 에셋(창 프리팹에 직렬화된 참조)이며, 필요한 만큼 Instantiate 하고 남는 것은 끄고 재사용한다.
    /// 자식을 코드로 조립하지 않고 프리팹 단위로만 만든다.
    /// </summary>
    public class UIItemList<T> where T : Component
    {
        private readonly T Template;
        private readonly Transform Parent;
        private readonly List<T> Items = new List<T>();
        private int ActiveCount;

        public UIItemList(T InTemplate, Transform InParent)
        {
            Template = InTemplate;
            Parent = InParent;
        }

        public int Count
        {
            get { return ActiveCount; }
        }

        public T this[int InIndex]
        {
            get { return Items[InIndex]; }
        }

        /// <summary>정확히 InCount 개만 켠다 (모자라면 Instantiate).</summary>
        public void SetCount(int InCount)
        {
            while (Items.Count < InCount)
            {
                Items.Add(spawn());
            }

            for (int i = 0; i < Items.Count; ++i)
            {
                bool active = i < InCount;
                if (Items[i].gameObject.activeSelf != active)
                {
                    Items[i].gameObject.SetActive(active);
                }
            }

            ActiveCount = InCount;
        }

        /// <summary>다음 항목 하나를 켜서 돌려준다. 맨 뒤 형제로 옮겨 레이아웃 순서를 유지한다.</summary>
        public T Add()
        {
            if (ActiveCount >= Items.Count)
            {
                Items.Add(spawn());
            }

            var item = Items[ActiveCount];
            item.gameObject.SetActive(true);
            item.transform.SetAsLastSibling();
            ActiveCount += 1;
            return item;
        }

        /// <summary>모든 항목을 끈다 (파괴하지 않음).</summary>
        public void Clear()
        {
            SetCount(0);
        }

        private T spawn()
        {
            var item = Object.Instantiate(Template, Parent, false);
            item.name = Template.name;
            return item;
        }
    }
}
