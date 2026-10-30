using System.Collections;
using System.Collections.Generic;
using Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 성 짓기 시나리오: 빈 부지에 완전한 성(5×5칸, 15m × 15m)을 처음부터 짓고, 완성된 모습을 10초 동안 카메라로 돌며 보여준다.
///
///   위에서 본 배치 (칸 = 3m, ▣ 성 심장, ▤ 바닥, ─│ 벽, ◫ 창문 벽, ▯ 문 벽, ● 기둥, ✦ 램프)
///
///        ●──●──●─◫●──●──●
///        │  ▤  ▤  ▤  ▤  ▤  │
///        ●  ▤ 작업 관  제련 ▤ ●
///        ◫  ▤  ▤  ▣  ▤  ▤  ◫      ✦ 램프 4개는 안쪽 모서리
///        ●  ▤  ▤  ▤  ▤  ▤  ●
///        │  ▤  ▤  ▤  ▤  ▤  │
///        ●──●──●─▯●──●──●      ← 남쪽 가운데 문
///
/// 단계마다 설치 성공 · 개수 · 재료 차감(StructureTable 비용 합)을 확인하고, 관을 지으면 리스폰 지점이 관 앞으로 바뀌는지도 본다.
/// 부지는 시작 지점 남서쪽의 평지 (높이 차 0.7m) 이며, 그 안의 채집 노드는 먼저 치운다.
/// </summary>
public partial class PlayScenarioTests
{
    private const int Wood = 1;
    private const int Stone = 2;
    private const int Fiber = 3;

    private const int CastleHeart = 1;
    private const int Floor = 2;
    private const int Wall = 3;
    private const int Workbench = 4;
    private const int Smelter = 5;
    private const int Coffin = 6;
    private const int Lamp = 7;
    private const int WallDoor = 8;
    private const int WallWindow = 9;
    private const int Pillar = 10;

    /// <summary>성 크기 (칸). 5 → 가운데 칸 기준 -2 ~ +2.</summary>
    private const int CastleCells = 5;
    private const float PlaceInterval = 0.06f;
    private const float ShowcaseSeconds = 10.0f;

    /// <summary>시작 지점 기준 부지 위치 (평지, 장애물 적음).</summary>
    private static readonly Vector3 SiteOffset = new Vector3(-30.0f, 0.0f, 24.0f);

    private BuildManager Build;
    private Vector3 Center;
    private float Grid;
    private readonly Dictionary<int, int> Spent = new Dictionary<int, int>();
    private int Placed;

