using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ActionRPG.Core.DataDriven;
using UnityEditor;
using UnityEngine;

namespace ActionRPG.Core.EditorTools.DataDriven
{
    /// <summary>
    /// CsvImportProfile 하나를 읽어 [CsvColumn] 이 붙은 필드에 리플렉션으로 값을 채운
    /// ScriptableObject 에셋들을 만들거나 갱신한다.
    ///
    /// 이 클래스는 어떤 게임 데이터 타입이든 동작하는 제네릭 엔진이다 — SkillDefinition,
    /// EnemyDefinition, 새로 만들 그 어떤 테이블이든 필드에 [CsvColumn] 만 붙이면
    /// 코드를 한 줄도 더 안 짜도 CSV 로 데이터를 몰 수 있다.
    ///
    /// 지원 필드 타입: string, int, long, float, double, bool, enum, 그리고
    /// 위 타입들의 배열(세미콜론으로 구분된 값, 예: "1;2;3" → int[]{1,2,3}).
    /// </summary>
    public static class CsvScriptableObjectImporter
    {
        private const char ArraySeparator = ';';

        [MenuItem("ActionRPG/데이터/CSV 임포트 (선택된 프로필)", true)]
        private static bool ValidateImportSelected() => Selection.activeObject is CsvImportProfile;

        [MenuItem("ActionRPG/데이터/CSV 임포트 (선택된 프로필)")]
        private static void ImportSelected()
        {
            if (Selection.activeObject is CsvImportProfile profile) Import(profile);
        }

