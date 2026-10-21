using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    /// <summary>테이블 한 행. 모든 행은 int 인덱스(Index 컬럼)를 키로 가진다.</summary>
    public interface ITableRow
    {
        int GetKey();
    }

    /// <summary>
    /// CSV 를 변환해 저장한 ScriptableObject 테이블의 공통 부모 (에디터 임포터가 이 타입으로 찾는다).
    /// Source 에 원본 CSV(TextAsset) 를 연결해 두면 "CSV 임포트" 메뉴가 다시 변환해 준다.
    /// </summary>
    public abstract class TableAssetBase : ScriptableObject
    {
        public TextAsset Source;

        public abstract System.Type GetRowType();
        public abstract void SetRows(System.Collections.IList InRows);
        public abstract int Count { get; }

        public virtual void OnBind() { }
    }

    /// <summary>
    /// 런타임은 CSV 를 파싱하지 않고 이 ScriptableObject 만 읽는다.
    /// 첫 조회 때 int 키 → 행 Dictionary 를 만든다.
    /// </summary>
    public abstract class TableAsset<TRow> : TableAssetBase where TRow : class, ITableRow
    {
        [SerializeField] private List<TRow> Rows = new List<TRow>();

        [System.NonSerialized] private Dictionary<int, TRow> Lookup;

        public override int Count
        {
            get { return Rows.Count; }
        }

        public IReadOnlyList<TRow> All
        {
            get { return Rows; }
        }

        public override System.Type GetRowType()
        {
            return typeof(TRow);
        }

        public override void SetRows(System.Collections.IList InRows)
        {
            Rows.Clear();
            for (int i = 0; i < InRows.Count; ++i)
            {
                Rows.Add(InRows[i] as TRow);
            }

            Lookup = null;
        }

        public TRow Get(int InKey)
        {
            TRow row;
            if (TryGet(InKey, out row) == false)
            {
                Log.Warning($"{name}: key {InKey} not found");
                return null;
            }

            return row;
        }

        public bool TryGet(int InKey, out TRow OutRow)
        {
            if (Lookup == null)
            {
                buildLookup();
            }

            return Lookup.TryGetValue(InKey, out OutRow);
        }

        private void buildLookup()
        {
            Lookup = new Dictionary<int, TRow>(Rows.Count);
            for (int i = 0; i < Rows.Count; ++i)
            {
                var row = Rows[i];
                if (row == null)
                {
                    continue;
                }

                int key = row.GetKey();
                if (Lookup.ContainsKey(key) == true)
                {
                    Log.Error($"{name}: duplicated key {key}");
                    continue;
                }

                Lookup.Add(key, row);
            }
        }

        protected virtual void OnEnable()
        {
            Lookup = null;
        }
    }
}
