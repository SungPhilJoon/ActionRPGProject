using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.Editor
{
    /// <summary>
    /// StaticBehaviour / DynamicBehaviour 의 Static 플래그를 저장 시점에 다시 맞춘다.
    /// 인스펙터에서 붙이면 Reset/OnValidate 가 바로 맞추지만, 툴 · 스크립트의 AddComponent 는 그 콜백이 없으므로 여기서 잡는다.
    ///  - 씬 저장 직전 (EditorSceneManager.sceneSaving): 그 씬의 모든 Static/Dynamic Behaviour
    ///  - 프리팹 모드 저장 직전 (PrefabStage.prefabSaving): 그 프리팹 루트 아래 전부
    ///  - 메뉴 Window > Framework > Rendering > Apply Static Flags (Open Scenes): 열린 씬 전부를 지금
    /// </summary>
    [InitializeOnLoad]
    internal static class StaticBehaviourFlagsHook
    {
        static StaticBehaviourFlagsHook()
        {
            EditorSceneManager.sceneSaving += onSceneSaving;
            PrefabStage.prefabSaving += onPrefabSaving;
        }

        [MenuItem("Window/Framework/Rendering/Apply Static Flags (Open Scenes)")]
        private static void applyOpenScenes()
        {
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                count += applyScene(SceneManager.GetSceneAt(i));
            }

            Debug.Log($"Static/Dynamic Behaviour {count} 개의 Static 플래그를 맞췄습니다.");
        }

        private static void onSceneSaving(Scene InScene, string InPath)
        {
            applyScene(InScene);
        }

        private static void onPrefabSaving(GameObject InPrefabRoot)
        {
            applyUnder(InPrefabRoot);
        }

        private static int applyScene(Scene InScene)
        {
            if (InScene.isLoaded == false)
            {
                return 0;
            }

            int count = 0;
            foreach (var root in InScene.GetRootGameObjects())
            {
                count += applyUnder(root);
            }

            return count;
        }

        private static int applyUnder(GameObject InRoot)
        {
            int count = 0;
            foreach (var behaviour in InRoot.GetComponentsInChildren<StaticBehaviour>(true))
            {
                StaticEditorFlagsUtil.Apply(behaviour);
                count += 1;
            }

            foreach (var behaviour in InRoot.GetComponentsInChildren<DynamicBehaviour>(true))
            {
                StaticEditorFlagsUtil.Apply(behaviour);
                count += 1;
            }

            return count;
        }
    }
}
