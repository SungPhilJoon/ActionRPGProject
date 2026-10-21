using System;
using System.Collections;
using ActionRPG.Core;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>튜토리얼 스텝 묶음.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Tutorial/Sequence", fileName = "TUT_NewSequence")]
    public class TutorialSequence : ScriptableObject
    {
        public string DisplayName = "기본 조작 배우기";
        [TextArea(2, 4)] public string Intro;
        public TutorialStep[] Steps = Array.Empty<TutorialStep>();
    }
}
