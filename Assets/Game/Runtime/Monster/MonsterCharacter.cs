using Framework;
using UnityEngine;

/// <summary>
/// 몬스터 액터. 사망 시 처치한 플레이어에게 LootTable 드랍을 지급하고 일정 시간 뒤 사라진다.
/// </summary>
[RequireComponent(typeof(MonsterBrain))]
public class MonsterCharacter : Actor
{
    private const float CorpseSeconds = 4.0f;

    public Vector3 Home { get; private set; }
    public MonsterBrain Brain { get; private set; }
    public int SpawnId;

    private float CorpseRemaining = -1.0f;

    public void InitializeMonster(CharacterRow InRow, Vector3 InHome, int InSpawnId)
    {
        Home = InHome;
        SpawnId = InSpawnId;
        Initialize(InRow, ActorKey.Generate());
        Brain = GetComponent<MonsterBrain>();
        Brain.Initialize(this);
    }

    protected override void Update()
    {
        base.Update();
        if (CorpseRemaining < 0.0f)
        {
            return;
        }

        CorpseRemaining -= Time.deltaTime;
        if (CorpseRemaining <= 0.0f)
        {
            Destroy(gameObject);
        }
    }

    public override void Die(Actor InKiller)
    {
        if (IsDead == true)
        {
            return;
        }

        base.Die(InKiller);
        CorpseRemaining = CorpseSeconds;
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }

        var player = InKiller as PlayerCharacter;
        if (player != null)
        {
            LootUtil.Grant(Row.Loot, player);
        }
    }
}
