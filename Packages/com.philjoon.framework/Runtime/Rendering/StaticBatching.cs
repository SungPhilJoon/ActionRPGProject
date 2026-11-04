using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    /// <summary>
    /// 런타임에 생긴 StaticBehaviour 의 정적 배칭. 요청을 모았다가 한 번에 StaticBatchingUtility.Combine 으로 합친다
    /// (물체마다 따로 합치면 배치가 잘게 나뉘어 이득이 줄어든다).
    ///  - AutoFlush(기본): 요청이 생긴 프레임의 LateUpdate 에 합친다 (숨은 러너가 그때만 깨어난다).
    ///  - 대량 생성(월드 빌드 등) 중에는 AutoFlush = false 로 두고 끝난 뒤 Flush() 를 한 번 부르면 배치가 가장 크게 묶인다.
    ///  - 씬에 놓여 이미 정적 배칭된 렌더러(isPartOfStaticBatch)는 건너뛴다.
    ///  - 에디터 · 개발 빌드: 배칭된 StaticBehaviour 를 프레임마다 몇 개씩 돌아가며 이동 검사 (CheckMoved).
    /// </summary>
    public static class StaticBatching
    {
        /// <summary>프레임마다 이동을 검사하는 StaticBehaviour 수 (에디터 · 개발 빌드).</summary>
        public const int MoveChecksPerFrame = 32;

        public static bool AutoFlush = true;

        private static readonly List<StaticBehaviour> Pending = new List<StaticBehaviour>();
        private static readonly List<StaticBehaviour> Batched = new List<StaticBehaviour>();
        private static readonly List<GameObject> Targets = new List<GameObject>();
        private static StaticBatchRunner Runner;
        private static int CheckCursor;

        /// <summary>지금까지 런타임에 합친 GameObject 수 (진단용).</summary>
        public static int CombinedCount { get; private set; }

        public static int PendingCount
        {
            get { return Pending.Count; }
        }

        public static void Request(StaticBehaviour InBehaviour)
        {
            if (InBehaviour == null || Pending.Contains(InBehaviour) == true)
            {
                return;
            }

            Pending.Add(InBehaviour);
            wakeRunner();
        }

        public static void Unregister(StaticBehaviour InBehaviour)
        {
            Pending.Remove(InBehaviour);
            Batched.Remove(InBehaviour);
        }

        /// <summary>쌓인 요청을 지금 합친다.</summary>
        public static void Flush()
        {
            if (Pending.Count == 0)
            {
                return;
            }

            Targets.Clear();
            for (int i = 0; i < Pending.Count; ++i)
            {
                if (Pending[i] != null)
                {
                    Pending[i].CollectBatchTargets(Targets);
                }
            }

            if (Targets.Count > 0)
            {
                // 루트 null = 월드 공간 그대로 합친다 (정적 물체는 움직이지 않으므로 기준 루트가 필요 없다).
                StaticBatchingUtility.Combine(Targets.ToArray(), null);
                CombinedCount += Targets.Count;
            }

            for (int i = 0; i < Pending.Count; ++i)
            {
                var behaviour = Pending[i];
                if (behaviour == null)
                {
                    continue;
                }

                behaviour.MarkBatched();
                Batched.Add(behaviour);
            }

            Log.Message($"StaticBatching: {Pending.Count} 개 물체 · {Targets.Count} 개 렌더러 합침");
            Pending.Clear();
            Targets.Clear();
        }

        internal static void Tick()
        {
            if (AutoFlush == true)
            {
                Flush();
            }

            checkMoved();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void checkMoved()
        {
            int count = Mathf.Min(MoveChecksPerFrame, Batched.Count);
            for (int i = 0; i < count; ++i)
            {
                CheckCursor = CheckCursor >= Batched.Count ? 0 : CheckCursor;
                var behaviour = Batched[CheckCursor];
                CheckCursor += 1;
                if (behaviour != null)
                {
                    behaviour.CheckMoved();
                }
            }
        }

        private static void wakeRunner()
        {
            if (Runner == null)
            {
                var go = new GameObject("[StaticBatching]");
                go.hideFlags = HideFlags.HideInHierarchy;
                Object.DontDestroyOnLoad(go);
                Runner = go.AddComponent<StaticBatchRunner>();
            }

            Runner.enabled = true;
        }

        /// <summary>러너가 할 일이 없으면 잠든다 (에디터 · 개발 빌드는 이동 검사 때문에 배칭된 물체가 있는 동안 깨어 있다).</summary>
        internal static bool HasWork
        {
            get
            {
                bool validating = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                validating = Batched.Count > 0;
#endif
                return Pending.Count > 0 || validating == true;
            }
        }
    }
}
