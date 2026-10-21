using System;
using System.Collections.Generic;
using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Skills
{
    /// <summary>
    /// 모든 스킬 에셋의 목록. 네트워크로는 ushort ID 만 오가므로,
    /// 양쪽 피어가 같은 ID -> 같은 스킬로 해석하려면 이 테이블이 반드시 동일해야 한다.
    /// Resources/SkillDatabase.asset 하나만 두고 전역으로 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/Skills/Skill Database", fileName = "SkillDatabase")]
    public class SkillDatabase : ScriptableObject
    {
        private const string ResourcePath = "SkillDatabase";

        [SerializeField] private SkillDefinition[] skills = Array.Empty<SkillDefinition>();

        private Dictionary<ushort, SkillDefinition> _byId;
        private static SkillDatabase _instance;

        public IReadOnlyList<SkillDefinition> All => skills;

        public static SkillDatabase Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<SkillDatabase>(ResourcePath);
                if (_instance == null)
                    Debug.LogError($"[SkillDatabase] Resources/{ResourcePath}.asset 을 찾지 못했습니다. ActionRPG > 샘플 콘텐츠 생성 메뉴로 만들 수 있습니다.");
                else
                    _instance.BuildIndex();
                return _instance;
            }
        }

        private void OnEnable() => BuildIndex();

        public void BuildIndex()
        {
            _byId = new Dictionary<ushort, SkillDefinition>(skills.Length);
            for (int i = 0; i < skills.Length; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;
                if (skill.Id == 0)
                {
                    Debug.LogError($"[SkillDatabase] {skill.name} 의 Id 가 0 입니다. 1 이상으로 설정하세요.", skill);
                    continue;
                }
                if (_byId.TryGetValue(skill.Id, out var existing))
                {
                    Debug.LogError($"[SkillDatabase] Id {skill.Id} 가 {existing.name} 와 {skill.name} 에 중복되었습니다.", skill);
                    continue;
                }
                _byId.Add(skill.Id, skill);
            }
        }

        public SkillDefinition Get(ushort id)
        {
            if (id == 0) return null;
            if (_byId == null) BuildIndex();
            return _byId.TryGetValue(id, out var skill) ? skill : null;
        }

        public bool TryGet(ushort id, out SkillDefinition skill)
        {
            skill = Get(id);
            return skill != null;
        }

        /// <summary>덱 구성 UI 가 후보 목록을 받을 때 사용.</summary>
        public void GetAll(List<SkillDefinition> results)
        {
            results.Clear();
            for (int i = 0; i < skills.Length; i++)
                if (skills[i] != null) results.Add(skills[i]);
        }

        /// <summary>로드아웃이 실제로 유효한지 검사한다. 서버에서 클라이언트가 보낸 값을 믿기 전에 호출한다.</summary>
        public bool Validate(in SkillLoadout loadout, out string reason)
        {
            if (loadout.HasDuplicate())
            {
                reason = "같은 스킬을 두 칸에 넣을 수 없습니다.";
                return false;
            }

            for (int i = 0; i < GameConstants.SkillSlotCount; i++)
            {
                ushort id = loadout[i];
                if (id == 0) continue;
                if (Get(id) == null)
                {
                    reason = $"{i + 1}번 슬롯의 스킬 ID {id} 가 데이터베이스에 없습니다.";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        /// <summary>덱 코스트 합계.</summary>
        public int GetLoadoutCost(in SkillLoadout loadout)
        {
            int total = 0;
            for (int i = 0; i < GameConstants.SkillSlotCount; i++)
            {
                var skill = Get(loadout[i]);
                if (skill != null) total += skill.DeckCost;
            }
            return total;
        }

#if UNITY_EDITOR
        /// <summary>에디터 전용. 샘플 콘텐츠 생성기가 목록을 채울 때 쓴다.</summary>
        public void EditorSetSkills(SkillDefinition[] value)
        {
            skills = value;
            BuildIndex();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;
    }
}
