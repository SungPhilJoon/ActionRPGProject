using System;
using System.Collections.Generic;
using System.IO;
using Framework.Editor;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// 의존성 지도 서비스 (툴 창 · 플레이 전 검사 · 테스트 종료 훅이 공용).
///  - Analyze: 정적 그래프 + 정적 이슈
///  - UpdateIssues: 정적 이슈 + 테스트 기록(DependencyReports/Runs) 이슈 → issues.json 상태 갱신 → Documentation/VRising/DependencyIssues.md
/// </summary>
public static class DependencyMapService
{
    public const string ReportMarkdown = "../Documentation/VRising/DependencyIssues.md";
    public const string PrePlayCheckKey = "DependencyMap.PrePlayCheck";

    public static DependencyGraph LastGraph { get; private set; }

    public static event Action OnUpdated;

    public static string ReportPath
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, ReportMarkdown)); }
    }

    public static DependencyGraph Analyze()
    {
        LastGraph = AddressablesDependencyAnalyzer.Build(true);
        return LastGraph;
    }

    /// <summary>정적 검사 + 기록을 합쳐 이슈 목록 · 보고서를 갱신한다.</summary>
    public static DependencyIssueList UpdateIssues()
    {
        var graph = LastGraph != null ? LastGraph : Analyze();
        var runs = DependencyIssueTracker.LoadRuns();
        var latest = DependencyIssueTracker.Latest(runs);
        var current = new List<DependencyIssue>(graph.Issues);
        current.AddRange(DependencyIssueTracker.RuntimeIssues(latest, graph));
        var list = DependencyIssueTracker.Update(current);
        DependencyIssueTracker.WriteMarkdown(ReportPath, "Addressables 의존성 이슈 (자동 생성)", list, graph, latest);
        OnUpdated?.Invoke();
        Debug.Log($"[Dependency] issues updated: {list.Issues.Count} (report {ReportPath})");
        return list;
    }

    public static bool PrePlayCheckEnabled
    {
        get { return EditorPrefs.GetBool(PrePlayCheckKey, true); }
        set { EditorPrefs.SetBool(PrePlayCheckKey, value); }
    }
}

/// <summary>
/// 플레이 전 검사: Play 를 누르면(편집 모드를 나가기 직전) 빠른 검사(사용처가 요구하는 주소가 모두 등록됐는지)를 돌린다.
/// 문제가 있으면 콘솔에 오류로 알리고 Play 는 그대로 진행한다 (자동 테스트가 대화상자에 막히지 않도록).
/// 끄기: 의존성 지도 창 상단 "플레이 전 자동 검사" 체크 해제.
/// </summary>
[InitializeOnLoad]
public static class DependencyPrePlayCheck
{
    static DependencyPrePlayCheck()
    {
        EditorApplication.playModeStateChanged -= onPlayModeChanged;
        EditorApplication.playModeStateChanged += onPlayModeChanged;
    }

    private static void onPlayModeChanged(PlayModeStateChange InChange)
    {
        if (InChange != PlayModeStateChange.ExitingEditMode || DependencyMapService.PrePlayCheckEnabled == false)
        {
            return;
        }

        var issues = AddressablesDependencyAnalyzer.QuickCheck();
        foreach (var item in issues)
        {
            Debug.LogError($"[의존성 · 플레이 전 검사] {item.Title} — {item.HowToFix}");
        }

        if (issues.Count > 0)
        {
            Debug.LogError($"[의존성 · 플레이 전 검사] 문제 {issues.Count}건. Game > Tools > Dependency Map 에서 확인하세요.");
        }
    }
}

/// <summary>
/// 테스트가 끝나면 새 기록이 생겼는지 보고, 생겼으면 이슈 목록 · 보고서를 자동 갱신한다 (기록 → 이슈 업데이트).
/// Play 모드 테스트는 도메인 리로드를 거치므로 시작 시각은 SessionState 에 보관한다.
/// </summary>
[InitializeOnLoad]
public static class DependencyTestWatcher
{
    private const string StartedKey = "DependencyMap.TestRunStarted";

    static DependencyTestWatcher()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RegisterCallbacks(new Callbacks());
    }

    private class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor InTestsToRun)
        {
            SessionState.SetString(StartedKey, DateTime.Now.ToBinary().ToString());
        }

        public void RunFinished(ITestResultAdaptor InResult)
        {
            string started = SessionState.GetString(StartedKey, string.Empty);
            long binary;
            if (long.TryParse(started, out binary) == false)
            {
                return;
            }

            var since = DateTime.FromBinary(binary);
            string folder = Framework.DependencyRecorder.RunsFolder;
            if (Directory.Exists(folder) == false)
            {
                return;
            }

            foreach (var file in Directory.GetFiles(folder, "*.json"))
            {
                if (File.GetLastWriteTime(file) >= since)
                {
                    EditorApplication.delayCall += () => DependencyMapService.UpdateIssues();
                    return;
                }
            }
        }

        public void TestStarted(ITestAdaptor InTest)
        {
        }

        public void TestFinished(ITestResultAdaptor InResult)
        {
        }
    }
}
