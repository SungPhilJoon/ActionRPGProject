using System;
using ActionRPG.Core;
using UnityEngine.InputSystem;

namespace ActionRPG.InputSystem
{
    /// <summary>
    /// 게임의 입력 스킴 전체를 코드로 직접 정의한다.
    ///
    /// Input Actions 에디터 창에서 만든 .inputactions JSON 에셋을 런타임에
    /// 문자열 이름으로 찾는 대신(예전 방식은 Resources/RPGControls.inputactions +
    /// FindAction("Move") 였다), InputActionMap/InputAction/바인딩을 전부 C# 코드로
    /// 생성한다. 장점:
    ///  - 바인딩 변경 사항이 git diff 에 코드로 보인다 (바이너리/JSON 에셋보다 리뷰하기 쉽다).
    ///  - 오타로 "Move" 대신 "Mvoe" 를 찾는 실수가 컴파일 타임에 걸리진 않지만,
    ///    최소한 프로퍼티(Move, Look, ...)를 통해 접근하므로 IDE 자동완성이 된다.
    ///  - 액션 에셋 파일 하나를 잃어버리거나 실수로 깨뜨릴 위험이 없다.
    ///
    /// 여전히 Input Actions 에디터 창으로 바인딩을 "눈으로 보면서" 만지고 싶다면
    /// Assets/Resources/RPGControls.inputactions 를 참고용으로 남겨 두었다 —
    /// 이 클래스의 바인딩과 1:1로 대응되게 유지한다.
    /// </summary>
    public sealed class RPGInputActions : IDisposable
    {
        public readonly InputActionMap Player;
        public readonly InputActionMap UI;

        // --- Player ---
        public readonly InputAction Move;
        public readonly InputAction Look;
        public readonly InputAction Sprint;
        public readonly InputAction Jump;
        public readonly InputAction Dodge;
        public readonly InputAction Attack;
        public readonly InputAction Interact;
        public readonly InputAction SwapWeapon;
        public readonly InputAction[] Skills = new InputAction[GameConstants.SkillSlotCount];

        // --- UI ---
        public readonly InputAction Navigate;
        public readonly InputAction Submit;
        public readonly InputAction Cancel;
        public readonly InputAction Point;
        public readonly InputAction Click;
        public readonly InputAction ScrollWheel;

        private bool _disposed;

        public RPGInputActions()
        {
            Player = new InputActionMap("Player");
            UI = new InputActionMap("UI");

            Move = Player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Look = Player.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            Sprint = Player.AddAction("Sprint", InputActionType.Button);
            Jump = Player.AddAction("Jump", InputActionType.Button);
            Dodge = Player.AddAction("Dodge", InputActionType.Button);
            Attack = Player.AddAction("Attack", InputActionType.Button);
            Interact = Player.AddAction("Interact", InputActionType.Button);
            SwapWeapon = Player.AddAction("SwapWeapon", InputActionType.Button);
            Navigate = UI.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            Submit = UI.AddAction("Submit", InputActionType.Button);
            Cancel = UI.AddAction("Cancel", InputActionType.Button);
            Point = UI.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            Click = UI.AddAction("Click", InputActionType.PassThrough);
            ScrollWheel = UI.AddAction("ScrollWheel", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            
            BuildPlayerMap();
            BuildUIMap();
        }

        private void BuildPlayerMap()
        {
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick").WithProcessor("StickDeadzone");

            Look.AddBinding("<Mouse>/delta").WithProcessor("ScaleVector2(x=0.05,y=0.05)");
            Look.AddBinding("<Gamepad>/rightStick").WithProcessors("StickDeadzone,ScaleVector2(x=2,y=2)");

            Sprint.AddBinding("<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Jump.AddBinding("<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");

            Dodge.AddBinding("<Keyboard>/leftCtrl");
            Dodge.AddBinding("<Gamepad>/buttonEast");

            Attack.AddBinding("<Mouse>/leftButton");
            Attack.AddBinding("<Gamepad>/rightTrigger");

            Interact.AddBinding("<Keyboard>/g");
            Interact.AddBinding("<Gamepad>/buttonWest");

            SwapWeapon.AddBinding("<Keyboard>/tab");
            SwapWeapon.AddBinding("<Gamepad>/dpad/up");

            BindSkill(0, "<Keyboard>/q", "<Gamepad>/leftShoulder");
            BindSkill(1, "<Keyboard>/e", "<Gamepad>/rightShoulder");
            BindSkill(2, "<Keyboard>/r", "<Gamepad>/buttonNorth");
            BindSkill(3, "<Keyboard>/f", "<Gamepad>/leftTrigger");
        }

        private void BindSkill(int slot, string keyboardPath, string gamepadPath)
        {
            var action = Player.AddAction($"Skill{slot + 1}", InputActionType.Button);
            action.AddBinding(keyboardPath);
            action.AddBinding(gamepadPath);
            Skills[slot] = action;
        }

        private void BuildUIMap()
        {
            Navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Gamepad>/leftStick/up")
                .With("Down", "<Gamepad>/leftStick/down")
                .With("Left", "<Gamepad>/leftStick/left")
                .With("Right", "<Gamepad>/leftStick/right");

            Submit.AddBinding("<Keyboard>/enter");
            Submit.AddBinding("<Gamepad>/buttonSouth");

            Cancel.AddBinding("<Keyboard>/escape");
            Cancel.AddBinding("<Gamepad>/buttonEast");

            Point.AddBinding("<Mouse>/position");

            Click.AddBinding("<Mouse>/leftButton");

            ScrollWheel.AddBinding("<Mouse>/scroll");
        }

        public void EnablePlayer() => Player.Enable();
        public void DisablePlayer() => Player.Disable();
        public void EnableUI() => UI.Enable();
        public void DisableUI() => UI.Disable();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Player.Disable();
            UI.Disable();
            // Player.Dispose();
            // UI.Dispose();
        }
    }
}
