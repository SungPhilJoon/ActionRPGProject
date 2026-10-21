using System.IO;
using ActionRPG.Core.EditorTools.DataDriven;
using ActionRPG.Data;
using UnityEditor;
using UnityEngine;

namespace ActionRPG.EditorTools
{
    /// <summary>
    /// CSV 데이터 드리븐 파이프라인 데모용 CsvImportProfile 을 한 번에 만들어 준다.
    /// 실행 후 곧바로 ActionRPG &gt; 데이터 &gt; CSV 임포트 로 결과를 확인할 수 있다.
    /// </summary>
    public static class CsvDemoSetup
    {
        private const string ProfilePath = "Assets/Data/CSV_EnemyStats.asset";
        private const string CsvPath = "Assets/Data/CSV/EnemyStats.csv";
        private const string TargetScriptPath = "Assets/Scripts/Data/EnemyStatRow.cs";
        private const string OutputFolder = "Assets/Data/Generated/EnemyStats";

        [MenuItem("ActionRPG/데이터/CSV 데모 프로필 생성", priority = 20)]
        public static void CreateDemoProfile()
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(TargetScriptPath);

            if (csv == null) { Debug.LogError($"[CsvDemoSetup] {CsvPath} 를 찾지 못했습니다."); return; }
            if (script == null) { Debug.LogError($"[CsvDemoSetup] {TargetScriptPath} 를 찾지 못했습니다."); return; }

            Directory.CreateDirectory(OutputFolder);

            var profile = AssetDatabase.LoadAssetAtPath<CsvImportProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CsvImportProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.SourceCsv = csv;
            profile.TargetScript = script;
            profile.OutputFolder = OutputFolder;
            profile.IdColumnName = "Id";
            profile.AssetNamePrefix = "ESR_";
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CsvDemoSetup] '{ProfilePath}' 준비 완료. 선택한 뒤 " +
                      "ActionRPG > 데이터 > CSV 임포트 (선택된 프로필) 을 실행해 보세요.");

            // 바로 한 번 돌려서 결과물까지 보여 준다.
            CsvScriptableObjectImporter.Import(profile);
            Selection.activeObject = profile;
        }
    }
}
