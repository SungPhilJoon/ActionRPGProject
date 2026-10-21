using UnityEngine;

/// <summary>
/// Animator 파라미터 해시. 문자열은 이 클래스에서 한 번만 해시로 바뀌고 이후에는 int 로만 사용한다.
/// 파라미터 이름은 에디터 PrefabBuilder 가 만드는 AnimatorController 와 같아야 한다.
/// </summary>
public static class AnimatorParam
{
    public static readonly int Speed = Animator.StringToHash("Speed");
    public static readonly int Dead = Animator.StringToHash("Dead");
    /// <summary>액션 상태들의 speedParameter. AnimationRow.Speed × 호출 속도가 들어간다.</summary>
    public static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");

    /// <summary>에디터 AnimatorController 빌더가 액션 상태/트리거 이름으로 쓴다 (AnimationTable.Parameters 의 Name 과 맞춘다).</summary>
    public static string TriggerName(EAnim InAnim)
    {
        return InAnim.ToString();
    }
}

/// <summary>Animator 래퍼. 모델이 없거나 Animator 가 없어도 안전하게 무시한다.</summary>
public class ActorAnimator : MonoBehaviour
{
    public Animator Animator;
    /// <summary>무기 모델을 붙일 손 본. PrefabBuilder 가 연결한다.</summary>
    public Transform WeaponSocket;

    private GameObject WeaponModel;

    private void Awake()
    {
        if (Animator == null)
        {
            Animator = GetComponentInChildren<Animator>();
        }

        // 외부 애니메이션 클립(Magic Pig, ExplosiveLLC)에 박힌 AnimationEvent 는 데모 스크립트용이라 받는 쪽이 없다.
        // 게임은 이벤트 대신 어빌리티 타이밍(AbilityTask)을 쓰므로 이벤트 발송을 끈다.
        if (Animator != null)
        {
            Animator.fireEvents = false;
        }
    }

    public void SetSpeed(float InNormalizedSpeed)
    {
        if (Animator == null)
        {
            return;
        }

        Animator.SetFloat(AnimatorParam.Speed, InNormalizedSpeed, 0.1f, Time.deltaTime);
    }

    /// <summary>AnimationTable 의 InAnimId 행을 재생한다. InSpeed 는 행의 Speed 에 곱해진다 (공격 속도 등). 0 이나 없는 Index 는 무시.</summary>
    public void Play(int InAnimId, float InSpeed = 1.0f)
    {
        if (Animator == null)
        {
            return;
        }

        var row = Tables.Animation(InAnimId);
        if (row == null)
        {
            return;
        }

        Animator.applyRootMotion = row.ApplyRootMotion;
        Animator.SetFloat(AnimatorParam.AttackSpeed, row.Speed * InSpeed);
        var parameters = row.Parameters;
        for (int i = 0; i < parameters.Length; ++i)
        {
            parameters[i].Apply(Animator);
        }
    }

    public void SetDead(bool InDead)
    {
        if (Animator == null)
        {
            return;
        }

        Animator.SetBool(AnimatorParam.Dead, InDead);
    }

    public void SetWeaponModel(GameObject InPrefab)
    {
        if (WeaponModel != null)
        {
            Destroy(WeaponModel);
            WeaponModel = null;
        }

        if (InPrefab == null || WeaponSocket == null)
        {
            return;
        }

        WeaponModel = Instantiate(InPrefab, WeaponSocket, false);
        foreach (var collider in WeaponModel.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
    }
}
