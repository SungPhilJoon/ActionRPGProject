using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>처음 한 번: Game/Setup/0. Run All → 레이어, CSV, 콘텐츠, Addressables, 월드 씬을 순서대로 만든다.</summary>
public static class GameSetupMenu
{
    [MenuItem("Game/Setup/0. Run All (First Time)", priority = 0)]
    public static void RunAll()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
        {
            return;
        }

        ProjectSetup.Run();
        CsvTableImporter.ImportAll();
        ContentBuilder.BuildAll();
        MonsterStateTreeDefaults.CreateAllSilently();
        AddressablesSetup.Run();
        CsvTableImporter.Validate();
        WorldBuilder.Build();
        Debug.Log("[Setup] done. Open Assets/Game/Scenes/World.unity and press Play.");
    }

    [MenuItem("Game/Setup/2. Import CSV", priority = 2)]
    public static void ImportCsv()
    {
        CsvTableImporter.ImportAll();
    }

    [MenuItem("Game/Open World Scene", priority = 20)]
    public static void OpenWorld()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == true)
        {
            EditorSceneManager.OpenScene(EditorPaths.WorldScene);
        }
    }
}
