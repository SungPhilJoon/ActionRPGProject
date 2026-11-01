using Framework;
using UnityEngine;

/// <summary>
/// 캐릭터 퀘스트 화살표: 플레이어 발밑 둘레에서 다음 경로 꼭짓점(QuestNavigator) 쪽을 가리킨다.
/// 퀘스트 장소가 없거나 HideDistance 안으로 들어오면 · 죽으면 숨긴다. 앞뒤로 살짝 움직여 눈에 띄게 한다.
/// 프리팹 VFX/QuestArrow (ContentBuilder.QuestArrow) — 메시는 +Z 를 가리키는 납작한 화살촉.
/// </summary>
public class QuestGuideArrow : BaseBehaviour
{
    public Transform Visual;
    /// <summary>플레이어 중심에서 화살표까지 거리 (m).</summary>
    public float Radius = 1.8f;
    public float Height = 0.35f;
    public float HideDistance = 4.0f;
    public float BobAmplitude = 0.25f;
    public float BobSpeed = 4.0f;

    private void LateUpdate()
    {
        var player = PlayerCharacter.Local;
        var navigator = Managers.Get<QuestNavigator>();
        Vector3 corner = Vector3.zero;
        bool show = player != null && player.IsDead == false && navigator != null && navigator.HasGuide == true
                    && navigator.Distance > HideDistance && navigator.TryGetNextCorner(out corner) == true;
        if (Visual.gameObject.activeSelf != show)
        {
            Visual.gameObject.SetActive(show);
        }

        if (show == false)
        {
            return;
        }

        Vector3 origin = player.transform.position;
        Vector3 direction = MathUtil.Flat(corner - origin);
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();
        float bob = Mathf.Sin(Time.time * BobSpeed) * BobAmplitude;
        transform.SetPositionAndRotation(origin + direction * (Radius + bob) + Vector3.up * Height, Quaternion.LookRotation(direction, Vector3.up));
    }
}
