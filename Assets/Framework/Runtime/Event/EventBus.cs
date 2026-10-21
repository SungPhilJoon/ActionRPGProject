using System;
using System.Collections.Generic;

namespace Framework
{
    /// <summary>이벤트 페이로드 표식. 이벤트 종류는 문자열이 아니라 struct 타입 자체로 구분한다.</summary>
    public interface IGameEvent
    {
    }

    /// <summary>
    /// 타입 기반 정적 이벤트 버스 (Observer 패턴).
    /// 제네릭 정적 클래스를 쓰면 이벤트 타입마다 별도의 리스너 목록이 JIT 시점에 만들어지므로
    /// Dictionary 조회나 박싱 없이 발행할 수 있다.
    /// 구독자는 반드시 OnDisable/UnInitialize 에서 해제해야 한다.
    /// </summary>
    public static class EventBus<T> where T : struct, IGameEvent
    {
        private static readonly List<Action<T>> Listeners = new List<Action<T>>();
        private static readonly List<Action<T>> PublishBuffer = new List<Action<T>>();

        public static void Subscribe(Action<T> InListener)
        {
            if (Listeners.Contains(InListener) == true)
            {
                return;
            }

            Listeners.Add(InListener);
        }

        public static void Unsubscribe(Action<T> InListener)
        {
            Listeners.Remove(InListener);
        }

        /// <summary>발행 중 구독/해제가 일어나도 안전하도록 버퍼에 복사한 뒤 호출한다.</summary>
        public static void Publish(T InEvent)
        {
            if (Listeners.Count == 0)
            {
                return;
            }

            PublishBuffer.Clear();
            PublishBuffer.AddRange(Listeners);
            for (int i = 0; i < PublishBuffer.Count; ++i)
            {
                try
                {
                    PublishBuffer[i].Invoke(InEvent);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        public static void Clear()
        {
            Listeners.Clear();
        }
    }
}
