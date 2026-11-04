using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 하나의 에디터 저작 데이터 (외형 + 애니메이션). MonsterAuthoringEditor(인스펙터)가 편집하고 ContentBuilder 가 이걸로
/// Content/{Address}.prefab 과 AnimatorController 를 굽는다. 런타임은 이 에셋을 모른다 (프리팹만 로드).
/// 능력치·어빌리티는 CharacterTable.csv 가 원본이므로 여기에 두지 않고 CharacterId 로만 연결한다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Monster Authoring", fileName = "New Monster Authoring")]
public class MonsterAuthoring : ScriptableObject
{
    [Serializable]
    public struct ActionClip
    {
        public EAnim Anim;
        public AnimationClip Clip;
    }

    /// <summary>CharacterTable.Index.</summary>
    public int CharacterId;
    /// <summary>Content 기준 주소 (예: Characters/BanditThug). AssetTable.Address 와 같아야 한다.</summary>
    public string Address;

    [Header("Appearance")]
    /// <summary>외부 모델 프리팹 또는 FBX. 데모 스크립트/콜라이더/파티클은 빌드 시 제거된다.</summary>
    public GameObject Model;
    public float ModelScale = 1.0f;
    /// <summary>판정 반지름 (Actor.Radius, NavMeshAgent.radius).</summary>
    public float Radius = 0.5f;
    public float AgentHeight = 2.0f;

    [Header("Animation")]
    /// <summary>Generic 리그의 실제 이동 뼈. 지정하면 클립을 제자리(in-place)로 임포트한다 (Issues 35). 비우면 그대로.</summary>
    public string MotionNode;
    public AnimationClip Idle;
    public AnimationClip Walk;
    public AnimationClip Dead;
    /// <summary>Locomotion 블렌드 트리에서 Walk 가 100% 가 되는 Speed 비율.</summary>
    public float WalkThreshold = 0.5f;
    public List<ActionClip> Actions = new List<ActionClip>();

    public AnimationClip GetAction(EAnim InAnim)
    {
        for (int i = 0; i < Actions.Count; ++i)
        {
            if (Actions[i].Anim == InAnim)
            {
                return Actions[i].Clip;
            }
        }

        return null;
    }

    /// <summary>InClip 이 null 이면 그 액션을 지운다.</summary>
    public void SetAction(EAnim InAnim, AnimationClip InClip)
    {
        for (int i = 0; i < Actions.Count; ++i)
        {
            if (Actions[i].Anim == InAnim)
            {
                if (InClip == null)
                {
                    Actions.RemoveAt(i);
                }
                else
                {
                    Actions[i] = new ActionClip { Anim = InAnim, Clip = InClip };
                }

                return;
            }
        }

        if (InClip != null)
        {
            Actions.Add(new ActionClip { Anim = InAnim, Clip = InClip });
        }
    }
}
