using Framework;
using Framework.UI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스로 대상(공격할 몬스터 · 채집 노드)을 가리키고 지정한다.
///  - 호버: 마우스 아래 대상을 HoverColor 로 하이라이트 (Game/Highlight 셰이더 겹 렌더러, Framework.RendererHighlight)
///  - 선택: 대상 위에서 좌클릭(Combat 입력) → 몬스터는 HostileColor, 채집 노드는 NodeColor 로 하이라이트하고 HUD 중상단에 상태를 띄운다.
///    좌클릭은 그대로 공격도 한다 (V Rising 처럼). 빈 땅을 클릭해도 선택은 유지된다.
///  - 선택 해제: 다른 대상을 클릭, 대상이 죽거나 사라짐, TargetMaxDistance 보다 멀어짐.
/// UI 위나 입력을 막는 창이 열려 있으면 호버하지 않는다. 문서: Documentation/VRising/Targeting.md
/// </summary>
public class TargetSelector : IManager
{
    public static readonly Color HoverColor = new Color(1.0f, 0.9f, 0.55f, 0.8f);
    public static readonly Color HostileColor = new Color(1.0f, 0.22f, 0.18f, 1.0f);
    public static readonly Color NodeColor = new Color(0.35f, 0.9f, 1.0f, 1.0f);

    private const int TargetMask = (1 << LayerId.Monster) | (1 << LayerId.Interactable);
    private const float PickRadius = 0.4f;
    private const float PickDistance = 200.0f;

    private readonly RaycastHit[] Hits = new RaycastHit[16];
    private Material HighlightMaterial;

    /// <summary>마우스 아래 대상 (MonsterCharacter 또는 ResourceNode). 없으면 null.</summary>
    public BaseBehaviour Hovered { get; private set; }

    /// <summary>지정한 대상 (MonsterCharacter 또는 ResourceNode). 없으면 null.</summary>
    public BaseBehaviour Selected { get; private set; }

    void IManager.Initialize()
    {
    }

    /// <summary>머티리얼은 프리로드가 끝난 뒤(플레이어가 생긴 뒤) 처음 쓸 때 캐시에서 꺼낸다.</summary>
    private void ensureMaterial()
    {
        if (HighlightMaterial != null)
        {
            return;
        }

        var loader = Managers.Get<AssetLoader>();
        string address = Tables.Address(Tables.ConstInt(EConst.HighlightMaterialAsset));
        HighlightMaterial = loader != null && address != null ? loader.LoadSync<Material>(address) : null;
        if (HighlightMaterial == null)
        {
            Log.Warning("TargetSelector: highlight material missing (Game/Setup/3e. Build Highlight Material)");
        }
    }

    void IManager.UnInitialize()
    {
        Hovered = null;
        Selected = null;
    }

    void IManager.Update(float InDeltaTime)
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            setHovered(null);
            return;
        }

        ensureMaterial();
        validateSelection(player);
        setHovered(pick());

        var input = Managers.Get<GameInput>();
        if (Hovered != null && input != null && input.Pressed(EInputAction.Primary) == true)
        {
            Select(Hovered);
        }
    }

    /// <summary>대상 지정 (null 이면 해제). 테스트 · 치트에서도 쓴다.</summary>
    public void Select(BaseBehaviour InTarget)
    {
        if (Selected == InTarget)
        {
            return;
        }

        var previous = Selected;
        Selected = InTarget;
        refresh(previous);
        refresh(Selected);
        EventBus<TargetSelectedEvent>.Publish(new TargetSelectedEvent { Target = Selected });
    }

    /// <summary>InTarget 이 지금 지정할 수 있는 대상인가 (살아 있는 적 몬스터, 고갈되지 않은 채집 노드).</summary>
    public static bool IsTargetable(BaseBehaviour InTarget)
    {
        var monster = InTarget as MonsterCharacter;
        if (monster != null)
        {
            return monster.IsDead == false && monster.Team != ETeam.Neutral;
        }

        var node = InTarget as ResourceNode;
        return node != null && node.IsDepleted == false;
    }

    // ------------------------------------------------------------------ pick

    private BaseBehaviour pick()
    {
        var ui = Managers.Get<UIManager>();
        bool blocked = (ui != null && ui.IsAnyBlockingWindowOpen() == true) || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() == true);
        var context = SceneContext.Current;
        var input = Managers.Get<GameInput>();
        if (blocked == true || context == null || context.Camera == null || context.Camera.Camera == null || input == null)
        {
            return null;
        }

        var ray = context.Camera.Camera.ScreenPointToRay(input.PointerPosition);
        var target = nearest(Physics.RaycastNonAlloc(ray, Hits, PickDistance, TargetMask, QueryTriggerInteraction.Ignore));
        if (target == null)
        {
            // 작은 대상(덤불 · 멀리 있는 몬스터)도 잡히도록 조금 두꺼운 광선으로 한 번 더.
            target = nearest(Physics.SphereCastNonAlloc(ray, PickRadius, Hits, PickDistance, TargetMask, QueryTriggerInteraction.Ignore));
        }

        return target;
    }

    private BaseBehaviour nearest(int InCount)
    {
        BaseBehaviour best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < InCount; ++i)
        {
            var target = resolve(Hits[i].collider);
            if (target != null && Hits[i].distance < bestDistance)
            {
                best = target;
                bestDistance = Hits[i].distance;
            }
        }

        return best;
    }

    private static BaseBehaviour resolve(Collider InCollider)
    {
        if (InCollider == null)
        {
            return null;
        }

        BaseBehaviour target = InCollider.GetComponentInParent<MonsterCharacter>();
        if (target == null)
        {
            target = InCollider.GetComponentInParent<ResourceNode>();
        }

        return target != null && IsTargetable(target) == true ? target : null;
    }

    // ------------------------------------------------------------------ state

    private void setHovered(BaseBehaviour InTarget)
    {
        if (Hovered == InTarget)
        {
            return;
        }

        var previous = Hovered;
        Hovered = InTarget;
        refresh(previous);
        refresh(Hovered);
    }

    private void validateSelection(PlayerCharacter InPlayer)
    {
        if (ReferenceEquals(Selected, null) == true)
        {
            return;
        }

        // 채집 노드는 고갈돼도 선택을 유지한다 (재생까지 남은 시간을 보여 준다). 몬스터는 죽으면 해제.
        var monster = Selected as MonsterCharacter;
        bool gone = Selected == null || (monster != null && monster.IsDead == true);
        bool far = gone == false && MathUtil.Flat(Selected.transform.position - InPlayer.transform.position).magnitude > Tables.Const(EConst.TargetMaxDistance);
        if (gone == true || far == true)
        {
            Select(null);
        }
    }

    /// <summary>InTarget 의 하이라이트를 지금 상태(선택 > 호버 > 없음)에 맞춘다.</summary>
    private void refresh(BaseBehaviour InTarget)
    {
        if (InTarget == null || HighlightMaterial == null)
        {
            return;
        }

        var highlight = ComponentUtil.GetOrAdd<RendererHighlight>(InTarget.gameObject);
        if (InTarget == Selected)
        {
            highlight.Show(HighlightMaterial, InTarget is MonsterCharacter ? HostileColor : NodeColor);
        }
        else if (InTarget == Hovered)
        {
            highlight.Show(HighlightMaterial, HoverColor);
        }
        else
        {
            highlight.Hide();
        }
    }
}
