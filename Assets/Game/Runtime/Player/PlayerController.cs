using Framework;
using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 로컬 플레이어 입력 → 액터 명령. (멀티플레이에서는 이 클래스가 "명령 전송" 으로 바뀌고
/// PlayerCharacter 는 서버 권한으로 실행된다 — Multiplayer.md 의 확장 계획 참고)
/// </summary>
[RequireComponent(typeof(PlayerCharacter))]
public class PlayerController : MonoBehaviour
{
    private static readonly EInputAction[] SlotInputs =
    {
        EInputAction.Primary, EInputAction.WeaponQ, EInputAction.WeaponE, EInputAction.Travel,
        EInputAction.Spell1, EInputAction.Spell2, EInputAction.Ultimate, EInputAction.Feed,
    };

    private PlayerCharacter Player;
    private TopDownCamera Camera;
    private IInteractable CurrentInteractable;
    /// <summary>공격 중 입력을 순서대로 쌓아 두는 버퍼 (AbilityInputBuffer 참고).</summary>
    private readonly AbilityInputBuffer InputBuffer = new AbilityInputBuffer();

    /// <summary>건축 모드 등 다른 시스템이 좌클릭을 가져갈 때 true.</summary>
    public bool SuppressAbilities;

    public Vector3 AimPoint { get; private set; }

    public void Initialize(PlayerCharacter InPlayer, TopDownCamera InCamera)
    {
        Player = InPlayer;
        Camera = InCamera;
    }

    private void Update()
    {
        if (Player == null || Camera == null)
        {
            return;
        }

        var input = Managers.Get<GameInput>();
        var ui = Managers.Get<UIManager>();
        bool uiBlocking = ui != null && ui.IsAnyBlockingWindowOpen() == true;

        Vector2 move = input.ReadVector2(EInputAction.Move);
        Player.Motor.SetMoveInput(Player.IsDead == true ? Vector3.zero : Camera.ToWorldDirection(move));
        AimPoint = Camera.ScreenToGround(input.PointerPosition, Player.transform.position.y);

        updateInteractable();
        if (Player.IsDead == true || uiBlocking == true || SuppressAbilities == true)
        {
            InputBuffer.Clear();
            return;
        }

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() == true;
        for (int i = 0; i < SlotInputs.Length; ++i)
        {
            var action = SlotInputs[i];
            bool isPrimary = action == EInputAction.Primary;
            if (isPrimary == true && pointerOverUI == true)
            {
                continue;
            }

            // 평타는 누르고 있는 동안 계속 입력한다 (콤보 연결). 버퍼가 같은 슬롯을 한 번만 쌓는다.
            bool triggered = isPrimary == true ? input.Held(action) : input.Pressed(action);
            if (triggered == true)
            {
                InputBuffer.Push(Player, (EAbilitySlot)i, AimPoint);
            }
        }

        InputBuffer.Update(Player, AimPoint);

        if (input.Pressed(EInputAction.Interact) == true && CurrentInteractable != null)
        {
            if (Player.Anim != null)
            {
                Player.Anim.Play(Tables.ConstInt(EConst.InteractAnim));
            }

            CurrentInteractable.Interact(Player);
        }
    }

    private void updateInteractable()
    {
        IInteractable nearest = null;
        if (Player.IsDead == false)
        {
            nearest = InteractableRegistry.FindNearest(Player, Tables.Const(EConst.InteractRange));
        }

        if (nearest == CurrentInteractable)
        {
            return;
        }

        CurrentInteractable = nearest;
        EventBus<InteractTargetChangedEvent>.Publish(new InteractTargetChangedEvent { Target = nearest });
    }
}
