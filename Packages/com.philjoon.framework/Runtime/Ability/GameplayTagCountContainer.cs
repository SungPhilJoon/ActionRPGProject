using System;

namespace Framework.Ability
{
    /// <summary>
    /// 언리얼 FGameplayTagCountContainer 대응 — 액터가 "지금 가진" 태그 (참조 카운트).
    /// 여러 이펙트가 같은 태그를 부여해도 마지막 이펙트가 빠질 때까지 태그가 유지된다.
    ///  - ExplicitCounts / ExplicitBits : 직접 부여된 태그 (언리얼 ExplicitTagCountMap)
    ///  - TagCounts / TagBitsMatched    : 부여된 태그 + 부모 (언리얼 GameplayTagCountMap)
    /// 비교 규칙은 GameplayTagContainer 와 같다: 이쪽(가진 쪽)은 부모 포함, 질의 쪽은 명시 태그만.
    /// </summary>
    public class GameplayTagCountContainer
    {
        private int[] ExplicitCounts = new int[0];
        private int[] TagCounts = new int[0];
        private ulong[] Explicit;
        private ulong[] Matched;

        /// <summary>(태그, 새 카운트). 부모 포함 카운트가 0↔1 로 바뀔 때만 호출된다 (언리얼 NewOrRemoved).</summary>
        public event Action<int, int> OnTagChanged;

        /// <summary>언리얼 HasMatchingGameplayTag — InTag 이거나 그 자식을 가지고 있는가.</summary>
        public bool HasTag(int InTag)
        {
            return TagBits.Has(Matched, InTag);
        }

        /// <summary>InTag 를 명시적으로 가지고 있는가 (계층 무시).</summary>
        public bool HasTagExact(int InTag)
        {
            return TagBits.Has(Explicit, InTag);
        }

        /// <summary>InQuery 의 태그 중 하나라도 매칭되는가 (계층 적용). InQuery 가 비어 있으면 false.</summary>
        public bool HasAny(GameplayTagContainer InQuery)
        {
            return InQuery != null && TagBits.HasAny(Matched, InQuery.ExplicitBits);
        }

        /// <summary>InQuery 의 태그가 모두 매칭되는가 (계층 적용). InQuery 가 비어 있으면 true.</summary>
        public bool HasAll(GameplayTagContainer InQuery)
        {
            return InQuery == null || TagBits.HasAll(Matched, InQuery.ExplicitBits);
        }

        public bool HasAnyExact(GameplayTagContainer InQuery)
        {
            return InQuery != null && TagBits.HasAny(Explicit, InQuery.ExplicitBits);
        }

        public bool HasAllExact(GameplayTagContainer InQuery)
        {
            return InQuery == null || TagBits.HasAll(Explicit, InQuery.ExplicitBits);
        }

        /// <summary>부모 포함 카운트 (InTag 자신 + 자식들이 부여된 횟수).</summary>
        public int GetCount(int InTag)
        {
            return InTag >= 0 && InTag < TagCounts.Length ? TagCounts[InTag] : 0;
        }

        /// <summary>InTag 가 직접 부여된 횟수.</summary>
        public int GetExplicitCount(int InTag)
        {
            return InTag >= 0 && InTag < ExplicitCounts.Length ? ExplicitCounts[InTag] : 0;
        }

        internal ulong[] ExplicitBits
        {
            get { return Explicit; }
        }

        public void AddTag(int InTag)
        {
            update(InTag, 1);
        }

        public void RemoveTag(int InTag)
        {
            update(InTag, -1);
        }

        public void AddTags(GameplayTagContainer InTags)
        {
            if (InTags == null)
            {
                return;
            }

            for (int i = 0; i < InTags.Count; ++i)
            {
                update(InTags.GetTag(i), 1);
            }
        }

        public void RemoveTags(GameplayTagContainer InTags)
        {
            if (InTags == null)
            {
                return;
            }

            for (int i = 0; i < InTags.Count; ++i)
            {
                update(InTags.GetTag(i), -1);
            }
        }

        private void update(int InTag, int InDelta)
        {
            if (InTag < 0)
            {
                return;
            }

            ensureCounts(InTag);
            int before = ExplicitCounts[InTag];
            int after = Math.Max(0, before + InDelta);
            if (before == after)
            {
                return;
            }

            ExplicitCounts[InTag] = after;
            if (after > 0)
            {
                TagBits.Set(ref Explicit, InTag);
            }
            else
            {
                TagBits.Unset(Explicit, InTag);
            }

            // 실제로 바뀐 만큼만 부모 체인 카운트에 반영 (없는 태그를 빼도 부모 카운트가 틀어지지 않게).
            int delta = after - before;
            int tag = InTag;
            while (tag >= 0)
            {
                changeMatched(tag, delta);
                tag = GameplayTagManager.GetParent(tag);
            }
        }

        private void changeMatched(int InTag, int InDelta)
        {
            ensureCounts(InTag);
            int before = TagCounts[InTag];
            int after = Math.Max(0, before + InDelta);
            TagCounts[InTag] = after;

            if (after > 0)
            {
                TagBits.Set(ref Matched, InTag);
            }
            else
            {
                TagBits.Unset(Matched, InTag);
            }

            bool toggled = (before == 0) != (after == 0);
            if (toggled == true)
            {
                OnTagChanged?.Invoke(InTag, after);
            }
        }

        private void ensureCounts(int InTag)
        {
            if (InTag < TagCounts.Length)
            {
                return;
            }

            int size = Math.Max(Math.Max(64, GameplayTagManager.Count), InTag + 1);
            Array.Resize(ref TagCounts, size);
            Array.Resize(ref ExplicitCounts, size);
        }
    }
}
