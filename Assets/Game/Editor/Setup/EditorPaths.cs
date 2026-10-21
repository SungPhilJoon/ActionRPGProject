/// <summary>에디터 툴이 쓰는 경로 모음 (런타임 코드는 경로를 모른다).</summary>
public static class EditorPaths
{
    public const string Root = "Assets/Game";
    public const string Csv = Root + "/Data/CSV";
    public const string Tables = Root + "/Data/Tables";
    public const string GameTablesAsset = Tables + "/GameTables.asset";

    /// <summary>Addressables 루트. 이 폴더 아래 파일의 "상대 경로(확장자 제외)" 가 그대로 주소가 된다.</summary>
    public const string Content = Root + "/Content";
    public const string Generated = Root + "/Art/Generated";
    public const string Kenney = Root + "/Art/ThirdParty/Kenney";
    public const string PolyHaven = Root + "/Art/ThirdParty/PolyHaven";
    public const string Scenes = Root + "/Scenes";
    public const string WorldScene = Scenes + "/World.unity";
    public const string UIScripts = Root + "/Runtime/UI/Windows";
    public const string UIPrefabs = Root + "/Prefabs/UI";
}
