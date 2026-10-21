using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Framework
{
    /// <summary>
    /// CSV 셀 문자열 → 필드 값 변환. 행 타입의 public 필드명과 CSV 헤더를 1:1 로 맞춘다.
    ///  - 배열: 셀 안에서 '|' 로 구분 (예: 1001|1002)
    ///  - Vector2/3: ';' 로 구분 (예: 0;1.5;0)
    ///  - enum: 이름 또는 정수, 플래그 enum 은 '|' 로 OR
    ///  - bool: 1/0, true/false
    ///  - [Serializable] struct: ';' 로 구분, public 필드 선언 순서 (예: Trigger;Attack1). 배열이면 '|' 로 여러 개
    /// 변환은 에디터 임포트 때 한 번만 일어나고, 런타임은 결과 ScriptableObject 만 읽는다.
    /// </summary>
    public static class CsvConvert
    {
        public const char ArraySeparator = '|';
        public const char VectorSeparator = ';';

        public static IList ToRows(Type InRowType, string InCsvText, out string OutError)
        {
            OutError = null;
            var listType = typeof(List<>).MakeGenericType(InRowType);
            var list = (IList)Activator.CreateInstance(listType);
            var table = CsvReader.Parse(InCsvText);
            if (table.Count == 0)
            {
                OutError = "empty csv";
                return list;
            }

            var header = table[0];
            var fields = new FieldInfo[header.Length];
            for (int c = 0; c < header.Length; ++c)
            {
                string name = header[c].Trim();
                if (name.Length == 0 || name[0] == '#')
                {
                    continue;
                }

                fields[c] = InRowType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (fields[c] == null)
                {
                    OutError = $"column '{name}' has no matching field in {InRowType.Name}";
                }
            }

            for (int r = 1; r < table.Count; ++r)
            {
                var cells = table[r];
                var row = Activator.CreateInstance(InRowType);
                for (int c = 0; c < fields.Length && c < cells.Length; ++c)
                {
                    if (fields[c] == null)
                    {
                        continue;
                    }

                    try
                    {
                        object value = ToValue(fields[c].FieldType, cells[c].Trim());
                        fields[c].SetValue(row, value);
                    }
                    catch (Exception e)
                    {
                        OutError = $"row {r + 1}, column '{header[c]}': '{cells[c]}' → {e.Message}";
                    }
                }

                list.Add(row);
            }

            return list;
        }

        public static object ToValue(Type InType, string InCell)
        {
            if (InType == typeof(string))
            {
                return InCell;
            }

            if (InType.IsArray == true)
            {
                var elementType = InType.GetElementType();
                if (InCell.Length == 0)
                {
                    return Array.CreateInstance(elementType, 0);
                }

                var parts = InCell.Split(ArraySeparator);
                var array = Array.CreateInstance(elementType, parts.Length);
                for (int i = 0; i < parts.Length; ++i)
                {
                    array.SetValue(ToValue(elementType, parts[i].Trim()), i);
                }

                return array;
            }

            if (InCell.Length == 0)
            {
                return InType.IsValueType == true ? Activator.CreateInstance(InType) : null;
            }

            if (InType == typeof(int))
            {
                return int.Parse(InCell, CultureInfo.InvariantCulture);
            }

            if (InType == typeof(float))
            {
                return float.Parse(InCell, CultureInfo.InvariantCulture);
            }

            if (InType == typeof(long))
            {
                return long.Parse(InCell, CultureInfo.InvariantCulture);
            }

            if (InType == typeof(bool))
            {
                if (InCell == "1")
                {
                    return true;
                }

                if (InCell == "0")
                {
                    return false;
                }

                return bool.Parse(InCell);
            }

            if (InType.IsEnum == true)
            {
                return toEnum(InType, InCell);
            }

            if (InType == typeof(Vector3))
            {
                var p = InCell.Split(VectorSeparator);
                return new Vector3(parseFloat(p, 0), parseFloat(p, 1), parseFloat(p, 2));
            }

            if (InType == typeof(Vector2))
            {
                var p = InCell.Split(VectorSeparator);
                return new Vector2(parseFloat(p, 0), parseFloat(p, 1));
            }

            if (InType == typeof(Color))
            {
                Color color;
                if (ColorUtility.TryParseHtmlString(InCell, out color) == true)
                {
                    return color;
                }

                return Color.white;
            }

            if (isSerializableStruct(InType) == true)
            {
                return toStruct(InType, InCell);
            }

            throw new NotSupportedException($"type {InType.Name} is not supported");
        }

        private static bool isSerializableStruct(Type InType)
        {
            return InType.IsValueType == true && InType.IsPrimitive == false && InType.IsEnum == false
                && InType.IsDefined(typeof(SerializableAttribute), false) == true;
        }

        /// <summary>[Serializable] struct: ';' 로 구분한 값을 public 필드 선언 순서대로 넣는다. 뒤쪽 값은 생략 가능 (기본값).</summary>
        private static object toStruct(Type InType, string InCell)
        {
            var fields = getStructFields(InType);
            var parts = InCell.Split(VectorSeparator);
            if (parts.Length > fields.Count)
            {
                throw new FormatException($"{InType.Name} has {fields.Count} fields but cell has {parts.Length} values");
            }

            object boxed = Activator.CreateInstance(InType);
            for (int i = 0; i < parts.Length; ++i)
            {
                fields[i].SetValue(boxed, ToValue(fields[i].FieldType, parts[i].Trim()));
            }

            return boxed;
        }

        private static List<FieldInfo> getStructFields(Type InType)
        {
            var result = new List<FieldInfo>();
            foreach (var field in InType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsNotSerialized == false)
                {
                    result.Add(field);
                }
            }

            // GetFields 는 순서를 보장하지 않으므로 선언 순서(MetadataToken)로 정렬한다.
            result.Sort((InA, InB) => InA.MetadataToken.CompareTo(InB.MetadataToken));
            return result;
        }

        private static object toEnum(Type InType, string InCell)
        {
            var parts = InCell.Split(ArraySeparator);
            long result = 0;
            for (int i = 0; i < parts.Length; ++i)
            {
                string part = parts[i].Trim();
                long number;
                if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) == true)
                {
                    result |= number;
                }
                else
                {
                    result |= Convert.ToInt64(Enum.Parse(InType, part, true));
                }
            }

            return Enum.ToObject(InType, result);
        }

        private static float parseFloat(string[] InParts, int InIndex)
        {
            if (InIndex >= InParts.Length)
            {
                return 0.0f;
            }

            return float.Parse(InParts[InIndex].Trim(), CultureInfo.InvariantCulture);
        }
    }
}
