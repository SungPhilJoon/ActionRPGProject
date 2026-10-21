using System.Collections.Generic;
using System.Text;

namespace Framework
{
    /// <summary>
    /// RFC4180 에 가까운 CSV 파서. 따옴표로 감싼 셀 안의 쉼표/줄바꿈/"" 이스케이프를 지원한다.
    /// 규칙:
    ///  - 첫 줄은 헤더(필드명).
    ///  - '#' 으로 시작하는 줄은 주석 (설명 행으로 사용).
    ///  - 헤더가 '#' 으로 시작하는 컬럼은 무시 (기획 메모 컬럼).
    /// </summary>
    public static class CsvReader
    {
        public static List<string[]> Parse(string InText)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(InText) == true)
            {
                return rows;
            }

            var cells = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;
            int length = InText.Length;

            for (int i = 0; i < length; ++i)
            {
                char c = InText[i];
                if (inQuotes == true)
                {
                    if (c == '"')
                    {
                        bool escaped = i + 1 < length && InText[i + 1] == '"';
                        if (escaped == true)
                        {
                            cell.Append('"');
                            ++i;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    cells.Add(cell.ToString());
                    cell.Length = 0;
                }
                else if (c == '\r')
                {
                    // \r\n 의 \r 은 무시한다.
                }
                else if (c == '\n')
                {
                    cells.Add(cell.ToString());
                    cell.Length = 0;
                    addRow(rows, cells);
                    cells.Clear();
                }
                else
                {
                    cell.Append(c);
                }
            }

            if (cell.Length > 0 || cells.Count > 0)
            {
                cells.Add(cell.ToString());
                addRow(rows, cells);
            }

            return rows;
        }

        private static void addRow(List<string[]> InRows, List<string> InCells)
        {
            bool empty = true;
            for (int i = 0; i < InCells.Count; ++i)
            {
                if (string.IsNullOrWhiteSpace(InCells[i]) == false)
                {
                    empty = false;
                    break;
                }
            }

            if (empty == true)
            {
                return;
            }

            bool comment = InCells[0].TrimStart().StartsWith("#") == true && InRows.Count > 0;
            if (comment == true)
            {
                return;
            }

            InRows.Add(InCells.ToArray());
        }
    }
}
