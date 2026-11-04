using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Framework.Editor
{
    /// <summary>
    /// 그림자를 드리우는 메시와 같은 모양의 MeshCollider 를 지정 레이어에 붙인다.
    /// 그림자 맵(GPU)을 CPU 에서 읽지 않고도, 물리 레이캐스트로 "화면에 보이는 그림자" 와 같은 그늘 판정을 하기 위한 용도.
    ///  - 그림자를 드리우지 않는 렌더러(ShadowCastingMode.Off)와 LOD1 이하 렌더러는 건너뛴다.
    ///  - 같은 메시의 MeshCollider 가 이미 있으면 건너뛴다 (중복 쿠킹 방지).
    ///  - 콜라이더는 렌더러의 자식 오브젝트로 만들어 위치/회전/스케일을 그대로 따른다.
    /// 에디터(빌드 시점)에서 만들어 두므로 런타임 메시 쿠킹 비용과 Read/Write 제약이 없다.
    /// </summary>
    public static class ShadeColliderUtil
    {
        private const string ChildName = "SunShade";

        /// <summary>InRoot 아래 그림자를 드리우는 MeshRenderer 마다 InLayer MeshCollider 를 붙인다. 붙인 개수를 돌려준다.</summary>
        public static int AddShadeColliders(GameObject InRoot, int InLayer)
        {
            if (InRoot == null)
            {
                return 0;
            }

            var skipped = collectLowerLodRenderers(InRoot);
            int count = 0;
            foreach (var renderer in InRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.shadowCastingMode == ShadowCastingMode.Off || skipped.Contains(renderer) == true)
                {
                    continue;
                }

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || hasMeshCollider(renderer.gameObject, filter.sharedMesh) == true)
                {
                    continue;
                }

                var shade = new GameObject(ChildName);
                shade.layer = InLayer;
                shade.isStatic = renderer.gameObject.isStatic;
                shade.transform.SetParent(renderer.transform, false);
                shade.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                ++count;
            }

            return count;
        }

        /// <summary>
        /// Terrain Tree 인스턴스마다 그늘 콜라이더를 만든다 (Terrain Tree 는 GameObject 가 아니라 물리 판정이 없다).
        /// 각 나무 프로토타입 프리팹의 메시를 그대로 MeshCollider 로 쓰고, 인스턴스의 위치/회전/폭·높이 스케일을 적용한다.
        /// </summary>
        public static GameObject BuildTreeShade(Terrain InTerrain, int InLayer, string InRootName)
        {
            var data = InTerrain.terrainData;
            var root = new GameObject(InRootName);
            root.isStatic = true;
            var shapes = new List<List<ShadeShape>>();
            foreach (var prototype in data.treePrototypes)
            {
                shapes.Add(collectShapes(prototype.prefab));
            }

            Vector3 origin = InTerrain.transform.position;
            Vector3 size = data.size;
            foreach (var instance in data.treeInstances)
            {
                if (instance.prototypeIndex < 0 || instance.prototypeIndex >= shapes.Count)
                {
                    continue;
                }

                var tree = new GameObject("Tree");
                tree.layer = InLayer;
                tree.isStatic = true;
                tree.transform.SetParent(root.transform, false);
                tree.transform.position = origin + Vector3.Scale(instance.position, size);
                tree.transform.rotation = Quaternion.Euler(0.0f, instance.rotation * Mathf.Rad2Deg, 0.0f);
                tree.transform.localScale = new Vector3(instance.widthScale, instance.heightScale, instance.widthScale);

                foreach (var shape in shapes[instance.prototypeIndex])
                {
                    var part = new GameObject(ChildName);
                    part.layer = InLayer;
                    part.isStatic = true;
                    part.transform.SetParent(tree.transform, false);
                    part.transform.localPosition = shape.Position;
                    part.transform.localRotation = shape.Rotation;
                    part.transform.localScale = shape.Scale;
                    part.AddComponent<MeshCollider>().sharedMesh = shape.Mesh;
                }
            }

            return root;
        }

        private struct ShadeShape
        {
            public Mesh Mesh;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        /// <summary>프리팹 루트 기준으로 각 메시의 상대 변환을 모은다.</summary>
        private static List<ShadeShape> collectShapes(GameObject InPrefab)
        {
            var shapes = new List<ShadeShape>();
            if (InPrefab == null)
            {
                return shapes;
            }

            var skipped = collectLowerLodRenderers(InPrefab);
            Matrix4x4 toRoot = InPrefab.transform.worldToLocalMatrix;
            Vector3 rootScale = InPrefab.transform.localScale;
            foreach (var renderer in InPrefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || renderer.shadowCastingMode == ShadowCastingMode.Off || skipped.Contains(renderer) == true)
                {
                    continue;
                }

                // Terrain 은 프로토타입 루트의 스케일을 인스턴스 스케일과 곱해 그린다 → 루트 스케일도 반영.
                Matrix4x4 local = Matrix4x4.Scale(rootScale) * toRoot * renderer.transform.localToWorldMatrix;
                shapes.Add(new ShadeShape
                {
                    Mesh = filter.sharedMesh,
                    Position = local.GetPosition(),
                    Rotation = local.rotation,
                    Scale = local.lossyScale,
                });
            }

            return shapes;
        }

        private static HashSet<Renderer> collectLowerLodRenderers(GameObject InRoot)
        {
            var result = new HashSet<Renderer>();
            foreach (var group in InRoot.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = group.GetLODs();
                for (int i = 1; i < lods.Length; ++i)
                {
                    foreach (var renderer in lods[i].renderers)
                    {
                        if (renderer != null)
                        {
                            result.Add(renderer);
                        }
                    }
                }
            }

            return result;
        }

        private static bool hasMeshCollider(GameObject InObject, Mesh InMesh)
        {
            foreach (var collider in InObject.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh == InMesh && (collider.transform == InObject.transform || collider.transform.parent == InObject.transform))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
