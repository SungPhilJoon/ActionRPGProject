namespace Framework
{
    /// <summary>
    /// 문자열을 직접 비교하지 않기 위한 해시 유틸.
    /// 데이터(CSV)나 콘솔 입력처럼 외부에서 들어오는 문자열은 한 번만 해시로 바꾸고 이후에는 int 로만 비교한다.
    /// FNV-1a 32bit, 대소문자 무시.
    /// </summary>
    public static class HashUtil
    {
        private const uint OffsetBasis = 2166136261;
        private const uint Prime = 16777619;

        public static int Fnv1a(string InText)
        {
            if (string.IsNullOrEmpty(InText) == true)
            {
                return 0;
            }

            uint hash = OffsetBasis;
            for (int i = 0; i < InText.Length; ++i)
            {
                char c = char.ToLowerInvariant(InText[i]);
                hash ^= c;
                hash *= Prime;
            }

            return (int)hash;
        }
    }
}
