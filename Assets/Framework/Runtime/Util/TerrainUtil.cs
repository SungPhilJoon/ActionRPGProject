using UnityEngine;

namespace Framework
{
    /// <summary>런타임 Terrain 편집 도우미.</summary>
    public static class TerrainUtil
    {
        /// <summary>
        /// Terrain 이 쓰는 TerrainData 를 복제해 Terrain/TerrainCollider 에 연결하고 원본을 돌려준다.
        /// 런타임에 디테일(풀)을 지우면 TerrainData 에셋 자체가 바뀌므로 (에디터 Play 모드에서는 디스크에 남는다) 복제본을 편집한다.
        /// </summary>
        public static TerrainData UseRuntimeCopy(Terrain InTerrain)
        {
            if (InTerrain == null || InTerrain.terrainData == null)
            {
                return null;
            }

            var original = InTerrain.terrainData;
            var copy = Object.Instantiate(original);
            copy.name = original.name + " (Runtime)";
            InTerrain.terrainData = copy;
            var collider = InTerrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.terrainData = copy;
            }

            return original;
        }

        /// <summary>UseRuntimeCopy 로 바꾼 복제본을 버리고 원본으로 되돌린다.</summary>
        public static void RestoreOriginal(Terrain InTerrain, TerrainData InOriginal)
        {
            if (InTerrain == null || InOriginal == null || InTerrain.terrainData == InOriginal)
            {
                return;
            }

            var copy = InTerrain.terrainData;
            InTerrain.terrainData = InOriginal;
            var collider = InTerrain.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.terrainData = InOriginal;
            }

            Object.Destroy(copy);
        }

        /// <summary>월드 XZ 사각형(InBounds) 안의 모든 디테일 레이어(풀)를 지운다. 지운 셀이 있으면 true.</summary>
        public static bool ClearDetails(Terrain InTerrain, Bounds InBounds)
        {
            if (InTerrain == null || InTerrain.terrainData == null)
            {
                return false;
            }

            var data = InTerrain.terrainData;
            int resolution = data.detailResolution;
            Vector3 origin = InTerrain.transform.position;
            Vector3 size = data.size;
            int x0 = Mathf.Clamp(Mathf.FloorToInt((InBounds.min.x - origin.x) / size.x * resolution), 0, resolution);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((InBounds.max.x - origin.x) / size.x * resolution), 0, resolution);
            int y0 = Mathf.Clamp(Mathf.FloorToInt((InBounds.min.z - origin.z) / size.z * resolution), 0, resolution);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((InBounds.max.z - origin.z) / size.z * resolution), 0, resolution);
            int width = x1 - x0;
            int height = y1 - y0;
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            bool changed = false;
            for (int layer = 0; layer < data.detailPrototypes.Length; ++layer)
            {
                var map = data.GetDetailLayer(x0, y0, width, height, layer);
                bool layerChanged = false;
                for (int y = 0; y < height; ++y)
                {
                    for (int x = 0; x < width; ++x)
                    {
                        if (map[y, x] != 0)
                        {
                            map[y, x] = 0;
                            layerChanged = true;
                        }
                    }
                }

                if (layerChanged == true)
                {
                    data.SetDetailLayer(x0, y0, layer, map);
                    changed = true;
                }
            }

            return changed;
        }
    }
}
