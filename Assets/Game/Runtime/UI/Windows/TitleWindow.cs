using System;
using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// 타이틀. 현재는 오프라인 1인 플레이만 활성화하고, 멀티플레이(최대 4인) 버튼은 다음 단계용으로 비활성 표시한다.
/// </summary>
public class TitleWindow : UIWindow
{
    public Action OnStartSinglePlayer;

    private TextMeshProUGUI Status;
    private UnityEngine.UI.Button StartButton;

    protected override void Build()
    {
        var back = UIFactory.CreatePanel(Rect, "Back", new Color(0.03f, 0.0f, 0.02f, 0.82f));
        UIFactory.Stretch(back, 0.0f);

        var title = UIFactory.CreateText(back, "Title", Tables.Text(EText.GameTitle), 84.0f, TextAlignmentOptions.Center);
        title.color = new Color(0.85f, 0.12f, 0.15f);
        title.fontStyle = FontStyles.Bold;
        UIFactory.Place(title, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 220.0f), new Vector2(1400.0f, 120.0f));

        var controls = UIFactory.CreateText(back, "Controls", Tables.Text(EText.Controls), 20.0f, TextAlignmentOptions.Center);
        controls.color = new Color(1.0f, 1.0f, 1.0f, 0.7f);
        UIFactory.Place(controls, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 60.0f), new Vector2(1500.0f, 160.0f));

        StartButton = UIFactory.CreateButton(back, "Single", Tables.Text(EText.SinglePlayer), 28.0f, onStart);
        UIFactory.Place(StartButton, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 40.0f), new Vector2(420.0f, 70.0f));

        var multi = UIFactory.CreateButton(back, "Multi", Tables.Text(EText.MultiplayerSoon), 22.0f, null);
        multi.interactable = false;
        UIFactory.Place(multi, new Vector2(0.5f, 0.5f), new Vector2(0.0f, -50.0f), new Vector2(420.0f, 60.0f));

        var quit = UIFactory.CreateButton(back, "Quit", Tables.Text(EText.Quit), 22.0f, onQuit);
        UIFactory.Place(quit, new Vector2(0.5f, 0.5f), new Vector2(0.0f, -130.0f), new Vector2(420.0f, 60.0f));

        Status = UIFactory.CreateText(back, "Status", string.Empty, 22.0f, TextAlignmentOptions.Center);
        UIFactory.Place(Status, new Vector2(0.5f, 0.5f), new Vector2(0.0f, -210.0f), new Vector2(800.0f, 40.0f));
    }

    public void SetLoading(bool InLoading)
    {
        Status.text = InLoading == true ? Tables.Text(EText.Loading) : string.Empty;
        StartButton.interactable = InLoading == false;
    }

    private void onStart()
    {
        OnStartSinglePlayer?.Invoke();
    }

    private void onQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
