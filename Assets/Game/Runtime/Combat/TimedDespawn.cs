using Framework;
using UnityEngine;

/// <summary>일정 시간 뒤 풀로 돌려보낸다. 파티클은 재사용 시 다시 재생한다.</summary>
public class TimedDespawn : MonoBehaviour, IPoolable
{
    private float Remaining = -1.0f;

    public void Begin(float InSeconds)
    {
        Remaining = InSeconds;
        var systems = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; ++i)
        {
            systems[i].Clear(true);
            systems[i].Play(true);
        }
    }

    public void OnSpawn()
    {
    }

    public void OnDespawn()
    {
        Remaining = -1.0f;
    }

    private void Update()
    {
        if (Remaining < 0.0f)
        {
            return;
        }

        Remaining -= Time.deltaTime;
        if (Remaining <= 0.0f)
        {
            Remaining = -1.0f;
            Managers.Get<GameObjectPool>().Despawn(gameObject);
        }
    }
}
