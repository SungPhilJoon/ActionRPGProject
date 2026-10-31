using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// 월드맵 (M). 월드 전체를 북쪽이 위로 보여 주고, 플레이어 · 상인 · 성 · 보스 · 퀘스트 대상 · 지역 이름을 표시한다.
/// 입력을 막는 창이라 열려 있는 동안 Combat 입력 맵(공격 · 카메라 줌)이 꺼진다. 레이아웃은 프리팹 UI/Windows/WorldMapWindow.
/// </summary>
public class WorldMapWindow : UIWindow
{
    public MapView View;
    public TextMeshProUGUI Hint;

    protected override void OnInitialize()
    {
        refreshHint();
    }

    protected override void OnOpen()
    {
        refreshHint();
        View.Refresh();
    }

    private void refreshHint()
    {
        Hint.text = $"[{UIUtil.KeyLabel(EInputAction.WorldMap)}] / [{UIUtil.KeyLabel(EInputAction.Cancel)}] {Tables.Text(EText.MapHint)}";
    }

    private void Update()
    {
        View.Refresh();
    }
}
