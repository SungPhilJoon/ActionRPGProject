using System.Collections.Generic;

namespace Framework.UI
{
    /// <summary>
    /// UI 레이어 단계 (2026-11-01). 레이어마다 캔버스 하나 (UIManager.RegistLayers), 정렬 순서가 클수록 위에 그려진다.
    /// 각 UIBehaviour 는 virtual GetLayer() 로 자기가 들어갈 레이어를 고르고, UIManager 는 그 레이어 캔버스의 자식으로 만든다.
    /// 레이어가 정책도 가진다: 열려 있으면 게임 입력을 막는가(UIWindow.BlocksGameInput 기본값), ESC(CloseAll)로 닫히는가.
    /// 예전에는 게임이 int 레이어 번호를 Create 호출마다 넘기고, 입력 차단은 창마다 따로 override 했다.
    /// </summary>
    public abstract class UILayer
    {
        public static readonly UILayer World = new WorldUILayer();
        public static readonly UILayer Hud = new HudUILayer();
        public static readonly UILayer Window = new WindowUILayer();
        public static readonly UILayer Popup = new PopupUILayer();
        public static readonly UILayer Toast = new ToastUILayer();
        public static readonly UILayer System = new SystemUILayer();

        /// <summary>아래 → 위 순서.</summary>
        public static readonly IReadOnlyList<UILayer> All = new[] { World, Hud, Window, Popup, Toast, System };

        public abstract string Name { get; }

        /// <summary>캔버스 정렬 순서.</summary>
        public abstract int SortingOrder { get; }

        /// <summary>이 레이어의 창이 열려 있으면 게임 입력(공격 · 카메라 줌 등)을 막는가.</summary>
        public virtual bool BlocksGameInput
        {
            get { return false; }
        }

        /// <summary>ESC(UIManager.CloseAll)로 닫히는가.</summary>
        public virtual bool ClosesOnCancel
        {
            get { return false; }
        }
    }

    /// <summary>월드 좌표를 따라가는 UI (머리 위 체력바 · 데미지 숫자).</summary>
    public sealed class WorldUILayer : UILayer
    {
        public override string Name => "World";
        public override int SortingOrder => 0;
    }

    /// <summary>항상 떠 있는 HUD (체력 · 어빌리티 · 미니맵). 입력을 막지 않는다.</summary>
    public sealed class HudUILayer : UILayer
    {
        public override string Name => "Hud";
        public override int SortingOrder => 10;
    }

    /// <summary>열고 닫는 창 (인벤토리 · 제작 · 지도 …). 열려 있으면 게임 입력을 막고 ESC 로 닫힌다.</summary>
    public sealed class WindowUILayer : UILayer
    {
        public override string Name => "Window";
        public override int SortingOrder => 20;
        public override bool BlocksGameInput => true;
        public override bool ClosesOnCancel => true;
    }

    /// <summary>창 위에 겹치는 정보 오버레이 (밸런스 미터). 입력을 막지 않는다.</summary>
    public sealed class PopupUILayer : UILayer
    {
        public override string Name => "Popup";
        public override int SortingOrder => 30;
    }

    /// <summary>토스트 알림 (아이템 획득 · 메시지). 창보다 위, 시스템 창보다 아래. 입력을 막지 않는다.</summary>
    public sealed class ToastUILayer : UILayer
    {
        public override string Name => "Toast";
        public override int SortingOrder => 35;
    }

    /// <summary>타이틀 · 치트 콘솔. 맨 위, 입력을 막지만 ESC 로 한꺼번에 닫히지는 않는다.</summary>
    public sealed class SystemUILayer : UILayer
    {
        public override string Name => "System";
        public override int SortingOrder => 40;
        public override bool BlocksGameInput => true;
    }
}
