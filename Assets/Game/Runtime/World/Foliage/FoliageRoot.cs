using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>
/// 씬의 장식 식생 목록 (WorldBuilder 가 채운다) + XZ 격자 공간 색인.
/// 건축 바운드와 겹치는 식생을 격자 칸 단위로 찾는다 (수천 그루를 매번 전부 검사하지 않음).
/// </summary>
public class FoliageRoot : BaseBehaviour
{
    private const float CellSize = 16.0f;

    public FoliageInstance[] Instances = new FoliageInstance[0];

    private readonly Dictionary<long, List<FoliageInstance>> Grid = new Dictionary<long, List<FoliageInstance>>();
    private float MaxRadius;

    protected override void OnUnityAwake()
    {
        base.OnUnityAwake();
        buildGrid();
    }

    /// <summary>InBounds(XZ)와 밑동 원이 겹치는 식생을 비활성화한다. 지운 수를 돌려준다.</summary>
    public int ClearArea(Bounds InBounds)
    {
        int count = 0;
        int x0 = cellOf(InBounds.min.x - MaxRadius);
        int x1 = cellOf(InBounds.max.x + MaxRadius);
        int z0 = cellOf(InBounds.min.z - MaxRadius);
        int z1 = cellOf(InBounds.max.z + MaxRadius);
        for (int z = z0; z <= z1; ++z)
        {
            for (int x = x0; x <= x1; ++x)
            {
                List<FoliageInstance> cell;
                if (Grid.TryGetValue(key(x, z), out cell) == false)
                {
                    continue;
                }

                for (int i = 0; i < cell.Count; ++i)
                {
                    var instance = cell[i];
                    if (instance == null || instance.IsCleared == true)
                    {
                        continue;
                    }

                    if (BoundsUtil.CircleOverlapsXZ(InBounds, instance.transform.position, instance.Radius) == true)
                    {
                        instance.Clear();
                        ++count;
                    }
                }
            }
        }

        return count;
    }

    private void buildGrid()
    {
        Grid.Clear();
        MaxRadius = 0.0f;
        for (int i = 0; i < Instances.Length; ++i)
        {
            var instance = Instances[i];
            if (instance == null)
            {
                continue;
            }

            Vector3 position = instance.transform.position;
            long cellKey = key(cellOf(position.x), cellOf(position.z));
            List<FoliageInstance> cell;
            if (Grid.TryGetValue(cellKey, out cell) == false)
            {
                cell = new List<FoliageInstance>();
                Grid.Add(cellKey, cell);
            }

            cell.Add(instance);
            MaxRadius = Mathf.Max(MaxRadius, instance.Radius);
        }
    }

    private static int cellOf(float InValue)
    {
        return Mathf.FloorToInt(InValue / CellSize);
    }

    private static long key(int InX, int InZ)
    {
        return ((long)InX << 32) | (uint)InZ;
    }
}