    [UnityTest]
    public IEnumerator Scenario_BuildCastle()
    {
        Build = Managers.Get<BuildManager>();
        Assume.That(Build.HasHeart, Is.False, "이미 성 심장이 있는 세션에서는 새 성을 지을 수 없습니다 (Play 를 새로 시작해 실행).");

        // ---------------------------------------------------------- 1. 부지 준비
        Grid = Tables.Const(EConst.BuildGridSize);
        Center = snapToCell(StartPosition + SiteOffset);
        float half = CastleCells * 0.5f * Grid;
        clearSite(Center, half + Grid);
        standAt(Center + new Vector3(0.0f, 0.0f, -Grid * 0.5f));

        var inventory = Player.Inventory;
        inventory.Add(Wood, 600);
        inventory.Add(Stone, 600);
        inventory.Add(Fiber, 100);
        var before = new Dictionary<int, int> { { Wood, inventory.Count(Wood) }, { Stone, inventory.Count(Stone) }, { Fiber, inventory.Count(Fiber) } };
        int structuresBefore = Build.StructureCount;
        Spent.Clear();
        Placed = 0;
        yield return WaitSeconds(0.3f);

        // ---------------------------------------------------------- 2. 성 심장
        yield return place(CastleHeart, Center, 0.0f, "성 심장");
        Assert.IsTrue(Build.HasHeart, "성 심장이 등록되어야 함");

        // 잘못된 설치는 거부되어야 한다 (실패 알림이 쇼케이스 전에 사라지도록 일찍 확인).
        Assert.IsFalse(Build.PlaceAt(Center + new Vector3(60.0f, 0.0f, 0.0f)), "건축 모드가 아닐 때는 설치되면 안 됨");
        Build.Begin(Floor);
        Assert.IsFalse(Build.PlaceAt(Player.transform.position + new Vector3(40.0f, 0.0f, 0.0f)), "플레이어에게서 12m 밖은 설치되면 안 됨");

        // ---------------------------------------------------------- 3. 바닥 5×5 (가운데부터 바깥으로)
        int range = CastleCells / 2;
        for (int ring = 0; ring <= range; ++ring)
        {
            for (int x = -ring; x <= ring; ++x)
            {
                for (int z = -ring; z <= ring; ++z)
                {
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) == ring)
                    {
                        yield return place(Floor, cell(x, z), 0.0f, $"바닥 ({x},{z})");
                    }
                }
            }
        }

        // ---------------------------------------------------------- 4. 외벽 (북 · 남은 X 방향, 동 · 서는 Z 방향)
        for (int i = -range; i <= range; ++i)
        {
            yield return place(i == 0 ? WallWindow : Wall, edge(i, +1, true), 0.0f, $"북쪽 벽 {i}");
            yield return place(i == 0 ? WallDoor : Wall, edge(i, -1, true), 180.0f, $"남쪽 벽 {i}");
            yield return place(i == 0 ? WallWindow : Wall, edge(i, +1, false), 90.0f, $"동쪽 벽 {i}");
            yield return place(i == 0 ? WallWindow : Wall, edge(i, -1, false), 270.0f, $"서쪽 벽 {i}");
        }

        // ---------------------------------------------------------- 5. 기둥 (외벽의 모든 꼭짓점 20개)
        for (int k = 0; k <= CastleCells; ++k)
        {
            yield return place(Pillar, corner(k, 0), 0.0f, $"기둥 남 {k}");
            yield return place(Pillar, corner(k, CastleCells), 0.0f, $"기둥 북 {k}");
            if (k > 0 && k < CastleCells)
            {
                yield return place(Pillar, corner(0, k), 0.0f, $"기둥 서 {k}");
                yield return place(Pillar, corner(CastleCells, k), 0.0f, $"기둥 동 {k}");
            }
        }

        // ---------------------------------------------------------- 6. 내부 시설
        yield return place(Workbench, cell(-1, 1), 0.0f, "작업대");
        yield return place(Smelter, cell(1, 1), 0.0f, "제련소");
        yield return place(Coffin, cell(0, 1), 0.0f, "관");
        Assert.Less(MathUtil.FlatDistance(Player.RespawnPoint, cell(0, 1)), Grid, "관을 지으면 리스폰 지점이 관 앞으로 바뀌어야 함");
        yield return place(Lamp, corner(1, 1), 0.0f, "램프 남서");
        yield return place(Lamp, corner(CastleCells - 1, 1), 0.0f, "램프 남동");
        yield return place(Lamp, corner(1, CastleCells - 1), 0.0f, "램프 북서");
        yield return place(Lamp, corner(CastleCells - 1, CastleCells - 1), 0.0f, "램프 북동");
        Build.Cancel();

        // ---------------------------------------------------------- 7. 결과 확인
        // 성 심장 1 + 바닥 25 + 외벽 20 + 기둥 20 (외곽 꼭짓점) + 시설 3 + 램프 4 = 73
        int expected = 1 + CastleCells * CastleCells + CastleCells * 4 + CastleCells * 4 + 3 + 4;
        Assert.AreEqual(expected, Placed, "설치한 건축물 수");
        Assert.AreEqual(structuresBefore + expected, Build.StructureCount, "BuildManager 가 세는 건축물 수");
        foreach (var pair in before)
        {
            int spent;
            Spent.TryGetValue(pair.Key, out spent);
            Assert.AreEqual(pair.Value - spent, inventory.Count(pair.Key), $"아이템 {pair.Key} 차감량 (StructureTable 비용 합 {spent})");
        }


        // ---------------------------------------------------------- 8. 완성된 성 보여주기 (10초)
        yield return showcase();
    }

    // ------------------------------------------------------------------ placement

    /// <summary>InStructureId 건축 모드로 InPoint 에 설치하고, 비용을 누적한 뒤 잠깐 기다린다 (짓는 과정이 보이도록).</summary>
    private IEnumerator place(int InStructureId, Vector3 InPoint, float InYaw, string InWhat)
    {
        if (Build.IsActive == false || Build.CurrentRow.Index != InStructureId)
        {
            Build.Begin(InStructureId);
        }

        Assert.IsTrue(Build.IsActive, $"{InWhat}: 건축 모드 진입 실패 (재료 부족?)");
        var row = Build.CurrentRow;
        standNear(InPoint);
        Build.SetRotation(InYaw);
        Assert.IsTrue(Build.PlaceAt(InPoint), $"{InWhat}: 설치 실패 ({InPoint})");

        for (int i = 0; i < row.Costs.Length; ++i)
        {
            int spent;
            Spent.TryGetValue(row.Costs[i], out spent);
            Spent[row.Costs[i]] = spent + row.CostCounts[i];
        }

        Placed += 1;
        yield return WaitSeconds(PlaceInterval);
    }

    /// <summary>성 가운데 칸 기준 (InX, InZ) 칸의 중심.</summary>
    private Vector3 cell(int InX, int InZ)
    {
        return Center + new Vector3(InX * Grid, 0.0f, InZ * Grid);
    }

    /// <summary>외벽 위치: InAlongX 면 북/남 변의 InIndex 번째 칸 경계, 아니면 동/서 변. InSide = +1 북·동, -1 남·서.</summary>
    private Vector3 edge(int InIndex, int InSide, bool InAlongX)
    {
        float outer = CastleCells * 0.5f * Grid * InSide;
        return InAlongX == true ? Center + new Vector3(InIndex * Grid, 0.0f, outer) : Center + new Vector3(outer, 0.0f, InIndex * Grid);
    }

    /// <summary>칸 꼭짓점: (0,0) = 남서 바깥 모서리, (CastleCells, CastleCells) = 북동 바깥 모서리.</summary>
    private Vector3 corner(int InX, int InZ)
    {
        float origin = -CastleCells * 0.5f * Grid;
        return Center + new Vector3(origin + InX * Grid, 0.0f, origin + InZ * Grid);
    }

    private Vector3 snapToCell(Vector3 InPoint)
    {
        return new Vector3(Mathf.Round(InPoint.x / Grid) * Grid, InPoint.y, Mathf.Round(InPoint.z / Grid) * Grid);
    }

    /// <summary>부지 안의 채집 노드(나무 · 덤불 등)를 치운다.</summary>
    private static void clearSite(Vector3 InCenter, float InHalfSize)
    {
        foreach (var node in Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
        {
            var offset = node.transform.position - InCenter;
            if (Mathf.Abs(offset.x) <= InHalfSize && Mathf.Abs(offset.z) <= InHalfSize)
            {
                Object.Destroy(node.gameObject);
            }
        }
    }

    /// <summary>
    /// 설치 지점에서 성 안쪽으로 3m 떨어진 곳에 선다 (설치 거리 12m 제한 + 이미 지은 건물 콜라이더에 밀리는 것 대비).
    /// 가운데 근처 지점이면 그 남쪽 3m.
    /// </summary>
    private void standNear(Vector3 InPoint)
    {
        var toCenter = MathUtil.Flat(Center - InPoint);
        var spot = toCenter.magnitude > 3.5f ? InPoint + toCenter.normalized * 3.0f : InPoint + new Vector3(0.0f, 0.0f, -3.0f);
        standAt(spot);
    }

    private void standAt(Vector3 InPoint)
    {
        Player.Motor.Teleport(CombatUtil.SnapToGround(InPoint));
    }

    // ------------------------------------------------------------------ showcase

    /// <summary>
    /// 오후(15시, 비스듬한 햇빛 + 그림자)로 바꾸고 HUD 를 숨긴 뒤, 카메라가 성 가운데를 기준으로 높이서 한 바퀴 돈다 (10초).
    /// 햇빛 경고가 화면을 가리지 않도록 그동안만 플레이어를 무적(Cheat_God, 햇빛 노출 무시)으로 둔다. 끝나면 모두 되돌린다.
    /// 1/4 · 3/4 지점에서 화면을 Temp/CastleShowcase_1.png, _2.png 로 저장한다 (결과 확인용, git 제외 폴더).
    /// </summary>
    private IEnumerator showcase()
    {
        var dayNight = Managers.Get<DayNightManager>();
        var camera = Object.FindFirstObjectByType<TopDownCamera>();
        Assert.IsNotNull(camera, "TopDownCamera 가 없습니다");

        dayNight.SetHour(15.0f);
        Player.AddTag((int)ETag.Cheat_God);
        var hud = Managers.Get<Framework.UI.UIManager>().Get<HudWindow>();
        if (hud != null)
        {
            hud.Close();
        }

        standAt(cell(-1, -1));
        var anchor = new GameObject("CastleShowcaseAnchor");
        anchor.transform.position = CombatUtil.SnapToGround(Center) + Vector3.up * 2.0f;
        var previousTarget = camera.Target;
        camera.Target = anchor.transform;
        float startYaw = camera.YawDegrees;
        float startTime = Time.time;
        int captures = 0;
        try
        {
            yield return WaitSeconds(ShowcaseSeconds, () =>
            {
                float t = Mathf.Clamp01((Time.time - startTime) / ShowcaseSeconds);
                float distance = Mathf.Lerp(26.0f, 36.0f, Mathf.Sin(t * Mathf.PI));
                camera.SetView(startYaw + 360.0f * t, Mathf.Lerp(32.0f, 48.0f, t), distance);
                if ((captures == 0 && t >= 0.25f) || (captures == 1 && t >= 0.75f))
                {
                    captures += 1;
                    ScreenCapture.CaptureScreenshot($"Temp/CastleShowcase_{captures}.png");
                }
            });
        }
        finally
        {
            camera.Target = previousTarget;
            camera.ResetView();
            Object.Destroy(anchor);
            dayNight.SetHour(0.0f);
            Player.RemoveTag((int)ETag.Cheat_God);
            if (hud != null)
            {
                hud.Open();
            }
        }
    }
}
