using Framework;
using UnityEngine;

/// <summary>
/// 월드 빌더가 배치한 장식 식생(나무/덤불) 한 그루. 건축물과 겹치면 FoliageManager 가 비활성화한다.
/// 채집 노드 나무(ResourceNode)는 대상이 아니다.
/// </summary>
[DisallowMultipleComponent]
public class FoliageInstance : BaseBehaviour
{
    [Tooltip("건축 충돌 반지름 (m, XZ). 줄기/덤불 밑동 기준")]
    public float Radius = 0.6f;

    public bool IsCleared
    {
        get { return gameObject.activeSelf == false; }
    }

    public void Clear()
    {
        gameObject.SetActive(false);
    }

    public void Restore()
    {
        gameObject.SetActive(true);
    }
}
