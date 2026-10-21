using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Framework;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 게임 데이터 테이블 임포터 (범용 변환은 Framework.Editor 의 TableImportUtility).
///  - Data/CSV/{TableType}.csv  →  Data/Tables/{TableType}.asset (TableAsset 하위 타입을 리플렉션으로 수집)
///  - 모든 테이블을 묶은 GameTables.asset 생성/갱신
///  - CSV 저장 시 자동 재변환은 Framework.Editor 의 TableImportPostprocessor 가 담당한다.
///  - 변환 후 참조 무결성(없는 이펙트/아이템/에셋 인덱스)을 검사해 콘솔에 경고한다.
/// </summary>
public static class CsvTableImporter
{
    [MenuItem("Game/Data/Import All CSV", priority = 10)]
    public static void ImportAll()
    {
        EditorAssetUtil.EnsureFolder(EditorPaths.Tables);
        var tableTypes = TypeCache.GetTypesDerivedFrom<TableAssetBase>().Where(t => t.IsAbstract == false && t.IsGenericType == false).ToList();
        var created = new Dictionary<Type, TableAssetBase>();

        foreach (var type in tableTypes)
        {
            string csvPath = $"{EditorPaths.Csv}/{type.Name}.csv";
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
            if (csv == null)
            {
                Debug.LogWarning($"[CSV] {csvPath} not found. ({type.Name} skipped)");
                continue;
            }

            string assetPath = $"{EditorPaths.Tables}/{type.Name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TableAssetBase>(assetPath);
            if (asset == null)
            {
                asset = (TableAssetBase)ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            asset.Source = csv;
            TableImportUtility.Import(asset);
            created[type] = asset;
        }

        buildGameTables(created);
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log($"[CSV] imported {created.Count} tables");
    }

    private static void buildGameTables(Dictionary<Type, TableAssetBase> InTables)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        if (root == null)
        {
            root = ScriptableObject.CreateInstance<GameTables>();
            AssetDatabase.CreateAsset(root, EditorPaths.GameTablesAsset);
        }

        foreach (var field in typeof(GameTables).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            TableAssetBase table;
            if (InTables.TryGetValue(field.FieldType, out table) == true)
            {
                field.SetValue(root, table);
            }
        }

        EditorUtility.SetDirty(root);
    }

    // ------------------------------------------------------------------ validation

