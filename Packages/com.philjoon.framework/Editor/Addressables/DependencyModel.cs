using System;
using System.Collections.Generic;

namespace Framework.Editor
{
    public enum EDependencySeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public enum EIssueStatus
    {
        New = 0,
        Ongoing = 1,
        Resolved = 2,
    }

    /// <summary>의존성 그래프의 점 하나 = 에셋 하나 (Addressables 등록 에셋 또는 딸려 오는 에셋).</summary>
    [Serializable]
    public class DependencyNode
    {
        public string Path;
        public string Label;
        public string Address;
        public string Group;
        public string AssetType;
        public bool IsAddressable;
        public bool InResources;
        public long SizeBytes;
        /// <summary>이 에셋이 직접 참조하는 에셋 경로.</summary>
        public List<string> DependsOn = new List<string>();
        /// <summary>이 에셋을 직접 참조하는 에셋 경로.</summary>
        public List<string> UsedBy = new List<string>();
        /// <summary>에셋이 아닌 사용처 (데이터 테이블 행, 코드 등). 게임이 IDependencyUsageProvider 로 채운다.</summary>
        public List<string> ExternalUsers = new List<string>();
        /// <summary>(딸려 오는 에셋만) 이 에셋을 번들에 끌고 들어가는 Addressables 에셋 경로.</summary>
        public List<string> PulledInBy = new List<string>();

        /// <summary>의존 카운트 = 이 에셋에 기대는 것의 수 (에셋 + 외부 사용처).</summary>
        public int DependentCount
        {
            get { return UsedBy.Count + ExternalUsers.Count; }
        }
    }

    /// <summary>이슈 1건. 디자이너 · 기획자도 읽을 수 있게 제목 · 이유 · 해결 방법을 문장으로 쓴다.</summary>
    [Serializable]
    public class DependencyIssue
    {
        /// <summary>같은 문제를 실행 사이에서 같은 것으로 알아보는 키 (코드 + 대상).</summary>
        public string Key;
        public string Code;
        public EDependencySeverity Severity;
        public string Title;
        public string Why;
        public string HowToFix;
        /// <summary>누가 보면 되는지 (기획 / 아트 / 프로그래밍).</summary>
        public string Owner;
        /// <summary>정적 검사 / 테스트 시나리오 이름.</summary>
        public string Source;
        public List<string> Assets = new List<string>();
        public EIssueStatus Status;
        public string FirstSeen;
        public string LastSeen;
        public string ResolvedAt;
    }

    [Serializable]
    public class DependencyIssueList
    {
        public string UpdatedAt;
        public List<DependencyIssue> Issues = new List<DependencyIssue>();
    }

    /// <summary>
    /// 게임이 "이 주소를 누가 쓰는지" 를 알려 주는 확장점 (예: AssetTable 행, 코드 상수).
    /// Framework 는 게임 데이터를 모르므로 TypeCache 로 구현체를 찾아 호출한다 (StateTreeEditorExtension 과 같은 방식).
    /// </summary>
    public interface IDependencyUsageProvider
    {
        /// <summary>OutUsers: Addressables 주소 → 사용처 설명 목록.</summary>
        void Collect(Dictionary<string, List<string>> OutUsers);
    }

    /// <summary>분석 결과 (그래프 + 정적 이슈).</summary>
    public class DependencyGraph
    {
        public readonly Dictionary<string, DependencyNode> Nodes = new Dictionary<string, DependencyNode>();
        public readonly Dictionary<string, string> PathByAddress = new Dictionary<string, string>();
        public readonly List<DependencyIssue> Issues = new List<DependencyIssue>();
        public DateTime BuiltAt;
        public double BuildSeconds;

        public DependencyNode Find(string InPathOrAddress)
        {
            DependencyNode node;
            if (Nodes.TryGetValue(InPathOrAddress, out node) == true)
            {
                return node;
            }

            string path;
            if (PathByAddress.TryGetValue(InPathOrAddress, out path) == true && Nodes.TryGetValue(path, out node) == true)
            {
                return node;
            }

            return null;
        }
    }
}
