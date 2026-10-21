using UnityEditor;
using UnityEngine;

namespace ActionRPG.Core.EditorTools.DataDriven
{
    /// <summary>
    /// 기획자가 "이 CSV를 이 타입의 에셋들로 만들어 줘" 라고 지정하는 설정 하나.
    /// MonoScript 를 드래그하는 방식이라 코드를 몰라도 임포트 대상을 지정할 수 있다.
    ///
    /// 이 클래스 자체는 순수 데이터를 담을 뿐인데도 Editor 어셈블리에 두는 이유는
    /// <see cref="MonoScript"/> 필드가 UnityEditor 네임스페이스에 속해 있어서다 —
    /// Runtime 어셈블리에 두면 플레이어 빌드가 UnityEditor 를 참조하지 못해 컴파일이 깨진다.
    /// 이 프로필 에셋은 임포트 시점에만 쓰이고 런타임에 로드될 일이 없으므로 문제 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Data/CSV Import Profile", fileName = "CSV_NewProfile")]
    public class CsvImportProfile : ScriptableObject
    {
        [Header("입력")]
        [Tooltip("헤더가 첫 줄에 있는 UTF-8 CSV 파일.")]
        public TextAsset SourceCsv;

        [Header("대상")]
        [Tooltip("[CsvColumn] 필드를 가진 ScriptableObject 스크립트. .cs 파일을 그대로 드래그한다.")]
        public MonoScript TargetScript;
        [Tooltip("생성/갱신될 에셋들이 저장될 폴더 (Assets/ 로 시작).")]
        public string OutputFolder = "Assets/Data/Generated";

        [Header("옵션")]
        [Tooltip("ID 컬럼 이름. 이 값으로 파일 이름과 기존 에셋 매칭을 한다.")]
        public string IdColumnName = "Id";
        [Tooltip("체크하면, CSV에 더 이상 없는 ID의 기존 에셋을 자동으로 지운다.")]
        public bool DeleteOrphanedAssets = true;
        [Tooltip("파일명 접두사. 예: 'SK_' 를 주면 SK_Fireball.asset 처럼 생성된다.")]
        public string AssetNamePrefix = string.Empty;
    }
}
