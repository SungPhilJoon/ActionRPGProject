using System.Collections.Generic;
using System.Linq;
using ActionRPG.Core.Lifecycle;
using UnityEditor;
using UnityEngine;

namespace ActionRPG.Core.EditorTools
{
    /// <summary>
    /// LifecycleTracker 에 쌓인 기록을 실시간으로 보여 주는 최소 뷰어.
    /// 카테고리별 개수, 부모-자식 트리, 개별 오브젝트의 생존 시간을 확인할 수 있다.
    /// 플레이 모드에서만 의미가 있다.
    /// </summary>
    public class LifecycleTrackerWindow : EditorWindow
    {
        private Vector2 _scroll;
        private string _categoryFilter = string.Empty;
        private bool _showDisposed;

        [MenuItem("ActionRPG/Lifecycle Tracker")]
        public static void Open() => GetWindow<LifecycleTrackerWindow>("Lifecycle Tracker");

        private void OnEnable() => EditorApplication.update += Repaint;
        private void OnDisable() => EditorApplication.update -= Repaint;

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서만 데이터가 쌓입니다.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _categoryFilter = EditorGUILayout.TextField("카테고리 필터", _categoryFilter);
                _showDisposed = EditorGUILayout.ToggleLeft("해제된 것도 표시", _showDisposed, GUILayout.Width(140));
            }

            var records = LifecycleTracker.Snapshot()
                .Where(r => _showDisposed || r.State != LifecycleState.Disposed)
                .Where(r => string.IsNullOrEmpty(_categoryFilter) || r.Category.IndexOf(_categoryFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(r => r.Category).ThenBy(r => r.Id)
                .ToList();

            EditorGUILayout.LabelField($"활성 {records.Count(r => r.State != LifecycleState.Disposed)} / 전체 {records.Count}");

            var byCategory = records.GroupBy(r => r.Category);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var group in byCategory)
            {
                EditorGUILayout.LabelField($"▶ {group.Key} ({group.Count()})", EditorStyles.boldLabel);
                foreach (var record in group)
                {
                    string state = record.State switch
                    {
                        LifecycleState.Active => "●",
                        LifecycleState.Disposed => "✕",
                        _ => "○"
                    };
                    string parent = record.ParentId >= 0 ? $" ← #{record.ParentId}" : string.Empty;
                    EditorGUILayout.LabelField($"  {state} #{record.Id} {record.Name}{parent}  ({record.LifetimeSeconds:0.0}s)");
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
