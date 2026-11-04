using System;
using Framework.UI;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 타이틀. 현재는 오프라인 1인 플레이만 활성화하고, 멀티플레이(최대 4인) 버튼은 다음 단계용으로 비활성 표시한다.
/// 버튼은 프리팹에 영구 리스너로 OnStartClicked / OnQuitClicked 가 연결되어 있다.
/// </summary>
public class TitleWindow : UIWindow
{
    public override UILayer GetLayer()
    {
        return UILayer.System;
    }

    public Action OnStartSinglePlayer;

    public TextMeshProUGUI Status;
    public Button StartButton;

    public void SetLoading(bool InLoading)
    {
        Status.text = InLoading == true ? Tables.Text(EText.Loading) : string.Empty;
        StartButton.interactable = InLoading == false;
    }

    public void OnStartClicked()
    {
        OnStartSinglePlayer?.Invoke();
    }

    /// <summary>언어 버튼 (프리팹 영구 리스너). 다음 언어로 바꾸고, 버튼 문구는 UIText 가 LanguageChangedEvent 로 갱신한다.</summary>
    public void OnLanguageClicked()
    {
        GameLocalization.Next();
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        UnityEngine.Application.Quit();
#endif
    }
}
