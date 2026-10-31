using Framework;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 미니맵 (오른쪽 위, HUD 레이어). 플레이어 주변 EConst.MinimapRadius 반경을 카메라 방향 기준으로 보여 준다.
/// 입력을 막지 않는다. 월드맵이 열려 있는 동안은 숨긴다. 레이아웃은 프리팹 UI/Windows/MinimapWindow.
/// </summary>
public class MinimapWindow : UIWindow
{
    public MapView View;
    public RectTransform Frame;

    private WorldMapWindow WorldMap;

    public override bool BlocksGameInput
    {
        get { return false; }
    }

    protected override void OnInitialize()
    {
        View.ViewRadius = Tables.Const(EConst.MinimapRadius);
    }

    private void Update()
    {
        if (WorldMap == null)
        {
            var ui = Managers.Get<UIManager>();
            WorldMap = ui != null ? ui.Get<WorldMapWindow>() : null;
        }

        bool show = WorldMap == null || WorldMap.IsOpen == false;
        if (Frame.gameObject.activeSelf != show)
        {
            Frame.gameObject.SetActive(show);
        }

        if (show == true)
        {
            View.Refresh();
        }
    }
}
