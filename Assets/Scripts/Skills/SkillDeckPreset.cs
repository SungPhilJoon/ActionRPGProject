using System;
using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Skills
{
    /// <summary>
    /// 미리 만들어 둔 덱 프리셋. 튜토리얼 기본 덱이나 추천 빌드를 에셋으로 제공할 때 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Deck Preset", fileName = "DECK_NewPreset")]
    public class SkillDeckPreset : ScriptableObject
    {
        public string DisplayName = "기본 덱";
        [TextArea(2, 4)] public string Description;
        public Sprite Icon;

        [Tooltip("정확히 4칸. 비워 두면 해당 슬롯은 빈 칸이 된다.")]
        public SkillDefinition[] Skills = new SkillDefinition[GameConstants.SkillSlotCount];

        public SkillLoadout ToLoadout()
        {
            var loadout = new SkillLoadout();
            for (int i = 0; i < GameConstants.SkillSlotCount; i++)
            {
                if (Skills != null && i < Skills.Length && Skills[i] != null) loadout[i] = Skills[i].Id;
            }
            return loadout;
        }

        private void OnValidate()
        {
            if (Skills == null || Skills.Length != GameConstants.SkillSlotCount)
                Array.Resize(ref Skills, GameConstants.SkillSlotCount);
        }
    }

}
