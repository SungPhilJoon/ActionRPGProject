using System.Collections.Generic;
using System.Linq;
using Framework;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>이슈 · 테스트 기록 · 실시간 탭.</summary>
public partial class DependencyMapWindow
{
    private enum EIssueFilter
    {
        Open = 0,
        New = 1,
        Resolved = 2,
        All = 3,
    }

    private static readonly string[] IssueFilterLabels = { "열린 이슈", "새 이슈", "해결됨", "전체" };

    private EIssueFilter IssueFilter;
    private int SeverityMask = 7;
    private Vector2 IssueScroll;
    private readonly HashSet<string> ExpandedIssues = new HashSet<string>();
    private string SelectedScenario;
    private Vector2 RunListScroll;
    private Vector2 RunDetailScroll;
    private Vector2 LiveScroll;
    private readonly List<AssetEventRecord> LiveEvents = new List<AssetEventRecord>();

    // ------------------------------------------------------------------ issues

    private void drawIssuesTab()
    {
        help("이슈는 정적 검사와 의존성 테스트 기록에서 자동으로 만들어집니다. ③ 이슈 업데이트를 누르거나 테스트가 끝날 때마다 다시 계산되며, " +
             "지난번에 없던 것은 '새 이슈', 계속 있던 것은 '계속', 사라진 것은 '해결됨' 으로 남습니다. 각 이슈를 펼치면 왜 문제인지, 어떻게 고치는지, 누가 보면 되는지가 나옵니다.");

        EditorGUILayout.BeginHorizontal();
        IssueFilter = (EIssueFilter)GUILayout.Toolbar((int)IssueFilter, IssueFilterLabels, GUILayout.Width(360.0f));
        GUILayout.Space(16.0f);
        SeverityMask = severityToggle(SeverityMask, 4, "⛔ 오류");
        SeverityMask = severityToggle(SeverityMask, 2, "⚠ 경고");
        SeverityMask = severityToggle(SeverityMask, 1, "ℹ 참고");
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(string.IsNullOrEmpty(Issues.UpdatedAt) == true ? "아직 갱신 안 됨" : $"마지막 갱신 {Issues.UpdatedAt}", GUILayout.Width(170.0f));
        EditorGUILayout.EndHorizontal();

        var list = Issues.Issues.Where(issueVisible).ToList();
        if (list.Count == 0)
        {
            EditorGUILayout.HelpBox(Issues.Issues.Count == 0 ? "이슈 목록이 없습니다. ① 정적 검사 또는 ② 의존성 테스트를 실행하세요." : "조건에 맞는 이슈가 없습니다. ✔", MessageType.None);
            return;
        }

        IssueScroll = EditorGUILayout.BeginScrollView(IssueScroll);
        foreach (var issue in list)
        {
            drawIssue(issue);
        }

        EditorGUILayout.EndScrollView();
    }

    private static int severityToggle(int InMask, int InBit, string InLabel)
    {
        bool on = (InMask & InBit) != 0;
        bool next = GUILayout.Toggle(on, InLabel, EditorStyles.miniButton, GUILayout.Width(70.0f));
        return next == true ? (InMask | InBit) : (InMask & ~InBit);
    }

    private bool issueVisible(DependencyIssue InIssue)
    {
        int bit = InIssue.Severity == EDependencySeverity.Error ? 4 : InIssue.Severity == EDependencySeverity.Warning ? 2 : 1;
        if ((SeverityMask & bit) == 0)
        {
            return false;
        }

        switch (IssueFilter)
        {
            case EIssueFilter.Open:
                return InIssue.Status != EIssueStatus.Resolved;
            case EIssueFilter.New:
                return InIssue.Status == EIssueStatus.New;
            case EIssueFilter.Resolved:
                return InIssue.Status == EIssueStatus.Resolved;
            default:
                return true;
        }
    }

