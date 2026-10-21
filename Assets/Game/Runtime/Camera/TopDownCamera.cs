using Framework;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// V Rising 식 쿼터뷰 카메라. 휠 = 줌, 마우스 휠 버튼 드래그 = 회전.
/// 마우스 커서 아래 지면 좌표(조준점)를 계산해 플레이어 컨트롤러에 제공한다.
/// </summary>
public class TopDownCamera : MonoBehaviour
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

        apply(true);
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
