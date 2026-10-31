using System.Collections;
using Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 루트 모션은 메두사 Attack1(AnimationTable 15) 에만 켠다. 다른 행은 모두 false, 메두사 공격이 끝나면 다시 false.
/// </summary>
public class RootMotionTests : GamePlayTestBase
{
    private const int Medusa = 2;
    private const int MedusaAttack1 = 15;

    [Test]
    public void OnlyMedusaAttack1_HasRootMotion()
    {
        foreach (var row in Tables.Root.Animation.All)
        {
            Assert.AreEqual(row.Index == MedusaAttack1, row.ApplyRootMotion, $"AnimationTable {row.Index} ApplyRootMotion");
        }

        CharacterRow medusa;
        Assert.IsTrue(Tables.Root.Character.TryGet(Medusa, out medusa));
        AbilityRow attack;
        Assert.IsTrue(Tables.Root.Ability.TryGet(medusa.Abilities[0], out attack));
        CollectionAssert.AreEqual(new[] { MedusaAttack1 }, attack.Anims, "메두사 평타 = 루트 모션 행");
    }

    [UnityTest]
    public IEnumerator MedusaAttack_TurnsRootMotionOnThenOff()
    {
        Player.AddTag((int)ETag.Cheat_God);
        MonsterCharacter monster = null;
        yield return SpawnMonster(Medusa, new Vector3(0.0f, 0.0f, 3.0f), m => monster = m);
        var animator = monster.Anim.Animator;
        Assert.IsFalse(animator.applyRootMotion, "스폰 직후 false");

        yield return WaitFor(() => animator.applyRootMotion == true, 15.0f, "메두사 Attack1 재생 중 루트 모션 켜짐");
        yield return WaitFor(() => animator.applyRootMotion == false, 5.0f, "공격이 끝나면 루트 모션 꺼짐");
        Assert.IsFalse(Player.Anim.Animator.applyRootMotion, "플레이어는 루트 모션 없음");
    }
}
