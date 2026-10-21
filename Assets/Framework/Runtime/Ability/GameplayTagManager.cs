using System;

namespace Framework.Ability
{
    /// <summary>
    /// 언리얼 UGameplayTagsManager 대응 — 태그 계층(자식 → 부모) 등록부.
    /// 태그는 문자열이 아니라 0 이상의 int id 이며, 상한은 고정이 아니라 등록된 최대 id 에 맞춰 늘어난다.
    /// 계층 비교 규칙(언리얼과 동일): "A.B.C" 는 "A.B" 에 매칭되지만, "A.B" 는 "A.B.C" 에 매칭되지 않고, 형제("A.B" ↔ "A.C")끼리도 매칭되지 않는다.
    /// </summary>
    public static class GameplayTagManager
    {
        public const int InvalidTag = -1;
        private const int InitialCapacity = 128;

        private static int[] Parents = createParents(InitialCapacity);
        private static int TagCount;

        /// <summary>계층이 바뀔 때마다 증가. 정의용 컨테이너가 부모 비트 캐시를 다시 만들지 판단하는 데 쓴다.</summary>
        public static int Version { get; private set; }

        /// <summary>등록된 최대 id + 1.</summary>
        public static int Count
        {
            get { return TagCount; }
        }

        public static void Clear()
        {
            Parents = createParents(InitialCapacity);
            TagCount = 0;
            ++Version;
        }

        /// <summary>태그를 등록한다. 최상위 태그는 InParent = InvalidTag.</summary>
        public static void SetParent(int InTag, int InParent)
        {
            if (InTag < 0 || InTag == InParent)
            {
                return;
            }

            ensureCapacity(Math.Max(InTag, InParent) + 1);
            Parents[InTag] = InParent < 0 ? InvalidTag : InParent;
            TagCount = Math.Max(TagCount, InTag + 1);
            if (InParent >= 0)
            {
                TagCount = Math.Max(TagCount, InParent + 1);
            }

            ++Version;
        }

        public static int GetParent(int InTag)
        {
            if (InTag < 0 || InTag >= Parents.Length)
            {
                return InvalidTag;
            }

            return Parents[InTag];
        }

        /// <summary>언리얼 FGameplayTag::MatchesTag. InTag 가 InQuery 와 같거나 InQuery 의 자손이면 true.</summary>
        public static bool MatchesTag(int InTag, int InQuery)
        {
            if (InTag < 0 || InQuery < 0)
            {
                return false;
            }

            int tag = InTag;
            while (tag >= 0)
            {
                if (tag == InQuery)
                {
                    return true;
                }

                tag = GetParent(tag);
            }

            return false;
        }

        private static void ensureCapacity(int InSize)
        {
            if (InSize <= Parents.Length)
            {
                return;
            }

            int size = Parents.Length;
            while (size < InSize)
            {
                size *= 2;
            }

            var parents = createParents(size);
            Array.Copy(Parents, parents, Parents.Length);
            Parents = parents;
        }

        private static int[] createParents(int InSize)
        {
            var parents = new int[InSize];
            for (int i = 0; i < parents.Length; ++i)
            {
                parents[i] = InvalidTag;
            }

            return parents;
        }
    }

    /// <summary>
    /// 태그 비트셋 연산 (64태그당 ulong 1개). 길이가 다른 배열끼리도 비교할 수 있다 (없는 워드 = 0).
    /// 비교는 워드 단위 AND 라서 할당과 태그별 루프가 없다.
    /// </summary>
    internal static class TagBits
    {
        public static int WordIndex(int InTag)
        {
            return InTag >> 6;
        }

        public static ulong Bit(int InTag)
        {
            return 1UL << (InTag & 63);
        }

        public static void Ensure(ref ulong[] InOutBits, int InTag)
        {
            int words = WordIndex(InTag) + 1;
            if (InOutBits == null)
            {
                InOutBits = new ulong[words];
                return;
            }

            if (InOutBits.Length < words)
            {
                Array.Resize(ref InOutBits, words);
            }
        }

        public static void Set(ref ulong[] InOutBits, int InTag)
        {
            Ensure(ref InOutBits, InTag);
            InOutBits[WordIndex(InTag)] |= Bit(InTag);
        }

        public static void Unset(ulong[] InBits, int InTag)
        {
            int word = WordIndex(InTag);
            if (InBits != null && word < InBits.Length)
            {
                InBits[word] &= ~Bit(InTag);
            }
        }

        public static bool Has(ulong[] InBits, int InTag)
        {
            if (InBits == null || InTag < 0)
            {
                return false;
            }

            int word = WordIndex(InTag);
            return word < InBits.Length && (InBits[word] & Bit(InTag)) != 0;
        }

        /// <summary>InQuery 의 비트 중 하나라도 InBits 에 있는가. InQuery 가 비어 있으면 false.</summary>
        public static bool HasAny(ulong[] InBits, ulong[] InQuery)
        {
            if (InBits == null || InQuery == null)
            {
                return false;
            }

            int words = Math.Min(InBits.Length, InQuery.Length);
            for (int i = 0; i < words; ++i)
            {
                if ((InBits[i] & InQuery[i]) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>InQuery 의 모든 비트가 InBits 에 있는가. InQuery 가 비어 있으면 true.</summary>
        public static bool HasAll(ulong[] InBits, ulong[] InQuery)
        {
            if (InQuery == null)
            {
                return true;
            }

            for (int i = 0; i < InQuery.Length; ++i)
            {
                ulong bits = InBits != null && i < InBits.Length ? InBits[i] : 0UL;
                if ((bits & InQuery[i]) != InQuery[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
