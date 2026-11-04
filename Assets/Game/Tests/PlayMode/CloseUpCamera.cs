using System.Collections.Generic;
using Framework;
using Framework.UI;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 테스트 연출용 근접 카메라. 게임 카메라(TopDownCamera)의 Target 을 대상 상체 높이의 기준점으로 바꾸고,
/// 대상 정면에서 낮은 각도로 가까이 보게 한다 (SetView).
///  - 높이 · 거리는 대상 모델의 실제 크기(휴머노이드면 머리 · 엉덩이 뼈, 아니면 렌더러 바운드)로 정한다 → 모델 · 스케일이 바뀌어도 화면 가운데에 꽉 차게.
///  - 화면을 가리는 UI(World · Hud · Toast 레이어 캔버스)를 끄고, 잘 보이게 오후(15시)로 바꾼다. 그동안 플레이어는 햇빛 무시(Cheat_God).
/// End 에서 모두 되돌린다 (밤 0시 고정은 GamePlayTestBase 의 기본값).
/// </summary>
public class CloseUpCamera
{
    /// <summary>모델 높이 대비 정면 거리.</summary>
    public float DistancePerHeight = 1.5f;
    /// <summary>최소 거리 (작은 모델용, m).</summary>
    public float MinDistance = 2.5f;
    /// <summary>내려다보는 각도 (도). 0 = 수평.</summary>
    public float Pitch = 6.0f;
    /// <summary>바라볼 높이 = 엉덩이 → 머리 사이의 이 비율 (0.6 ≈ 가슴). 휴머노이드가 아니면 바운드 가운데 + 반높이 × 이 값 − 0.25.</summary>
    public float LookRatio = 0.6f;
    /// <summary>연출 동안의 시각 (시). 음수면 바꾸지 않는다.</summary>
    public float Hour = 15.0f;

    /// <summary>TopDownCamera 가 기준점 위로 더하는 높이 (TopDownCamera.apply 의 Vector3.up * 1).</summary>
    private const float CameraLift = 1.0f;
    private const float NightHour = 0.0f;

    private static readonly UILayer[] HiddenLayers = { UILayer.World, UILayer.Hud, UILayer.Toast };

    private TopDownCamera Camera;
    private Transform PreviousTarget;
    private GameObject Anchor;
    private PlayerCharacter Player;
    private readonly List<Canvas> HiddenCanvases = new List<Canvas>();

    public void Begin(Actor InSubject)
    {
        Camera = Object.FindFirstObjectByType<TopDownCamera>();
        Assert.IsNotNull(Camera, "TopDownCamera 가 없습니다");

        float height;
        var look = lookPoint(InSubject, out height);
        var feet = InSubject.transform.position;
        Log.Message($"[CloseUp] {InSubject.name} 발 y={feet.y:0.00} 지면 y={CombatUtil.SnapToGround(feet).y:0.00} 모델 높이={height:0.00} 바라보는 y={look.y:0.00}");
        Anchor = new GameObject("CloseUpAnchor");
        Anchor.transform.position = look - Vector3.up * CameraLift;
        Anchor.transform.SetParent(InSubject.transform, true);

        PreviousTarget = Camera.Target;
        Camera.Target = Anchor.transform;
        float faceYaw = InSubject.transform.eulerAngles.y + 180.0f;
        Camera.SetView(faceYaw, Pitch, Mathf.Max(height * DistancePerHeight, MinDistance));

        hideUi();
        if (Hour >= 0.0f)
        {
            Managers.Get<DayNightManager>().SetHour(Hour);
            Player = PlayerCharacter.Local;
            if (Player != null)
            {
                Player.AddTag((int)ETag.Cheat_God);
            }
        }
    }

    public void End()
    {
        if (Camera != null)
        {
            Camera.Target = PreviousTarget;
            Camera.ResetView();
        }

        if (Anchor != null)
        {
            Object.Destroy(Anchor);
        }

        foreach (var canvas in HiddenCanvases)
        {
            if (canvas != null)
            {
                canvas.enabled = true;
            }
        }

        HiddenCanvases.Clear();
        if (Hour >= 0.0f)
        {
            var dayNight = Managers.Get<DayNightManager>();
            if (dayNight != null)
            {
                dayNight.SetHour(NightHour);
            }

            if (Player != null)
            {
                Player.RemoveTag((int)ETag.Cheat_God);
            }
        }
    }

    private void hideUi()
    {
        var ui = Managers.Get<UIManager>();
        foreach (var layer in HiddenLayers)
        {
            var canvas = ui != null ? ui.GetCanvas(layer) : null;
            if (canvas != null && canvas.enabled == true)
            {
                canvas.enabled = false;
                HiddenCanvases.Add(canvas);
            }
        }
    }

    /// <summary>바라볼 점과 모델 높이 (발 = 액터 위치 기준). 휴머노이드는 뼈로, 아니면 바운드로.</summary>
    private Vector3 lookPoint(Actor InSubject, out float OutHeight)
    {
        var feet = InSubject.transform.position;
        var animator = InSubject.GetComponentInChildren<Animator>();
        var head = animator != null && animator.isHuman == true ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        var hips = animator != null && animator.isHuman == true ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        if (head != null && hips != null)
        {
            OutHeight = Mathf.Max(head.position.y - feet.y, 0.5f);
            return Vector3.Lerp(hips.position, head.position, LookRatio);
        }

        var bounds = modelBounds(InSubject);
        OutHeight = bounds.size.y;
        return bounds.center + Vector3.up * bounds.extents.y * (LookRatio - 0.25f);
    }

    /// <summary>캐릭터 모델(스킨 · 메시 렌더러)을 감싸는 바운드. 렌더러가 없으면 반지름 기준 기본 크기.</summary>
    private static Bounds modelBounds(Actor InSubject)
    {
        bool found = false;
        var bounds = new Bounds(InSubject.transform.position + Vector3.up, Vector3.one * 2.0f);
        foreach (var renderer in InSubject.GetComponentsInChildren<Renderer>())
        {
            bool model = renderer is SkinnedMeshRenderer || renderer is MeshRenderer;
            if (model == false || renderer.enabled == false)
            {
                continue;
            }

            if (found == false)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }
}
