using System.Collections.Generic;
using System.Linq;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 의존성 그래프 탭: [목록] | [이걸 쓰는 곳 ← 선택한 에셋 → 이게 쓰는 것] | [자세히].
/// 점을 누르면 그 에셋으로 이동 (뒤로 가기 지원). 점 색 = 종류, 테두리 · 아이콘 = 이슈 심각도, "의존 n" = 의존 카운트.
/// </summary>
public partial class DependencyMapWindow
{
    private const float ListWidth = 290.0f;
    private const float DetailWidth = 320.0f;
    private const float NodeWidth = 230.0f;
    private const float NodeHeight = 46.0f;
    private const float RowGap = 10.0f;
    private const int ColumnLimit = 40;

    private string SelectedPath;
    private string Search = string.Empty;
    private bool OnlyAddressable = true;
    private bool OnlyIssues;
    private readonly Stack<string> History = new Stack<string>();
    private Vector2 ListScroll;
    private Vector2 GraphScroll;
    private Vector2 DetailScroll;

    private void select(string InPathOrAddress)
    {
        if (Graph == null)
        {
            Graph = DependencyMapService.LastGraph;
        }

        var node = Graph != null ? Graph.Find(InPathOrAddress) : null;
        if (node == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(SelectedPath) == false && SelectedPath != node.Path)
        {
            History.Push(SelectedPath);
        }

        SelectedPath = node.Path;
        GraphScroll = Vector2.zero;
        Repaint();
    }

    private void drawGraphTab()
    {
        if (Graph == null)
        {
            help("그래프를 그리려면 먼저 정적 검사가 필요합니다.");
            if (GUILayout.Button("① 정적 검사 실행", GUILayout.Height(36.0f)) == true)
            {
                runStaticCheck();
            }

            return;
        }

        help("왼쪽 목록에서 에셋을 고르세요. 가운데 그림에서 왼쪽 = 이 에셋을 쓰는 곳(다른 에셋, 데이터 테이블 행), 오른쪽 = 이 에셋이 쓰는 것. " +
             "파란 상자 = 게임이 이름으로 불러오는 에셋, 회색 = 딸려 오는 에셋, 보라 = 데이터 테이블 · 코드. 상자를 누르면 그 에셋으로 이동합니다.");

        EditorGUILayout.BeginHorizontal();
        drawNodeList();
        drawGraphCanvas();
        drawNodeDetail();
        EditorGUILayout.EndHorizontal();
    }

    private void drawNodeList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
        Search = EditorGUILayout.TextField(Search, EditorStyles.toolbarSearchField);
        EditorGUILayout.BeginHorizontal();
        OnlyAddressable = GUILayout.Toggle(OnlyAddressable, "등록 에셋만", EditorStyles.miniButtonLeft);
        OnlyIssues = GUILayout.Toggle(OnlyIssues, "문제 있는 것만", EditorStyles.miniButtonRight);
        EditorGUILayout.EndHorizontal();

