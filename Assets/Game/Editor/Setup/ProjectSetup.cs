using UnityEditor;
using UnityEngine;

/// <summary>
/// 레이어 이름 등록. 런타임은 LayerId 의 int 상수만 쓰고, 이름은 에디터 가독성을 위해서만 등록한다.
/// </summary>
public static class ProjectSetup
{
    private static readonly int[] Layers = { LayerId.Player, LayerId.Monster, LayerId.Ground, LayerId.Interactable, LayerId.Structure, LayerId.SunBlocker, LayerId.Projectile };
    private static readonly string[] Names = { "Player", "Monster", "Ground", "Interactable", "Structure", "SunBlocker", "Projectile" };

    [MenuItem("Game/Setup/1. Project Settings (Layers, Physics)", priority = 1)]
    public static void Run()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 0; i < Layers.Length; ++i)
        {
            var element = layers.GetArrayElementAtIndex(Layers[i]);
            element.stringValue = Names[i];
        }

        tagManager.ApplyModifiedProperties();

        // 서로 충돌할 필요 없는 레이어 조합 (CharacterController 가 SunBlocker 트리거/발사체와 부딪히지 않게).
        Physics.IgnoreLayerCollision(LayerId.Player, LayerId.SunBlocker, true);
        Physics.IgnoreLayerCollision(LayerId.Monster, LayerId.SunBlocker, true);
        Physics.IgnoreLayerCollision(LayerId.Projectile, LayerId.Player, true);
        Physics.IgnoreLayerCollision(LayerId.Projectile, LayerId.Monster, true);
        Physics.queriesHitTriggers = false;
        ConfigureNavMeshAreas();
        Debug.Log("[Setup] layers and physics configured");
    }

    /// <summary>NavMesh 영역 이름 · 기본 비용 (NavAreaId). 길 우선 비용은 런타임 QuestNavigator 가 ConstTable.OffRoadCost 로 다시 맞춘다.</summary>
    public static void ConfigureNavMeshAreas()
    {
        var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
        var areas = settings.FindProperty("areas");
        var road = areas.GetArrayElementAtIndex(NavAreaId.Road);
        road.FindPropertyRelative("name").stringValue = "Road";
        road.FindPropertyRelative("cost").floatValue = 1.0f;
        areas.GetArrayElementAtIndex(NavAreaId.Walkable).FindPropertyRelative("cost").floatValue = 3.0f;
        settings.ApplyModifiedProperties();
    }
}
