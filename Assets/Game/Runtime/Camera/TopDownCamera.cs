using Framework;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// V Rising 식 쿼터뷰 카메라. 휠 = 줌, 마우스 휠 버튼 드래그 = 회전.
/// 마우스 커서 아래 지면 좌표(조준점)를 계산해 플레이어 컨트롤러에 제공한다.
/// </summary>
public class TopDownCamera : BaseBehaviour
{
    public Camera Camera;
    public Transform Target;

    private float Yaw;
    private float Pitch;
    private float Distance;
    private float MinDistance;
    private float MaxDistance;
    private Vector3 FollowPosition;

    public float YawDegrees
    {
        get { return Yaw; }
    }

    /// <summary>연출용 시점 지정 (줌 범위 제한 없음). 다음 프레임부터 Target 을 따라가며 적용된다. 원래대로는 ResetView.</summary>
    public void SetView(float InYaw, float InPitch, float InDistance)
    {
        Yaw = InYaw;
        Pitch = InPitch;
        Distance = InDistance;
    }

    /// <summary>ConstTable 기본 시점 (요 · 피치 · 거리)으로 되돌린다.</summary>
    public void ResetView()
    {
        Yaw = Tables.Const(EConst.CameraYaw);
        Pitch = Tables.Const(EConst.CameraPitch);
        Distance = Tables.Const(EConst.CameraDistance);
    }

    public void Setup(Transform InTarget)
    {
        if (Camera == null)
        {
            Camera = GetComponent<Camera>();
        }

        Target = InTarget;
        Yaw = Tables.Const(EConst.CameraYaw);
        Pitch = Tables.Const(EConst.CameraPitch);
        Distance = Tables.Const(EConst.CameraDistance);
        MinDistance = Distance * 0.5f;
        MaxDistance = Distance * 1.6f;
        if (Target != null)
        {
            FollowPosition = Target.position;
        }

        setupOcclusion();
        apply(true);
    }

    /// <summary>
    /// 카메라 가림 처리 (Framework.CameraOcclusionSystem). 감지·응답 전략을 바꾸려면 여기서 SetDetector / SetResponse 를 호출한다.
    /// 문서: Documentation/VRising/CameraOcclusion.md
    /// </summary>
    private void setupOcclusion()
    {
        var occlusion = ComponentUtil.GetOrAdd<CameraOcclusionSystem>(gameObject);
        occlusion.Settings.Mask = LayerId.CameraOcclusionMask;
        occlusion.SetTarget(Target);
    }

    private void LateUpdate()
    {
        if (Target == null)
        {
            return;
        }

        var input = Managers.Get<GameInput>();
        if (input != null)
        {
            float scroll = input.ReadFloat(EInputAction.Zoom);
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Distance = Mathf.Clamp(Distance - Mathf.Sign(scroll) * 1.5f, MinDistance, MaxDistance);
            }

            if (input.Held(EInputAction.RotateCamera) == true && Mouse.current != null)
            {
                Yaw += Mouse.current.delta.ReadValue().x * 0.25f;
            }
        }

        apply(false);
    }

    private void apply(bool InInstant)
    {
        if (Target == null)
        {
            return;
        }

        FollowPosition = InInstant == true ? Target.position : Vector3.Lerp(FollowPosition, Target.position, 1.0f - Mathf.Exp(-12.0f * Time.deltaTime));
        var rotation = Quaternion.Euler(Pitch, Yaw, 0.0f);
        transform.position = FollowPosition + Vector3.up * 1.0f - rotation * Vector3.forward * Distance;
        transform.rotation = rotation;
    }

    /// <summary>카메라 기준 평면 이동 방향 (입력 → 월드).</summary>
    public Vector3 ToWorldDirection(Vector2 InInput)
    {
        var yawRotation = Quaternion.Euler(0.0f, Yaw, 0.0f);
        return yawRotation * new Vector3(InInput.x, 0.0f, InInput.y);
    }

    /// <summary>화면 좌표 아래의 지면 지점. 지면이 없으면 InFallbackHeight 평면과의 교점.</summary>
    public Vector3 ScreenToGround(Vector2 InScreenPosition, float InFallbackHeight)
    {
        if (Camera == null)
        {
            return Vector3.zero;
        }

        Ray ray = Camera.ScreenPointToRay(InScreenPosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 300.0f, LayerId.GroundMask, QueryTriggerInteraction.Ignore) == true)
        {
            return hit.point;
        }

        var plane = new Plane(Vector3.up, new Vector3(0.0f, InFallbackHeight, 0.0f));
        float enter;
        if (plane.Raycast(ray, out enter) == true)
        {
            return ray.GetPoint(enter);
        }

        return Target != null ? Target.position : Vector3.zero;
    }
}
