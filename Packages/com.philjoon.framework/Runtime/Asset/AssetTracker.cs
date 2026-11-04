using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    /// <summary>AssetTracker 이벤트 종류.</summary>
    public enum EAssetEvent
    {
        LoadStart = 0,
        AddRef = 1,
        Loaded = 2,
        Failed = 3,
        Release = 4,
        Unloaded = 5,
        SyncWait = 6,
    }

    /// <summary>로드/해제 이벤트 1건.</summary>
    public struct AssetEventRecord
    {
        public EAssetEvent Kind;
        public string Address;
        public string Type;
        public string Owner;
        public string Scope;
        public int RefCount;
        public float Milliseconds;
        public int Frame;
        public float Time;
    }

    /// <summary>현재 메모리에 올라와 있는 (참조 카운트 &gt; 0) 에셋 1개.</summary>
    public class LiveAsset
    {
        public string Address;
        public string Type;
        public int RefCount;
        public int TotalLoads;
        public int TotalReleases;
        public float LoadMilliseconds;
        public float SyncWaitMilliseconds;
        public bool Failed;
        public float FirstLoadTime;
        public readonly List<string> Owners = new List<string>();
    }

    /// <summary>
    /// 런타임 Addressables 의존성 추적기 (AssetLoader 가 보고한다).
    ///  - Live: 지금 잡혀 있는 에셋과 참조 카운트, 누가 잡았는지(Owner), 동기 대기 시간
    ///  - Events: 최근 이벤트 기록 (링 버퍼)
    ///  - Scope: 테스트 · 툴이 "지금 무엇을 하는 중인지" 붙이는 라벨 (기록 파일에서 구간을 나눌 때 쓴다)
    /// 에디터 의존성 툴(실시간 탭)과 Play 모드 의존성 테스트가 읽는다. 게임 로직은 이 클래스를 쓰지 않는다.
    /// </summary>
    public static class AssetTracker
    {
        public const int MaxEvents = 4000;

        private static readonly Dictionary<long, LiveAsset> LiveMap = new Dictionary<long, LiveAsset>();
        private static readonly List<AssetEventRecord> EventBuffer = new List<AssetEventRecord>();
        private static int EventStart;
        private static long TotalEventCount;

        /// <summary>현재 구간 라벨. 테스트 시나리오가 BeginScope/EndScope 로 바꾼다.</summary>
        public static string CurrentScope { get; private set; } = string.Empty;

        /// <summary>이벤트가 추가될 때마다 (에디터 창 갱신용).</summary>
        public static event Action<AssetEventRecord> OnEvent;

        public static long EventCount
        {
            get { return TotalEventCount; }
        }

        public static IEnumerable<LiveAsset> Live
        {
            get { return LiveMap.Values; }
        }

        public static int LiveCount
        {
            get
            {
                int count = 0;
                foreach (var pair in LiveMap)
                {
                    if (pair.Value.RefCount > 0)
                    {
                        count += 1;
                    }
                }

                return count;
            }
        }

        public static void BeginScope(string InScope)
        {
            CurrentScope = InScope ?? string.Empty;
        }

        public static void EndScope()
        {
            CurrentScope = string.Empty;
        }

        /// <summary>전체 기록을 비운다 (도메인 리로드 없이 Play 를 다시 시작할 때).</summary>
        public static void Reset()
        {
            LiveMap.Clear();
            EventBuffer.Clear();
            EventStart = 0;
            TotalEventCount = 0;
            CurrentScope = string.Empty;
        }

        /// <summary>InFromEventCount 이후의 이벤트를 OutEvents 에 채운다 (버퍼에서 밀려난 이벤트는 빠진다).</summary>
        public static void CopyEventsSince(long InFromEventCount, List<AssetEventRecord> OutEvents)
        {
            long first = TotalEventCount - EventBuffer.Count;
            long from = Math.Max(InFromEventCount, first);
            for (long i = from; i < TotalEventCount; ++i)
            {
                int index = (int)((EventStart + (i - first)) % MaxEvents);
                if (EventBuffer.Count < MaxEvents)
                {
                    index = (int)(i - first);
                }

                OutEvents.Add(EventBuffer[index]);
            }
        }

        /// <summary>참조 카운트 스냅샷 (주소|타입 → 카운트). 시나리오 전후 비교용.</summary>
        public static Dictionary<string, int> SnapshotRefCounts()
        {
            var result = new Dictionary<string, int>();
            foreach (var pair in LiveMap)
            {
                result[pair.Value.Address + "|" + pair.Value.Type] = pair.Value.RefCount;
            }

            return result;
        }

        // ------------------------------------------------------------------ AssetLoader 보고

        internal static void ReportLoadStart(long InKey, string InAddress, Type InType, UnityEngine.Object InOwner)
        {
            var live = getOrAdd(InKey, InAddress, InType);
            live.RefCount = 1;
            live.TotalLoads += 1;
            live.FirstLoadTime = UnityEngine.Time.realtimeSinceStartup;
            addOwner(live, InOwner);
            push(EAssetEvent.LoadStart, live, InOwner, 0.0f);
        }

        internal static void ReportAddRef(long InKey, string InAddress, Type InType, int InRefCount, UnityEngine.Object InOwner)
        {
            var live = getOrAdd(InKey, InAddress, InType);
            live.RefCount = InRefCount;
            live.TotalLoads += 1;
            addOwner(live, InOwner);
            push(EAssetEvent.AddRef, live, InOwner, 0.0f);
        }

        internal static void ReportLoaded(long InKey, bool InSucceeded)
        {
            LiveAsset live;
            if (LiveMap.TryGetValue(InKey, out live) == false || live.LoadMilliseconds > 0.0f || live.Failed == true)
            {
                return;
            }

            live.LoadMilliseconds = Mathf.Max(0.01f, (UnityEngine.Time.realtimeSinceStartup - live.FirstLoadTime) * 1000.0f);
            live.Failed = InSucceeded == false;
            push(InSucceeded == true ? EAssetEvent.Loaded : EAssetEvent.Failed, live, null, live.LoadMilliseconds);
        }

        internal static void ReportSyncWait(long InKey, float InMilliseconds)
        {
            LiveAsset live;
            if (LiveMap.TryGetValue(InKey, out live) == false)
            {
                return;
            }

            live.SyncWaitMilliseconds += InMilliseconds;
            push(EAssetEvent.SyncWait, live, null, InMilliseconds);
        }

        internal static void ReportRelease(long InKey, int InRefCount)
        {
            LiveAsset live;
            if (LiveMap.TryGetValue(InKey, out live) == false)
            {
                return;
            }

            live.RefCount = InRefCount;
            live.TotalReleases += 1;
            push(InRefCount > 0 ? EAssetEvent.Release : EAssetEvent.Unloaded, live, null, 0.0f);
        }

        // ------------------------------------------------------------------ internal

        private static LiveAsset getOrAdd(long InKey, string InAddress, Type InType)
        {
            LiveAsset live;
            if (LiveMap.TryGetValue(InKey, out live) == false)
            {
                live = new LiveAsset { Address = InAddress, Type = InType.Name };
                LiveMap.Add(InKey, live);
            }

            return live;
        }

        private static void addOwner(LiveAsset InLive, UnityEngine.Object InOwner)
        {
            string owner = describeOwner(InOwner);
            if (InLive.Owners.Contains(owner) == false && InLive.Owners.Count < 16)
            {
                InLive.Owners.Add(owner);
            }
        }

        private static string describeOwner(UnityEngine.Object InOwner)
        {
            if (ReferenceEquals(InOwner, null) == true)
            {
                return callerName();
            }

            return InOwner == null ? "(destroyed)" : $"{InOwner.name} ({InOwner.GetType().Name})";
        }

        /// <summary>
        /// 소유자 없이 로드한 경우(LoadSync, 매니저) 호출한 코드 위치 "클래스.메서드" (툴 표시용).
        /// 에디터 · 개발 빌드에서만 스택을 읽는다 (릴리스는 비용 때문에 "(code)").
        /// </summary>
        private static string callerName()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var trace = new System.Diagnostics.StackTrace(2, false);
            for (int i = 0; i < trace.FrameCount; ++i)
            {
                var method = trace.GetFrame(i).GetMethod();
                var type = method != null ? method.DeclaringType : null;
                if (type == null || type == typeof(AssetTracker) || type == typeof(AssetLoader) || type.DeclaringType == typeof(AssetLoader)
                    || type.Assembly == typeof(object).Assembly || type.Assembly == typeof(Cysharp.Threading.Tasks.UniTask).Assembly)
                {
                    continue;
                }

                // async 메서드는 컴파일러가 만든 중첩 타입(<메서드>d__N.MoveNext) 안에서 돈다 → 바깥 타입 + 원래 메서드 이름으로 바꾼다.
                string name = method.Name;
                if (type.Name.Length > 0 && type.Name[0] == '<' && type.DeclaringType != null)
                {
                    int close = type.Name.IndexOf('>');
                    name = close > 1 ? type.Name.Substring(1, close - 1) : name;
                    type = type.DeclaringType;
                }

                return type.Name + "." + name;
            }
#endif
            return string.IsNullOrEmpty(CurrentScope) == true ? "(code)" : CurrentScope;
        }

        private static void push(EAssetEvent InKind, LiveAsset InLive, UnityEngine.Object InOwner, float InMilliseconds)
        {
            var record = new AssetEventRecord
            {
                Kind = InKind,
                Address = InLive.Address,
                Type = InLive.Type,
                Owner = describeOwner(InOwner),
                Scope = CurrentScope,
                RefCount = InLive.RefCount,
                Milliseconds = InMilliseconds,
                Frame = UnityEngine.Time.frameCount,
                Time = UnityEngine.Time.realtimeSinceStartup,
            };

            if (EventBuffer.Count < MaxEvents)
            {
                EventBuffer.Add(record);
            }
            else
            {
                EventBuffer[EventStart] = record;
                EventStart = (EventStart + 1) % MaxEvents;
            }

            TotalEventCount += 1;
            OnEvent?.Invoke(record);
        }
    }
}
