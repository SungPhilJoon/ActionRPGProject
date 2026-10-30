using System.Text;
using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>치트 콘솔 (` 키). 입력 중에는 게임 입력을 막는다.</summary>
public class CheatConsoleWindow : UIWindow
{
    private const int VisibleLines = 18;

    public TMP_InputField Input;
    public TextMeshProUGUI Output;

    protected override void OnInitialize()
    {
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
