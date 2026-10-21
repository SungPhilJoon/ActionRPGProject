using System.Text;
using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>치트 콘솔 (` 키). 입력 중에는 게임 입력을 막는다.</summary>
public class CheatConsoleWindow : UIWindow
{
    private const int VisibleLines = 18;

    private TMP_InputField Input;
    private TextMeshProUGUI Output;

    protected override void Build()
    {
        var frame = UIFactory.CreateWindowFrame(Rect, "Frame");
        UIFactory.Place(frame, new Vector2(0.0f, 1.0f), new Vector2(20.0f, -20.0f), new Vector2(900.0f, 520.0f));

        var title = UIFactory.CreateText(frame, "Title", Tables.Text(EText.CheatTitle), 24.0f, TextAlignmentOptions.Left);
        UIFactory.Place(title, new Vector2(0.0f, 1.0f), new Vector2(16.0f, -8.0f), new Vector2(860.0f, 34.0f));

        Output = UIFactory.CreateText(frame, "Output", Tables.Text(EText.CheatHint), 17.0f, TextAlignmentOptions.BottomLeft);
        Output.textWrappingMode = TextWrappingModes.Normal;
        Output.overflowMode = TextOverflowModes.Truncate;
        UIFactory.Place(Output, new Vector2(0.0f, 0.0f), new Vector2(16.0f, 64.0f), new Vector2(868.0f, 410.0f));

        Input = UIFactory.CreateInputField(frame, "Input", "help", 20.0f);
        UIFactory.Place(Input, new Vector2(0.0f, 0.0f), new Vector2(12.0f, 12.0f), new Vector2(876.0f, 44.0f));
        Input.onSubmit.AddListener(submit);
        Input.onSelect.AddListener(text => setFocus(true));
        Input.onDeselect.AddListener(text => setFocus(false));
    }

    private void OnEnable()
    {
        var cheat = Managers.Get<CheatManager>();
        if (cheat != null)
        {
            cheat.OnOutput += onOutput;
        }
    }

    private void OnDisable()
    {
        var cheat = Managers.Get<CheatManager>();
        if (cheat != null)
        {
            cheat.OnOutput -= onOutput;
        }

        setFocus(false);
    }

    protected override void OnOpen()
    {
        refreshOutput();
        Input.text = string.Empty;
        Input.ActivateInputField();
        Input.Select();
    }

    private void submit(string InText)
    {
        Managers.Get<CheatManager>().Execute(InText);
        Input.text = string.Empty;
        Input.ActivateInputField();
    }

    private void onOutput(string InLine)
    {
        refreshOutput();
    }

    private void refreshOutput()
    {
        var lines = Managers.Get<CheatManager>().Lines;
        var builder = new StringBuilder();
        int start = Mathf.Max(0, lines.Count - VisibleLines);
        for (int i = start; i < lines.Count; ++i)
        {
            builder.AppendLine(lines[i]);
        }

        Output.text = lines.Count == 0 ? Tables.Text(EText.CheatHint) : builder.ToString();
    }

    private void setFocus(bool InFocused)
    {
        var input = Managers.Get<GameInput>();
        if (input != null)
        {
            input.TextInputFocused = InFocused;
        }
    }
}
