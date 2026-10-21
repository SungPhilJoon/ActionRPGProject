using ActionRPG.Core.DataDriven;
using UnityEngine;

namespace ActionRPG.Data
{
    /// <summary>
    /// CSV 로 데이터 드리븐하는 예시 테이블. Assets/Data/CSV/EnemyStats.csv 한 장으로
    /// 기획자가 이 타입의 에셋들을 직접 만들고 갱신할 수 있다.
    ///
    /// 반영 방법:
    ///   1) CSV 를 고친다 (Assets/Data/CSV/EnemyStats.csv).
    ///   2) 해당 CsvImportProfile 에셋을 선택하고
    ///      ActionRPG > 데이터 > CSV 임포트 (선택된 프로필) 실행.
    ///   3) Assets/Data/Generated/EnemyStats 아래에 ID별 에셋이 생성/갱신된다.
    ///
    /// EnemyDefinition 처럼 배열/중첩 스킬 데이터가 있는 복잡한 테이블은 CSV 한 장으로
    /// 표현하기 어려우므로, 이런 "평평한(flat)" 밸런스 수치 테이블에 이 패턴을 쓰고
    /// EnemyDefinition 은 그대로 ScriptableObject 로 직접 만드는 편이 낫다.
    /// 필요하면 EnemyDefinition 에도 [CsvColumn] 을 똑같이 붙여서 기본 스탯 필드만
    /// CSV 로 몰 수도 있다 — 임포터는 타입을 가리지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Data/Enemy Stat Row (CSV Demo)", fileName = "ESR_New")]
    public class EnemyStatRow : ScriptableObject
    {
        [CsvColumn("Id"), CsvPrimaryKey]
        public string Id;

        [CsvColumn("DisplayName", Required = true)]
        public string DisplayName;

        [CsvColumn("MaxHealth")]
        public float MaxHealth = 60f;

        [CsvColumn("AttackPower")]
        public float AttackPower = 8f;

        [CsvColumn("Defense")]
        public float Defense = 2f;

        [CsvColumn("MoveSpeed")]
        public float MoveSpeed = 3.4f;

        [CsvColumn("ExperienceReward")]
        public int ExperienceReward = 10;

        [CsvColumn("IsBoss")]
        public bool IsBoss;

        /// <summary>세미콜론으로 구분한 값 목록도 배열 필드로 받을 수 있다는 예시. "화상;출혈" 같은 태그 목록에 쓴다.</summary>
        [CsvColumn("Tags")]
        public string[] Tags = System.Array.Empty<string>();
    }
}
