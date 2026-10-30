using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 에디터(GAS Tool)에서 만드는 어빌리티 정의 — 언리얼 Blueprint GameplayAbility 의 Class Defaults + ActivateAbility 그래프 대응.
///  - 태그 규칙: Cancel / Block / ActivationOwned / ActivationRequired / ActivationBlocked (UGameplayAbility 의 같은 이름 프로퍼티)
///  - 비용: BloodPool 소모 (범용 비용 이펙트 + SetByCaller). 쿨다운은 AbilityTable.Cooldown (범용 쿨다운 이펙트)
///  - Steps: 발동 시 위에서부터 순서대로 실행하는 단계 (AbilityTask 대응). 대기 단계가 시간을 만들고, 마지막 단계 뒤 EndAbility.
/// AbilityTable 의 Type=Graph 행이 Graph 컬럼(AssetTable Index, Kind=Data)으로 이 에셋을 가리킨다.
/// 이름 · 슬롯 · AbilityTags · 쿨다운 · AI 사용 거리(Range) · 아이콘은 AbilityTable 행에 둔다 (UI/AI 가 함께 쓰는 값).
/// </summary>
[CreateAssetMenu(menuName = "Game/Ability Graph")]
public class AbilityGraphAsset : ScriptableObject
{
    [TextArea] public string Description;

    [Header("Tags")]
    [Tooltip("발동하면 이 태그를 가진 다른 실행 중 어빌리티를 취소한다 (CancelAbilitiesWithTag).")]
    public ETag[] CancelAbilitiesWithTag = new ETag[0];
    [Tooltip("실행 중 이 태그를 가진 다른 어빌리티의 발동을 막는다 (BlockAbilitiesWithTag).")]
    public ETag[] BlockAbilitiesWithTag = new ETag[0];
    [Tooltip("실행 중 자신에게 부여되는 태그 (ActivationOwnedTags). 예: State_Attacking, State_Invulnerable.")]
    public ETag[] ActivationOwnedTags = new ETag[0];
    [Tooltip("이 태그가 모두 있어야 발동 가능 (ActivationRequiredTags).")]
    public ETag[] ActivationRequiredTags = new ETag[0];
    [Tooltip("이 태그 중 하나라도 있으면 발동 불가 (ActivationBlockedTags). State_Dead / Stun 은 항상 막힌다.")]
    public ETag[] ActivationBlockedTags = new ETag[0];

    [Header("Cost")]
    [Tooltip("발동 시 소모할 BloodPool. 0 이면 비용 없음.")]
    public float BloodCost;

    [Header("Steps (ActivateAbility)")]
    [SerializeReference] public List<AbilityStep> Steps = new List<AbilityStep>();
}
