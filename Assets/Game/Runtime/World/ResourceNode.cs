using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>
/// 채집 노드 (나무/바위/식물). V Rising 처럼 무기로 때려서 채집한다.
/// 타격 1회 = 무기 채집 배율만큼 내구도 감소 + 드랍 아이템을 바로 인벤토리에 넣는다.
/// </summary>
public class ResourceNode : BaseBehaviour
{
    private static readonly List<ResourceNode> Nodes = new List<ResourceNode>();

    public ResourceNodeRow Row { get; private set; }
    public float Health { get; private set; }
    public bool IsDepleted { get; private set; }

    /// <summary>고갈 후 다시 생기기까지 남은 시간 (초, 대상 정보창).</summary>
    public float RespawnRemaining { get; private set; }
    private Renderer[] Renderers;
    private Collider[] Colliders;
    private Vector3 BaseScale;
    private float ShakeTime;

    public void Initialize(ResourceNodeRow InRow)
    {
        Row = InRow;
        Health = InRow.Health;
        Renderers = GetComponentsInChildren<Renderer>(true);
        Colliders = GetComponentsInChildren<Collider>(true);
        BaseScale = transform.localScale;
        Nodes.Add(this);

        // 채집 대상은 카메라를 가려도 반투명해지지 않는다 (가림 처리에서 제외, CameraOcclusion.md). 대신 마우스로 가리키면 하이라이트 (TargetSelector).
        var occluder = GetComponent<CameraOccluder>();
        if (occluder != null)
        {
            occluder.enabled = false;
        }
    }

    protected override void OnUnityDestroy()
    {
        Nodes.Remove(this);
        base.OnUnityDestroy();
    }

    public static void ClearAll()
    {
        Nodes.Clear();
    }

    private void Update()
    {
        if (ShakeTime > 0.0f)
        {
            ShakeTime -= Time.deltaTime;
            float s = 1.0f + Mathf.Sin(ShakeTime * 60.0f) * 0.04f * (ShakeTime / 0.2f);
            transform.localScale = BaseScale * s;
            if (ShakeTime <= 0.0f)
            {
                transform.localScale = BaseScale;
            }
        }

        if (IsDepleted == false)
        {
            return;
        }

        RespawnRemaining -= Time.deltaTime;
        if (RespawnRemaining <= 0.0f)
        {
            setDepleted(false);
            Health = Row.Health;
        }
    }

    /// <summary>플레이어 근접 공격이 맞았을 때. 획득한 아이템 수를 돌려준다.</summary>
    public int Harvest(PlayerCharacter InPlayer)
    {
        if (IsDepleted == true || InPlayer == null)
        {
            return 0;
        }

        float multiplier = getHarvestMultiplier(InPlayer.CurrentWeapon);
        if (multiplier <= 0.0f)
        {
            return 0;
        }

        ShakeTime = 0.2f;
        Health -= multiplier;
        float yieldBonus = 1.0f + InPlayer.Attributes.Get(EAttribute.ResourceYield) * 0.01f;
        int amount = Mathf.Max(1, Mathf.RoundToInt(Random.Range(Row.DropMin, Row.DropMax + 1) * yieldBonus));
        int left = InPlayer.Inventory.Add(Row.DropItem, amount);
        EventBus<ItemObtainedEvent>.Publish(new ItemObtainedEvent { Player = InPlayer, ItemId = Row.DropItem, Count = amount - left, Source = EItemSource.Harvest });

        if (Health <= 0.0f)
        {
            setDepleted(true);
            RespawnRemaining = Row.Respawn;
        }

        return amount;
    }

    private float getHarvestMultiplier(WeaponRow InWeapon)
    {
        if (InWeapon == null)
        {
            return 0.5f;
        }

        // 종류별 무기 효율은 HarvestKind 하위 클래스가 안다 (HarvestKinds.cs). 종류가 없으면(None) 채집 불가.
        var kind = HarvestKind.Of(Row.Harvest);
        return kind != null ? kind.GetMultiplier(InWeapon) : 0.0f;
    }

    private void setDepleted(bool InDepleted)
    {
        IsDepleted = InDepleted;
        for (int i = 0; i < Renderers.Length; ++i)
        {
            Renderers[i].enabled = InDepleted == false;
        }

        for (int i = 0; i < Colliders.Length; ++i)
        {
            Colliders[i].enabled = InDepleted == false;
        }
    }

    /// <summary>부채꼴 안에 채집 가능한 노드가 있으면 그 노드의 채집 애니메이션(AnimationTable Index)을 돌려준다 (없으면 0).</summary>
    public static int FindHarvestAnim(PlayerCharacter InPlayer, Vector3 InCenter, Vector3 InForward, float InRange, float InAngle)
    {
        for (int i = 0; i < Nodes.Count; ++i)
        {
            var node = Nodes[i];
            if (node == null || node.IsDepleted == true)
            {
                continue;
            }

            Vector3 toNode = node.transform.position - InCenter;
            float distance = MathUtil.Flat(toNode).magnitude;
            if (distance > InRange + 0.8f || (distance > 1.0f && MathUtil.IsInArc(InForward, toNode, InAngle) == false))
            {
                continue;
            }

            if (node.Row.Anim != 0)
            {
                return node.Row.Anim;
            }
        }

        return 0;
    }

    /// <summary>부채꼴 안의 노드를 채집한다 (MeleeComboAbility 에서 플레이어만 호출).</summary>
    public static void HarvestArc(PlayerCharacter InPlayer, Vector3 InCenter, Vector3 InForward, float InRange, float InAngle)
    {
        for (int i = 0; i < Nodes.Count; ++i)
        {
            var node = Nodes[i];
            if (node == null || node.IsDepleted == true)
            {
                continue;
            }

            Vector3 toNode = node.transform.position - InCenter;
            float distance = MathUtil.Flat(toNode).magnitude;
            if (distance > InRange + 0.8f)
            {
                continue;
            }

            if (distance > 1.0f && MathUtil.IsInArc(InForward, toNode, InAngle) == false)
            {
                continue;
            }

            node.Harvest(InPlayer);
        }
    }
}
