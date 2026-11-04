using System.Collections.Generic;

namespace Framework.Ability
{
    /// <summary>
    /// 언리얼 FGameplayTagContainer 대응 — 어빌리티/이펙트 정의에 쓰는 태그 묶음.
    /// 의미는 언리얼과 같고, 저장과 비교만 비트셋으로 한다.
    ///  - Explicit : 직접 넣은 태그 (명시 태그)
    ///  - Matched  : 명시 태그 + 그 부모들 (언리얼 ParentTags 캐시). 계층이 바뀌면(GameplayTagManager.Version) 다시 만든다.
    /// 비교 규칙: "가진 쪽" 은 Matched(부모 포함)로, "질의 쪽" 은 Explicit(명시만)로 비교한다. 질의 쪽은 절대 부모로 펼치지 않는다.
    ///   HasAny(Q) = (this.Matched &amp; Q.Explicit) != 0
    ///   HasAll(Q) = (this.Matched &amp; Q.Explicit) == Q.Explicit
    /// </summary>
    public class GameplayTagContainer
    {
        private readonly List<int> Tags = new List<int>();
        private ulong[] Explicit;
        private ulong[] Matched;
        private int MatchedVersion = -1;

        public int Count
        {
            get { return Tags.Count; }
        }

        public bool IsEmpty
        {
            get { return Tags.Count == 0; }
        }

        /// <summary>명시 태그를 순서대로 읽는다 (0 ~ Count-1).</summary>
        public int GetTag(int InIndex)
        {
            return Tags[InIndex];
        }

        public void Add(int InTag)
        {
            if (InTag < 0 || TagBits.Has(Explicit, InTag) == true)
            {
                return;
            }

            Tags.Add(InTag);
            TagBits.Set(ref Explicit, InTag);
            MatchedVersion = -1;
        }

        public void Remove(int InTag)
        {
            if (TagBits.Has(Explicit, InTag) == false)
            {
                return;
            }

            Tags.Remove(InTag);
            TagBits.Unset(Explicit, InTag);
            MatchedVersion = -1;
        }

        public void Clear()
        {
            Tags.Clear();
            Explicit = null;
            Matched = null;
            MatchedVersion = -1;
        }

        /// <summary>언리얼 HasTag — InTag 이거나 그 자식을 가지고 있는가.</summary>
        public bool HasTag(int InTag)
        {
            return TagBits.Has(getMatched(), InTag);
        }

        /// <summary>언리얼 HasTagExact — InTag 를 명시적으로 가지고 있는가 (계층 무시).</summary>
        public bool HasTagExact(int InTag)
        {
            return TagBits.Has(Explicit, InTag);
        }

        /// <summary>InQuery 의 태그 중 하나라도 매칭되는가 (계층 적용). InQuery 가 비어 있으면 false.</summary>
        public bool HasAny(GameplayTagContainer InQuery)
        {
            return InQuery != null && TagBits.HasAny(getMatched(), InQuery.Explicit);
        }

        /// <summary>InQuery 의 태그가 모두 매칭되는가 (계층 적용). InQuery 가 비어 있으면 true.</summary>
        public bool HasAll(GameplayTagContainer InQuery)
        {
            return InQuery == null || TagBits.HasAll(getMatched(), InQuery.Explicit);
        }

        public bool HasAnyExact(GameplayTagContainer InQuery)
        {
            return InQuery != null && TagBits.HasAny(Explicit, InQuery.Explicit);
        }

        public bool HasAllExact(GameplayTagContainer InQuery)
        {
            return InQuery == null || TagBits.HasAll(Explicit, InQuery.Explicit);
        }

        /// <summary>
        /// InOther 의 "명시 태그" 중 하나라도 이 컨테이너에 매칭되는가.
        /// 언리얼 AreAbilityTagsBlocked 의 AbilityTags.HasAny(BlockedAbilityTags.GetExplicitGameplayTags()) 와 같다.
        /// </summary>
        public bool HasAnyExplicitOf(GameplayTagCountContainer InOther)
        {
            return InOther != null && TagBits.HasAny(getMatched(), InOther.ExplicitBits);
        }

        internal ulong[] ExplicitBits
        {
            get { return Explicit; }
        }

        /// <summary>명시 태그 + 부모 비트. 태그나 계층이 바뀌었을 때만 다시 계산한다.</summary>
        private ulong[] getMatched()
        {
            if (MatchedVersion == GameplayTagManager.Version && (Matched != null || Tags.Count == 0))
            {
                return Matched;
            }

            Matched = null;
            for (int i = 0; i < Tags.Count; ++i)
            {
                int tag = Tags[i];
                while (tag >= 0)
                {
                    TagBits.Set(ref Matched, tag);
                    tag = GameplayTagManager.GetParent(tag);
                }
            }

            MatchedVersion = GameplayTagManager.Version;
            return Matched;
        }
    }
}
