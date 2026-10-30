using System.Collections;
using Framework;
using Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Addressables 의존성 추적 테스트 (Category "Dependency", Documentation/VRising/Dependency.md).
/// 의존성 문제가 의심되는 동작을 실제로 플레이하면서 AssetTracker 기록을 DependencyReports/Runs/*.json 으로 남긴다.
/// 테스트가 끝나면 에디터(DependencyTestWatcher)가 기록을 읽어 이슈 목록 · 보고서(DependencyIssues.md)를 자동 갱신한다.
///  - 실패 조건: 로드 실패만 (실제로 게임이 깨지는 것)
///  - 반납 안 된 참조 · 긴 동기 대기는 테스트를 실패시키지 않고 기록 → 이슈로 추적
/// 각 시나리오는 같은 동작을 Repeat 번 반복한다 — 반복할수록 참조가 늘면 누수, 한 번만 늘면 "처음 한 번 로드해 계속 씀" 이다.
/// </summary>
[Category("Dependency")]
public class DependencyScenarioTests : GamePlayTestBase
{
    private const int Repeat = 3;

    [UnityTest]
    public IEnumerator Boot_LoadedAtStartup()
    {
        // 게임 시작(테이블 · UI · 프리로드 · 월드 스폰)에서 잡힌 에셋 전체. 계속 들고 있는 것이 정상이라 누수 판정은 하지 않는다.
        var recorder = DependencyRecorder.Begin("Boot", "게임 시작 ~ 싱글 플레이 시작까지 로드된 에셋 (프리로드 포함)", 1, false, true);
        yield return null;
        finish(recorder);
    }

    [UnityTest]
    public IEnumerator UI_OpenCloseWindows()
    {
        var ui = Managers.Get<UIManager>();
        var windows = new UIWindow[]
        {
            ui.Get<InventoryWindow>(), ui.Get<CraftWindow>(), ui.Get<BuildWindow>(), ui.Get<ShopWindow>(),
            ui.Get<CheatConsoleWindow>(), ui.Get<BalanceOverlayWindow>(),
        };

        var recorder = DependencyRecorder.Begin("UI_OpenCloseWindows", "인벤토리 · 제작 · 건축 · 상점 · 치트 · 밸런스 창을 열고 닫기 (아이콘 스프라이트 로드)", Repeat, true);
        for (int r = 0; r < Repeat; ++r)
        {
            foreach (var window in windows)
            {
                Assert.IsNotNull(window, "UI 창 프리팹이 로드되지 않았습니다");
                window.Open();
                yield return null;
                window.Close();
                yield return null;
            }
        }

        finish(recorder);
    }

    [UnityTest]
    public IEnumerator Monster_SpawnAndRemove()
    {
        int[] monsters = { 2, 3, 4 };
        var recorder = DependencyRecorder.Begin("Monster_SpawnAndRemove", "메두사 · 코브라 · 트롤을 스폰 → 처치 → 제거 (캐릭터 프리팹 · 피격 VFX)", Repeat, true);
        for (int r = 0; r < Repeat; ++r)
        {
            foreach (int id in monsters)
            {
                MonsterCharacter monster = null;
                yield return SpawnMonster(id, new Vector3(0.0f, 0.0f, 8.0f), m => monster = m);
                yield return WaitSeconds(0.3f);
                monster.Die(Player);
                yield return WaitSeconds(0.3f);
                Object.Destroy(monster.gameObject);
                yield return null;
            }
        }

        finish(recorder);
    }

    [UnityTest]
    public IEnumerator Build_GhostBeginCancel()
    {
        var build = Managers.Get<BuildManager>();
        var structures = Tables.Root.Structure.All;
        var recorder = DependencyRecorder.Begin("Build_GhostBeginCancel", "건축 모드 진입(고스트 프리팹 동기 로드) → 취소, 모든 건축물", Repeat, true);
        for (int r = 0; r < Repeat; ++r)
        {
            for (int i = 0; i < structures.Count; ++i)
            {
                build.Begin(structures[i].Index);
                yield return null;
                build.Cancel();
                yield return null;
            }
        }

        finish(recorder);
    }

    [UnityTest]
    public IEnumerator Ability_CastAllSlots()
    {
        var recorder = DependencyRecorder.Begin("Ability_CastAllSlots", "플레이어 어빌리티 8칸을 모두 시전 (스킬 · 발사체 · 큐 VFX)", Repeat, true);
        for (int r = 0; r < Repeat; ++r)
        {
            for (int slot = 0; slot < (int)EAbilitySlot.Count; ++slot)
            {
                Player.Asc.ClearCooldowns();
                Player.TryActivate((EAbilitySlot)slot, Player.transform.position + Player.transform.forward * 6.0f);
                yield return WaitSeconds(0.6f);
            }
        }

        yield return WaitSeconds(1.0f);
        finish(recorder);
    }

    private static void finish(DependencyRecorder InRecorder)
    {
        string path;
        var record = InRecorder.End(true, out path);
        TestContext.WriteLine($"[Dependency] {record.Scenario}: loads {record.LoadEvents}, releases {record.ReleaseEvents}, sync {record.SyncWaitMilliseconds:0}ms → {path}");
        Assert.AreEqual(0, record.Failures.Count, "로드 실패: " + string.Join(", ", record.Failures));
    }
}
