using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Framework.Editor
{
    /// <summary>
    /// Addressables 정적 의존성 분석 (플레이 전 검사). 게임 타입을 모른다 — 사용처 정보는 IDependencyUsageProvider 구현체가 준다.
    ///  1) 모든 그룹의 에셋을 점으로 만든다.
    ///  2) 각 Addressables 에셋에서 AssetDatabase 의존성을 따라가며, 다른 Addressables 에셋을 만나면 멈춘다.
    ///     그 사이에 지나간(등록되지 않은) 에셋 = 번들에 "딸려 오는" 에셋 (PulledInBy 에 누가 끌고 왔는지 기록).
    ///  3) 정적 이슈: 없는 주소, 그룹 간 중복, Resources 중복, Editor 전용 의존, 깨진 참조, 쓰이지 않는 에셋, 무거운 에셋.
    /// </summary>
    public static class AddressablesDependencyAnalyzer
    {
        private const long HeavyBytes = 128L * 1024L * 1024L;

        /// <summary>전체 분석. InScanMissing = 프리팹 · 머티리얼 안의 깨진 참조까지 검사 (조금 느림).</summary>
        public static DependencyGraph Build(bool InScanMissing)
        {
            var started = DateTime.Now;
            var graph = new DependencyGraph();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                graph.Issues.Add(issue("S-NO-SETTINGS", EDependencySeverity.Error, "Addressables 설정이 없습니다",
                    "Addressables 가 초기화되지 않아 어떤 에셋도 런타임에 로드할 수 없습니다.",
                    "Window > Asset Management > Addressables > Groups 에서 설정을 만들거나 Game > Setup > 4. Addressables 를 실행하세요.", "프로그래밍", "settings"));
                return graph;
            }

            collectEntries(settings, graph);
            var roots = graph.Nodes.Values.Where(n => n.IsAddressable == true).Select(n => n.Path).ToList();
            foreach (var root in roots)
            {
                walk(graph, root, root, new HashSet<string>());
            }

            collectExternalUsers(graph);
            addStaticIssues(graph, InScanMissing);
            graph.BuiltAt = DateTime.Now;
            graph.BuildSeconds = (graph.BuiltAt - started).TotalSeconds;
            return graph;
        }

        /// <summary>플레이 직전용 빠른 검사: 사용처가 요구하는 주소가 모두 등록돼 있는지만 본다.</summary>
        public static List<DependencyIssue> QuickCheck()
        {
            var result = new List<DependencyIssue>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return result;
            }

            var addresses = new HashSet<string>();
            var entries = new List<AddressableAssetEntry>();
            foreach (var group in settings.groups)
            {
                if (group != null)
                {
                    group.GatherAllAssets(entries, true, true, false);
                }
            }

            foreach (var entry in entries)
            {
                addresses.Add(entry.address);
            }

            foreach (var pair in collectUsers())
            {
                if (addresses.Contains(pair.Key) == false)
                {
                    result.Add(missingAddressIssue(pair.Key, pair.Value));
                }
            }

            return result;
        }

        // ------------------------------------------------------------------ graph

        private static void collectEntries(AddressableAssetSettings InSettings, DependencyGraph InGraph)
        {
            var entries = new List<AddressableAssetEntry>();
            foreach (var group in InSettings.groups)
            {
                if (group == null)
                {
                    continue;
                }

                entries.Clear();
                group.GatherAllAssets(entries, true, true, false);
                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.AssetPath) == true || AssetDatabase.IsValidFolder(entry.AssetPath) == true)
                    {
                        continue;
                    }

                    var node = getOrAdd(InGraph, entry.AssetPath);
                    node.IsAddressable = true;
                    node.Address = entry.address;
                    node.Group = group.Name;
                    InGraph.PathByAddress[entry.address] = entry.AssetPath;
                }
            }
        }

        private static void walk(DependencyGraph InGraph, string InPath, string InRoot, HashSet<string> InVisited)
        {
            if (InVisited.Add(InPath) == false)
            {
                return;
            }

            var node = InGraph.Nodes[InPath];
            foreach (var dependency in AssetDatabase.GetDependencies(InPath, false))
            {
                if (dependency == InPath || isIgnored(dependency) == true)
                {
                    continue;
                }

                var child = getOrAdd(InGraph, dependency);
                if (node.DependsOn.Contains(dependency) == false)
                {
                    node.DependsOn.Add(dependency);
                }

                if (child.UsedBy.Contains(InPath) == false)
                {
                    child.UsedBy.Add(InPath);
                }

                if (child.IsAddressable == true)
                {
                    continue;
                }

                if (child.PulledInBy.Contains(InRoot) == false)
                {
                    child.PulledInBy.Add(InRoot);
                }

                walk(InGraph, dependency, InRoot, InVisited);
            }
        }

        private static bool isIgnored(string InPath)
        {
            string extension = Path.GetExtension(InPath);
            return extension == ".cs" || extension == ".dll" || extension == ".asmdef" || extension == ".asmref";
        }

        private static DependencyNode getOrAdd(DependencyGraph InGraph, string InPath)
        {
            DependencyNode node;
            if (InGraph.Nodes.TryGetValue(InPath, out node) == true)
            {
                return node;
            }

            node = new DependencyNode();
            node.Path = InPath;
            node.Label = Path.GetFileNameWithoutExtension(InPath);
            var type = AssetDatabase.GetMainAssetTypeAtPath(InPath);
            node.AssetType = type != null ? type.Name : "?";
            node.InResources = InPath.Contains("/Resources/") == true;
            var info = new FileInfo(InPath);
            node.SizeBytes = info.Exists == true ? info.Length : 0;
            InGraph.Nodes.Add(InPath, node);
            return node;
        }

        private static Dictionary<string, List<string>> collectUsers()
        {
            var users = new Dictionary<string, List<string>>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IDependencyUsageProvider>())
            {
                if (type.IsAbstract == true || type.IsInterface == true)
                {
                    continue;
                }

                try
                {
                    ((IDependencyUsageProvider)Activator.CreateInstance(type)).Collect(users);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Dependency] provider {type.Name} failed: {e.Message}");
                }
            }

            return users;
        }

        private static void collectExternalUsers(DependencyGraph InGraph)
        {
            foreach (var pair in collectUsers())
            {
                var node = InGraph.Find(pair.Key);
                if (node == null)
                {
                    InGraph.Issues.Add(missingAddressIssue(pair.Key, pair.Value));
                    continue;
                }

                foreach (var user in pair.Value)
                {
                    if (node.ExternalUsers.Contains(user) == false)
                    {
                        node.ExternalUsers.Add(user);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ static issues

        private static void addStaticIssues(DependencyGraph InGraph, bool InScanMissing)
        {
            var resourcesDuplicates = new Dictionary<string, List<string>>();
            var unused = new List<string>();
            foreach (var node in InGraph.Nodes.Values)
            {
                if (node.Path.Contains("/Editor/") == true && node.UsedBy.Count > 0)
                {
                    InGraph.Issues.Add(issue("S-EDITOR-DEP", EDependencySeverity.Error, $"Editor 전용 에셋을 게임 에셋이 참조함: {node.Label}",
                        "Editor 폴더 에셋은 빌드에 들어가지 않습니다. 번들 빌드가 실패하거나 실행 중 참조가 비게 됩니다.",
                        "참조하는 에셋(아래 목록)에서 이 참조를 빼거나, 에셋을 Editor 폴더 밖으로 옮기세요.", "프로그래밍", node.Path, node.UsedBy.ToArray()));
                }

                if (node.IsAddressable == false && node.PulledInBy.Count > 0)
                {
                    var groups = node.PulledInBy.Select(p => InGraph.Nodes[p].Group).Distinct().ToList();
                    if (groups.Count > 1)
                    {
                        InGraph.Issues.Add(issue("S-DUP-GROUP", EDependencySeverity.Warning, $"여러 그룹에 중복으로 들어가는 에셋: {node.Label}",
                            $"그룹 {string.Join(", ", groups)} 의 번들에 각각 복사됩니다. 용량이 늘고 메모리에 두 번 올라올 수 있습니다.",
                            "이 에셋을 Addressables 에 따로 등록하거나(공용 그룹), 한 그룹으로 모으세요.", "프로그래밍", node.Path, node.PulledInBy.ToArray()));
                    }

                    if (node.InResources == true)
                    {
                        string folder = node.Path.Substring(0, node.Path.IndexOf("/Resources/", StringComparison.Ordinal) + "/Resources".Length);
                        List<string> list;
                        if (resourcesDuplicates.TryGetValue(folder, out list) == false)
                        {
                            list = new List<string>();
                            resourcesDuplicates.Add(folder, list);
                        }

                        list.Add(node.Path);
                    }
                }

                if (node.IsAddressable == true && node.UsedBy.Count == 0 && node.ExternalUsers.Count == 0)
                {
                    unused.Add(node.Path);
                }
            }

            foreach (var pair in resourcesDuplicates)
            {
                InGraph.Issues.Add(issue("S-DUP-RESOURCES", EDependencySeverity.Warning, $"Resources 폴더 에셋 {pair.Value.Count}개가 Addressables 번들에도 들어감",
                    $"{pair.Key} 의 에셋은 빌드 시 Resources 에 한 벌, 그 에셋을 쓰는 번들에 한 벌 들어갑니다 (용량 · 메모리 2배).",
                    "외부 에셋 원본은 고치지 않는 규칙이므로, 이 에셋들을 Addressables 에 직접 등록하거나 복사본을 Content 쪽으로 옮기는 방안을 검토하세요.", "프로그래밍 · 아트", pair.Key, pair.Value.ToArray()));
            }

            if (unused.Count > 0)
            {
                InGraph.Issues.Add(issue("S-UNUSED", EDependencySeverity.Info, $"아무도 쓰지 않는 Addressables 에셋 {unused.Count}개",
                    "데이터 테이블에도 없고 다른 에셋도 참조하지 않습니다. 코드에서 주소를 직접 쓰는 경우일 수도 있습니다.",
                    "정말 안 쓰면 Content 폴더에서 지우고, 쓰는 것이라면 AssetTable 에 행을 추가하세요.", "기획 · 프로그래밍", "unused", unused.ToArray()));
            }

            foreach (var node in InGraph.Nodes.Values.Where(n => n.IsAddressable == true))
            {
                long pulled = InGraph.Nodes.Values.Where(n => n.PulledInBy.Contains(node.Path) == true).Sum(n => n.SizeBytes);
                if (pulled > HeavyBytes)
                {
                    InGraph.Issues.Add(issue("S-HEAVY", EDependencySeverity.Info, $"무거운 에셋: {node.Label} (딸려 오는 원본 {pulled / (1024 * 1024)}MB)",
                        "이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).",
                        "텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.", "아트", node.Path));
                }

                if (InScanMissing == true)
                {
                    scanMissing(InGraph, node);
                }
            }
        }

        /// <summary>프리팹 · ScriptableObject · 머티리얼 안의 깨진 오브젝트 참조와 사라진 스크립트를 찾는다.</summary>
        private static void scanMissing(DependencyGraph InGraph, DependencyNode InNode)
        {
            var broken = new List<string>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(InNode.Path))
            {
                if (asset == null)
                {
                    broken.Add("(사라진 스크립트 또는 하위 에셋)");
                    continue;
                }

                var go = asset as GameObject;
                if (go != null && GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
                {
                    broken.Add($"{go.name}: 사라진 스크립트");
                }

                if (asset is Component == false && asset is ScriptableObject == false && asset is Material == false)
                {
                    continue;
                }

                var serialized = new SerializedObject(asset);
                var property = serialized.GetIterator();
                while (property.NextVisible(true) == true)
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    {
                        broken.Add($"{asset.name}.{property.propertyPath}");
                    }
                }
            }

            if (broken.Count > 0)
            {
                InGraph.Issues.Add(issue("S-MISSING-REF", EDependencySeverity.Error, $"깨진 참조: {InNode.Label} ({broken.Count}곳)",
                    "지워졌거나 옮겨진 에셋을 가리키고 있습니다. 게임에서 텍스처가 분홍색이 되거나, 오브젝트가 비어 보이거나, 스크립트가 동작하지 않습니다.",
                    "에셋을 열어 비어 있는 칸(Missing)을 다시 지정하세요. 빌더가 만든 프리팹이면 해당 Build 메뉴를 다시 실행하세요.", "아트 · 프로그래밍", InNode.Path, broken.Take(20).ToArray()));
            }
        }

        private static DependencyIssue missingAddressIssue(string InAddress, List<string> InUsers)
        {
            return issue("S-ADDR-MISSING", EDependencySeverity.Error, $"등록되지 않은 주소: {InAddress}",
                $"{string.Join(", ", InUsers.Take(3))} 에서 이 주소를 로드하지만 Addressables 에 없습니다. 게임에서 해당 오브젝트가 나타나지 않습니다.",
                "Content 폴더에 에셋이 있는지 확인하고 Game > Setup > 4. Addressables 를 실행하세요. 테이블 오타라면 해당 CSV 를 고치세요.", "기획 · 프로그래밍", InAddress);
        }

        public static DependencyIssue issue(string InCode, EDependencySeverity InSeverity, string InTitle, string InWhy, string InHow, string InOwner, string InSubject, params string[] InAssets)
        {
            var result = new DependencyIssue
            {
                Key = InCode + ":" + InSubject,
                Code = InCode,
                Severity = InSeverity,
                Title = InTitle,
                Why = InWhy,
                HowToFix = InHow,
                Owner = InOwner,
                Source = "정적 검사",
            };

            result.Assets.Add(InSubject);
            foreach (var asset in InAssets)
            {
                if (result.Assets.Contains(asset) == false)
                {
                    result.Assets.Add(asset);
                }
            }

            return result;
        }
    }
}
