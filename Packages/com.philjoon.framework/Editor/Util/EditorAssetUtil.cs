using System.IO;
using UnityEditor;

namespace Framework.Editor
{
    /// <summary>에디터 공용 에셋 유틸.</summary>
    public static class EditorAssetUtil
    {
        /// <summary>"Assets/A/B/C" 폴더를 부모부터 순서대로 만든다 (이미 있으면 무시).</summary>
        public static void EnsureFolder(string InPath)
        {
            if (AssetDatabase.IsValidFolder(InPath) == true)
            {
                return;
            }

            string parent = Path.GetDirectoryName(InPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(InPath));
        }
    }
}
