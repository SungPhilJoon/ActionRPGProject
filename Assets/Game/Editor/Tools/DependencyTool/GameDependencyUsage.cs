using System;
using System.Collections.Generic;
using Framework.Editor;
using UnityEditor;

/// <summary>
/// 의존성 그래프에 "누가 이 주소를 쓰는지" 를 알려 준다 (IDependencyUsageProvider).
///  - AssetTable 행: 모든 런타임 로드는 AssetTable Index → 주소로 간다
///  - EUIAsset: UI 프리팹은 코드(GameBootstrap)가 enum 으로 요청
///  - GameTables: 부트스트랩이 AssetReference 로 로드
/// </summary>
public class GameDependencyUsage : IDependencyUsageProvider
{
    public void Collect(Dictionary<string, List<string>> OutUsers)
    {
        var tables = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        if (tables == null)
        {
            return;
        }

        foreach (var row in tables.Asset.All)
        {
            string user = $"AssetTable #{row.Index} ({row.Kind})";
            if (Enum.IsDefined(typeof(EUIAsset), (int)row.Index) == true)
            {
                user += $" · 코드 GameBootstrap → EUIAsset.{(EUIAsset)(int)row.Index}";
            }

            add(OutUsers, row.Address, user);
        }

        add(OutUsers, "Tables/GameTables", "GameBootstrap.TablesReference (게임 시작 시 가장 먼저 로드)");
    }

    private static void add(Dictionary<string, List<string>> InUsers, string InAddress, string InUser)
    {
        if (string.IsNullOrEmpty(InAddress) == true)
        {
            return;
        }

        List<string> list;
        if (InUsers.TryGetValue(InAddress, out list) == false)
        {
            list = new List<string>();
            InUsers.Add(InAddress, list);
        }

        list.Add(InUser);
    }
}