        [MenuItem("ActionRPG/데이터/CSV 전체 임포트 (프로젝트 내 모든 프로필)")]
        public static void ImportAll()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(CsvImportProfile)}");
            int count = 0;
            foreach (var guid in guids)
            {
                var profile = AssetDatabase.LoadAssetAtPath<CsvImportProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null) continue;
                Import(profile);
                count++;
            }
            Debug.Log($"[CsvScriptableObjectImporter] 프로필 {count}개 임포트 완료.");
        }

        public static void Import(CsvImportProfile profile)
        {
            if (profile == null) return;

            if (profile.SourceCsv == null) { Debug.LogError($"[CsvScriptableObjectImporter] '{profile.name}': SourceCsv 가 비어 있습니다.", profile); return; }
            if (profile.TargetScript == null) { Debug.LogError($"[CsvScriptableObjectImporter] '{profile.name}': TargetScript 가 비어 있습니다.", profile); return; }

            var targetType = profile.TargetScript.GetClass();
            if (targetType == null || !typeof(ScriptableObject).IsAssignableFrom(targetType))
            {
                Debug.LogError($"[CsvScriptableObjectImporter] '{profile.name}': TargetScript 는 ScriptableObject 를 상속해야 합니다.", profile);
                return;
            }

            var fields = CollectCsvFields(targetType);
            if (fields.Count == 0)
            {
                Debug.LogWarning($"[CsvScriptableObjectImporter] '{targetType.Name}' 에 [CsvColumn] 필드가 하나도 없습니다.");
                return;
            }

            var (headers, rows) = CsvParser.Parse(profile.SourceCsv.text);
            var headerIndex = BuildHeaderIndex(headers);

            if (!headerIndex.ContainsKey(profile.IdColumnName))
            {
                Debug.LogError($"[CsvScriptableObjectImporter] '{profile.name}': ID 컬럼 '{profile.IdColumnName}' 을 CSV 헤더에서 찾지 못했습니다.", profile);
                return;
            }

            Directory.CreateDirectory(profile.OutputFolder);

            var seenIds = new HashSet<string>();
            int created = 0, updated = 0, skipped = 0;

            foreach (var row in rows)
            {
                string id = ReadCell(row, headerIndex, profile.IdColumnName);
                if (string.IsNullOrWhiteSpace(id)) { skipped++; continue; }

                if (!seenIds.Add(id))
                    Debug.LogWarning($"[CsvScriptableObjectImporter] ID '{id}' 가 CSV 에 중복됩니다. 마지막 행 값으로 덮어씁니다.");

                string assetName = profile.AssetNamePrefix + SanitizeFileName(id);
                string assetPath = $"{profile.OutputFolder}/{assetName}.asset";

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                bool isNew = asset == null;
                if (isNew)
                {
                    asset = ScriptableObject.CreateInstance(targetType);
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                ApplyRow(asset, fields, row, headerIndex, profile.name);
                EditorUtility.SetDirty(asset);

                if (isNew) created++; else updated++;
            }

            if (profile.DeleteOrphanedAssets) DeleteOrphans(profile.OutputFolder, profile.AssetNamePrefix, seenIds, targetType);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CsvScriptableObjectImporter] '{profile.name}' 완료 — 생성 {created}, 갱신 {updated}, 건너뜀 {skipped} (총 {rows.Count}행).");
        }

        private struct FieldBinding
        {
            public FieldInfo Field;
            public string ColumnName;
            public bool Required;
        }

        private static List<FieldBinding> CollectCsvFields(Type type)
        {
            var result = new List<FieldBinding>();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attr = field.GetCustomAttribute<CsvColumnAttribute>();
                if (attr == null) continue;
                result.Add(new FieldBinding
                {
                    Field = field,
                    ColumnName = string.IsNullOrEmpty(attr.ColumnName) ? field.Name : attr.ColumnName,
                    Required = attr.Required
                });
            }
            return result;
        }

        private static Dictionary<string, int> BuildHeaderIndex(string[] headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++) map[headers[i].Trim()] = i;
            return map;
        }

        private static string ReadCell(string[] row, Dictionary<string, int> headerIndex, string column)
        {
            if (!headerIndex.TryGetValue(column, out int index) || index >= row.Length) return null;
            return row[index];
        }

        private static void ApplyRow(ScriptableObject asset, List<FieldBinding> fields, string[] row, Dictionary<string, int> headerIndex, string profileName)
        {
            foreach (var binding in fields)
            {
                string raw = ReadCell(row, headerIndex, binding.ColumnName);

                if (string.IsNullOrEmpty(raw))
                {
                    if (binding.Required)
                        Debug.LogWarning($"[CsvScriptableObjectImporter] '{profileName}': 필수 컬럼 '{binding.ColumnName}' 이 비어 있습니다 ({asset.name}).");
                    continue;
                }

                object converted = ConvertValue(raw, binding.Field.FieldType, binding.ColumnName);
                if (converted != null) binding.Field.SetValue(asset, converted);
            }
        }

        private static object ConvertValue(string raw, Type fieldType, string columnName)
        {
            try
            {
                if (fieldType.IsArray)
                {
                    var elementType = fieldType.GetElementType();
                    var parts = raw.Split(ArraySeparator);
                    var array = Array.CreateInstance(elementType, parts.Length);
                    for (int i = 0; i < parts.Length; i++)
                        array.SetValue(ConvertScalar(parts[i].Trim(), elementType), i);
                    return array;
                }
                return ConvertScalar(raw, fieldType);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CsvScriptableObjectImporter] 컬럼 '{columnName}' 값 '{raw}' 을 {fieldType.Name} 으로 변환하지 못했습니다: {e.Message}");
                return null;
            }
        }

        private static object ConvertScalar(string raw, Type type)
        {
            raw = raw.Trim();

            if (type == typeof(string)) return raw;
            if (type == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return long.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(ushort)) return ushort.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return float.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(double)) return double.Parse(raw, CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return raw is "1" or "true" or "TRUE" or "True";
            if (type.IsEnum) return Enum.Parse(type, raw, ignoreCase: true);

            throw new NotSupportedException($"지원하지 않는 필드 타입: {type.Name}");
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private static void DeleteOrphans(string folder, string prefix, HashSet<string> validIds, Type targetType)
        {
            var guids = AssetDatabase.FindAssets($"t:{targetType.Name}", new[] { folder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                string id = fileName.StartsWith(prefix) ? fileName.Substring(prefix.Length) : fileName;

                if (!validIds.Contains(id))
                {
                    Debug.Log($"[CsvScriptableObjectImporter] CSV 에서 사라진 항목 삭제: {path}");
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }
    }
}
