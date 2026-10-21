using System;
using Framework;
using UnityEngine;

/// <summary>
/// 애니메이션을 실행할 Animator 파라미터 하나. CSV 셀 형식: Type;Name;Value (Value 생략 가능, 예: Trigger;Attack1 / Float;Blend;0.5).
/// 이름은 임포트 결과에만 남고, 런타임은 OnBind 에서 만든 Hash 만 사용한다.
/// </summary>
[Serializable]
public struct AnimationParameter
{
    public EAnimParamType Type;
    public string Name;
    /// <summary>Bool: 0 이 아니면 true, Int: 반올림, Float: 그대로. Trigger 는 무시.</summary>
    public float Value;

    [NonSerialized] public int Hash;

    public void Initialize()
    {
        Hash = Animator.StringToHash(Name);
    }

    public void Apply(Animator InAnimator)
    {
        switch (Type)
        {
            case EAnimParamType.Trigger:
                InAnimator.SetTrigger(Hash);
                break;
            case EAnimParamType.Bool:
                InAnimator.SetBool(Hash, Value != 0.0f);
                break;
            case EAnimParamType.Float:
                InAnimator.SetFloat(Hash, Value);
                break;
            case EAnimParamType.Int:
                InAnimator.SetInteger(Hash, Mathf.RoundToInt(Value));
                break;
        }
    }
}

[Serializable]
public class AnimationRow : ITableRow
{
    public int Index;
    /// <summary>재생 속도 배율. 호출 쪽 속도(공격 속도 등)와 곱해 AttackSpeed 파라미터(액션 상태의 speedParameter)에 넣는다.</summary>
    public float Speed;
    /// <summary>재생 시 순서대로 적용할 파라미터들 ('|' 로 여러 개).</summary>
    public AnimationParameter[] Parameters;
    public bool ApplyRootMotion;

    public void Initialize()
    {
        if (Parameters == null)
        {
            return;
        }

        for (int i = 0; i < Parameters.Length; ++i)
        {
            Parameters[i].Initialize();
        }
    }

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>애니메이션 재생 정의. AbilityTable.Anims, ResourceNodeTable.Anim, ConstTable(BuildAnim/InteractAnim) 이 이 Index 를 참조한다.</summary>
[CreateAssetMenu(menuName = "Game/Tables/AnimationTable")]
public class AnimationTable : TableAsset<AnimationRow>
{
    public override void OnBind()
    {
        var rows = All;
        for (int i = 0; i < rows.Count; ++i)
        {
            rows[i].Initialize();
        }
    }
}
