using System;

namespace ActionRPG.Core.DataDriven
{
    /// <summary>
    /// ScriptableObject 필드를 CSV 컬럼과 연결한다.
    /// 기획자가 CSV 를 고치면 <c>ActionRPG &gt; 데이터 &gt; CSV 임포트</c> 한 번으로
    /// 이 필드에 값이 채워진 에셋이 갱신된다. 에디터 전용 리플렉션 기반이라
    /// 런타임 성능에는 전혀 영향이 없다 (임포트 결과물은 평범한 ScriptableObject 이므로).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvColumnAttribute : Attribute
    {
        /// <summary>CSV 헤더 이름. 비우면 필드 이름을 그대로 쓴다.</summary>
        public string ColumnName { get; }
        /// <summary>true 면 이 컬럼이 비어 있을 때 임포트를 실패시킨다.</summary>
        public bool Required { get; set; }

        public CsvColumnAttribute(string columnName = null) => ColumnName = columnName;
    }

    /// <summary>
    /// 이 필드(문자열, 보통 Id)가 행(row)을 구분하는 기본 키임을 표시한다.
    /// 같은 값이 두 번 나오면 임포터가 경고를 낸다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CsvPrimaryKeyAttribute : Attribute
    {
    }
}
