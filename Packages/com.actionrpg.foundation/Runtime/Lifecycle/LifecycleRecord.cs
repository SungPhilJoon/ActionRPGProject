using System;

namespace ActionRPG.Core.Lifecycle
{
    public enum LifecycleState : byte
    {
        Registered = 0,
        Active = 1,
        Disposed = 2
    }

    /// <summary>
    /// <see cref="LifecycleTracker"/> 가 관리하는 오브젝트 한 개의 스냅샷.
    /// 값 타입처럼 읽기 전용으로 다뤄야 한다 — 실제 값 갱신은 트래커를 통해서만.
    /// </summary>
    public sealed class LifecycleRecord
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; }
        public string Category { get; internal set; }
        public int ParentId { get; internal set; } = -1;
        public LifecycleState State { get; internal set; }
        public float CreatedAtRealtime { get; internal set; }
        public float DisposedAtRealtime { get; internal set; }
        /// <summary>실제 오브젝트로 되짚어 가고 싶을 때 쓰는 약한 참조. Editor 툴 전용이며 게임 로직에서는 쓰지 않는다.</summary>
        public WeakReference<object> Target { get; internal set; }

        public float LifetimeSeconds => (State == LifecycleState.Disposed ? DisposedAtRealtime : UnityEngine.Time.realtimeSinceStartup) - CreatedAtRealtime;
    }
}
