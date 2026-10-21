using System.Diagnostics;

namespace Framework
{
    /// <summary>
    /// 프로젝트 공용 로그. 에디터/개발 빌드에서만 Message 가 남도록 Conditional 로 묶는다.
    /// </summary>
    public static class Log
    {
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Message(string InMessage)
        {
            UnityEngine.Debug.Log(InMessage);
        }

        public static void Warning(string InMessage)
        {
            UnityEngine.Debug.LogWarning(InMessage);
        }

        public static void Error(string InMessage)
        {
            UnityEngine.Debug.LogError(InMessage);
        }
    }
}
