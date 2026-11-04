using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>
    /// CSV(TextAsset) → TableAsset 변환 (게임 데이터 종류를 모르는 범용 부분).
    /// 어떤 테이블을 어디에 만들지는 게임 쪽 에디터 코드(CsvTableImporter)가 정한다.
    /// </summary>
    public static class TableImportUtility
    {
        /// <summary>InAsset.Source 의 CSV 를 행 타입으로 변환해 넣는다. 성공하면 true.</summary>
        public static bool Import(TableAssetBase InAsset)
        {
            if (InAsset == null || InAsset.Source == null)
            {
                return false;
            }

            string error;
            var rows = CsvConvert.ToRows(InAsset.GetRowType(), InAsset.Source.text, out error);
            if (string.IsNullOrEmpty(error) == false)
            {
                Debug.LogError($"[CSV] {InAsset.name}: {error}", InAsset);
            }

            InAsset.SetRows(rows);
            EditorUtility.SetDirty(InAsset);
            return string.IsNullOrEmpty(error) == true;
        }
    }

    /// <summary>
    /// CSV 를 저장하면 그 CSV 를 Source 로 쓰는 모든 TableAsset 을 자동으로 다시 변환한다.
    /// 경로 규칙 없이 Source 참조로 찾으므로 프로젝트 폴더 구조와 무관하게 동작한다.
    /// </summary>
    public class TableImportPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] InImported, string[] InDeleted, string[] InMoved, string[] InMovedFrom)
        {
            bool hasCsv = false;
            foreach (var path in InImported)
            {
                if (path.EndsWith(".csv") == true)
                {
                    hasCsv = true;
                    break;
                }
            }

            if (hasCsv == false)
            {
                return;
            }

            bool any = false;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(TableAssetBase)))
            {
                var table = AssetDatabase.LoadAssetAtPath<TableAssetBase>(AssetDatabase.GUIDToAssetPath(guid));
                if (table == null || table.Source == null)
                {
                    continue;
                }

                string sourcePath = AssetDatabase.GetAssetPath(table.Source);
                if (System.Array.IndexOf(InImported, sourcePath) < 0)
                {
                    continue;
                }

                TableImportUtility.Import(table);
                any = true;
            }

            if (any == true)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
