using Framework;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// InputTable.csv 로부터 Input System 액션을 코드로 생성한다.
/// 게임 코드는 EInputAction(int) 으로만 입력을 조회하고, 키 변경은 CSV 수정으로 끝난다.
///  - Binding1/Binding2: 컨트롤 경로 (예: &lt;Keyboard&gt;/q)
///  - Composite 가 있으면 Binding1 을 ';' 로 나눠 composite 파트(up;down;left;right)로 쓴다.
/// </summary>
public class GameInput : IManager
{
    private const char PartSeparator = ';';
    private static readonly string[] Vector2Parts = { "up", "down", "left", "right" };

    private InputActionMap Map;
    private readonly InputAction[] Actions = new InputAction[(int)EInputAction.Count];

    /// <summary>텍스트 입력 중(치트 콘솔)이면 게임 입력을 막는다.</summary>
    public bool TextInputFocused;

    void IManager.Initialize()
    {
        Map = new InputActionMap("Gameplay");
        var rows = Tables.Root.Input.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            createAction(rows[i]);
        }

        Map.Enable();
    }

    void IManager.UnInitialize()
    {
        if (Map != null)
        {
            Map.Disable();
            Map.Dispose();
            Map = null;
        }
    }

    void IManager.Update(float InDeltaTime)
    {
    }

    public bool Pressed(EInputAction InAction)
    {
        var action = get(InAction);
        return action != null && TextInputFocused == false && action.WasPressedThisFrame() == true;
    }

    /// <summary>텍스트 입력 중에도 받아야 하는 키 (콘솔 토글, 제출, 취소).</summary>
    public bool PressedAlways(EInputAction InAction)
    {
        var action = get(InAction);
        return action != null && action.WasPressedThisFrame() == true;
    }

    public bool Held(EInputAction InAction)
    {
        var action = get(InAction);
        return action != null && TextInputFocused == false && action.IsPressed() == true;
    }

    public bool Released(EInputAction InAction)
    {
        var action = get(InAction);
        return action != null && action.WasReleasedThisFrame() == true;
    }

    public Vector2 ReadVector2(EInputAction InAction)
    {
        var action = get(InAction);
        if (action == null || TextInputFocused == true)
        {
            return Vector2.zero;
        }

        return action.ReadValue<Vector2>();
    }

    public float ReadFloat(EInputAction InAction)
    {
        var action = get(InAction);
        if (action == null || TextInputFocused == true)
        {
            return 0.0f;
        }

        return action.ReadValue<float>();
    }

    public Vector2 PointerPosition
    {
        get
        {
            var action = get(EInputAction.Point);
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }
    }

    private InputAction get(EInputAction InAction)
    {
        int index = (int)InAction;
        if (index < 0 || index >= Actions.Length)
        {
            return null;
        }

        return Actions[index];
    }

    private void createAction(InputRow InRow)
    {
        int index = (int)InRow.Index;
        if (index < 0 || index >= Actions.Length)
        {
            return;
        }

        bool isVector = InRow.Index == EInputAction.Move || InRow.Index == EInputAction.Point;
        bool isAxis = InRow.Index == EInputAction.Zoom;
        InputActionType type = isVector == true || isAxis == true ? InputActionType.Value : InputActionType.Button;
        string controlType = isVector == true ? "Vector2" : (isAxis == true ? "Axis" : null);

        var action = Map.AddAction(InRow.Index.ToString(), type, null, null, null, null, controlType);
        if (string.IsNullOrEmpty(InRow.Composite) == false)
        {
            var parts = InRow.Binding1.Split(PartSeparator);
            var composite = action.AddCompositeBinding(InRow.Composite);
            for (int i = 0; i < parts.Length && i < Vector2Parts.Length; ++i)
            {
                composite.With(Vector2Parts[i], parts[i].Trim());
            }
        }
        else if (string.IsNullOrEmpty(InRow.Binding1) == false)
        {
            action.AddBinding(InRow.Binding1);
        }

        if (string.IsNullOrEmpty(InRow.Binding2) == false)
        {
            action.AddBinding(InRow.Binding2);
        }

        Actions[index] = action;
    }
}
