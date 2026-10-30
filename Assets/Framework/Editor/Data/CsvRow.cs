using System;
using System.Collections.Generic;
using System.Globalization;

namespace Framework.Editor
{
    /// <summary>
    /// 에디터 툴이 편집하는 CSV 한 행의 작업 사본. 셀은 문자열 그대로 들고 있고 컬럼 이름으로 타입별 읽기/쓰기를 한다.
    /// Save 전까지 파일에 쓰지 않으며, 값이 실제로 바뀌면 Dirty 가 된다. 파일 형식(배열 '|', 열거형 이름)은 CsvConvert 와 같다.
    /// </summary>
    public class CsvRow
    {
        public string Path { get; private set; }
        public string[] Header { get; private set; }
        public string[] Cells { get; private set; }
        public bool Dirty { get; private set; }

        public int Key
        {
            get { return GetInt(Header.Length > 0 ? Header[0] : string.Empty); }
        }

        /// <summary>키가 InKey 인 행. 없으면 null.</summary>
        public static CsvRow Load(string InPath, int InKey)
        {
            var cells = CsvEditUtility.ReadRow(InPath, InKey);
            return cells == null ? null : new CsvRow(InPath, CsvEditUtility.ReadHeader(InPath), cells, false);
        }

        /// <summary>새 행 (Save 해야 파일에 추가된다). InSource 가 있으면 그 셀을 복사하고 키만 바꾼다.</summary>
        public static CsvRow Create(string InPath, int InKey, CsvRow InSource)
        {
            var header = CsvEditUtility.ReadHeader(InPath);
            var cells = InSource != null ? (string[])InSource.Cells.Clone() : new string[header.Length];
            var row = new CsvRow(InPath, header, cells, true);
            row.Cells[0] = InKey.ToString(CultureInfo.InvariantCulture);
            return row;
        }

        private CsvRow(string InPath, string[] InHeader, string[] InCells, bool InDirty)
        {
            Path = InPath;
            Header = InHeader;
            Cells = new string[Math.Max(InHeader.Length, InCells.Length)];
            for (int i = 0; i < Cells.Length; ++i)
            {
                Cells[i] = i < InCells.Length && InCells[i] != null ? InCells[i] : string.Empty;
            }

            Dirty = InDirty;
        }

        public void Save()
        {
            CsvEditUtility.WriteRow(Path, Cells);
            Dirty = false;
        }

        // ------------------------------------------------------------------ string

        public bool Has(string InColumn)
        {
            return CsvEditUtility.ColumnOf(Header, InColumn) >= 0;
        }

        public string Get(string InColumn)
        {
            int column = CsvEditUtility.ColumnOf(Header, InColumn);
            return column >= 0 ? Cells[column] : string.Empty;
        }

        public void Set(string InColumn, string InValue)
        {
            int column = CsvEditUtility.ColumnOf(Header, InColumn);
            string value = InValue != null ? InValue : string.Empty;
            if (column < 0 || Cells[column] == value)
            {
                return;
            }

            Cells[column] = value;
            Dirty = true;
        }

        // ------------------------------------------------------------------ typed

        public int GetInt(string InColumn)
        {
            int value;
            int.TryParse(Get(InColumn), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            return value;
        }

        public void SetInt(string InColumn, int InValue)
        {
            Set(InColumn, InValue.ToString(CultureInfo.InvariantCulture));
        }

        public float GetFloat(string InColumn)
        {
            float value;
            float.TryParse(Get(InColumn), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            return value;
        }

        public void SetFloat(string InColumn, float InValue)
        {
            // 같은 값이면 원래 표기("1.0" 등)를 유지한다.
            if (Has(InColumn) == true && GetFloat(InColumn) == InValue && Get(InColumn).Length > 0)
            {
                return;
            }

            Set(InColumn, InValue.ToString(CultureInfo.InvariantCulture));
        }

        public bool GetBool(string InColumn)
        {
            string cell = Get(InColumn);
            return cell == "1" || string.Equals(cell, "true", StringComparison.OrdinalIgnoreCase) == true;
        }

        public void SetBool(string InColumn, bool InValue)
        {
            Set(InColumn, InValue == true ? "1" : "0");
        }

        public int[] GetInts(string InColumn)
        {
            var result = new List<int>();
            foreach (var part in Get(InColumn).Split(CsvConvert.ArraySeparator))
            {
                int value;
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) == true)
                {
                    result.Add(value);
                }
            }

            return result.ToArray();
        }

        public void SetInts(string InColumn, IList<int> InValues)
        {
            var parts = new string[InValues.Count];
            for (int i = 0; i < parts.Length; ++i)
            {
                parts[i] = InValues[i].ToString(CultureInfo.InvariantCulture);
            }

            Set(InColumn, string.Join(CsvConvert.ArraySeparator.ToString(), parts));
        }

        public T GetEnum<T>(string InColumn, T InFallback) where T : struct
        {
            T value;
            return Enum.TryParse(Get(InColumn), out value) == true ? value : InFallback;
        }

        public void SetEnum<T>(string InColumn, T InValue) where T : struct
        {
            Set(InColumn, InValue.ToString());
        }

        public T[] GetEnums<T>(string InColumn) where T : struct
        {
            var result = new List<T>();
            foreach (var part in Get(InColumn).Split(CsvConvert.ArraySeparator))
            {
                T value;
                if (Enum.TryParse(part.Trim(), out value) == true)
                {
                    result.Add(value);
                }
            }

            return result.ToArray();
        }

        public void SetEnums<T>(string InColumn, IList<T> InValues) where T : struct
        {
            var parts = new string[InValues.Count];
            for (int i = 0; i < parts.Length; ++i)
            {
                parts[i] = InValues[i].ToString();
            }

            Set(InColumn, string.Join(CsvConvert.ArraySeparator.ToString(), parts));
        }
    }
}
