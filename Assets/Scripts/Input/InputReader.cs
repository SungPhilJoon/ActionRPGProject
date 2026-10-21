using System;
using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.InputSystem
{
    /// <summary>
    /// Input System 을 게임플레이 코드에서 떼어내는 얇은 래퍼.
    /// 로컬 플레이어(소유자)만 인스턴스를 활성화한다.
    ///
    /// 바인딩 자체는 <see cref="RPGInputActions"/> 에 순수 C# 코드로 정의되어 있다.
    /// 이 클래스는 그 액션들을 구독해서 이벤트/프로퍼티로 노출하는 역할만 한다.
    /// </summary>
    public class InputReader : MonoBehaviour
    {
        // 연속 값
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool AttackHeld { get; private set; }

        // 이번 프레임에 눌린 버튼
        public event Action Jumped;
        public event Action Dodged;
        public event Action AttackPressed;
        public event Action AttackReleased;
        public event Action Interacted;
        public event Action WeaponSwapped;
        /// <summary>인자는 0~3 슬롯 인덱스.</summary>
        public event Action<int> SkillPressed;

        private RPGInputActions _actions;
        private bool _gameplayEnabled;

        public bool GameplayEnabled => _gameplayEnabled;
        /// <summary>UI(메뉴/덱 편집 등)에서 Navigate/Submit/Cancel 이 필요할 때 직접 꺼내 쓴다.</summary>
        public RPGInputActions Actions => _actions;

        private void Awake() => _actions = new RPGInputActions();

        private void OnEnable()
        {
            Subscribe();
            EnableGameplay(true);
        }

        private void OnDisable()
        {
            Unsubscribe();
            _actions.DisablePlayer();
            _actions.DisableUI();
            _gameplayEnabled = false;
            Move = Vector2.zero;
            Look = Vector2.zero;
        }

        private void OnDestroy() => _actions?.Dispose();

        private void Subscribe()
        {
            _actions.Jump.performed += OnJump;
            _actions.Dodge.performed += OnDodge;
            _actions.Attack.started += OnAttackStarted;
            _actions.Attack.canceled += OnAttackCanceled;
            _actions.Interact.performed += OnInteract;
            _actions.SwapWeapon.performed += OnSwap;
            for (int i = 0; i < _actions.Skills.Length; i++)
            {
                int slot = i;
                _actions.Skills[i].performed += ctx => OnSkill(slot);
            }
        }

        private void Unsubscribe()
        {
            _actions.Jump.performed -= OnJump;
            _actions.Dodge.performed -= OnDodge;
            _actions.Attack.started -= OnAttackStarted;
            _actions.Attack.canceled -= OnAttackCanceled;
            _actions.Interact.performed -= OnInteract;
            _actions.SwapWeapon.performed -= OnSwap;
            // 스킬은 람다로 붙였으므로 액션 비활성화로 충분하다. RPGInputActions 는 이 오브젝트 전용 인스턴스다.
        }

        private void Update()
        {
            if (!_gameplayEnabled) return;
            Move = _actions.Move.ReadValue<Vector2>();
            Look = _actions.Look.ReadValue<Vector2>();
            SprintHeld = _actions.Sprint.IsPressed();
            AttackHeld = _actions.Attack.IsPressed();
        }

        /// <summary>대화, 메뉴, 사망 중에는 false 로 두어 조작을 막는다.</summary>
        public void EnableGameplay(bool enable)
        {
            _gameplayEnabled = enable;
            if (enable)
            {
                _actions.EnablePlayer();
            }
            else
            {
                _actions.DisablePlayer();
                Move = Vector2.zero;
                Look = Vector2.zero;
                SprintHeld = false;
                AttackHeld = false;
            }
        }

        public void EnableUI(bool enable)
        {
            if (enable) _actions.EnableUI();
            else _actions.DisableUI();
        }

        /// <summary>마우스 커서 잠금. 메뉴를 열 때 false 로.</summary>
        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnJump(UnityEngine.InputSystem.InputAction.CallbackContext _) => Jumped?.Invoke();
        private void OnDodge(UnityEngine.InputSystem.InputAction.CallbackContext _) => Dodged?.Invoke();
        private void OnAttackStarted(UnityEngine.InputSystem.InputAction.CallbackContext _) => AttackPressed?.Invoke();
        private void OnAttackCanceled(UnityEngine.InputSystem.InputAction.CallbackContext _) => AttackReleased?.Invoke();
        private void OnInteract(UnityEngine.InputSystem.InputAction.CallbackContext _) => Interacted?.Invoke();
        private void OnSwap(UnityEngine.InputSystem.InputAction.CallbackContext _) => WeaponSwapped?.Invoke();
        private void OnSkill(int slot) { if (_gameplayEnabled) SkillPressed?.Invoke(slot); }
    }
}
