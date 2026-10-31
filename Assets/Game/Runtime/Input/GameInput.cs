using Framework;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// InputTable.csv 로부터 Input System 액션을 코드로 생성한다.
/// 게임 코드는 EInputAction(int) 으로만 입력을 조회하고, 키 변경은 CSV 수정으로 끝난다.
///  - Binding1/Binding2: 컨트롤 경로 (예: &lt;Keyboard&gt;/q)
///  - Composite 가 있으면 Binding1 을 ';' 로 나눠 composite 파트(up;down;left;right)로 쓴다.
///  - Map: 액션이 속한 입력 맵 (EInputMap). 맵마다 InputActionMap 을 따로 만들어 통째로 켜고 끈다.
///    UI 창이 열리면 GameBootstrap 이 Combat 맵을 꺼서 어빌리티 · 카메라 줌/회전이 반응하지 않는다. 꺼진 맵의 액션은 눌림 · 값이 모두 0 이다.
/// </summary>
public class GameInput : IManager
{
    private const char PartSeparator = ';';
    private static readonly string[] Vector2Parts = { "up", "down", "left", "right" };

    private readonly InputActionMap[] Maps = new InputActionMap[(int)EInputMap.Count];
    private readonly InputAction[] Actions = new InputAction[(int)EInputAction.Count];

    /// <summary>텍스트 입력 중(치트 콘솔)이면 게임 입력을 막는다.</summary>
    public bool TextInputFocused;

    void IManager.Initialize()
    {
        for (int i = 0; i < Maps.Length; ++i)
        {
            Maps[i] = new InputActionMap(((EInputMap)i).ToString());
        }

        var rows = Tables.Root.Input.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            createAction(rows[i]);
        }

        for (int i = 0; i < Maps.Length; ++i)
        {
            Maps[i].Enable();
        }
    }

    void IManager.UnInitialize()
    {
        for (int i = 0; i < Maps.Length; ++i)
        {
            if (Maps[i] != null)
            {
                Maps[i].Disable();
                Maps[i].Dispose();
                Maps[i] = null;
            }
        }
    }

    void IManager.Update(float InDeltaTime)
    {
    }

    public bool IsMapEnabled(EInputMap InMap)
    {
        var map = Maps[(int)InMap];
        return map != null && map.enabled == true;
    }

    /// <summary>InMap 맵을 켜거나 끈다. 이미 그 상태면 아무것도 하지 않는다 (매 프레임 호출해도 된다).</summary>
    public void SetMapEnabled(EInputMap InMap, bool InEnabled)
    {
        var map = Maps[(int)InMap];
        if (map == null || map.enabled == InEnabled)
        {
            return;
        }

        if (InEnabled == true)
        {
            map.Enable();
        }
        else
        {
            map.Disable();
        }
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

        var map = Maps[(int)InRow.Map];
        var action = map.AddAction(InRow.Index.ToString(), type, null, null, null, null, controlType);
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