        var nodes = Graph.Nodes.Values.Where(listFilter).OrderByDescending(n => n.DependentCount).ThenBy(n => n.Label).ToList();
        EditorGUILayout.LabelField($"{nodes.Count}개 · 의존 카운트 많은 순", EditorStyles.miniLabel);
        ListScroll = EditorGUILayout.BeginScrollView(ListScroll);
        foreach (var node in nodes)
        {
            bool selected = node.Path == SelectedPath;
            var rect = GUILayoutUtility.GetRect(ListWidth - 20.0f, 22.0f);
            if (selected == true)
            {
                EditorGUI.DrawRect(rect, new Color(0.24f, 0.37f, 0.58f));
            }

            if (GUI.Button(new Rect(rect.x, rect.y, rect.width - 56.0f, rect.height), new GUIContent($"{severityIcon(node)} {displayName(node)}", node.Path), EditorStyles.label) == true)
            {
                select(node.Path);
            }

            GUI.Label(new Rect(rect.xMax - 56.0f, rect.y, 56.0f, rect.height), $"의존 {node.DependentCount}", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private bool listFilter(DependencyNode InNode)
    {
        if (OnlyAddressable == true && InNode.IsAddressable == false)
        {
            return false;
        }

        EDependencySeverity severity;
        if (OnlyIssues == true && tryGetSeverity(InNode, out severity) == false)
        {
            return false;
        }

        if (string.IsNullOrEmpty(Search) == true)
        {
            return true;
        }

        return InNode.Path.IndexOf(Search, System.StringComparison.OrdinalIgnoreCase) >= 0
            || (InNode.Address != null && InNode.Address.IndexOf(Search, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void drawGraphCanvas()
    {
        EditorGUILayout.BeginVertical();
        var node = string.IsNullOrEmpty(SelectedPath) == false ? Graph.Find(SelectedPath) : null;
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(History.Count == 0))
        {
            if (GUILayout.Button("◀ 뒤로", GUILayout.Width(70.0f)) == true)
            {
                SelectedPath = History.Pop();
            }
        }

        EditorGUILayout.LabelField(node != null ? $"{node.Label}  —  이걸 쓰는 곳 {node.DependentCount} · 이게 쓰는 것 {node.DependsOn.Count}" : "왼쪽에서 에셋을 고르세요", Heading);
        EditorGUILayout.EndHorizontal();

        var area = GUILayoutUtility.GetRect(200.0f, 200.0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.16f));
        if (node == null)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        var users = new List<GraphItem>();
        foreach (var external in node.ExternalUsers)
        {
            users.Add(new GraphItem { Label = external, Sub = "데이터 · 코드", Color = ExternalColor });
        }

        foreach (var path in node.UsedBy)
        {
            users.Add(item(path));
        }

        var uses = node.DependsOn.Select(item).ToList();
        int rows = Mathf.Max(1, Mathf.Max(Mathf.Min(users.Count, ColumnLimit + 1), Mathf.Min(uses.Count, ColumnLimit + 1)));
        float contentHeight = Mathf.Max(area.height - 4.0f, rows * (NodeHeight + RowGap) + 40.0f);
        var view = new Rect(0.0f, 0.0f, area.width - 16.0f, contentHeight);
        GraphScroll = GUI.BeginScrollView(area, GraphScroll, view);

        float leftX = 16.0f;
        float rightX = view.width - NodeWidth - 16.0f;
        float centerX = (view.width - NodeWidth) * 0.5f;
        // 가운데 상자는 양쪽 열 높이의 중간에 둔다 (선이 짧고 위아래로 고르게 퍼지게).
        int span = Mathf.Max(1, Mathf.Max(Mathf.Min(users.Count, ColumnLimit), Mathf.Min(uses.Count, ColumnLimit)));
        float centerY = 28.0f + span * (NodeHeight + RowGap) * 0.5f - (NodeHeight + 12.0f) * 0.5f;
        var centerRect = new Rect(centerX, Mathf.Max(28.0f, centerY), NodeWidth, NodeHeight + 12.0f);

        GUI.Label(new Rect(leftX, 4.0f, NodeWidth, 18.0f), $"⬅ 이걸 쓰는 곳 ({users.Count})", EditorStyles.boldLabel);
        GUI.Label(new Rect(rightX, 4.0f, NodeWidth, 18.0f), $"이게 쓰는 것 ({uses.Count}) ➡", EditorStyles.boldLabel);
        drawColumn(users, leftX, centerRect, true);
        drawColumn(uses, rightX, centerRect, false);
        drawNode(centerRect, item(node.Path), true);
        GUI.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private class GraphItem
    {
        public string Path;
        public string Label;
        public string Sub;
        public Color Color;
        public bool HasSeverity;
        public EDependencySeverity Severity;
    }

    private GraphItem item(string InPath)
    {
        var node = Graph.Find(InPath);
        if (node == null)
        {
            return new GraphItem { Path = InPath, Label = InPath, Sub = "?", Color = ImplicitColor };
        }

        var result = new GraphItem
        {
            Path = node.Path,
            Label = node.Label,
            Sub = $"{node.AssetType} · 의존 {node.DependentCount}" + (node.IsAddressable == true ? $" · {node.Address}" : $" · {sizeText(node.SizeBytes)}"),
            Color = node.IsAddressable == true ? AddressableColor : ImplicitColor,
        };
        result.HasSeverity = tryGetSeverity(node, out result.Severity);
        return result;
    }

    private void drawColumn(List<GraphItem> InItems, float InX, Rect InCenter, bool InLeft)
    {
        int shown = Mathf.Min(InItems.Count, ColumnLimit);
        for (int i = 0; i < shown; ++i)
        {
            var rect = new Rect(InX, 28.0f + i * (NodeHeight + RowGap), NodeWidth, NodeHeight);
            var from = InLeft == true ? new Vector3(rect.xMax, rect.center.y) : new Vector3(InCenter.xMax, InCenter.center.y);
            var to = InLeft == true ? new Vector3(InCenter.xMin, InCenter.center.y) : new Vector3(rect.xMin, rect.center.y);
            if (Event.current.type == EventType.Repaint)
            {
                Handles.DrawBezier(from, to, from + Vector3.right * 60.0f, to + Vector3.left * 60.0f, new Color(0.7f, 0.7f, 0.75f, 0.55f), null, 2.0f);
            }

            if (drawNode(rect, InItems[i], false) == true && string.IsNullOrEmpty(InItems[i].Path) == false)
            {
                select(InItems[i].Path);
            }
        }

        if (InItems.Count > shown)
        {
            GUI.Label(new Rect(InX, 28.0f + shown * (NodeHeight + RowGap), NodeWidth, 20.0f), $"+ {InItems.Count - shown}개 더 (자세히 패널 목록 참고)", EditorStyles.miniLabel);
        }
    }

    /// <summary>상자 하나. 눌렸으면 true.</summary>
    private bool drawNode(Rect InRect, GraphItem InItem, bool InCenter)
    {
        var border = InItem.HasSeverity == true ? severityColor(InItem.Severity) : new Color(0.5f, 0.5f, 0.55f);
        float thickness = InItem.HasSeverity == true || InCenter == true ? 3.0f : 1.0f;
        EditorGUI.DrawRect(InRect, border);
        EditorGUI.DrawRect(new Rect(InRect.x + thickness, InRect.y + thickness, InRect.width - thickness * 2.0f, InRect.height - thickness * 2.0f), InCenter == true ? InItem.Color * 1.25f : InItem.Color);
        string icon = InItem.HasSeverity == false ? string.Empty : InItem.Severity == EDependencySeverity.Error ? "⛔ " : InItem.Severity == EDependencySeverity.Warning ? "⚠ " : "ℹ ";
        GUI.Label(new Rect(InRect.x + 6.0f, InRect.y + 4.0f, InRect.width - 12.0f, 18.0f), new GUIContent(icon + InItem.Label, InItem.Path ?? InItem.Label), NodeTitle);
        GUI.Label(new Rect(InRect.x + 6.0f, InRect.y + 22.0f, InRect.width - 12.0f, 16.0f), InItem.Sub, NodeText);
        if (InCenter == true)
        {
            return false;
        }

        return GUI.Button(InRect, GUIContent.none, GUIStyle.none);
    }

    private void drawNodeDetail()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(DetailWidth));
        var node = string.IsNullOrEmpty(SelectedPath) == false ? Graph.Find(SelectedPath) : null;
        if (node == null)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        DetailScroll = EditorGUILayout.BeginScrollView(DetailScroll);
        EditorGUILayout.LabelField(node.Label, Heading);
        EditorGUILayout.LabelField(node.IsAddressable == true ? "게임이 이름으로 불러오는 에셋 (Addressables)" : "딸려 오는 에셋 (직접 등록 안 됨)", EditorStyles.miniLabel);
        field("주소", node.IsAddressable == true ? node.Address : "-");
        field("그룹", node.IsAddressable == true ? node.Group : "-");
        field("종류", node.AssetType);
        field("원본 크기", sizeText(node.SizeBytes));
        field("의존 카운트", $"{node.DependentCount} (에셋 {node.UsedBy.Count} + 테이블 · 코드 {node.ExternalUsers.Count})");
        if (node.IsAddressable == false)
        {
            field("끌고 오는 등록 에셋", node.PulledInBy.Count.ToString());
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("프로젝트에서 보기") == true)
        {
            EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(node.Path));
        }

        if (GUILayout.Button("열기") == true)
        {
            AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(node.Path));
        }

        EditorGUILayout.EndHorizontal();

        RuntimeStat stat;
        EditorGUILayout.Space(6.0f);
        EditorGUILayout.LabelField("테스트 실행 기록", EditorStyles.boldLabel);
        if (node.IsAddressable == true && StatsByAddress.TryGetValue(node.Address, out stat) == true)
        {
            field("로드 / 반납", $"{stat.Loads} / {stat.Releases}");
            field("남은 참조", stat.Leaked > 0 ? $"+{stat.Leaked} ⚠" : "0 ✔");
            field("멈칫(동기 대기)", $"{stat.SyncMilliseconds:0}ms");
            field("등장 시나리오", string.Join(", ", stat.Scenarios.Distinct()));
        }
        else
        {
            EditorGUILayout.LabelField("테스트 중 로드되지 않음 (또는 딸려 오는 에셋)", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6.0f);
        EditorGUILayout.LabelField("관련 이슈", EditorStyles.boldLabel);
        bool any = false;
        foreach (var issue in Issues.Issues.Where(i => i.Status != EIssueStatus.Resolved && (i.Assets.Contains(node.Path) == true || (node.Address != null && i.Assets.Contains(node.Address) == true))))
        {
            any = true;
            EditorGUILayout.LabelField($"{DependencyIssueTracker.severityLabel(issue.Severity)} {issue.Title}", Wrap);
            EditorGUILayout.LabelField("→ " + issue.HowToFix, EditorStyles.wordWrappedMiniLabel);
        }

        if (any == false)
        {
            EditorGUILayout.LabelField("✔ 문제 없음", EditorStyles.miniLabel);
        }

        if (node.ExternalUsers.Count > 0)
        {
            EditorGUILayout.Space(6.0f);
            EditorGUILayout.LabelField("데이터 · 코드 사용처", EditorStyles.boldLabel);
            foreach (var user in node.ExternalUsers)
            {
                EditorGUILayout.LabelField("• " + user, EditorStyles.wordWrappedMiniLabel);
            }
        }

        if (node.IsAddressable == false && node.PulledInBy.Count > 0)
        {
            EditorGUILayout.Space(6.0f);
            EditorGUILayout.LabelField("이 에셋을 끌고 오는 등록 에셋", EditorStyles.boldLabel);
            foreach (var path in node.PulledInBy.Take(30))
            {
                if (GUILayout.Button(Graph.Nodes[path].Label, EditorStyles.linkLabel) == true)
                {
                    select(path);
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void field(string InLabel, string InValue)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(InLabel, GUILayout.Width(110.0f));
        EditorGUILayout.SelectableLabel(InValue ?? "-", EditorStyles.label, GUILayout.Height(18.0f));
        EditorGUILayout.EndHorizontal();
    }
}
