using System.Collections.Generic;
using System.Text;

namespace ActionRPG.Core.DataDriven
{
    /// <summary>
    /// 최소한의 RFC4180 스타일 CSV 파서. 따옴표로 감싼 필드 안의 쉼표/줄바꿈/이스케이프된
    /// 큰따옴표("")를 올바르게 처리한다. 외부 라이브러리 의존 없이 순수 C# 이라
    /// 에디터 임포트뿐 아니라 런타임에 TextAsset 을 직접 읽을 때도 쓸 수 있다.
    /// </summary>
    public static class CsvParser
    {
        /// <summary>첫 줄을 헤더로 보고 (헤더 배열, 데이터 행 목록) 을 돌려준다.</summary>
        public static (string[] Headers, List<string[]> Rows) Parse(string csvText)
        {
            var records = ParseRecords(csvText);
            if (records.Count == 0) return (System.Array.Empty<string>(), new List<string[]>());

            var headers = records[0];
            var rows = records.GetRange(1, records.Count - 1);

            // '#' 로 시작하는 첫 컬럼은 주석/비활성 행으로 취급해 건너뛴다.
            rows.RemoveAll(row => row.Length > 0 && row[0].StartsWith("#"));

            return (headers, rows);
        }

        private static List<string[]> ParseRecords(string text)
        {
            var records = new List<string[]>();
            if (string.IsNullOrEmpty(text)) return records;

            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            int i = 0;
            int length = text.Length;

            void EndField()
            {
                fields.Add(current.ToString());
                current.Clear();
            }

            void EndRecord()
            {
                EndField();
                records.Add(fields.ToArray());
                fields.Clear();
            }

            while (i < length)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < length && text[i + 1] == '"') { current.Append('"'); i += 2; continue; }
                        inQuotes = false;
                        i++;
                        continue;
                    }
                    current.Append(c);
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        i++;
                        break;
                    case ',':
                        EndField();
                        i++;
                        break;
                    case '\r':
                        i++;
                        break;
                    case '\n':
                        EndRecord();
                        i++;
                        break;
                    default:
                        current.Append(c);
                        i++;
                        break;
                }
            }

            // 파일이 개행으로 끝나지 않아도 마지막 레코드를 놓치지 않는다.
            if (current.Length > 0 || fields.Count > 0) EndRecord();

            // 완전히 빈 줄(빈 필드 하나)은 제거한다.
            records.RemoveAll(r => r.Length == 1 && r[0].Length == 0);
            return records;
        }
    }
}
