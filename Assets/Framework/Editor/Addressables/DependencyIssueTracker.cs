using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>테스트 기록 파일 1개 (경로 + 내용).</summary>
    public class DependencyRunFile
    {
        public string Path;
        public DependencyRunRecord Record;
    }

    /// <summary>
    /// 의존성 이슈 추적:
    ///  1) 테스트 기록(DependencyReports/Runs/*.json)에서 런타임 이슈를 만든다 — 로드 실패, 반납 안 된 참조(누수), 긴 동기 대기, 등록 밖 주소
    ///  2) 정적 이슈와 합쳐 이전 목록(DependencyReports/issues.json)과 비교 → 새 이슈 / 계속 / 해결됨 상태와 처음 · 마지막 발견일을 갱신
    ///  3) 사람이 읽는 보고서(Markdown)를 쓴다
    /// </summary>
    public static class DependencyIssueTracker
    {
        public const float SyncWaitWarningMilliseconds = 16.0f;

        public static string ReportsFolder
        {
            get { return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "DependencyReports")); }
        }

        public static string IssuesPath
        {
            get { return System.IO.Path.Combine(ReportsFolder, "issues.json"); }
        }

        /// <summary>기록 파일 전체 (오래된 것 → 최신 순).</summary>
        public static List<DependencyRunFile> LoadRuns()
        {
            var result = new List<DependencyRunFile>();
            string folder = DependencyRecorder.RunsFolder;
            if (Directory.Exists(folder) == false)
            {
                return result;
            }

            foreach (var file in Directory.GetFiles(folder, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                try
                {
                    var record = JsonUtility.FromJson<DependencyRunRecord>(File.ReadAllText(file));
                    if (record != null)
                    {
                        result.Add(new DependencyRunFile { Path = file, Record = record });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Dependency] cannot read {file}: {e.Message}");
                }
            }

            return result;
        }

        /// <summary>시나리오별 가장 최근 기록.</summary>
        public static List<DependencyRunFile> Latest(List<DependencyRunFile> InRuns)
        {
            var map = new Dictionary<string, DependencyRunFile>();
            foreach (var run in InRuns)
            {
                map[run.Record.Scenario] = run;
            }

            return map.Values.OrderBy(r => r.Record.Scenario, StringComparer.Ordinal).ToList();
        }

        /// <summary>최근 기록들에서 런타임 이슈를 만든다. InGraph 는 등록 밖 주소 확인용 (null 이면 생략).</summary>
        public static List<DependencyIssue> RuntimeIssues(List<DependencyRunFile> InLatest, DependencyGraph InGraph)
        {
            var issues = new List<DependencyIssue>();
            foreach (var run in InLatest)
            {
                var record = run.Record;
                foreach (var asset in record.Assets)
                {
                    string subject = asset.Address;
                    if (asset.Failed == true)
                    {
                        issues.Add(runtime("R-FAIL", EDependencySeverity.Error, record, $"로드 실패: {subject}",
                            "실행 중 이 에셋을 불러오지 못했습니다. 화면에서 해당 오브젝트 · 아이콘이 빠집니다.",
                            "정적 검사의 '등록되지 않은 주소' 이슈와 같이 보세요. 주소 오타, Addressables 미등록, 타입 불일치(Sprite/Texture) 를 확인하세요.", "기획 · 프로그래밍", subject));
                    }

                    int leaked = asset.RefCountAfter - asset.RefCountBefore;
                    if (record.ExpectsCleanup == true && leaked > 0 && asset.Loads > asset.Releases)
                    {
                        string repeat = record.Repeat > 1 ? $" ({record.Repeat}회 반복에서 +{leaked} — 반복할수록 늘어남)" : $" (+{leaked})";
                        issues.Add(runtime("R-LEAK", EDependencySeverity.Warning, record, $"반납 안 된 참조: {subject}{repeat}",
                            $"'{record.Scenario}' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.",
                            $"이 에셋을 로드한 곳({string.Join(", ", asset.Owners.Take(3))})에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.", "프로그래밍", subject));
                    }

                    if (asset.SyncWaitMilliseconds > SyncWaitWarningMilliseconds)
                    {
                        issues.Add(runtime("R-SYNC", EDependencySeverity.Warning, record, $"멈칫(동기 로드 대기 {asset.SyncWaitMilliseconds:0}ms): {subject}",
                            "게임이 이 에셋이 로드될 때까지 그 자리에서 기다렸습니다. 화면이 순간 멈춘 것처럼 보입니다.",
                            "미리 로드(프리로드)하거나 비동기 로드로 바꾸세요.", "프로그래밍", subject));
                    }

                    if (InGraph != null && InGraph.Find(subject) == null)
                    {
                        issues.Add(runtime("R-OUTSIDE", EDependencySeverity.Info, record, $"Addressables 그래프에 없는 주소를 실행 중에 요청: {subject}",
                            "정적 검사로는 보이지 않는 주소를 코드가 직접 만들어 요청했습니다.",
                            "AssetTable 에 행을 만들어 데이터로 관리하면 툴에서 추적할 수 있습니다.", "프로그래밍", subject));
                    }
                }
            }

            return issues;
        }

        /// <summary>이전 목록과 합쳐 상태(새/계속/해결)를 갱신하고 저장한다.</summary>
        public static DependencyIssueList Update(List<DependencyIssue> InCurrent)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            var previous = Load();
            var previousByKey = new Dictionary<string, DependencyIssue>();
            foreach (var item in previous.Issues)
            {
                previousByKey[item.Key] = item;
            }

            var result = new DependencyIssueList { UpdatedAt = now };
            var seen = new HashSet<string>();
            foreach (var item in InCurrent)
            {
                if (seen.Add(item.Key) == false)
                {
                    continue;
                }

                DependencyIssue old;
                if (previousByKey.TryGetValue(item.Key, out old) == true && old.Status != EIssueStatus.Resolved)
                {
                    item.Status = EIssueStatus.Ongoing;
                    item.FirstSeen = old.FirstSeen;
                }
                else
                {
                    item.Status = EIssueStatus.New;
                    item.FirstSeen = now;
                }

                item.LastSeen = now;
                result.Issues.Add(item);
            }

            foreach (var old in previous.Issues)
            {
                if (seen.Contains(old.Key) == true)
                {
                    continue;
                }

                if (old.Status != EIssueStatus.Resolved)
                {
                    old.Status = EIssueStatus.Resolved;
                    old.ResolvedAt = now;
                }

                result.Issues.Add(old);
            }

            result.Issues.Sort(compare);
            Directory.CreateDirectory(ReportsFolder);
            File.WriteAllText(IssuesPath, JsonUtility.ToJson(result, true));
            return result;
        }

        public static DependencyIssueList Load()
        {
            if (File.Exists(IssuesPath) == false)
            {
                return new DependencyIssueList();
            }

            try
            {
                return JsonUtility.FromJson<DependencyIssueList>(File.ReadAllText(IssuesPath)) ?? new DependencyIssueList();
            }
            catch (Exception)
            {
                return new DependencyIssueList();
            }
        }

        /// <summary>사람이 읽는 보고서. InTitle 아래에 요약 → 이슈(상태별) → 시나리오 기록 순.</summary>
        public static void WriteMarkdown(string InPath, string InTitle, DependencyIssueList InIssues, DependencyGraph InGraph, List<DependencyRunFile> InLatest)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"# {InTitle}");
            builder.AppendLine();
            builder.AppendLine($"> 자동 생성 문서 — 의존성 툴(Game > Tools > Dependency Map)이 덮어씁니다. 직접 고치지 마세요. 마지막 갱신 {InIssues.UpdatedAt}");
            builder.AppendLine();
            int open = InIssues.Issues.Count(i => i.Status != EIssueStatus.Resolved);
            builder.AppendLine("## 요약");
            builder.AppendLine();
            builder.AppendLine("| 항목 | 값 |");
            builder.AppendLine("|---|---|");
            builder.AppendLine($"| 열린 이슈 | {open} (오류 {count(InIssues, EDependencySeverity.Error)} · 경고 {count(InIssues, EDependencySeverity.Warning)} · 참고 {count(InIssues, EDependencySeverity.Info)}) |");
            builder.AppendLine($"| 이번에 새로 생긴 이슈 | {InIssues.Issues.Count(i => i.Status == EIssueStatus.New)} |");
            builder.AppendLine($"| 해결된 이슈 (기록) | {InIssues.Issues.Count(i => i.Status == EIssueStatus.Resolved)} |");
            if (InGraph != null)
            {
                builder.AppendLine($"| Addressables 에셋 | {InGraph.Nodes.Values.Count(n => n.IsAddressable == true)} |");
                builder.AppendLine($"| 딸려 오는 에셋 | {InGraph.Nodes.Values.Count(n => n.IsAddressable == false)} |");
            }

            builder.AppendLine($"| 테스트 시나리오 기록 | {InLatest.Count} |");
            builder.AppendLine();

            builder.AppendLine("## 이슈");
            builder.AppendLine();
            foreach (var item in InIssues.Issues)
            {
                string state = item.Status == EIssueStatus.New ? "🆕 새 이슈" : item.Status == EIssueStatus.Ongoing ? "⏳ 계속" : "✅ 해결됨";
                builder.AppendLine($"### {severityLabel(item.Severity)} {item.Title}");
                builder.AppendLine();
                builder.AppendLine($"- 상태: {state} · 처음 발견 {item.FirstSeen} · 마지막 {(item.Status == EIssueStatus.Resolved ? item.ResolvedAt : item.LastSeen)} · 출처 {item.Source} · 담당 {item.Owner} · `{item.Code}`");
                builder.AppendLine($"- 왜 문제인가: {item.Why}");
                builder.AppendLine($"- 어떻게 고치나: {item.HowToFix}");
                if (item.Assets.Count > 0)
                {
                    builder.AppendLine($"- 관련: {string.Join(", ", item.Assets.Take(8).Select(a => $"`{a}`"))}{(item.Assets.Count > 8 ? $" 외 {item.Assets.Count - 8}개" : string.Empty)}");
                }

                builder.AppendLine();
            }

            builder.AppendLine("## 테스트 시나리오 기록 (시나리오별 최신)");
            builder.AppendLine();
            builder.AppendLine("| 시나리오 | 실행 시각 | 로드 | 반납 | 남은 참조 증가 | 동기 대기(ms) | 실패 |");
            builder.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var run in InLatest)
            {
                var record = run.Record;
                int grew = record.Assets.Sum(a => Mathf.Max(0, a.RefCountAfter - a.RefCountBefore));
                builder.AppendLine($"| {record.Scenario} | {record.StartedAt} | {record.LoadEvents} | {record.ReleaseEvents} | {grew} | {record.SyncWaitMilliseconds:0} | {record.Failures.Count} |");
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(InPath));
            File.WriteAllText(InPath, builder.ToString());
        }

        public static string severityLabel(EDependencySeverity InSeverity)
        {
            switch (InSeverity)
            {
                case EDependencySeverity.Error:
                    return "⛔ 오류";
                case EDependencySeverity.Warning:
                    return "⚠️ 경고";
                default:
                    return "ℹ️ 참고";
            }
        }

        private static int count(DependencyIssueList InList, EDependencySeverity InSeverity)
        {
            return InList.Issues.Count(i => i.Severity == InSeverity && i.Status != EIssueStatus.Resolved);
        }

        private static int compare(DependencyIssue InA, DependencyIssue InB)
        {
            int resolved = (InA.Status == EIssueStatus.Resolved).CompareTo(InB.Status == EIssueStatus.Resolved);
            if (resolved != 0)
            {
                return resolved;
            }

            int severity = InB.Severity.CompareTo(InA.Severity);
            return severity != 0 ? severity : string.CompareOrdinal(InA.Title, InB.Title);
        }

        private static DependencyIssue runtime(string InCode, EDependencySeverity InSeverity, DependencyRunRecord InRecord, string InTitle, string InWhy, string InHow, string InOwner, string InSubject)
        {
            var result = AddressablesDependencyAnalyzer.issue(InCode, InSeverity, InTitle, InWhy, InHow, InOwner, InSubject);
            result.Key = InCode + ":" + InRecord.Scenario + ":" + InSubject;
            result.Source = "테스트: " + InRecord.Scenario;
            return result;
        }
    }
}
