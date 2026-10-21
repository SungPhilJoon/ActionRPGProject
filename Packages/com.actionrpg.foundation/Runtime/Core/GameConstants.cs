using UnityEngine;

namespace ActionRPG.Core
{
    /// <summary>모든 시스템이 공유하는 상수 모음. 매직 넘버/문자열을 한 곳에 모아 둔다.</summary>
    public static class GameConstants
    {
        public const int MaxPlayers = 4;
        public const int SkillSlotCount = 4;
        public const int DeckCapacity = 4;

        /// <summary>스킬 하나를 쓰면 나머지 스킬에도 걸리는 최소 대기 시간.</summary>
        public const float GlobalCooldown = 0.3f;

        public const ushort DefaultPort = 7777;
        public const string DefaultAddress = "127.0.0.1";

        public static class Scenes
        {
            public const string Boot = "Boot";
            public const string MainMenu = "MainMenu";
            public const string Tutorial = "Tutorial";
            public const string Lobby = "Lobby";
            public const string Dungeon01 = "Dungeon_01";
        }

        public static class Layers
        {
            public const string Player = "Player";
            public const string Enemy = "Enemy";
            public const string Ground = "Ground";
            public const string Interactable = "Interactable";

            public static int PlayerMask => LayerMask.GetMask(Player);
            public static int EnemyMask => LayerMask.GetMask(Enemy);
            public static int GroundMask => LayerMask.GetMask(Ground, "Default");
            public static int CharacterMask => LayerMask.GetMask(Player, Enemy);
            public static int InteractableMask => LayerMask.GetMask(Interactable);
        }

        /// <summary>Animator 파라미터 해시. 이름이 없으면 드라이버가 조용히 무시한다.</summary>
        public static class Anim
        {
            public static readonly int Speed = Animator.StringToHash("Speed");
            public static readonly int MoveX = Animator.StringToHash("MoveX");
            public static readonly int MoveY = Animator.StringToHash("MoveY");
            public static readonly int Grounded = Animator.StringToHash("Grounded");
            public static readonly int Sprinting = Animator.StringToHash("Sprinting");
            public static readonly int WeaponType = Animator.StringToHash("WeaponType");
            public static readonly int Attack = Animator.StringToHash("Attack");
            public static readonly int AttackIndex = Animator.StringToHash("AttackIndex");
            public static readonly int CastSkill = Animator.StringToHash("CastSkill");
            public static readonly int SkillIndex = Animator.StringToHash("SkillIndex");
            public static readonly int Dodge = Animator.StringToHash("Dodge");
            public static readonly int Jump = Animator.StringToHash("Jump");
            public static readonly int Hit = Animator.StringToHash("Hit");
            public static readonly int Die = Animator.StringToHash("Die");
            public static readonly int Revive = Animator.StringToHash("Revive");
        }
    }
}
