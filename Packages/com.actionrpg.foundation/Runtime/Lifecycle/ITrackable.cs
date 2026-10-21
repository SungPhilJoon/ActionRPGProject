namespace ActionRPG.Core.Lifecycle
{
    /// <summary>
    /// 수명 주기를 추적하고 싶은 오브젝트가 구현한다.
    /// MonoBehaviour 든 순수 C# 오브젝트든 상관없다 — <see cref="LifecycleTracker"/> 는
    /// 실제 UnityEngine.Object 참조 없이도 카테고리/ID/부모 관계만으로 그래프를 만든다.
    /// </summary>
    public interface ITrackable
    {
        /// <summary>같은 카테고리 안에서 유일해야 하는 이름표. 디버깅용이므로 사람이 읽기 쉬운 값을 쓴다.</summary>
        string TrackableName { get; }

        /// <summary>"Player", "Enemy", "Projectile", "Skill" 처럼 굵직한 분류. 에디터 툴이 트리를 그룹핑할 때 쓴다.</summary>
        string TrackableCategory { get; }
    }
}
