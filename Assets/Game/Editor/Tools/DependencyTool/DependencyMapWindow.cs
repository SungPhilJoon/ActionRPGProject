using System.Collections.Generic;
using System.IO;
using System.Linq;
using Framework.Editor;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// 의존성 지도 (Game > Tools > Dependency Map). 프로그래머가 아니어도 읽을 수 있게 만든 Addressables 의존성 툴.
///  - 한눈에 보기: 오류/경고/참고 · 새 이슈 · 해결된 이슈 수, 많이 의존받는 에셋, 무거운 에셋, 용어 설명
///  - 의존성 그래프: 에셋 하나를 골라 "이걸 쓰는 곳 ← 에셋 → 이게 쓰는 것" 을 그림으로, 의존 카운트 표시
///  - 이슈: 왜 문제인지 / 어떻게 고치는지 / 누가 보면 되는지, 새 · 계속 · 해결 상태와 발견일
///  - 테스트 기록: 의존성 테스트 시나리오별 로드 · 반납 · 남은 참조 · 동기 대기, 실행할 때마다의 추이
///  - 실시간: Play 중 지금 메모리에 잡혀 있는 에셋과 참조 카운트, 최근 로드/해제
/// 데이터: 정적 검사(AddressablesDependencyAnalyzer) + 테스트 기록(DependencyReports/Runs) + 이슈 이력(DependencyReports/issues.json).
/// 문서: Documentation/VRising/Dependency.md
/// </summary>
public partial class DependencyMapWindow : EditorWindow
{
    private enum ETab
    {
        Overview = 0,
        Graph = 1,
        Issues = 2,
        Runs = 3,
        Live = 4,
    }

    private static readonly string[] TabLabels = { "🏠 한눈에 보기", "🕸 의존성 그래프", "⚠ 이슈", "🧪 테스트 기록", "▶ 실시간 (Play)" };

    private static readonly Color ErrorColor = new Color(0.86f, 0.28f, 0.28f);
    private static readonly Color WarningColor = new Color(0.95f, 0.66f, 0.22f);
    private static readonly Color InfoColor = new Color(0.36f, 0.62f, 0.92f);
    private static readonly Color OkColor = new Color(0.32f, 0.72f, 0.38f);
    private static readonly Color AddressableColor = new Color(0.20f, 0.36f, 0.56f);
    private static readonly Color ImplicitColor = new Color(0.32f, 0.32f, 0.34f);
    private static readonly Color ExternalColor = new Color(0.42f, 0.28f, 0.55f);

    private ETab Tab;
    private DependencyGraph Graph;
    private DependencyIssueList Issues = new DependencyIssueList();
    private List<DependencyRunFile> Runs = new List<DependencyRunFile>();
    private List<DependencyRunFile> Latest = new List<DependencyRunFile>();
    private readonly Dictionary<string, EDependencySeverity> SeverityByAsset = new Dictionary<string, EDependencySeverity>();
    private readonly Dictionary<string, RuntimeStat> StatsByAddress = new Dictionary<string, RuntimeStat>();
    private bool ShowHelp = true;
    private Vector2 OverviewScroll;

    private GUIStyle BigNumber;
    private GUIStyle CardLabel;
    private GUIStyle Heading;
    private GUIStyle Wrap;
    private GUIStyle NodeTitle;
    private GUIStyle NodeText;

    /// <summary>테스트 기록에서 모은 주소별 실행 통계.</summary>
    private class RuntimeStat
    {
        public int Loads;
        public int Releases;
        public int Leaked;
        public float SyncMilliseconds;
        public readonly List<string> Scenarios = new List<string>();
    }

    [MenuItem("Game/Tools/Dependency Map (의존성 지도)", priority = 32)]
    [MenuItem("Window/Game/Dependency Map")]
    public static void Open()
    {
        var window = GetWindow<DependencyMapWindow>("의존성 지도");
        window.minSize = new Vector2(1100.0f, 620.0f);
    }

