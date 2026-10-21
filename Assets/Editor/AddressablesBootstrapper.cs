using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ActionRPG.EditorTools
{
    /// <summary>
    /// Addressables 를 "로컬 전용 에셋 번들 시스템"으로 초기 설정한다.
    ///
    /// 로컬로 쓴다는 것의 의미: 그룹의 Build/Load Path 를 kLocalBuildPath/kLocalLoadPath 로
    /// 두면, 에셋 번들이 빌드에 그대로 포함되고(StreamingAssets 밑) 원격 CDN 없이
    /// Addressables.LoadAssetAsync 로 그대로 로드된다. 나중에 서버를 두고 싶어지면
    /// 그룹의 경로만 Remote 로 바꾸면 되고 게임 코드(<see cref="ActionRPG.Core.AddressableAssets.CatalogService"/>)는
    /// 한 줄도 안 바뀐다.
    ///
    /// "커스텀 카탈로그" 확장 지점: 기본 카탈로그 외에 별도로 만든 카탈로그를
    /// StreamingAssets/AdditionalCatalogs 밑에 빌드해 두고, 런타임에
    /// CatalogService.LoadAdditionalCatalogAsync() 로 얹는 흐름을 이 스크립트가 폴더까지 준비해 둔다.
    /// </summary>
    public static class AddressablesBootstrapper
    {
        private const string DefaultGroupName = "ActionRPG-Local";
        private const string AdditionalCatalogFolder = "Assets/StreamingAssets/AdditionalCatalogs";

        [MenuItem("ActionRPG/Addressables/초기 설정 (로컬 그룹 생성)")]
        public static void SetupLocalGroup()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("[AddressablesBootstrapper] AddressableAssetSettings 를 생성하지 못했습니다.");
                return;
            }

            var group = settings.FindGroup(DefaultGroupName);
            if (group == null)
            {
                group = settings.CreateGroup(
                    DefaultGroupName,
                    setAsDefaultGroup: true,
                    readOnly: false,
                    postEvent: false,
                    schemasToCopy: null,
                    typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
            }

            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundleSchema != null)
            {
                // 원격 CDN 없이 빌드에 그대로 포함되는 로컬 경로.
                bundleSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                bundleSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                bundleSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            }

            Directory.CreateDirectory(AdditionalCatalogFolder);
            AssetDatabase.Refresh();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AddressablesBootstrapper] '{DefaultGroupName}' 그룹을 로컬 빌드/로드 경로로 준비했습니다. " +
                      $"추가 카탈로그는 {AdditionalCatalogFolder} 에 빌드해 CatalogService.LoadAdditionalCatalogAsync() 로 불러오세요.");
        }

        /// <summary>선택한 폴더 밑의 모든 프리팹을 Addressable 로 등록하고, 폴더 이름을 레이블로 붙인다.</summary>
        [MenuItem("ActionRPG/Addressables/선택 폴더의 프리팹을 Addressable 로 등록")]
        public static void MarkSelectedFolderAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;

            var group = settings.FindGroup(DefaultGroupName) ?? settings.DefaultGroup;
            if (group == null)
            {
                Debug.LogError("[AddressablesBootstrapper] 먼저 '초기 설정 (로컬 그룹 생성)' 을 실행하세요.");
                return;
            }

            string folder = null;
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path)) { folder = path; break; }
            }

            if (folder == null)
            {
                Debug.LogWarning("[AddressablesBootstrapper] 프로젝트 창에서 폴더를 하나 선택한 뒤 실행하세요.");
                return;
            }

            string label = Path.GetFileName(folder);
            settings.AddLabel(label, postEvent: false);

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            int count = 0;
            foreach (var guid in guids)
            {
                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                if (entry == null) continue;

                entry.address = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                entry.SetLabel(label, true, true, false);
                count++;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AddressablesBootstrapper] '{folder}' 아래 프리팹 {count}개를 그룹 '{group.Name}', 레이블 '{label}' 로 등록했습니다.");
        }
    }
}
