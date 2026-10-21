using System.Collections.Generic;
using UnityEngine;

namespace Framework.Ability
{
    public struct GameplayCueParams
    {
        public AbilitySystemComponent Source;
        public AbilitySystemComponent Target;
        public Vector3 Location;
        public Vector3 Direction;
        public float Magnitude;
    }

    /// <summary>
    /// 언리얼 GameplayCue 대응. 게임플레이 결과(피해, 버프)와 연출(VFX/SFX)을 분리한다.
    /// 이펙트는 CueId(int) 만 알고, 실제 연출은 게임 쪽 핸들러가 담당한다.
    /// </summary>
    public interface IGameplayCueHandler
    {
        /// <summary>인스턴트 이펙트 / 주기 실행 시 1회.</summary>
        void OnExecute(GameplayCueParams InParams);
        /// <summary>지속형 이펙트가 붙었을 때.</summary>
        void OnActive(GameplayCueParams InParams);
        /// <summary>지속형 이펙트가 떨어졌을 때.</summary>
        void OnRemove(GameplayCueParams InParams);
    }

    public static class GameplayCueManager
    {
        private static readonly Dictionary<int, IGameplayCueHandler> Handlers = new Dictionary<int, IGameplayCueHandler>();

        /// <summary>등록되지 않은 큐를 처리할 기본 핸들러 (데이터 테이블 기반 VFX 스포너 등).</summary>
        public static IGameplayCueHandler Fallback;

        public static void Register(int InCueId, IGameplayCueHandler InHandler)
        {
            Handlers[InCueId] = InHandler;
        }

        public static void Clear()
        {
            Handlers.Clear();
            Fallback = null;
        }

        public static void Execute(int InCueId, GameplayCueParams InParams)
        {
            var handler = find(InCueId);
            if (handler != null)
            {
                handler.OnExecute(InParams);
            }
        }

        public static void Add(int InCueId, GameplayCueParams InParams)
        {
            var handler = find(InCueId);
            if (handler != null)
            {
                handler.OnActive(InParams);
            }
        }

        public static void Remove(int InCueId, GameplayCueParams InParams)
        {
            var handler = find(InCueId);
            if (handler != null)
            {
                handler.OnRemove(InParams);
            }
        }

        private static IGameplayCueHandler find(int InCueId)
        {
            if (InCueId == 0)
            {
                return null;
            }

            IGameplayCueHandler handler;
            if (Handlers.TryGetValue(InCueId, out handler) == true)
            {
                return handler;
            }

            return Fallback;
        }
    }
}
