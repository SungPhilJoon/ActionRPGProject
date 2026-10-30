using System.Collections.Generic;
using Framework.StateTree;

namespace Framework.Editor
{
    /// <summary>
    /// StateTree 에디터 확장점. 프레임워크는 게임 타입을 모르므로, 게임 에디터 코드가 이 클래스를 상속하면
    /// StateTree 창이 자동으로 찾아(TypeCache) AI 작성 도우미를 붙인다. 창은 여전히 "AI 를 만드는 툴" 이다.
    ///  - Templates : New 메뉴의 트리 템플릿 (예: 근접/원거리/보스 몬스터)
    ///  - Blocks    : 선택 상태 아래에 한 번에 넣는 상태 묶음 (예: 공격·추적·대기, 조건부 어빌리티 사용)
    ///  - DrawContext : 트리 위 정보 패널 (예: 이 트리를 쓰는 몬스터와 그 어빌리티, 경고)
    ///  - DrawToolbar : 툴바 오른쪽 버튼 (예: Play 중 이 트리로 몬스터 스폰)
    /// 구현은 하나만 쓴다 (여러 개면 이름순 첫 번째).
    /// </summary>
    public abstract class StateTreeEditorExtension
    {
        public virtual void OnEnable(StateTreeEditorWindow InWindow)
        {
        }

        public virtual void OnDisable(StateTreeEditorWindow InWindow)
        {
        }

        public virtual IReadOnlyList<string> TemplateNames
        {
            get { return new string[0]; }
        }

        /// <summary>InIndex 번째 템플릿으로 새 트리 (저장은 창이 한다).</summary>
        public virtual StateTreeAsset CreateTemplate(int InIndex)
        {
            return null;
        }

        public virtual IReadOnlyList<string> BlockNames
        {
            get { return new string[0]; }
        }

        /// <summary>InParentId(0 = 루트) 아래에 InIndex 번째 블록을 추가한다. 창이 Undo 기록과 저장 표시를 한다.</summary>
        public virtual void AddBlock(StateTreeAsset InAsset, int InParentId, int InIndex)
        {
        }

        public virtual void DrawContext(StateTreeEditorWindow InWindow, StateTreeAsset InAsset)
        {
        }

        public virtual void DrawToolbar(StateTreeEditorWindow InWindow, StateTreeAsset InAsset)
        {
        }
    }
}