    private void drawIssue(DependencyIssue InIssue)
    {
        var rect = GUILayoutUtility.GetRect(100.0f, 26.0f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2.0f, 6.0f, rect.height - 4.0f), InIssue.Status == EIssueStatus.Resolved ? OkColor : severityColor(InIssue.Severity));
        string state = InIssue.Status == EIssueStatus.New ? "[새 이슈]" : InIssue.Status == EIssueStatus.Ongoing ? "[계속]" : "[해결됨]";
        bool expanded = ExpandedIssues.Contains(InIssue.Key);
        bool next = EditorGUI.Foldout(new Rect(rect.x + 12.0f, rect.y + 4.0f, rect.width - 220.0f, 18.0f), expanded,
            $"{DependencyIssueTracker.severityLabel(InIssue.Severity)}  {state}  {InIssue.Title}", true);
        GUI.Label(new Rect(rect.xMax - 200.0f, rect.y + 4.0f, 200.0f, 18.0f), $"담당 {InIssue.Owner}", EditorStyles.miniLabel);
        if (next != expanded)
        {
            if (next == true)
            {
                ExpandedIssues.Add(InIssue.Key);
            }
            else
            {
                ExpandedIssues.Remove(InIssue.Key);
            }
        }

        if (next == false)
        {
            return;
        }

        EditorGUI.indentLevel += 2;
        EditorGUILayout.LabelField($"<b>왜 문제인가</b>  {InIssue.Why}", Wrap);
        EditorGUILayout.LabelField($"<b>어떻게 고치나</b>  {InIssue.HowToFix}", Wrap);
        string seen = InIssue.Status == EIssueStatus.Resolved ? $"해결 {InIssue.ResolvedAt}" : $"마지막 {InIssue.LastSeen}";
        EditorGUILayout.LabelField($"출처 {InIssue.Source} · 처음 발견 {InIssue.FirstSeen} · {seen} · 코드 {InIssue.Code}", EditorStyles.miniLabel);
        foreach (var asset in InIssue.Assets.Take(12))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(asset, EditorStyles.miniLabel);
            var node = Graph != null ? Graph.Find(asset) : null;
            using (new EditorGUI.DisabledScope(node == null))
            {
                if (GUILayout.Button("그래프에서 보기", EditorStyles.miniButton, GUILayout.Width(100.0f)) == true)
                {
                    Tab = ETab.Graph;
                    select(asset);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        if (InIssue.Assets.Count > 12)
        {
            EditorGUILayout.LabelField($"… 외 {InIssue.Assets.Count - 12}개", EditorStyles.miniLabel);
        }

        EditorGUI.indentLevel -= 2;
        EditorGUILayout.Space(4.0f);
    }

    // ------------------------------------------------------------------ runs

    private void drawRunsTab()
    {
        help("② 의존성 테스트가 게임을 자동으로 플레이하면서 남긴 기록입니다. 각 시나리오는 같은 동작을 3번 반복합니다. " +
             "'남은 참조' 가 반복 횟수만큼 늘었다면 쓰고 나서 반납하지 않는 것(메모리 누수 의심)이고, 한 번만 늘었다면 처음 한 번 불러와 계속 쓰는 것입니다.");
        if (Latest.Count == 0)
        {
            EditorGUILayout.HelpBox("기록이 없습니다. 위의 ② 의존성 테스트 실행을 누르세요.", MessageType.None);
            return;
        }

        if (string.IsNullOrEmpty(SelectedScenario) == true)
        {
            SelectedScenario = Latest[0].Record.Scenario;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(330.0f));
        RunListScroll = EditorGUILayout.BeginScrollView(RunListScroll);
        foreach (var run in Latest)
        {
            var record = run.Record;
            int leaked = record.ExpectsCleanup == true ? record.Assets.Sum(a => Mathf.Max(0, a.RefCountAfter - a.RefCountBefore)) : 0;
            bool problem = record.Failures.Count > 0 || leaked > 0 || record.SyncWaitMilliseconds > DependencyIssueTracker.SyncWaitWarningMilliseconds;
            var rect = GUILayoutUtility.GetRect(310.0f, 58.0f);
            EditorGUI.DrawRect(rect, record.Scenario == SelectedScenario ? new Color(0.24f, 0.37f, 0.58f) : new Color(0.2f, 0.2f, 0.22f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5.0f, rect.height), record.Failures.Count > 0 ? ErrorColor : problem == true ? WarningColor : OkColor);
            GUI.Label(new Rect(rect.x + 10.0f, rect.y + 3.0f, rect.width - 14.0f, 18.0f), record.Scenario, EditorStyles.boldLabel);
            GUI.Label(new Rect(rect.x + 10.0f, rect.y + 20.0f, rect.width - 14.0f, 16.0f), record.StartedAt + $" · 실행 {Runs.Count(r => r.Record.Scenario == record.Scenario)}회", EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 10.0f, rect.y + 36.0f, rect.width - 14.0f, 16.0f),
                $"로드 {record.LoadEvents} · 반납 {record.ReleaseEvents} · 남은 참조 +{leaked} · 멈칫 {record.SyncWaitMilliseconds:0}ms · 실패 {record.Failures.Count}", EditorStyles.miniLabel);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none) == true)
            {
                SelectedScenario = record.Scenario;
            }