    /// <summary>다른 툴에서 특정 에셋을 그래프로 바로 열 때.</summary>
    public static void ShowAsset(string InPathOrAddress)
    {
        Open();
        var window = GetWindow<DependencyMapWindow>();
        window.Tab = ETab.Graph;
        window.select(InPathOrAddress);
    }

    private void OnEnable()
    {
        ShowHelp = EditorPrefs.GetBool("DependencyMap.ShowHelp", true);
        DependencyMapService.OnUpdated += reload;
        reload();
    }

    private void OnDisable()
    {
        DependencyMapService.OnUpdated -= reload;
    }

    private void reload()
    {
        Graph = DependencyMapService.LastGraph;
        Issues = DependencyIssueTracker.Load();
        Runs = DependencyIssueTracker.LoadRuns();
        Latest = DependencyIssueTracker.Latest(Runs);
        rebuildIndexes();
        Repaint();
    }

    private void rebuildIndexes()
    {
        SeverityByAsset.Clear();
        foreach (var item in Issues.Issues)
        {
            if (item.Status == EIssueStatus.Resolved)
            {
                continue;
            }

            foreach (var asset in item.Assets)
            {
                EDependencySeverity current;
                if (SeverityByAsset.TryGetValue(asset, out current) == false || item.Severity > current)
                {
                    SeverityByAsset[asset] = item.Severity;
                }
            }
        }

        StatsByAddress.Clear();
        foreach (var run in Latest)
        {
            foreach (var asset in run.Record.Assets)
            {
                RuntimeStat stat;
                if (StatsByAddress.TryGetValue(asset.Address, out stat) == false)
                {
                    stat = new RuntimeStat();
                    StatsByAddress.Add(asset.Address, stat);
                }

                stat.Loads += asset.Loads;
                stat.Releases += asset.Releases;
                if (run.Record.ExpectsCleanup == true)
                {
                    stat.Leaked += Mathf.Max(0, asset.RefCountAfter - asset.RefCountBefore);
                }

                stat.SyncMilliseconds += asset.SyncWaitMilliseconds;
                stat.Scenarios.Add(run.Record.Scenario);
            }
        }
    }

