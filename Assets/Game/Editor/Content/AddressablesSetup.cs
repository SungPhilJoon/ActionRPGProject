using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// Addressables 등록.
///  - Content 폴더 아래 모든 에셋: 주소 = Content 기준 상대 경로(확장자 제외) — AssetTable.Address 와 같은 규칙
///  - GameTables.asset: 부트스트랩이 AssetReference 로 로드
/// 그룹은 로컬 빌드(원격 CDN 없음). 에디터 플레이 모드는 "Use Asset Database" 로 바로 동작한다.
/// </summary>
public static class AddressablesSetup
{
    private const string ContentGroup = "GameContent";
    private const string DataGroup = "GameData";

    [MenuItem("Game/Setup/4. Addressables (Register Content)", priority = 4)]
    public static void Run()
    {
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var content = getOrCreateGroup(settings, ContentGroup);
        var data = getOrCreateGroup(settings, DataGroup);

        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { EditorPaths.Content }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path) == true)
            {
                continue;
            }

            string relative = path.Substring(EditorPaths.Content.Length + 1);
            string address = Path.ChangeExtension(relative, null).Replace('\\', '/');
            var entry = settings.CreateOrMoveEntry(guid, content, false, false);
            entry.address = address;
            count += 1;
        }

        string tablesGuid = AssetDatabase.AssetPathToGUID(EditorPaths.GameTablesAsset);
        if (string.IsNullOrEmpty(tablesGuid) == false)
        {
            var entry = settings.CreateOrMoveEntry(tablesGuid, data, false, false);
            entry.address = "Tables/GameTables";
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Addressables] registered {count} content assets");
    }

    private static AddressableAssetGroup getOrCreateGroup(AddressableAssetSettings InSettings, string InName)
    {
        var group = InSettings.FindGroup(InName);
        if (group != null)
        {
            return group;
        }

        group = InSettings.CreateGroup(InName, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        var schema = group.GetSchema<BundledAssetGroupSchema>();
        schema.BuildPath.SetVariableByName(InSettings, AddressableAssetSettings.kLocalBuildPath);
        schema.LoadPath.SetVariableByName(InSettings, AddressableAssetSettings.kLocalLoadPath);
        schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        return group;
    }

    [MenuItem("Game/Build/Build Addressables Content", priority = 50)]
    public static void BuildContent()
    {
        AddressableAssetSettings.BuildPlayerContent();
    }
}
