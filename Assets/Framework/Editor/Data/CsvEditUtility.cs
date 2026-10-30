using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;

namespace Framework.Editor
{
    /// <summary>
    /// 에디터 툴이 CSV 원본을 한 행 단위로 읽고 고치는 유틸. 첫 컬럼을 키(Index)로 본다.
    /// 다른 행·주석 행·줄바꿈 형식은 그대로 두고 대상 행 한 줄만 바꾸거나 끝에 추가한다
    /// (셀 안에 줄바꿈이 있는 CSV 는 대상이 아니다 — 이 프로젝트 테이블은 모두 한 행 = 한 줄).
    /// 저장 후 ImportAsset 하면 TableImportPostprocessor 가 TableAsset 을 다시 변환한다.
    /// </summary>
    public static class CsvEditUtility
    {
        public static string[] ReadHeader(string InPath)
        {
            var lines = readLines(InPath);
            return lines.Count > 0 ? parseLine(lines[0]) : new string[0];
        }

        public static int ColumnOf(string[] InHeader, string InName)
        {
            for (int i = 0; i < InHeader.Length; ++i)
            {
                if (InHeader[i] == InName)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>키가 InKey 인 행의 셀들. 없으면 null.</summary>
        public static string[] ReadRow(string InPath, int InKey)
        {
            var lines = readLines(InPath);
            int index = findLine(lines, InKey);
            return index < 0 ? null : parseLine(lines[index]);
        }

        /// <summary>InCells[0] 을 키로 같은 행이 있으면 교체, 없으면 파일 끝에 추가하고 다시 임포트한다.</summary>
        public static void WriteRow(string InPath, string[] InCells)
        {
            var lines = readLines(InPath);
            int key;
            if (InCells.Length == 0 || int.TryParse(InCells[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out key) == false)
            {
                Log.Error($"CsvEditUtility.WriteRow: first cell must be an int key ({InPath})");
                return;
            }

            string line = toLine(InCells);
            int index = findLine(lines, key);
            if (index < 0)
            {
                lines.Add(line);
            }
            else
            {
                lines[index] = line;
            }

            // 원본의 줄바꿈 형식과 BOM 유무를 그대로 유지한다.
            var bytes = File.ReadAllBytes(InPath);
            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string newline = Encoding.UTF8.GetString(bytes).Contains("\r\n") == true ? "\r\n" : "\n";
            File.WriteAllText(InPath, string.Join(newline, lines) + newline, new UTF8Encoding(hasBom));
            AssetDatabase.ImportAsset(InPath);
        }

        /// <summary>InMin 이상에서 아직 쓰이지 않은 가장 작은 키.</summary>
        public static int NextKey(string InPath, int InMin)
        {
            var used = new HashSet<int>();
            var lines = readLines(InPath);
            for (int i = 1; i < lines.Count; ++i)
            {
                int key;
                if (tryKey(lines[i], out key) == true)
                {
                    used.Add(key);
                }
            }

            int next = InMin;
            while (used.Contains(next) == true)
            {
                ++next;
            }

            return next;
        }

        // ------------------------------------------------------------------ internal

        private static List<string> readLines(string InPath)
        {
            var lines = new List<string>();
            if (File.Exists(InPath) == false)
            {
                return lines;
            }

            string text = File.ReadAllText(InPath).Replace("\r\n", "\n");
            lines.AddRange(text.Split('\n'));
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            return lines;
        }

        private static int findLine(List<string> InLines, int InKey)
        {
            for (int i = 1; i < InLines.Count; ++i)
            {
                int key;
                if (tryKey(InLines[i], out key) == true && key == InKey)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool tryKey(string InLine, out int OutKey)
        {
            int comma = InLine.IndexOf(',');
            string first = comma < 0 ? InLine : InLine.Substring(0, comma);
            return int.TryParse(first.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out OutKey);
        }

        private static string[] parseLine(string InLine)
        {
            var rows = CsvReader.Parse(InLine);
            return rows.Count > 0 ? rows[0] : new string[0];
        }

        private static string toLine(string[] InCells)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < InCells.Length; ++i)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                string cell = InCells[i] != null ? InCells[i] : string.Empty;
                bool quote = cell.IndexOf(',') >= 0 || cell.IndexOf('"') >= 0;
                if (quote == true)
                {
                    builder.Append('"').Append(cell.Replace("\"", "\"\"")).Append('"');
                }
                else
                {
                    builder.Append(cell);
                }
            }

            return builder.ToString();
        }
    }
}
