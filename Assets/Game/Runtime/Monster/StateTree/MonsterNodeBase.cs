using System;
using Framework.StateTree;

/// <summary>
/// 몬스터용 노드 공통 부모. 컨텍스트의 Owner 를 MonsterBrain 으로 꺼내는 헬퍼만 제공한다.
/// (언리얼에서 Schema 가 Context 데이터로 AIController/Pawn 을 제공하는 것과 같은 역할)
/// </summary>
[Serializable]
public abstract class MonsterTask : StateTreeTask
{
    protected static MonsterBrain Brain(StateTreeContext InContext)
    {
        return InContext.Owner as MonsterBrain;
    }
}

[Serializable]
public abstract class MonsterCondition : StateTreeCondition
{
    protected static MonsterBrain Brain(StateTreeContext InContext)
    {
        return InContext.Owner as MonsterBrain;
    }
}

[Serializable]
public abstract class MonsterEvaluator : StateTreeEvaluator
{
    protected static MonsterBrain Brain(StateTreeContext InContext)
    {
        return InContext.Owner as MonsterBrain;
    }
}
