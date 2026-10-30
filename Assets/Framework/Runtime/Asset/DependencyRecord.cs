using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Framework
{
    /// <summary>시나리오 하나에서 에셋 하나가 어떻게 쓰였는지.</summary>
    [Serializable]
    public class DependencyAssetUsage
    {
        public string Address;
        public string Type;
        public int Loads;
        public int Releases;
        public int RefCountBefore;
        public int RefCountAfter;
        public float LoadMilliseconds;
        public float SyncWaitMilliseconds;
        public bool Failed;
        public List<string> Owners = new List<string>();
    }

    /// <summary>
    /// 의존성 테스트 시나리오 1회 실행 기록 (JSON, 저장소 루트 DependencyReports/Runs).
    /// 의존성 툴이 이 기록들을 읽어 이슈 목록 · 그래프의 실행 통계를 갱신한다.
    /// </summary>
    [Serializable]
    public class DependencyRunRecord
    {
        public string Scenario;
        public string Description;
        public string StartedAt;
        public float DurationSeconds;
        public int Repeat;
        /// <summary>시나리오가 끝나면 시작 전 상태로 돌아와야 하는가 (true 일 때만 남은 참조를 누수로 본다).</summary>
        public bool ExpectsCleanup;
        public int LoadEvents;
        public int ReleaseEvents;
        public int SyncWaits;
        public float SyncWaitMilliseconds;
        public int LiveBefore;
        public int LiveAfter;
        public List<string> Failures = new List<string>();
        public List<DependencyAssetUsage> Assets = new List<DependencyAssetUsage>();
    }

    /// <summary>
    /// 시나리오 기록기. Begin → (플레이) → End 로 그 구간의 AssetTracker 이벤트를 모아 DependencyRunRecord 를 만들고 저장한다.
    /// 사용: Play 모드 의존성 테스트 (Game/Tests/PlayMode/DependencyScenarioTests).
    /// </summary>
    public class DependencyRecorder
    {
        private readonly DependencyRunRecord Record = new DependencyRunRecord();
        private long StartEvent;
        private float StartTime;
        private Dictionary<string, int> Before;

        /// <summary>기록 폴더 (저장소 루트/DependencyReports/Runs). 에디터 · 개발 빌드 공통.</summary>
        public static string RunsFolder
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "DependencyReports", "Runs")); }
        }

        /// <summary>
        /// 구간 기록 시작. InExpectsCleanup = 끝나면 원래 상태로 돌아와야 하는 시나리오(창 열고 닫기, 스폰 후 제거 …).
        /// InFromStart = 게임 시작부터의 이벤트를 모두 포함 (부팅 시나리오용, 버퍼에 남은 만큼).
        /// </summary>
        public static DependencyRecorder Begin(string InScenario, string InDescription, int InRepeat, bool InExpectsCleanup, bool InFromStart = false)
        {
            var recorder = new DependencyRecorder();
            recorder.Record.Scenario = InScenario;
            recorder.Record.Description = InDescription;
            recorder.Record.Repeat = InRepeat;
            recorder.Record.ExpectsCleanup = InExpectsCleanup;
            recorder.Record.StartedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            recorder.StartEvent = InFromStart == true ? 0 : AssetTracker.EventCount;
            recorder.StartTime = Time.realtimeSinceStartup;
            recorder.Before = InFromStart == true ? new Dictionary<string, int>() : AssetTracker.SnapshotRefCounts();
            recorder.Record.LiveBefore = AssetTracker.LiveCount;
            AssetTracker.BeginScope(InScenario);
            return recorder;
        }

        /// <summary>구간을 닫고 기록을 만든다. InSave 면 JSON 으로 저장하고 경로를 OutPath 로 돌려준다.</summary>
        public DependencyRunRecord End(bool InSave, out string OutPath)
        {
            AssetTracker.EndScope();
            Record.DurationSeconds = Time.realtimeSinceStartup - StartTime;
            Record.LiveAfter = AssetTracker.LiveCount;

            var events = new List<AssetEventRecord>();
            AssetTracker.CopyEventsSince(StartEvent, events);
            var usage = new Dictionary<string, DependencyAssetUsage>();
            for (int i = 0; i < events.Count; ++i)
            {
                var e = events[i];
                string key = e.Address + "|" + e.Type;
                DependencyAssetUsage item;
                if (usage.TryGetValue(key, out item) == false)
                {
                    int before;
                    Before.TryGetValue(key, out before);
                    item = new DependencyAssetUsage { Address = e.Address, Type = e.Type, RefCountBefore = before };
                    usage.Add(key, item);
                }

                switch (e.Kind)
                {
                    case EAssetEvent.LoadStart:
                    case EAssetEvent.AddRef:
                        item.Loads += 1;
                        Record.LoadEvents += 1;
                        if (item.Owners.Contains(e.Owner) == false && item.Owners.Count < 8)
                        {
                            item.Owners.Add(e.Owner);
                        }

                        break;
                    case EAssetEvent.Loaded:
                        item.LoadMilliseconds = e.Milliseconds;
                        break;
                    case EAssetEvent.Failed:
                        item.Failed = true;
                        Record.Failures.Add(e.Address);
                        break;
                    case EAssetEvent.Release:
                    case EAssetEvent.Unloaded:
                        item.Releases += 1;
                        Record.ReleaseEvents += 1;
                        break;
                    case EAssetEvent.SyncWait:
                        item.SyncWaitMilliseconds += e.Milliseconds;
                        Record.SyncWaits += 1;
                        Record.SyncWaitMilliseconds += e.Milliseconds;
                        break;
                }

                item.RefCountAfter = e.RefCount;
            }

            Record.Assets.AddRange(usage.Values);
            Record.Assets.Sort((a, b) => string.CompareOrdinal(a.Address, b.Address));

            OutPath = null;
            if (InSave == true)
            {
                Directory.CreateDirectory(RunsFolder);
                OutPath = Path.Combine(RunsFolder, $"{DateTime.Now:yyyyMMdd_HHmmss}_{Record.Scenario}.json");
                File.WriteAllText(OutPath, JsonUtility.ToJson(Record, true));
            }

            return Record;
        }
    }
}
