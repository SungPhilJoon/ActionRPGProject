using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 언리얼 FGameplayTag 에 대응한다. "State.Debuff.Stun" 처럼 점으로 구분한 계층형 이름이다.
    ///
    /// 네트워크로는 이름 대신 32비트 해시만 오간다. 해시는 이름(대소문자 무시)에서 결정적으로 계산되므로
    /// 별도의 태그 테이블을 동기화하지 않아도 모든 피어가 같은 값을 얻는다.
    /// 계층 판정(부모 태그 매칭)에는 이름이 필요하므로, 쓰는 태그는 <see cref="GameplayTagManager"/> 에 등록돼 있어야 한다.
    /// </summary>
    [Serializable]
    public struct GameplayTag : IEquatable<GameplayTag>
    {
        [SerializeField] private string tagName;
        [NonSerialized] private int _hash;

        public static readonly GameplayTag None = default;

        public string TagName => tagName ?? string.Empty;
        public bool IsValid => !string.IsNullOrEmpty(tagName) || _hash != 0;

        /// <summary>네트워크 식별자. 0 이면 빈 태그.</summary>
        public int Hash => _hash != 0 ? _hash : GameplayTagManager.ComputeHash(tagName);

        private GameplayTag(string name, int hash)
        {
            tagName = name;
            _hash = hash;
        }

        /// <summary>언리얼 RequestGameplayTag. 처음 요청되는 태그는 매니저에 등록된다.</summary>
        public static GameplayTag Request(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return None;
            name = name.Trim();
            int hash = GameplayTagManager.Register(name);
            return new GameplayTag(name, hash);
        }

        /// <summary>네트워크로 받은 해시에서 태그를 복원한다. 등록되지 않은 해시면 이름이 비어 있다.</summary>
        public static GameplayTag FromHash(int hash)
        {
            if (hash == 0) return None;
            return new GameplayTag(GameplayTagManager.GetName(hash), hash);
        }

        /// <summary>
        /// 언리얼 MatchesTag. this 가 other 와 같거나 other 의 자식이면 true.
        /// 예) "State.Debuff.Stun".MatchesTag("State.Debuff") == true
        /// </summary>
        public bool MatchesTag(GameplayTag other)
        {
            if (!other.IsValid || !IsValid) return false;
            int otherHash = other.Hash;
            int selfHash = Hash;
            if (selfHash == otherHash) return true;
            var parents = GameplayTagManager.GetParentHashes(selfHash);
            for (int i = 0; i < parents.Length; i++)
                if (parents[i] == otherHash) return true;
            return false;
        }

        public bool MatchesTagExact(GameplayTag other) => other.IsValid && Hash == other.Hash;

        /// <summary>바로 위 부모 태그. 최상위면 None.</summary>
        public GameplayTag RequestDirectParent()
        {
            string name = TagName;
            int dot = name.LastIndexOf('.');
            return dot > 0 ? Request(name.Substring(0, dot)) : None;
        }

        public bool Equals(GameplayTag other) => Hash == other.Hash;
        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);
        public override int GetHashCode() => Hash;
        public override string ToString() => IsValid ? (string.IsNullOrEmpty(tagName) ? $"#{_hash:X8}" : tagName) : "None";

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Equals(b);
        public static bool operator !=(GameplayTag a, GameplayTag b) => !a.Equals(b);
    }

    /// <summary>
    /// 언리얼 UGameplayTagsManager 의 최소 구현. 태그 이름 ↔ 해시, 부모 해시 목록을 보관한다.
    /// </summary>
    public static class GameplayTagManager
    {
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>(256);
        private static readonly Dictionary<int, int[]> Parents = new Dictionary<int, int[]>(256);
        private static readonly int[] NoParents = Array.Empty<int>();

        public static IEnumerable<string> AllTagNames => Names.Values;

        /// <summary>FNV-1a 32비트. 대소문자를 무시한다. 0 은 빈 태그용으로 예약한다.</summary>
        public static int ComputeHash(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < name.Length; i++)
                {
                    hash ^= char.ToLowerInvariant(name[i]);
                    hash *= 16777619;
                }
                int result = (int)hash;
                return result == 0 ? 1 : result;
            }
        }

        /// <summary>태그와 모든 부모 태그를 등록하고 해시를 돌려준다.</summary>
        public static int Register(string name)
        {
            int hash = ComputeHash(name);
            if (hash == 0 || Names.ContainsKey(hash)) return hash;

            Names[hash] = name;
            var parentList = new List<int>(4);
            int dot = name.LastIndexOf('.');
            while (dot > 0)
            {
                string parent = name.Substring(0, dot);
                int parentHash = ComputeHash(parent);
                parentList.Add(parentHash);
                if (!Names.ContainsKey(parentHash))
                {
                    Names[parentHash] = parent;
                    Parents[parentHash] = NoParents; // 아래 루프에서 다시 채운다
                }
                dot = parent.LastIndexOf('.');
            }
            Parents[hash] = parentList.Count > 0 ? parentList.ToArray() : NoParents;

            // 부모들의 부모 목록도 채운다.
            for (int i = 0; i < parentList.Count; i++)
            {
                int count = parentList.Count - i - 1;
                if (count <= 0) { Parents[parentList[i]] = NoParents; continue; }
                var array = new int[count];
                parentList.CopyTo(i + 1, array, 0, count);
                Parents[parentList[i]] = array;
            }
            return hash;
        }

        public static string GetName(int hash) => Names.TryGetValue(hash, out var name) ? name : string.Empty;

        public static int[] GetParentHashes(int hash) => Parents.TryGetValue(hash, out var parents) ? parents : NoParents;

        public static bool IsRegistered(int hash) => Names.ContainsKey(hash);
    }

    /// <summary>언리얼 FGameplayTagContainer. 인스펙터에서 편집할 수 있는 태그 목록.</summary>
    [Serializable]
    public class GameplayTagContainer : IEnumerable<GameplayTag>
    {
        [SerializeField] private List<GameplayTag> tags = new List<GameplayTag>();

        public static GameplayTagContainer Empty => new GameplayTagContainer();

        public GameplayTagContainer() { }

        public GameplayTagContainer(params GameplayTag[] initial)
        {
            for (int i = 0; i < initial.Length; i++) AddTag(initial[i]);
        }

        public GameplayTagContainer(GameplayTagContainer other)
        {
            if (other != null) AppendTags(other);
        }

        public int Count => tags.Count;
        public bool IsEmpty => tags.Count == 0;
        public GameplayTag this[int index] => tags[index];

        public void AddTag(GameplayTag tag)
        {
            if (!tag.IsValid || HasTagExact(tag)) return;
            tags.Add(tag);
        }

        public bool RemoveTag(GameplayTag tag)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].Hash != tag.Hash) continue;
                tags.RemoveAt(i);
                return true;
            }
            return false;
        }

        public void AppendTags(GameplayTagContainer other)
        {
            if (other == null) return;
            for (int i = 0; i < other.tags.Count; i++) AddTag(other.tags[i]);
        }

        public void Reset() => tags.Clear();

        /// <summary>컨테이너 안의 어떤 태그가 query 와 같거나 그 자식이면 true. (언리얼 HasTag)</summary>
        public bool HasTag(GameplayTag query)
        {
            for (int i = 0; i < tags.Count; i++)
                if (tags[i].MatchesTag(query)) return true;
            return false;
        }

        public bool HasTagExact(GameplayTag query)
        {
            int hash = query.Hash;
            for (int i = 0; i < tags.Count; i++)
                if (tags[i].Hash == hash) return true;
            return false;
        }

        /// <summary>other 의 태그 중 하나라도 이 컨테이너가 가지고 있으면 true. other 가 비어 있으면 false.</summary>
        public bool HasAny(GameplayTagContainer other)
        {
            if (other == null) return false;
            for (int i = 0; i < other.tags.Count; i++)
                if (HasTag(other.tags[i])) return true;
            return false;
        }

        /// <summary>other 의 태그를 모두 가지고 있으면 true. other 가 비어 있으면 true.</summary>
        public bool HasAll(GameplayTagContainer other)
        {
            if (other == null) return true;
            for (int i = 0; i < other.tags.Count; i++)
                if (!HasTag(other.tags[i])) return false;
            return true;
        }

        public bool HasAnyExact(GameplayTagContainer other)
        {
            if (other == null) return false;
            for (int i = 0; i < other.tags.Count; i++)
                if (HasTagExact(other.tags[i])) return true;
            return false;
        }

        /// <summary>인스펙터에서 직렬화된 태그를 매니저에 등록한다. 에셋 OnEnable 에서 호출한다.</summary>
        public void RegisterAll()
        {
            for (int i = 0; i < tags.Count; i++)
                if (!string.IsNullOrEmpty(tags[i].TagName)) tags[i] = GameplayTag.Request(tags[i].TagName);
        }

        public IEnumerator<GameplayTag> GetEnumerator() => tags.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => tags.GetEnumerator();

        public override string ToString() => tags.Count == 0 ? "(없음)" : string.Join(", ", tags);
    }

    /// <summary>언리얼 FGameplayTagRequirements. 반드시 있어야 할 태그와 있으면 안 되는 태그.</summary>
    [Serializable]
    public class GameplayTagRequirements
    {
        public GameplayTagContainer RequireTags = new GameplayTagContainer();
        public GameplayTagContainer IgnoreTags = new GameplayTagContainer();

        public bool IsEmpty => RequireTags.IsEmpty && IgnoreTags.IsEmpty;

        public bool RequirementsMet(GameplayTagContainer tags)
        {
            bool hasRequired = tags.HasAll(RequireTags);
            bool hasIgnored = tags.HasAny(IgnoreTags);
            return hasRequired && !hasIgnored;
        }

        public bool RequirementsMet(GameplayTagCountContainer tags)
        {
            return tags.HasAllMatchingGameplayTags(RequireTags) && !tags.HasAnyMatchingGameplayTags(IgnoreTags);
        }

        public void RegisterAll()
        {
            RequireTags.RegisterAll();
            IgnoreTags.RegisterAll();
        }
    }

    /// <summary>언리얼 EGameplayTagEventType.</summary>
    public enum GameplayTagEventType : byte
    {
        /// <summary>개수가 0 ↔ 1 이상으로 바뀔 때만.</summary>
        NewOrRemoved = 0,
        /// <summary>개수가 바뀔 때마다.</summary>
        AnyCountChange = 1
    }

    /// <summary>
    /// 언리얼 FGameplayTagCountContainer. ASC 가 소유한 태그를 "몇 개의 출처가 이 태그를 줬는가"로 센다.
    /// 자식 태그가 추가되면 부모 태그의 개수도 함께 올라가므로 "State.Debuff" 질의가 "State.Debuff.Stun" 에 반응한다.
    /// </summary>
    public sealed class GameplayTagCountContainer
    {
        /// <summary>태그 → 이벤트를 담는 객체. 언리얼 RegisterGameplayTagEvent 가 돌려주는 델리게이트에 해당한다.</summary>
        public sealed class TagEvent
        {
            /// <summary>(tag, newCount)</summary>
            public event Action<GameplayTag, int> OnEvent;
            internal void Invoke(GameplayTag tag, int count) => OnEvent?.Invoke(tag, count);
            internal bool HasListeners => OnEvent != null;
        }

        private readonly Dictionary<int, int> _explicitCounts = new Dictionary<int, int>(32);
        private readonly Dictionary<int, int> _counts = new Dictionary<int, int>(64);
        private readonly Dictionary<int, TagEvent> _newOrRemovedEvents = new Dictionary<int, TagEvent>();
        private readonly Dictionary<int, TagEvent> _anyChangeEvents = new Dictionary<int, TagEvent>();
        private readonly List<(GameplayTag Tag, int Count, bool NewOrRemoved)> _pending = new List<(GameplayTag, int, bool)>(8);

        /// <summary>어떤 태그든 0 ↔ 1 이상으로 바뀔 때. (tag, newCount)</summary>
        public event Action<GameplayTag, int> OnAnyTagNewOrRemove;

        /// <summary>어떤 태그든 개수가 바뀔 때. (tag, newCount)</summary>
        public event Action<GameplayTag, int> OnAnyTagCountChange;

        public IEnumerable<KeyValuePair<int, int>> ExplicitTagCounts => _explicitCounts;

        public bool HasMatchingGameplayTag(GameplayTag tag) => tag.IsValid && _counts.TryGetValue(tag.Hash, out int c) && c > 0;

        public bool HasTagExact(GameplayTag tag) => tag.IsValid && _explicitCounts.TryGetValue(tag.Hash, out int c) && c > 0;

        public bool HasAllMatchingGameplayTags(GameplayTagContainer container)
        {
            if (container == null) return true;
            for (int i = 0; i < container.Count; i++)
                if (!HasMatchingGameplayTag(container[i])) return false;
            return true;
        }

        public bool HasAnyMatchingGameplayTags(GameplayTagContainer container)
        {
            if (container == null) return false;
            for (int i = 0; i < container.Count; i++)
                if (HasMatchingGameplayTag(container[i])) return true;
            return false;
        }

        public int GetTagCount(GameplayTag tag) => tag.IsValid && _counts.TryGetValue(tag.Hash, out int c) ? c : 0;

        public int GetExplicitTagCount(GameplayTag tag) => tag.IsValid && _explicitCounts.TryGetValue(tag.Hash, out int c) ? c : 0;

        /// <summary>명시적으로 가진 태그 목록 (부모 확장 제외).</summary>
        public void GetExplicitTags(GameplayTagContainer outContainer)
        {
            foreach (var pair in _explicitCounts)
                if (pair.Value > 0) outContainer.AddTag(GameplayTag.FromHash(pair.Key));
        }

        public TagEvent RegisterGameplayTagEvent(GameplayTag tag, GameplayTagEventType eventType)
        {
            var map = eventType == GameplayTagEventType.NewOrRemoved ? _newOrRemovedEvents : _anyChangeEvents;
            int hash = tag.Hash;
            if (!map.TryGetValue(hash, out var evt))
            {
                evt = new TagEvent();
                map[hash] = evt;
            }
            return evt;
        }

        public void UpdateTagCount(GameplayTagContainer container, int delta)
        {
            if (container == null || delta == 0) return;
            for (int i = 0; i < container.Count; i++) UpdateTagCountInternal(container[i], delta);
            FlushEvents();
        }

        public void UpdateTagCount(GameplayTag tag, int delta)
        {
            if (!tag.IsValid || delta == 0) return;
            UpdateTagCountInternal(tag, delta);
            FlushEvents();
        }

        /// <summary>정확한 개수로 맞춘다. 클라이언트가 복제받은 개수를 반영할 때 쓴다.</summary>
        public void SetTagCount(GameplayTag tag, int newCount)
        {
            int current = GetExplicitTagCount(tag);
            if (current != newCount) UpdateTagCount(tag, newCount - current);
        }

        public void Reset()
        {
            _explicitCounts.Clear();
            _counts.Clear();
        }

        private void UpdateTagCountInternal(GameplayTag tag, int delta)
        {
            int hash = tag.Hash;
            _explicitCounts.TryGetValue(hash, out int explicitCount);
            int newExplicit = Mathf.Max(0, explicitCount + delta);
            int applied = newExplicit - explicitCount;
            if (applied == 0) return;
            if (newExplicit == 0) _explicitCounts.Remove(hash);
            else _explicitCounts[hash] = newExplicit;

            ApplyToCount(hash, applied);
            var parents = GameplayTagManager.GetParentHashes(hash);
            for (int i = 0; i < parents.Length; i++) ApplyToCount(parents[i], applied);
        }

        private void ApplyToCount(int hash, int delta)
        {
            _counts.TryGetValue(hash, out int old);
            int now = Mathf.Max(0, old + delta);
            if (now == 0) _counts.Remove(hash);
            else _counts[hash] = now;
            if (now == old) return;
            bool newOrRemoved = (old == 0) != (now == 0);
            _pending.Add((GameplayTag.FromHash(hash), now, newOrRemoved));
        }

        private void FlushEvents()
        {
            if (_pending.Count == 0) return;
            // 이벤트 핸들러가 다시 태그를 바꿀 수 있으므로 복사본으로 돈다.
            var snapshot = _pending.ToArray();
            _pending.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                var (tag, count, newOrRemoved) = snapshot[i];
                if (newOrRemoved)
                {
                    if (_newOrRemovedEvents.TryGetValue(tag.Hash, out var evt)) evt.Invoke(tag, count);
                    OnAnyTagNewOrRemove?.Invoke(tag, count);
                }
                if (_anyChangeEvents.TryGetValue(tag.Hash, out var any)) any.Invoke(tag, count);
                OnAnyTagCountChange?.Invoke(tag, count);
            }
        }
    }
}