    private void ensureStyles()
    {
        if (BigNumber != null)
        {
            return;
        }

        BigNumber = new GUIStyle(EditorStyles.boldLabel) { fontSize = 26, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        CardLabel = new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = Color.white } };
        Heading = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        Wrap = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 12, richText = true };
        NodeTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, clipping = TextClipping.Clip, normal = { textColor = Color.white } };
        NodeText = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10, clipping = TextClipping.Clip, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
    }

    private void OnGUI()
    {
        ensureStyles();
        drawToolbar();
        Tab = (ETab)GUILayout.Toolbar((int)Tab, TabLabels, GUILayout.Height(30.0f));
        EditorGUILayout.Space(4.0f);
        switch (Tab)
        {
            case ETab.Overview:
                drawOverview();
                break;
            case ETab.Graph:
                drawGraphTab();
                break;
            case ETab.Issues:
                drawIssuesTab();
                break;
            case ETab.Runs:
                drawRunsTab();
                break;
            default:
                drawLiveTab();
                break;
        }
    }

    private void Update()
    {
        if (Tab == ETab.Live && EditorApplication.isPlaying == true)
        {
            Repaint();
        }
    }

    // ------------------------------------------------------------------ toolbar

    private void drawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button(new GUIContent("① 정적 검사", "Addressables 등록 상태와 에셋끼리의 참조를 분석합니다 (몇 초)."), EditorStyles.toolbarButton, GUILayout.Width(90.0f)) == true)
        {
            runStaticCheck();
        }

        if (GUILayout.Button(new GUIContent("② 의존성 테스트 실행", "의존성이 의심되는 동작(창 열기, 몬스터 스폰, 건축, 스킬)을 자동으로 플레이하며 기록합니다. 끝나면 이슈가 자동 갱신됩니다."), EditorStyles.toolbarButton, GUILayout.Width(140.0f)) == true)
        {
            runDependencyTests();
        }

        if (GUILayout.Button(new GUIContent("③ 이슈 업데이트", "정적 검사 + 테스트 기록으로 이슈 목록과 보고서를 다시 만듭니다."), EditorStyles.toolbarButton, GUILayout.Width(110.0f)) == true)
        {
            DependencyMapService.UpdateIssues();
        }

        GUILayout.Space(12.0f);
        if (GUILayout.Button("보고서 열기 (md)", EditorStyles.toolbarButton, GUILayout.Width(110.0f)) == true)
        {
            EditorUtility.OpenWithDefaultApp(DependencyMapService.ReportPath);
        }

        if (GUILayout.Button("기록 폴더", EditorStyles.toolbarButton, GUILayout.Width(70.0f)) == true)
        {
            Directory.CreateDirectory(Framework.DependencyRecorder.RunsFolder);
            EditorUtility.RevealInFinder(Framework.DependencyRecorder.RunsFolder);
        }

        GUILayout.FlexibleSpace();
        bool prePlay = GUILayout.Toggle(DependencyMapService.PrePlayCheckEnabled, new GUIContent("플레이 전 자동 검사", "Play 를 누를 때마다 빠른 검사를 돌려 문제가 있으면 콘솔에 알립니다."), EditorStyles.toolbarButton, GUILayout.Width(120.0f));
        DependencyMapService.PrePlayCheckEnabled = prePlay;
        bool help = GUILayout.Toggle(ShowHelp, "도움말", EditorStyles.toolbarButton, GUILayout.Width(55.0f));
        if (help != ShowHelp)
        {
            ShowHelp = help;
            EditorPrefs.SetBool("DependencyMap.ShowHelp", help);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void runStaticCheck()
    {
        EditorUtility.DisplayProgressBar("의존성 지도", "Addressables 의존성을 분석하는 중…", 0.5f);
        try
        {
            Graph = DependencyMapService.Analyze();
            DependencyMapService.UpdateIssues();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void runDependencyTests()
    {
        if (EditorUtility.DisplayDialog("의존성 테스트", "Play 모드에서 의존성 테스트 5개를 실행합니다 (약 40초).\n실행 중에는 Unity 창을 앞에 두세요. 끝나면 이슈가 자동으로 갱신됩니다.", "실행", "취소") == false)
        {
            return;
        }

        var api = CreateInstance<TestRunnerApi>();
        var filter = new Filter { testMode = TestMode.PlayMode, categoryNames = new[] { "Dependency" } };
        api.Execute(new ExecutionSettings(filter));
    }

    private void help(string InText)
    {
        if (ShowHelp == true)
        {
            EditorGUILayout.HelpBox(InText, MessageType.Info);
        }
    }

    // ------------------------------------------------------------------ overview

    private void drawOverview()
    {
        OverviewScroll = EditorGUILayout.BeginScrollView(OverviewScroll);
        help("이 창은 게임이 실행 중에 불러오는 에셋(Addressables)이 서로 어떻게 묶여 있는지, 그리고 문제가 있는지를 보여 줍니다.\n" +
             "순서: ① 정적 검사 (플레이 없이 파일만 분석) → ② 의존성 테스트 (게임을 자동으로 플레이하며 기록) → 이슈 탭에서 확인. ③ 은 둘을 합쳐 다시 계산합니다.");

        var open = Issues.Issues.Where(i => i.Status != EIssueStatus.Resolved).ToList();
        EditorGUILayout.BeginHorizontal();
        card("오류", open.Count(i => i.Severity == EDependencySeverity.Error), ErrorColor, "게임이 실제로 깨지는 문제");
        card("경고", open.Count(i => i.Severity == EDependencySeverity.Warning), WarningColor, "메모리 · 멈칫 · 용량 문제");
        card("참고", open.Count(i => i.Severity == EDependencySeverity.Info), InfoColor, "알아 두면 좋은 것");
        card("새 이슈", Issues.Issues.Count(i => i.Status == EIssueStatus.New), new Color(0.55f, 0.4f, 0.75f), "이번 갱신에서 처음 발견");
        card("해결됨", Issues.Issues.Count(i => i.Status == EIssueStatus.Resolved), OkColor, "예전에 있었는데 사라짐");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8.0f);
        string graphInfo = Graph != null
            ? $"정적 검사: {Graph.BuiltAt:yyyy-MM-dd HH:mm} ({Graph.BuildSeconds:0.0}초) · Addressables 에셋 {Graph.Nodes.Values.Count(n => n.IsAddressable == true)}개 · 딸려 오는 에셋 {Graph.Nodes.Values.Count(n => n.IsAddressable == false)}개"
            : "정적 검사: 아직 실행 안 함 (이 창을 열 때마다 ① 을 눌러 주세요 — 그래프 탭에 필요)";
        EditorGUILayout.LabelField(graphInfo, Wrap);
        EditorGUILayout.LabelField($"이슈 목록: {(string.IsNullOrEmpty(Issues.UpdatedAt) == true ? "없음" : Issues.UpdatedAt + " 갱신")} · 테스트 기록: 시나리오 {Latest.Count}개, 파일 {Runs.Count}개", Wrap);

        if (Graph != null)
        {
            EditorGUILayout.Space(10.0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.5f - 12.0f));
            EditorGUILayout.LabelField("가장 많이 의존받는 에셋 TOP 10", Heading);
            EditorGUILayout.LabelField("많은 곳이 기대고 있을수록, 이 에셋을 바꾸거나 지울 때 영향이 큽니다.", EditorStyles.miniLabel);
            var top = Graph.Nodes.Values.OrderByDescending(n => n.DependentCount).ThenBy(n => n.Label).Take(10).ToList();
            int max = Mathf.Max(1, top.Count > 0 ? top[0].DependentCount : 1);
            foreach (var node in top)
            {
                barRow(node, node.DependentCount, max, $"의존 {node.DependentCount}");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("딸려 오는 원본이 가장 큰 에셋 TOP 10", Heading);
            EditorGUILayout.LabelField("이 에셋을 불러오면 같이 메모리에 올라오는 텍스처 · 메시 원본 파일 크기의 합입니다.", EditorStyles.miniLabel);
            var heavy = Graph.Nodes.Values.Where(n => n.IsAddressable == true)
                .Select(n => new KeyValuePair<DependencyNode, long>(n, Graph.Nodes.Values.Where(c => c.PulledInBy.Contains(n.Path) == true).Sum(c => c.SizeBytes)))
                .OrderByDescending(p => p.Value).Take(10).ToList();
            long heaviest = heavy.Count > 0 ? System.Math.Max(1L, heavy[0].Value) : 1L;
            foreach (var pair in heavy)
            {
                barRow(pair.Key, (int)(pair.Value * 1000L / heaviest), 1000, $"{pair.Value / (1024.0f * 1024.0f):0}MB");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(12.0f);
        EditorGUILayout.LabelField("용어", Heading);
        EditorGUILayout.LabelField(
            "• <b>Addressables 에셋</b>: 게임이 이름(주소)으로 불러오는 에셋. 예) UI/Windows/HudWindow, Characters/Player\n" +
            "• <b>딸려 오는 에셋</b>: 직접 등록되지 않았지만 Addressables 에셋이 쓰고 있어서 같이 묶여 들어가는 텍스처 · 머티리얼 · 메시\n" +
            "• <b>의존 카운트</b>: 이 에셋에 기대는 것의 수 (다른 에셋 + 데이터 테이블 행). 많을수록 바꿀 때 조심\n" +
            "• <b>참조 카운트</b> (실행 중): 지금 이 에셋을 붙잡고 있는 요청 수. 0 이 되어야 메모리에서 내려간다\n" +
            "• <b>반납 안 된 참조</b>: 다 쓰고 원래 상태로 돌아왔는데 참조 카운트가 남은 것 → 메모리 누수 의심\n" +
            "• <b>멈칫 (동기 대기)</b>: 에셋이 다 불러와질 때까지 게임이 그 자리에서 기다린 시간", Wrap);
        EditorGUILayout.EndScrollView();
    }

    private void card(string InLabel, int InValue, Color InColor, string InHint)
    {
        var rect = GUILayoutUtility.GetRect(150.0f, 76.0f, GUILayout.ExpandWidth(true));
        rect.xMin += 3.0f;
        rect.xMax -= 3.0f;
        EditorGUI.DrawRect(rect, InValue > 0 ? InColor * 0.85f : new Color(0.25f, 0.25f, 0.27f));
        GUI.Label(new Rect(rect.x, rect.y + 4.0f, rect.width, 34.0f), InValue.ToString(), BigNumber);
        GUI.Label(new Rect(rect.x, rect.y + 36.0f, rect.width, 18.0f), InLabel, CardLabel);
        GUI.Label(new Rect(rect.x, rect.y + 54.0f, rect.width, 18.0f), InHint, CardLabel);
    }

    private void barRow(DependencyNode InNode, int InValue, int InMax, string InText)
    {
        var rect = GUILayoutUtility.GetRect(100.0f, 20.0f, GUILayout.ExpandWidth(true));
        float labelWidth = Mathf.Min(220.0f, rect.width * 0.45f);
        if (GUI.Button(new Rect(rect.x, rect.y, labelWidth, rect.height), new GUIContent(severityIcon(InNode) + " " + displayName(InNode), InNode.Path), EditorStyles.label) == true)
        {
            Tab = ETab.Graph;
            select(InNode.Path);
        }

        var bar = new Rect(rect.x + labelWidth + 4.0f, rect.y + 3.0f, (rect.width - labelWidth - 70.0f) * InValue / Mathf.Max(1, InMax), rect.height - 6.0f);
        EditorGUI.DrawRect(bar, InNode.IsAddressable == true ? AddressableColor * 1.4f : ImplicitColor * 1.4f);
        GUI.Label(new Rect(rect.xMax - 64.0f, rect.y, 64.0f, rect.height), InText, EditorStyles.miniLabel);
    }

    // ------------------------------------------------------------------ shared helpers

    private string severityIcon(DependencyNode InNode)
    {
        EDependencySeverity severity;
        if (tryGetSeverity(InNode, out severity) == false)
        {
            return "✔";
        }

        return severity == EDependencySeverity.Error ? "⛔" : severity == EDependencySeverity.Warning ? "⚠" : "ℹ";
    }

    private bool tryGetSeverity(DependencyNode InNode, out EDependencySeverity OutSeverity)
    {
        bool found = SeverityByAsset.TryGetValue(InNode.Path, out OutSeverity);
        EDependencySeverity byAddress;
        if (string.IsNullOrEmpty(InNode.Address) == false && SeverityByAsset.TryGetValue(InNode.Address, out byAddress) == true)
        {
            if (found == false || byAddress > OutSeverity)
            {
                OutSeverity = byAddress;
            }

            found = true;
        }

        return found;
    }

    /// <summary>목록 표시 이름: 등록 에셋은 주소(같은 파일 이름이 여러 개여도 구분됨), 딸려 오는 에셋은 이름 · 종류.</summary>
    private static string displayName(DependencyNode InNode)
    {
        return InNode.IsAddressable == true ? InNode.Address : $"{InNode.Label} · {InNode.AssetType}";
    }

    private static Color severityColor(EDependencySeverity InSeverity)
    {
        return InSeverity == EDependencySeverity.Error ? ErrorColor : InSeverity == EDependencySeverity.Warning ? WarningColor : InfoColor;
    }

    private static string sizeText(long InBytes)
    {
        if (InBytes >= 1024L * 1024L)
        {
            return $"{InBytes / (1024.0f * 1024.0f):0.0}MB";
        }

        return $"{InBytes / 1024.0f:0}KB";
    }
}
