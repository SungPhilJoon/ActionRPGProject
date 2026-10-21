using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Core.Lifecycle
{
    /// <summary>
    /// 게임 오브젝트/순수 C# 오브젝트의 생성-소멸 관계를 추적하는 전역 레지스트리.
    ///
    /// 목적은 딱 하나: 나중에 만들 에디터 툴(Window)이 "지금 몇 개의 발사체가 떠 있는지",
    /// "이 스킬 캐스터가 만든 오브젝트들이 전부 정리됐는지", "특정 몬스터가 스폰된 뒤
    /// 얼마나 살았는지" 같은 걸 실시간으로 그려 볼 수 있게 데이터를 남겨 두는 것이다.
    /// 게임 로직은 이 클래스에 전혀 의존하지 않는다 — 등록을 안 해도 게임은 그대로 돌아간다.
    ///
    /// 사용법:
    ///   private LifecycleHandle _handle;
    ///   void OnEnable()  => _handle = LifecycleTracker.Register(this, parentHandle: default);
    ///   void OnDisable() => _handle.Dispose();
    ///
    /// 부모-자식 매핑 예시: SkillCaster 가 발사체를 스폰하면서
    ///   var h = LifecycleTracker.Register(projectile, parent: casterHandle);
    /// 로 등록하면, 에디터 툴에서 "이 캐스터가 만든 것들" 트리를 그릴 수 있다.
    /// </summary>
    public static class LifecycleTracker
    {
        private static readonly Dictionary<int, LifecycleRecord> Records = new Dictionary<int, LifecycleRecord>(256);
        private static int _nextId = 1;

        /// <summary>true 로 켜면 기록을 남긴다. 빌드에서는 꺼서 오버헤드를 없앨 수 있다.</summary>
        public static bool Enabled = Debug.isDebugBuild || Application.isEditor;

        public static event Action<LifecycleRecord> Registered;
        public static event Action<LifecycleRecord> Disposed;

        public static LifecycleHandle Register(ITrackable trackable, LifecycleHandle parent = default)
            => Register(trackable.TrackableName, trackable.TrackableCategory, trackable, parent);

        public static LifecycleHandle Register(string name, string category, object target = null, LifecycleHandle parent = default)
        {
            if (!Enabled) return LifecycleHandle.Invalid;

            var record = new LifecycleRecord
            {
                Id = _nextId++,
                Name = name,
                Category = category,
                ParentId = parent.IsValid ? parent.Id : -1,
                State = LifecycleState.Active,
                CreatedAtRealtime = Time.realtimeSinceStartup,
                Target = target != null ? new WeakReference<object>(target) : null
            };

            Records[record.Id] = record;
            Registered?.Invoke(record);
            return new LifecycleHandle(record.Id);
        }

        internal static void Release(int id)
        {
            if (!Records.TryGetValue(id, out var record) || record.State == LifecycleState.Disposed) return;

            record.State = LifecycleState.Disposed;
            record.DisposedAtRealtime = Time.realtimeSinceStartup;
            Disposed?.Invoke(record);
        }

        /// <summary>디스포즈된 기록까지 포함해 지금까지의 전체 스냅샷. 에디터 툴 전용 — 매 프레임 호출하지 말 것.</summary>
        public static IReadOnlyCollection<LifecycleRecord> Snapshot() => Records.Values;

        public static LifecycleRecord Find(int id) => Records.TryGetValue(id, out var record) ? record : null;

        /// <summary>디스포즈된 지 오래된 기록을 정리해 메모리가 무한히 늘어나지 않게 한다.</summary>
        public static void PruneDisposedOlderThan(float seconds)
        {
            float cutoff = Time.realtimeSinceStartup - seconds;
            var toRemove = new List<int>();
            foreach (var pair in Records)
                if (pair.Value.State == LifecycleState.Disposed && pair.Value.DisposedAtRealtime < cutoff)
                    toRemove.Add(pair.Key);

            for (int i = 0; i < toRemove.Count; i++) Records.Remove(toRemove[i]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Records.Clear();
            _nextId = 1;
            Registered = null;
            Disposed = null;
        }
    }

    /// <summary>
    /// Register() 가 돌려주는 핸들. IDisposable 이라 using 블록이나 OnDisable 에서
    /// 그대로 정리하면 된다. struct 라 GC 압박이 없다.
    /// </summary>
    public readonly struct LifecycleHandle : IDisposable, IEquatable<LifecycleHandle>
    {
        public readonly int Id;
        public static readonly LifecycleHandle Invalid = new LifecycleHandle(0);

        internal LifecycleHandle(int id) => Id = id;

        public bool IsValid => Id != 0;

        public void Dispose()
        {
            if (IsValid) LifecycleTracker.Release(Id);
        }

        public bool Equals(LifecycleHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is LifecycleHandle other && Equals(other);
        public override int GetHashCode() => Id;
    }
}
