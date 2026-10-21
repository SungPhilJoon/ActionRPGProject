using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Core.AbilitySystem
{
    /// <summary>
    /// 언리얼 UGameplayCueNotify_Static. 큐 태그 하나에 대한 연출을 에셋으로 정의한다.
    /// <see cref="AbilitySystemGlobals"/> 의 목록에 넣어 두면 자동 등록된다.
    /// </summary>
    public abstract class GameplayCueNotify : ScriptableObject
    {
        public GameplayTag GameplayCueTag;

        protected virtual void OnEnable()
        {
            if (!string.IsNullOrEmpty(GameplayCueTag.TagName)) GameplayCueTag = GameplayTag.Request(GameplayCueTag.TagName);
        }

        public void HandleGameplayCue(GameObject target, GameplayCueEvent eventType, in GameplayCueParameters parameters)
        {
            switch (eventType)
            {
                case GameplayCueEvent.OnActive: OnActive(target, parameters); break;
                case GameplayCueEvent.WhileActive: WhileActive(target, parameters); break;
                case GameplayCueEvent.Executed: OnExecute(target, parameters); break;
                case GameplayCueEvent.Removed: OnRemove(target, parameters); break;
            }
        }

        public virtual void OnExecute(GameObject target, in GameplayCueParameters parameters) { }
        public virtual void OnActive(GameObject target, in GameplayCueParameters parameters) { }
        public virtual void WhileActive(GameObject target, in GameplayCueParameters parameters) { }
        public virtual void OnRemove(GameObject target, in GameplayCueParameters parameters) { }
    }
}
