
using System.Collections.Generic;
using Framework.Ability;
using UnityEngine;

public class ActorBehaviour : BaseBehaviour
{
    public long ActorId { get;  private set; }
    
    public AbilitySystemComponent ASC { get; private set; }
    
    public AttributeSet Attributes { get; private set; }

    [SerializeField] protected GameplayAbility[] initializeAbilities = null;
    protected Dictionary<GameplayTagContainer, GameplayAbility> applyAbilities = new Dictionary<GameplayTagContainer, GameplayAbility>();

    protected override void OnUnityAwake()
    {
        base.OnUnityAwake();
        
        
    }

    protected override void OnUnityDestroy()
    {
        
        
        base.OnUnityDestroy();
    }

    public virtual void CommitAbility(GameplayTagContainer gameplayTag, GameplayAbility ability)
    {
        
    }

    public virtual void RemoveAbility(GameplayTagContainer gameplayTag, GameplayAbility ability)
    {
        
    }
}