    [MenuItem("Game/Data/Validate Tables", priority = 11)]
    public static void Validate()
    {
        var t = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        if (t == null)
        {
            return;
        }

        int problems = 0;
        Action<string> warn = message =>
        {
            problems += 1;
            Debug.LogWarning("[Validate] " + message);
        };

        foreach (var ability in t.Ability.All)
        {
            checkEffects(t, ability.HitEffects, $"Ability {ability.Index}.HitEffects", warn);
            checkEffects(t, ability.SelfEffects, $"Ability {ability.Index}.SelfEffects", warn);
            checkAsset(t, ability.Projectile, $"Ability {ability.Index}.Projectile", warn);
            checkCue(t, ability.CastCue, $"Ability {ability.Index}.CastCue", warn);
            checkCue(t, ability.HitCue, $"Ability {ability.Index}.HitCue", warn);
            checkText(t, ability.NameId, $"Ability {ability.Index}.NameId", warn);
            checkAnims(t, ability.Anims, $"Ability {ability.Index}.Anims", warn);
            bool needsProjectile = ability.Type == EAbilityType.Projectile && ability.Projectile == 0;
            if (needsProjectile == true)
            {
                warn($"Ability {ability.Index} is Projectile but has no Projectile asset");
            }
        }

        foreach (var character in t.Character.All)
        {
            checkAsset(t, character.Prefab, $"Character {character.Index}.Prefab", warn);
            checkText(t, character.NameId, $"Character {character.Index}.NameId", warn);
            foreach (int ability in character.Abilities)
            {
                if (t.Ability.TryGet(ability, out _) == false)
                {
                    warn($"Character {character.Index} ability {ability} missing");
                }
            }
        }

        foreach (var item in t.Item.All)
        {
            checkAsset(t, item.Icon, $"Item {item.Index}.Icon", warn);
            checkEffects(t, item.Effects, $"Item {item.Index}.Effects", warn);
            checkText(t, item.NameId, $"Item {item.Index}.NameId", warn);
        }

        foreach (var node in t.ResourceNode.All)
        {
            checkAnim(t, node.Anim, $"ResourceNode {node.Index}.Anim", warn);
        }

        checkAnim(t, Mathf.RoundToInt(constValue(t, EConst.BuildAnim)), "Const BuildAnim", warn);
        checkAnim(t, Mathf.RoundToInt(constValue(t, EConst.InteractAnim)), "Const InteractAnim", warn);

        foreach (var anim in t.Animation.All)
        {
            if (anim.Speed <= 0.0f)
            {
                warn($"Animation {anim.Index}: Speed must be > 0");
            }

            if (anim.Parameters.Length == 0)
            {
                warn($"Animation {anim.Index}: no Parameters");
            }
        }

        foreach (var cue in t.Cue.All)
        {
            checkAsset(t, cue.Asset, $"Cue {cue.Index}.Asset", warn);
        }

        foreach (var shop in t.Shop.All)
        {
            if (shop.Price != 0)
            {
                warn($"Shop {shop.Index}: Price must be 0 (all items are free by project rule)");
            }
        }

        foreach (var asset in t.Asset.All)
        {
            string ext = asset.Kind == EAssetKind.Sprite ? ".png" : (asset.Kind == EAssetKind.Material ? ".mat" : (asset.Kind == EAssetKind.Data ? ".asset" : ".prefab"));
            string path = $"{EditorPaths.Content}/{asset.Address}{ext}";
            if (File.Exists(path) == false)
            {
                warn($"Asset {asset.Index} '{asset.Address}' → {path} not found (run Game/Setup/Build Content)");
            }
        }

        Debug.Log(problems == 0 ? "[Validate] OK" : $"[Validate] {problems} problem(s)");
    }

    private static void checkEffects(GameTables InTables, int[] InIds, string InContext, Action<string> InWarn)
    {
        if (InIds == null)
        {
            return;
        }

        foreach (int id in InIds)
        {
            if (InTables.Effect.TryGet(id, out _) == false)
            {
                InWarn($"{InContext}: effect {id} missing");
            }
        }
    }

    private static void checkAsset(GameTables InTables, int InId, string InContext, Action<string> InWarn)
    {
        if (InId != 0 && InTables.Asset.TryGet(InId, out _) == false)
        {
            InWarn($"{InContext}: asset {InId} missing");
        }
    }

    private static void checkCue(GameTables InTables, int InId, string InContext, Action<string> InWarn)
    {
        if (InId != 0 && InTables.Cue.TryGet(InId, out _) == false)
        {
            InWarn($"{InContext}: cue {InId} missing");
        }
    }

    private static void checkAnims(GameTables InTables, int[] InIds, string InContext, Action<string> InWarn)
    {
        if (InIds == null)
        {
            return;
        }

        foreach (int id in InIds)
        {
            checkAnim(InTables, id, InContext, InWarn);
        }
    }

    private static void checkAnim(GameTables InTables, int InId, string InContext, Action<string> InWarn)
    {
        if (InId != 0 && InTables.Animation.TryGet(InId, out _) == false)
        {
            InWarn($"{InContext}: animation {InId} missing");
        }
    }

    private static float constValue(GameTables InTables, EConst InConst)
    {
        ConstRow row;
        return InTables.Const.TryGet((int)InConst, out row) == true ? row.Value : 0.0f;
    }

    private static void checkText(GameTables InTables, int InId, string InContext, Action<string> InWarn)
    {
        if (InId != 0 && InTables.String.TryGet(InId, out _) == false)
        {
            InWarn($"{InContext}: string {InId} missing");
        }
    }
}