            GUILayout.Space(4.0f);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        var selected = Latest.FirstOrDefault(r => r.Record.Scenario == SelectedScenario);
        if (selected != null)
        {
            drawRunDetail(selected);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void drawRunDetail(DependencyRunFile InRun)
    {
        var record = InRun.Record;
        EditorGUILayout.LabelField(record.Scenario, Heading);
        EditorGUILayout.LabelField(record.Description, Wrap);
        EditorGUILayout.LabelField($"반복 {record.Repeat}회 · {record.DurationSeconds:0.0}초 · 메모리에 잡힌 에셋 {record.LiveBefore} → {record.LiveAfter}" + (record.ExpectsCleanup == true ? " · 끝나면 원래대로 돌아와야 하는 시나리오" : " · 계속 들고 있는 것이 정상인 시나리오"), EditorStyles.miniLabel);

        EditorGUILayout.Space(4.0f);
        EditorGUILayout.LabelField("실행할 때마다 '남은 참조' 추이 (막대가 길수록 많이 남음)", EditorStyles.boldLabel);
        var history = Runs.Where(r => r.Record.Scenario == record.Scenario).ToList();
        int maxLeak = Mathf.Max(1, history.Max(r => r.Record.Assets.Sum(a => Mathf.Max(0, a.RefCountAfter - a.RefCountBefore))));
        foreach (var run in history.Skip(Mathf.Max(0, history.Count - 8)))
        {
            int leak = run.Record.Assets.Sum(a => Mathf.Max(0, a.RefCountAfter - a.RefCountBefore));
            var rect = GUILayoutUtility.GetRect(100.0f, 16.0f, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(rect.x, rect.y, 140.0f, rect.height), run.Record.StartedAt, EditorStyles.miniLabel);
            EditorGUI.DrawRect(new Rect(rect.x + 145.0f, rect.y + 3.0f, (rect.width - 200.0f) * leak / maxLeak, rect.height - 6.0f), leak > 0 ? WarningColor : OkColor);
            GUI.Label(new Rect(rect.xMax - 50.0f, rect.y, 50.0f, rect.height), $"+{leak}", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6.0f);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("에셋 (주소)", GUILayout.Width(300.0f));
        GUILayout.Label("로드", GUILayout.Width(45.0f));
        GUILayout.Label("반납", GUILayout.Width(45.0f));
        GUILayout.Label("참조 전→후", GUILayout.Width(80.0f));
        GUILayout.Label("멈칫", GUILayout.Width(60.0f));
        GUILayout.Label("누가 불렀나");
        EditorGUILayout.EndHorizontal();

        RunDetailScroll = EditorGUILayout.BeginScrollView(RunDetailScroll);
        foreach (var asset in record.Assets.OrderByDescending(a => a.RefCountAfter - a.RefCountBefore).ThenByDescending(a => a.Loads))
        {
            int delta = asset.RefCountAfter - asset.RefCountBefore;
            bool bad = asset.Failed == true || (record.ExpectsCleanup == true && delta > 0) || asset.SyncWaitMilliseconds > DependencyIssueTracker.SyncWaitWarningMilliseconds;
            var rect = EditorGUILayout.BeginHorizontal();
            if (bad == true)
            {
                EditorGUI.DrawRect(rect, new Color(0.45f, 0.32f, 0.12f, 0.35f));
            }

            if (GUILayout.Button(new GUIContent((asset.Failed == true ? "⛔ " : bad == true ? "⚠ " : string.Empty) + asset.Address, asset.Type), EditorStyles.label, GUILayout.Width(300.0f)) == true && Graph != null)
            {
                Tab = ETab.Graph;
                select(asset.Address);
            }

            GUILayout.Label(asset.Loads.ToString(), GUILayout.Width(45.0f));
            GUILayout.Label(asset.Releases.ToString(), GUILayout.Width(45.0f));
            GUILayout.Label($"{asset.RefCountBefore}→{asset.RefCountAfter}", GUILayout.Width(80.0f));
            GUILayout.Label(asset.SyncWaitMilliseconds > 0.0f ? $"{asset.SyncWaitMilliseconds:0}ms" : "-", GUILayout.Width(60.0f));
            GUILayout.Label(string.Join(", ", asset.Owners), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ live

    private void drawLiveTab()
    {
        help("Play 중에 지금 메모리에 잡혀 있는 에셋과 참조 카운트를 보여 줍니다. 창을 열고 닫거나 몬스터를 잡아 보면서 숫자가 원래대로 돌아오는지 확인하세요. " +
             "참조가 계속 늘기만 한다면 반납이 빠진 것입니다.");
        if (EditorApplication.isPlaying == false)
        {
            EditorGUILayout.HelpBox("Play 모드에서만 보입니다.", MessageType.None);
            return;
        }

        var live = AssetTracker.Live.Where(l => l.RefCount > 0).OrderByDescending(l => l.RefCount).ThenBy(l => l.Address).ToList();
        EditorGUILayout.LabelField($"잡혀 있는 에셋 {live.Count}개 · 지금까지 이벤트 {AssetTracker.EventCount}건 · 현재 구간 '{AssetTracker.CurrentScope}'", Heading);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("에셋 (주소)", GUILayout.Width(300.0f));
        GUILayout.Label("타입", GUILayout.Width(90.0f));
        GUILayout.Label("참조", GUILayout.Width(45.0f));
        GUILayout.Label("로드/반납", GUILayout.Width(70.0f));
        GUILayout.Label("멈칫", GUILayout.Width(60.0f));
        GUILayout.Label("누가 잡았나");
        EditorGUILayout.EndHorizontal();

        LiveScroll = EditorGUILayout.BeginScrollView(LiveScroll, GUILayout.Height(position.height * 0.5f));
        foreach (var asset in live)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label((asset.Failed == true ? "⛔ " : asset.RefCount > 3 ? "⚠ " : string.Empty) + asset.Address, GUILayout.Width(300.0f));
            GUILayout.Label(asset.Type, GUILayout.Width(90.0f));
            GUILayout.Label(asset.RefCount.ToString(), GUILayout.Width(45.0f));
            GUILayout.Label($"{asset.TotalLoads}/{asset.TotalReleases}", GUILayout.Width(70.0f));
            GUILayout.Label(asset.SyncWaitMilliseconds > 0.0f ? $"{asset.SyncWaitMilliseconds:0}ms" : "-", GUILayout.Width(60.0f));
            GUILayout.Label(string.Join(", ", asset.Owners), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField("최근 이벤트", EditorStyles.boldLabel);
        LiveEvents.Clear();
        AssetTracker.CopyEventsSince(System.Math.Max(0, AssetTracker.EventCount - 25), LiveEvents);
        for (int i = LiveEvents.Count - 1; i >= 0; --i)
        {
            var e = LiveEvents[i];
            EditorGUILayout.LabelField($"[{e.Frame}] {eventLabel(e.Kind)}  {e.Address} ({e.Type})  참조 {e.RefCount}" + (e.Milliseconds > 0.0f ? $"  {e.Milliseconds:0.0}ms" : string.Empty) + $"  · {e.Owner}", EditorStyles.miniLabel);
        }
    }

    private static string eventLabel(EAssetEvent InKind)
    {
        switch (InKind)
        {
            case EAssetEvent.LoadStart:
                return "📥 로드 시작";
            case EAssetEvent.AddRef:
                return "➕ 참조 추가";
            case EAssetEvent.Loaded:
                return "✔ 로드 완료";
            case EAssetEvent.Failed:
                return "⛔ 로드 실패";
            case EAssetEvent.Release:
                return "➖ 반납";
            case EAssetEvent.Unloaded:
                return "🗑 메모리에서 내림";
            default:
                return "⏸ 멈칫(동기 대기)";
        }
    }
}
