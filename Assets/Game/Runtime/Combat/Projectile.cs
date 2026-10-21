using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>
/// 직선 발사체. 물리 콜라이더 대신 ActorRegistry 거리 판정으로 액터 명중을 확인하고,
/// 지형/구조물은 SphereCast 로 막는다. 풀에서 재사용된다.
/// </summary>
public class Projectile : MonoBehaviour, IPoolable
{
    private Actor Source;
    private AbilityRow Row;
    private Vector3 Direction;
    private float Travelled;
    private bool Active;
    private readonly List<Actor> Buffer = new List<Actor>(8);

    public void Launch(Actor InSource, AbilityRow InRow, Vector3 InDirection)
    {
        Source = InSource;
        Row = InRow;
        Direction = MathUtil.Flat(InDirection).normalized;
        Travelled = 0.0f;
        Active = true;
        transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);
    }

    public void OnSpawn()
    {
        var trail = GetComponentInChildren<TrailRenderer>();
        if (trail != null)
        {
            trail.Clear();
        }
    }

    public void OnDespawn()
    {
        Active = false;
        Source = null;
        Row = null;
    }

    private void Update()
    {
        if (Active == false)
        {
            return;
        }

        if (Source == null)
        {
            finish();
            return;
        }

        float step = Row.Speed * Time.deltaTime;
        float radius = Mathf.Max(0.1f, Row.Radius);
        Vector3 position = transform.position;

        RaycastHit hit;
        if (Physics.SphereCast(position, radius * 0.5f, Direction, out hit, step, LayerId.ProjectileBlockMask, QueryTriggerInteraction.Ignore) == true)
        {
            transform.position = hit.point;
            if (Row.HitCue != 0)
            {
                CueSpawner.Spawn(Row.HitCue, hit.point, Direction);
            }

            finish();
            return;
        }

        Vector3 next = position + Direction * step;
        Vector3 middle = (position + next) * 0.5f;
        Managers.Get<ActorRegistry>().FindHostiles(Source, middle, radius + step * 0.5f, Buffer);
        CombatUtil.RemoveUntargetable(Buffer);
        if (Buffer.Count > 0)
        {
            var target = pickClosest(position);
            CombatUtil.ApplyAbilityHit(Source, Row, target, 1.0f, target.AimOrigin);
            finish();
            return;
        }

        transform.position = next;
        Travelled += step;
        if (Travelled >= Row.Range)
        {
            finish();
        }
    }

    private Actor pickClosest(Vector3 InPosition)
    {
        Actor best = Buffer[0];
        float bestDistance = float.MaxValue;
        for (int i = 0; i < Buffer.Count; ++i)
        {
            float distance = (Buffer[i].transform.position - InPosition).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = Buffer[i];
            }
        }

        return best;
    }

    private void finish()
    {
        Active = false;
        Managers.Get<GameObjectPool>().Despawn(gameObject);
    }
}

public static class ProjectileSystem
{
    public static void Fire(Actor InSource, AbilityRow InRow, Vector3 InOrigin, Vector3 InDirection)
    {
        string address = Tables.Address(InRow.Projectile);
        var prefab = Managers.Get<AssetLoader>().LoadSync<GameObject>(address);
        if (prefab == null)
        {
            Log.Warning($"Projectile prefab missing for ability {InRow.Index}");
            return;
        }

        var instance = Managers.Get<GameObjectPool>().Spawn(prefab, InOrigin, Quaternion.LookRotation(InDirection));
        var projectile = ComponentUtil.GetOrAdd<Projectile>(instance);
        projectile.Launch(InSource, InRow, InDirection);
    }
}
