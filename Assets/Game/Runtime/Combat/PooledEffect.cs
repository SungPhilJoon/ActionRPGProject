using Framework;
using UnityEngine;

/// <summary>
/// 외부 이펙트 프리팹(SpecialSkillsEffectsPack) 을 풀에서 재사용할 때 쓰는 래퍼.
/// 이펙트 팩의 보조 스크립트(DelayActive, NewMaterialChange 등)는 Start/Awake 에서 한 번만 초기화하고
/// 다 끝나면 자기 오브젝트를 Destroy 하기 때문에, 풀로 재사용하면 일부가 사라지거나 타이밍이 어긋난다.
/// → 래퍼(루트)만 풀링하고, 꺼낼 때마다 원본 이펙트를 자식으로 새로 만든다 (돌려놓을 때 삭제).
/// 크기는 자식에 적용한다 (루트 스케일은 CueTable.Scale 이 덮어쓰기 때문).
/// </summary>
public class PooledEffect : MonoBehaviour, IPoolable
{
    [Tooltip("원본 이펙트 프리팹")]
    public GameObject Source;
    [Tooltip("원본을 게임 크기로 맞추는 배율 (빌더가 파티클 크기로 계산)")]
    public float ChildScale = 1.0f;
    public Vector3 ChildOffset;
    public Vector3 ChildEuler;
    [Tooltip("지속형 상태이상 표시처럼 이펙트가 빠질 때까지 반복해야 하면 true")]
    public bool Loop;

    private GameObject Instance;

    private void OnEnable()
    {
        // 풀을 거치지 않고 씬에 직접 놓였을 때도 보이게.
        if (Instance == null)
        {
            spawnChild();
        }
    }

    public void OnSpawn()
    {
        // OnEnable 이 먼저 불렸으면 이미 새 자식이 있다 (OnDespawn 에서 이전 자식은 지웠음).
        if (Instance == null)
        {
            spawnChild();
        }
    }

    public void OnDespawn()
    {
        destroyChild();
    }

    private void spawnChild()
    {
        if (Source == null)
        {
            return;
        }

        Instance = Instantiate(Source, transform);
        Instance.transform.localPosition = ChildOffset;
        Instance.transform.localRotation = Quaternion.Euler(ChildEuler);
        Instance.transform.localScale = Source.transform.localScale * ChildScale;
        if (Loop == true)
        {
            foreach (var system in Instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.loop = true;
            }
        }
    }

    private void destroyChild()
    {
        if (Instance != null)
        {
            Destroy(Instance);
            Instance = null;
        }
    }
}
