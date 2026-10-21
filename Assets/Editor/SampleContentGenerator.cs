using System.Collections.Generic;
using System.IO;
using ActionRPG.Abilities;
using ActionRPG.AI;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using ActionRPG.Core.EditorTools.AbilitySystem;
using ActionRPG.Skills;
using ActionRPG.Skills.Effects;
using ActionRPG.Tutorial;
using UnityEditor;
using UnityEngine;

namespace ActionRPG.EditorTools
{
    /// <summary>
    /// 프레임워크를 바로 굴려 볼 수 있는 샘플 데이터를 한 번에 만든다.
    /// 이미 있는 에셋은 다시 만들지 않고 값만 갱신하므로 여러 번 눌러도 안전하다.
    ///
    /// GAS 구성:
    ///   Assets/Data/Abilities/Calculations  — EXEC_Damage, EXEC_Heal, MMC_Cooldown_*
    ///   Assets/Data/Abilities/Effects       — GE_Damage, GE_Heal, GE_Cost_Mana, GE_Cooldown_*, GE_Invulnerable,
    ///                                         GE_ManaRegen, GE_InitStats_Player, 상태이상(GE_Stun · Poison · Slow · Rally)
    ///   Assets/Data/Abilities               — GA_BasicAttack, AS_Player, AS_Enemy
    ///   Assets/Resources                    — ActionRPGGameData, AbilitySystemGlobals, SkillDatabase
    /// </summary>
    public static class SampleContentGenerator
    {
        private const string DataRoot = "Assets/Data";
        private const string SkillPath = DataRoot + "/Skills";
        private const string EffectPath = DataRoot + "/Skills/Effects";
        private const string LegacyStatusPath = DataRoot + "/Skills/Status";
        private const string AbilityPath = DataRoot + "/Abilities";
        private const string GameplayEffectPath = AbilityPath + "/Effects";
        private const string CalculationPath = AbilityPath + "/Calculations";
        private const string EnemyPath = DataRoot + "/Enemies";
        private const string TutorialPath = DataRoot + "/Tutorial";
        private const string ResourcePath = "Assets/Resources";

        [MenuItem("ActionRPG/샘플 콘텐츠 생성 (스킬 · 적 · 튜토리얼)", priority = 10)]
        public static void Generate()
        {
            EnsureFolders();
            GameTags.Initialize();
            DeleteLegacyStatusAssets();

            var gas = CreateAbilitySystemAssets();
            var skills = CreateSkills(gas);
            var database = CreateDatabase(skills);
            CreateDeckPresets(skills);
            CreateEnemies(gas);
            CreateTutorialSequence();

            AssetDatabase.SaveAssets();
            AbilitySystemGlobalsBuilder.Refresh();
            AssetDatabase.Refresh();

            Debug.Log($"[ActionRPG] 샘플 콘텐츠 생성 완료. 스킬 {skills.Count}개, 데이터베이스: {AssetDatabase.GetAssetPath(database)}");
            Selection.activeObject = database;
        }

        // ---------------------------------------------------------------- 폴더

        private static void EnsureFolders()
        {
            foreach (var path in new[] { DataRoot, SkillPath, EffectPath, AbilityPath, GameplayEffectPath, CalculationPath, EnemyPath, TutorialPath, ResourcePath })
                EnsureFolder(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>이미 있으면 그대로 쓰고, 없으면 만든다.</summary>
        private static T GetOrCreate<T>(string folder, string assetName) where T : ScriptableObject
        {
            string path = $"{folder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        /// <summary>GAS 이전의 StatusEffectDefinition 에셋(SE_*)은 스크립트가 없어졌으므로 지운다.</summary>
        private static void DeleteLegacyStatusAssets()
        {
            if (!AssetDatabase.IsValidFolder(LegacyStatusPath)) return;
            foreach (var name in new[] { "SE_Stun", "SE_Poison", "SE_Slow", "SE_Rally" })
            {
                string path = $"{LegacyStatusPath}/{name}.asset";
                if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            }
            if (AssetDatabase.FindAssets(string.Empty, new[] { LegacyStatusPath }).Length == 0) AssetDatabase.DeleteAsset(LegacyStatusPath);
        }

        // ================================================================ GAS 에셋

        private class GasAssets
        {
            public GameplayEffect Damage, Heal, CostMana, CooldownSkill, CooldownGlobal, CooldownBasicAttack, Invulnerable, ManaRegen, InitPlayer;
            public GameplayEffect Stun, Poison, Slow, Rally;
            public GA_BasicAttack BasicAttack;
            public AbilitySet PlayerSet, EnemySet;
        }

        private static GasAssets CreateAbilitySystemAssets()
        {
            var gas = new GasAssets();

            // ---- 계산 클래스
            var damageExec = GetOrCreate<DamageExecution>(CalculationPath, "EXEC_Damage");
            var healExec = GetOrCreate<HealExecution>(CalculationPath, "EXEC_Heal");
            var skillCooldownCalc = GetOrCreate<CooldownMagnitudeCalculation>(CalculationPath, "MMC_Cooldown_Skill");
            skillCooldownCalc.ApplyCooldownReduction = true;
            skillCooldownCalc.DivideByAttackSpeed = false;
            EditorUtility.SetDirty(skillCooldownCalc);
            var attackCooldownCalc = GetOrCreate<CooldownMagnitudeCalculation>(CalculationPath, "MMC_Cooldown_BasicAttack");
            attackCooldownCalc.ApplyCooldownReduction = false;
            attackCooldownCalc.DivideByAttackSpeed = true;
            EditorUtility.SetDirty(attackCooldownCalc);

            // ---- 공용 이펙트
            gas.Damage = Effect("GE_Damage", GameplayEffectDurationType.Instant, "피해", "DamageExecution 으로 피해를 계산한다.");
            gas.Damage.Executions.Add(new GameplayEffectExecutionDefinition { CalculationClass = damageExec });
            gas.Damage.AssetTags.AddTag(GameplayTag.Request("Effect.Damage"));

            gas.Heal = Effect("GE_Heal", GameplayEffectDurationType.Instant, "회복", "HealExecution 으로 회복량을 계산한다.");
            gas.Heal.Executions.Add(new GameplayEffectExecutionDefinition { CalculationClass = healExec });
            gas.Heal.AssetTags.AddTag(GameplayTag.Request("Effect.Heal"));

            gas.CostMana = Effect("GE_Cost_Mana", GameplayEffectDurationType.Instant, "마나 비용", "SetByCaller.ManaCost(음수)만큼 마나를 쓴다.");
            gas.CostMana.Modifiers.Add(Modifier(ManaSet.ManaAttribute, GameplayModOp.Additive,
                GameplayEffectModifierMagnitude.FromSetByCaller(GameTags.SetByCaller_ManaCost)));

            gas.CooldownSkill = Effect("GE_Cooldown_Skill", GameplayEffectDurationType.HasDuration, "스킬 쿨다운",
                "길이 = SetByCaller.Cooldown × (1 - 쿨다운 감소). 스킬별 태그(Cooldown.Skill.<Id>)는 스펙에 동적으로 붙는다.");
            gas.CooldownSkill.DurationMagnitude = GameplayEffectModifierMagnitude.FromCustom(skillCooldownCalc);
            gas.CooldownSkill.GrantedTags.AddTag(GameTags.Cooldown_Skill);

            gas.CooldownGlobal = Effect("GE_Cooldown_Global", GameplayEffectDurationType.HasDuration, "글로벌 쿨다운", "모든 스킬 공용 짧은 대기.");
            gas.CooldownGlobal.DurationMagnitude = GameplayEffectModifierMagnitude.FromScalable(GameConstants.GlobalCooldown);
            gas.CooldownGlobal.GrantedTags.AddTag(GameTags.Cooldown_Global);

            gas.CooldownBasicAttack = Effect("GE_Cooldown_BasicAttack", GameplayEffectDurationType.HasDuration, "평타 쿨다운",
                "길이 = SetByCaller.Cooldown ÷ 공격 속도.");
            gas.CooldownBasicAttack.DurationMagnitude = GameplayEffectModifierMagnitude.FromCustom(attackCooldownCalc);
            gas.CooldownBasicAttack.GrantedTags.AddTag(GameTags.Cooldown_BasicAttack);

            gas.Invulnerable = Effect("GE_Invulnerable", GameplayEffectDurationType.HasDuration, "무적", "SetByCaller.Duration 동안 피해를 받지 않는다.");
            gas.Invulnerable.DurationMagnitude = GameplayEffectModifierMagnitude.FromSetByCaller(GameTags.SetByCaller_Duration);
            gas.Invulnerable.GrantedTags.AddTag(GameTags.State_Invulnerable);

            const float regenPeriod = 0.25f;
            gas.ManaRegen = Effect("GE_ManaRegen", GameplayEffectDurationType.Infinite, "마나 재생", "0.25초마다 ManaRegen × 0.25 만큼 마나를 채운다.");
            gas.ManaRegen.Period = new ScalableFloat(regenPeriod);
            gas.ManaRegen.bExecutePeriodicEffectOnApplication = false;
            gas.ManaRegen.Modifiers.Add(Modifier(ManaSet.ManaAttribute, GameplayModOp.Additive,
                GameplayEffectModifierMagnitude.FromAttribute(ManaSet.ManaRegenAttribute, GameplayEffectAttributeCaptureSource.Target, false, regenPeriod)));

            gas.InitPlayer = Effect("GE_InitStats_Player", GameplayEffectDurationType.Instant, "플레이어 초기 능력치",
                "스폰 시 한 번 적용해 어트리뷰트 BaseValue 를 덮어쓴다 (언리얼 GE_DefaultAttributes).");
            AddOverrideStats(gas.InitPlayer, StatBlock.DefaultPlayer);

            // ---- 상태이상
            gas.Stun = Effect("GE_Stun", GameplayEffectDurationType.HasDuration, "기절", "잠시 아무것도 할 수 없다.");
            gas.Stun.DurationMagnitude = GameplayEffectModifierMagnitude.FromScalable(1.2f);
            gas.Stun.GrantedTags.AddTag(GameTags.State_Debuff_Stun);
            gas.Stun.AssetTags.AddTag(GameTags.State_Debuff);
            gas.Stun.StackingType = GameplayEffectStackingType.AggregateByTarget;
            gas.Stun.StackLimitCount = 1;

            gas.Poison = Effect("GE_Poison", GameplayEffectDurationType.HasDuration, "중독", "초당 지속 피해를 입는다. 3번까지 중첩.");
            gas.Poison.DurationMagnitude = GameplayEffectModifierMagnitude.FromScalable(6f);
            gas.Poison.Period = new ScalableFloat(1f);
            gas.Poison.bExecutePeriodicEffectOnApplication = false;
            gas.Poison.Modifiers.Add(Modifier(HealthSet.IncomingDamageAttribute, GameplayModOp.Additive, GameplayEffectModifierMagnitude.FromScalable(4f)));
            gas.Poison.StackingType = GameplayEffectStackingType.AggregateByTarget;
            gas.Poison.StackLimitCount = 3;
            gas.Poison.GrantedTags.AddTag(GameTags.State_Debuff_Poison);
            gas.Poison.AssetTags.AddTag(GameTags.State_Debuff);
            gas.Poison.AssetTags.AddTag(GameTags.Damage_Type_Magical);

            gas.Slow = Effect("GE_Slow", GameplayEffectDurationType.HasDuration, "둔화", "이동 속도가 40% 감소한다.");
            gas.Slow.DurationMagnitude = GameplayEffectModifierMagnitude.FromScalable(3f);
            gas.Slow.Modifiers.Add(Modifier(CombatSet.MoveSpeedAttribute, GameplayModOp.Multiplicitive, GameplayEffectModifierMagnitude.FromScalable(0.6f)));
            gas.Slow.GrantedTags.AddTag(GameTags.State_Debuff_Slow);
            gas.Slow.AssetTags.AddTag(GameTags.State_Debuff);
            gas.Slow.StackingType = GameplayEffectStackingType.AggregateByTarget;
            gas.Slow.StackLimitCount = 1;

            gas.Rally = Effect("GE_Rally", GameplayEffectDurationType.HasDuration, "전투의 함성", "공격력과 주문력이 25% 증가한다.");
            gas.Rally.DurationMagnitude = GameplayEffectModifierMagnitude.FromScalable(10f);
            gas.Rally.Modifiers.Add(Modifier(CombatSet.AttackPowerAttribute, GameplayModOp.Multiplicitive, GameplayEffectModifierMagnitude.FromScalable(1.25f)));
            gas.Rally.Modifiers.Add(Modifier(CombatSet.AbilityPowerAttribute, GameplayModOp.Multiplicitive, GameplayEffectModifierMagnitude.FromScalable(1.25f)));
            gas.Rally.GrantedTags.AddTag(GameTags.State_Buff_Rally);
            gas.Rally.AssetTags.AddTag(GameTags.State_Buff);
            gas.Rally.StackingType = GameplayEffectStackingType.AggregateByTarget;
            gas.Rally.StackLimitCount = 1;

            foreach (var effect in new[] { gas.Damage, gas.Heal, gas.CostMana, gas.CooldownSkill, gas.CooldownGlobal, gas.CooldownBasicAttack,
                         gas.Invulnerable, gas.ManaRegen, gas.InitPlayer, gas.Stun, gas.Poison, gas.Slow, gas.Rally })
                EditorUtility.SetDirty(effect);

            // ---- 평타 어빌리티
            gas.BasicAttack = GetOrCreate<GA_BasicAttack>(AbilityPath, "GA_BasicAttack");
            gas.BasicAttack.NetExecutionPolicy = GameplayAbilityNetExecutionPolicy.LocalPredicted;
            gas.BasicAttack.InstancingPolicy = GameplayAbilityInstancingPolicy.InstancedPerActor;
            gas.BasicAttack.CooldownGameplayEffect = gas.CooldownBasicAttack;
            EditorUtility.SetDirty(gas.BasicAttack);

            // ---- 어빌리티 세트 (라이라 AbilitySet)
            gas.PlayerSet = GetOrCreate<AbilitySet>(AbilityPath, "AS_Player");
            gas.PlayerSet.GrantedAttributes = new[] { AttributeEntry<HealthSet>(), AttributeEntry<ManaSet>(), AttributeEntry<CombatSet>() };
            gas.PlayerSet.GrantedGameplayAbilities = new[]
            {
                new AbilitySet.AbilityEntry { Ability = gas.BasicAttack, AbilityLevel = 1, InputID = AbilityInputID.BasicAttack }
            };
            gas.PlayerSet.GrantedGameplayEffects = new[]
            {
                new AbilitySet.EffectEntry { GameplayEffect = gas.InitPlayer, EffectLevel = 1f },
                new AbilitySet.EffectEntry { GameplayEffect = gas.ManaRegen, EffectLevel = 1f }
            };
            EditorUtility.SetDirty(gas.PlayerSet);

            gas.EnemySet = GetOrCreate<AbilitySet>(AbilityPath, "AS_Enemy");
            gas.EnemySet.GrantedAttributes = new[] { AttributeEntry<HealthSet>(), AttributeEntry<CombatSet>() };
            gas.EnemySet.GrantedGameplayAbilities = new AbilitySet.AbilityEntry[0];
            gas.EnemySet.GrantedGameplayEffects = new AbilitySet.EffectEntry[0];
            EditorUtility.SetDirty(gas.EnemySet);

            // ---- 게임 데이터
            var gameData = GetOrCreate<ActionRPGGameData>(ResourcePath, ActionRPGGameData.ResourcePath);
            gameData.DamageGameplayEffect_SetByCaller = gas.Damage;
            gameData.HealGameplayEffect_SetByCaller = gas.Heal;
            gameData.ManaCostGameplayEffect = gas.CostMana;
            gameData.SkillCooldownGameplayEffect = gas.CooldownSkill;
            gameData.GlobalCooldownGameplayEffect = gas.CooldownGlobal;
            gameData.InvulnerableGameplayEffect = gas.Invulnerable;
            EditorUtility.SetDirty(gameData);

            return gas;
        }

        private static GameplayEffect Effect(string assetName, GameplayEffectDurationType duration, string displayName, string description)
        {
            var effect = GetOrCreate<GameplayEffect>(GameplayEffectPath, assetName);
            effect.DurationPolicy = duration;
            effect.DurationMagnitude = GameplayEffectModifierMagnitude.FromScalable(0f);
            effect.Period = new ScalableFloat(0f);
            effect.bExecutePeriodicEffectOnApplication = true;
            effect.Modifiers.Clear();
            effect.Executions.Clear();
            effect.AssetTags.Reset();
            effect.GrantedTags.Reset();
            effect.StackingType = GameplayEffectStackingType.None;
            effect.StackLimitCount = 1;
            effect.DisplayName = displayName;
            effect.Description = description;
            return effect;
        }

        private static GameplayModifierInfo Modifier(GameplayAttribute attribute, GameplayModOp op, GameplayEffectModifierMagnitude magnitude) =>
            new GameplayModifierInfo { Attribute = attribute, ModifierOp = op, ModifierMagnitude = magnitude };

        private static void AddOverrideStats(GameplayEffect effect, StatBlock stats)
        {
            void Override(GameplayAttribute attribute, float value) =>
                effect.Modifiers.Add(Modifier(attribute, GameplayModOp.Override, GameplayEffectModifierMagnitude.FromScalable(value)));

            // MaxHealth 를 먼저 올려야 Health 가 잘리지 않는다.
            Override(HealthSet.MaxHealthAttribute, stats.MaxHealth);
            Override(HealthSet.HealthAttribute, stats.MaxHealth);
            Override(ManaSet.MaxManaAttribute, stats.MaxMana);
            Override(ManaSet.ManaAttribute, stats.MaxMana);
            Override(ManaSet.ManaRegenAttribute, stats.ManaRegen);
            Override(CombatSet.AttackPowerAttribute, stats.AttackPower);
            Override(CombatSet.AbilityPowerAttribute, stats.AbilityPower);
            Override(CombatSet.DefenseAttribute, stats.Defense);
            Override(CombatSet.MoveSpeedAttribute, stats.MoveSpeed);
            Override(CombatSet.AttackSpeedAttribute, stats.AttackSpeed);
            Override(CombatSet.CritChanceAttribute, stats.CritChance);
            Override(CombatSet.CritDamageAttribute, stats.CritDamage);
            Override(CombatSet.CooldownReductionAttribute, stats.CooldownReduction);
        }

        private static AbilitySet.AttributeSetEntry AttributeEntry<T>() where T : AttributeSet =>
            new AbilitySet.AttributeSetEntry { AttributeSetType = $"{typeof(T).FullName}, {typeof(T).Assembly.GetName().Name}" };

        // ================================================================ 스킬

        private static List<SkillDefinition> CreateSkills(GasAssets gas)
        {
            var list = new List<SkillDefinition>();

            // 1. 강타 — 검, 단일 부채꼴
            {
                var effect = GetOrCreate<ArcDamageEffect>(EffectPath, "EF_HeavySlash");
                effect.Radius = 3.2f;
                effect.ArcDegrees = 110f;
                effect.FlatDamage = 12f;
                effect.AttackPowerScale = 1.6f;
                effect.Knockback = 3f;
                EditorUtility.SetDirty(effect);

                list.Add(MakeSkill(gas, 1, "SK_HeavySlash", "강타", "앞을 크게 베어 강한 피해를 준다.",
                    SkillCategory.Attack, WeaponType.Sword, SkillTargeting.Direction,
                    cooldown: 5f, mana: 12f, range: 3.2f, cost: 1, effects: new SkillEffect[] { effect }));
            }

            // 2. 회전베기 — 검, 2단 360도
            {
                var first = GetOrCreate<ArcDamageEffect>(EffectPath, "EF_Whirlwind1");
                first.Radius = 3.6f;
                first.ArcDegrees = 360f;
                first.FlatDamage = 8f;
                first.AttackPowerScale = 0.9f;
                first.Knockback = 1f;
                first.DelaySeconds = 0f;
                EditorUtility.SetDirty(first);

                var second = GetOrCreate<ArcDamageEffect>(EffectPath, "EF_Whirlwind2");
                second.Radius = 3.6f;
                second.ArcDegrees = 360f;
                second.FlatDamage = 8f;
                second.AttackPowerScale = 0.9f;
                second.Knockback = 3f;
                second.DelaySeconds = 0.35f;
                EditorUtility.SetDirty(second);

                list.Add(MakeSkill(gas, 2, "SK_Whirlwind", "회전베기", "제자리에서 두 번 회전하며 주위를 모두 벤다.",
                    SkillCategory.Attack, WeaponType.Sword, SkillTargeting.Self,
                    cooldown: 9f, mana: 22f, range: 0f, cost: 2,
                    effects: new SkillEffect[] { first, second }, movementLock: 0.7f));
            }

            // 3. 방패돌진 — 검, 돌진 + 기절
            {
                var dash = GetOrCreate<DashEffect>(EffectPath, "EF_ChargeDash");
                dash.Distance = 7f;
                dash.Duration = 0.28f;
                dash.InvulnerableDuringDash = true;
                EditorUtility.SetDirty(dash);

                var impact = GetOrCreate<ArcDamageEffect>(EffectPath, "EF_ChargeImpact");
                impact.DelaySeconds = 0.28f;
                impact.Radius = 2.8f;
                impact.ArcDegrees = 140f;
                impact.FlatDamage = 10f;
                impact.AttackPowerScale = 1.1f;
                impact.Knockback = 5f;
                impact.ApplyStatus = gas.Stun;
                EditorUtility.SetDirty(impact);

                list.Add(MakeSkill(gas, 3, "SK_ShieldCharge", "방패 돌진", "앞으로 돌진해 부딪힌 적을 기절시킨다.",
                    SkillCategory.Mobility, WeaponType.Sword, SkillTargeting.Direction,
                    cooldown: 11f, mana: 18f, range: 7f, cost: 2,
                    effects: new SkillEffect[] { dash, impact }, movementLock: 0f));
            }

            // 4. 대지 가르기 — 검, 지면 장판
            {
                var area = GetOrCreate<AreaDamageEffect>(EffectPath, "EF_EarthRender");
                area.DelaySeconds = 0.45f;
                area.Radius = 4.5f;
                area.CenterOnCaster = false;
                area.FlatDamage = 22f;
                area.AttackPowerScale = 1.3f;
                area.DamageType = DamageType.Physical;
                area.ApplyStatus = gas.Slow;
                EditorUtility.SetDirty(area);

                list.Add(MakeSkill(gas, 4, "SK_EarthRender", "대지 가르기", "지정한 지점의 땅을 갈라 큰 피해를 주고 둔화시킨다.",
                    SkillCategory.Ultimate, WeaponType.Sword, SkillTargeting.GroundPoint,
                    cooldown: 14f, mana: 30f, range: 9f, cost: 3,
                    effects: new SkillEffect[] { area }, movementLock: 0.6f, castTime: 0.35f));
            }

            // 5. 강궁 — 활, 관통 화살
            {
                var shot = GetOrCreate<ProjectileEffect>(EffectPath, "EF_PowerShot");
                shot.Count = 1;
                shot.Speed = 42f;
                shot.FlatDamage = 14f;
                shot.AttackPowerScale = 1.7f;
                EditorUtility.SetDirty(shot);

                list.Add(MakeSkill(gas, 5, "SK_PowerShot", "강궁", "시위를 끝까지 당겨 강력한 화살을 쏜다.",
                    SkillCategory.Attack, WeaponType.Bow, SkillTargeting.Direction,
                    cooldown: 6f, mana: 14f, range: 30f, cost: 1,
                    effects: new SkillEffect[] { shot }, castTime: 0.3f, movementLock: 0.4f));
            }

            // 6. 다중 사격 — 활, 부채꼴 5발
            {
                var volley = GetOrCreate<ProjectileEffect>(EffectPath, "EF_Multishot");
                volley.Count = 5;
                volley.SpreadDegrees = 40f;
                volley.Speed = 30f;
                volley.FlatDamage = 6f;
                volley.AttackPowerScale = 0.7f;
                EditorUtility.SetDirty(volley);

                list.Add(MakeSkill(gas, 6, "SK_Multishot", "다중 사격", "부채꼴로 화살 다섯 발을 동시에 날린다.",
                    SkillCategory.Attack, WeaponType.Bow, SkillTargeting.Direction,
                    cooldown: 10f, mana: 20f, range: 25f, cost: 2,
                    effects: new SkillEffect[] { volley }, castTime: 0.25f, movementLock: 0.4f));
            }

            // 7. 화살비 — 활, 지면 장판 2연타
            {
                var first = GetOrCreate<AreaDamageEffect>(EffectPath, "EF_ArrowRain1");
                first.DelaySeconds = 0.8f;
                first.Radius = 5f;
                first.FlatDamage = 12f;
                first.AttackPowerScale = 0.8f;
                EditorUtility.SetDirty(first);

                var second = GetOrCreate<AreaDamageEffect>(EffectPath, "EF_ArrowRain2");
                second.DelaySeconds = 1.6f;
                second.Radius = 5f;
                second.FlatDamage = 12f;
                second.AttackPowerScale = 0.8f;
                EditorUtility.SetDirty(second);

                list.Add(MakeSkill(gas, 7, "SK_ArrowRain", "화살비", "지정한 지역에 화살을 두 차례 퍼붓는다.",
                    SkillCategory.Ultimate, WeaponType.Bow, SkillTargeting.GroundPoint,
                    cooldown: 16f, mana: 32f, range: 18f, cost: 3,
                    effects: new SkillEffect[] { first, second }, castTime: 0.4f, movementLock: 0.5f));
            }

            // 8. 독화살 — 활, 중독
            {
                var shot = GetOrCreate<ProjectileEffect>(EffectPath, "EF_VenomArrow");
                shot.Count = 1;
                shot.Speed = 34f;
                shot.FlatDamage = 8f;
                shot.AttackPowerScale = 0.9f;
                shot.OnHitStatus = gas.Poison;
                EditorUtility.SetDirty(shot);

                list.Add(MakeSkill(gas, 8, "SK_VenomArrow", "독화살", "맞은 적을 중독시키는 화살. 최대 3번까지 중첩된다.",
                    SkillCategory.Control, WeaponType.Bow, SkillTargeting.Direction,
                    cooldown: 8f, mana: 16f, range: 28f, cost: 2,
                    effects: new SkillEffect[] { shot }, castTime: 0.25f, movementLock: 0.35f));
            }

            // 9. 응급 치료 — 공용, 파티 회복
            {
                var heal = GetOrCreate<HealEffect>(EffectPath, "EF_FieldDressing");
                heal.IncludeSelf = true;
                heal.AllyRadius = 8f;
                heal.FlatHeal = 25f;
                heal.AbilityPowerScale = 1.2f;
                EditorUtility.SetDirty(heal);

                list.Add(MakeSkill(gas, 9, "SK_FieldDressing", "응급 치료", "주변 아군의 체력을 회복시킨다.",
                    SkillCategory.Support, WeaponType.Unarmed, SkillTargeting.Self,
                    cooldown: 20f, mana: 35f, range: 8f, cost: 2,
                    effects: new SkillEffect[] { heal }, castTime: 0.5f, movementLock: 0.6f));
            }

            // 10. 전투의 함성 — 공용, 파티 버프
            {
                var buff = GetOrCreate<ApplyStatusEffect>(EffectPath, "EF_RallyCry");
                buff.Status = gas.Rally;
                buff.Targets = ApplyStatusEffect.TargetGroup.AlliesInRadius;
                buff.Radius = 10f;
                EditorUtility.SetDirty(buff);

                list.Add(MakeSkill(gas, 10, "SK_RallyCry", "전투의 함성", "주변 아군의 공격력을 10초간 올린다.",
                    SkillCategory.Support, WeaponType.Unarmed, SkillTargeting.Self,
                    cooldown: 25f, mana: 30f, range: 10f, cost: 2,
                    effects: new SkillEffect[] { buff }, castTime: 0.4f, movementLock: 0.5f));
            }

            return list;
        }

        private static SkillDefinition MakeSkill(
            GasAssets gas, ushort id, string assetName, string displayName, string description,
            SkillCategory category, WeaponType weapon, SkillTargeting targeting,
            float cooldown, float mana, float range, int cost,
            SkillEffect[] effects, float movementLock = 0.3f, float castTime = 0.2f)
        {
            var skill = GetOrCreate<SkillDefinition>(SkillPath, assetName);
            skill.Id = id;
            skill.DisplayName = displayName;
            skill.Description = description;
            skill.Category = category;
            skill.RequiredWeapon = weapon;
            skill.AutoSwapWeapon = true;
            skill.Targeting = targeting;
            skill.Cooldown = cooldown;
            skill.ManaCost = mana;
            skill.Range = range;
            skill.DeckCost = cost;
            skill.Effects = effects;
            skill.MovementLockTime = movementLock;
            skill.CastTime = castTime;

            // GAS 설정: 공용 비용 · 쿨다운 이펙트에 SetByCaller 로 값을 넘긴다.
            skill.NetExecutionPolicy = GameplayAbilityNetExecutionPolicy.LocalPredicted;
            skill.InstancingPolicy = GameplayAbilityInstancingPolicy.InstancedPerActor;
            skill.CostGameplayEffect = gas.CostMana;
            skill.CooldownGameplayEffect = gas.CooldownSkill;
            skill.AbilityTags.Reset();
            skill.AbilityTags.AddTag(GameTags.Ability_Skill);
            skill.AbilityTags.AddTag(GameplayTag.Request($"Ability.Skill.{assetName.Replace("SK_", string.Empty)}"));
            skill.ActivationOwnedTags.Reset();
            skill.ActivationOwnedTags.AddTag(GameTags.State_Casting);
            skill.ActivationBlockedTags.Reset();
            skill.ActivationBlockedTags.AddTag(GameTags.State_Dead);
            skill.ActivationBlockedTags.AddTag(GameTags.State_Debuff_Stun);
            skill.ActivationBlockedTags.AddTag(GameTags.State_Debuff_Silence);

            EditorUtility.SetDirty(skill);
            return skill;
        }

        // ---------------------------------------------------------------- 데이터베이스 / 덱

        private static SkillDatabase CreateDatabase(List<SkillDefinition> skills)
        {
            var database = GetOrCreate<SkillDatabase>(ResourcePath, "SkillDatabase");
            database.EditorSetSkills(skills.ToArray());
            EditorUtility.SetDirty(database);
            return database;
        }

        private static void CreateDeckPresets(List<SkillDefinition> skills)
        {
            var sword = GetOrCreate<SkillDeckPreset>(SkillPath, "DECK_Swordsman");
            sword.DisplayName = "검사 기본덱";
            sword.Description = "근접 전투 위주. 돌진으로 붙고 회전베기로 정리한다.";
            sword.Skills = new[] { Find(skills, 1), Find(skills, 2), Find(skills, 3), Find(skills, 9) };
            EditorUtility.SetDirty(sword);

            var archer = GetOrCreate<SkillDeckPreset>(SkillPath, "DECK_Archer");
            archer.DisplayName = "궁수 기본덱";
            archer.Description = "거리를 유지하며 화살로 압박한다.";
            archer.Skills = new[] { Find(skills, 5), Find(skills, 6), Find(skills, 8), Find(skills, 7) };
            EditorUtility.SetDirty(archer);
        }

        private static SkillDefinition Find(List<SkillDefinition> skills, ushort id) =>
            skills.Find(s => s != null && s.Id == id);

        // ---------------------------------------------------------------- 적

        private static void CreateEnemies(GasAssets gas)
        {
            var grunt = GetOrCreate<EnemyDefinition>(EnemyPath, "EN_GoblinWarrior");
            grunt.DisplayName = "고블린 전사";
            grunt.Stats = StatBlock.DefaultEnemy;
            grunt.DetectionRadius = 12f;
            grunt.LeashRadius = 25f;
            grunt.PreferredDistance = 1.8f;
            grunt.Patrol = true;
            grunt.Attacks = new[]
            {
                new EnemyDefinition.AttackPattern
                {
                    Name = "내려치기",
                    MaxRange = 2.6f,
                    Cooldown = 2.5f,
                    WindupTime = 0.65f,
                    RecoveryTime = 0.7f,
                    FlatDamage = 7f,
                    HitRadius = 2.4f,
                    ArcDegrees = 100f,
                    Knockback = 2f
                }
            };
            EditorUtility.SetDirty(grunt);

            var archer = GetOrCreate<EnemyDefinition>(EnemyPath, "EN_GoblinArcher");
            archer.DisplayName = "고블린 궁수";
            var archerStats = StatBlock.DefaultEnemy;
            archerStats.MaxHealth = 42f;
            archerStats.MoveSpeed = 3.8f;
            archer.Stats = archerStats;
            archer.DetectionRadius = 16f;
            archer.PreferredDistance = 9f;
            archer.Attacks = new[]
            {
                new EnemyDefinition.AttackPattern
                {
                    Name = "사격",
                    MinRange = 3f,
                    MaxRange = 16f,
                    Cooldown = 3f,
                    WindupTime = 0.9f,
                    RecoveryTime = 0.6f,
                    FlatDamage = 6f,
                    ProjectileSpeed = 20f
                }
            };
            EditorUtility.SetDirty(archer);

            var boss = GetOrCreate<EnemyDefinition>(EnemyPath, "EN_OrcChieftain");
            boss.DisplayName = "오크 족장";
            boss.IsBoss = true;
            var bossStats = StatBlock.DefaultEnemy;
            bossStats.MaxHealth = 600f;
            bossStats.AttackPower = 18f;
            bossStats.Defense = 12f;
            bossStats.MoveSpeed = 3.6f;
            boss.Stats = bossStats;
            boss.DetectionRadius = 20f;
            boss.LeashRadius = 0f;
            boss.Patrol = false;
            boss.Attacks = new[]
            {
                new EnemyDefinition.AttackPattern
                {
                    Name = "휘두르기",
                    MaxRange = 3.4f,
                    Cooldown = 3f,
                    Weight = 2f,
                    WindupTime = 0.7f,
                    RecoveryTime = 0.8f,
                    FlatDamage = 14f,
                    HitRadius = 3.4f,
                    ArcDegrees = 150f,
                    Knockback = 4f
                },
                new EnemyDefinition.AttackPattern
                {
                    Name = "도약 강타",
                    MinRange = 4f,
                    MaxRange = 12f,
                    Cooldown = 9f,
                    Weight = 1f,
                    WindupTime = 1.1f,
                    RecoveryTime = 1.2f,
                    AnimationIndex = 1,
                    FlatDamage = 22f,
                    HitRadius = 4.5f,
                    ArcDegrees = 360f,
                    Knockback = 7f,
                    ApplyStatus = gas.Stun
                }
            };
            EditorUtility.SetDirty(boss);
        }

        // ---------------------------------------------------------------- 튜토리얼

        private static void CreateTutorialSequence()
        {
            var intro = GetOrCreate<DialogueStep>(TutorialPath, "TS_01_Intro");
            intro.ObjectiveText = "이야기를 듣는다";
            intro.DialogueLine = "성채가 무너진 지 사흘. 살아남은 자는 그대뿐이다. 무기를 들고 일어서라.";
            intro.DialogueDuration = 5f;
            intro.WaitForDialogue = true;
            EditorUtility.SetDirty(intro);

            var move = GetOrCreate<MoveDistanceStep>(TutorialPath, "TS_02_Move");
            move.ObjectiveText = "WASD 로 이동하기";
            move.DialogueLine = "WASD 로 움직여 보아라.";
            move.RequiredDistance = 10f;
            EditorUtility.SetDirty(move);

            var look = GetOrCreate<LookAroundStep>(TutorialPath, "TS_03_Look");
            look.ObjectiveText = "마우스로 주변 둘러보기";
            look.RequiredDegrees = 180f;
            EditorUtility.SetDirty(look);

            var dodge = GetOrCreate<PerformActionStep>(TutorialPath, "TS_04_Dodge");
            dodge.ObjectiveText = "Left Ctrl 로 회피하기";
            dodge.DialogueLine = "적의 공격은 회피로 흘려라. Left Ctrl.";
            dodge.Action = PerformActionStep.ActionKind.Dodge;
            dodge.RequiredCount = 2;
            EditorUtility.SetDirty(dodge);

            var attack = GetOrCreate<PerformActionStep>(TutorialPath, "TS_05_Attack");
            attack.ObjectiveText = "마우스 좌클릭으로 공격하기";
            attack.Action = PerformActionStep.ActionKind.Attack;
            attack.RequiredCount = 3;
            EditorUtility.SetDirty(attack);

            var swap = GetOrCreate<PerformActionStep>(TutorialPath, "TS_06_SwapWeapon");
            swap.ObjectiveText = "Tab 으로 검과 활 바꾸기";
            swap.DialogueLine = "검으로는 붙어서, 활로는 떨어져서 싸운다. Tab 으로 바꾼다.";
            swap.Action = PerformActionStep.ActionKind.SwapWeapon;
            swap.RequiredCount = 2;
            EditorUtility.SetDirty(swap);

            var skill = GetOrCreate<PerformActionStep>(TutorialPath, "TS_07_Skill");
            skill.ObjectiveText = "Q E R F 로 스킬 사용하기";
            skill.DialogueLine = "출전 전에 고른 네 가지 기술이 손끝에 있다. Q, E, R, F.";
            skill.Action = PerformActionStep.ActionKind.AnySkill;
            skill.RequiredCount = 4;
            EditorUtility.SetDirty(skill);

            var fight = GetOrCreate<DefeatEnemiesStep>(TutorialPath, "TS_08_Fight");
            fight.ObjectiveText = "고블린 처치하기";
            fight.DialogueLine = "실전이다. 살아남아라.";
            fight.RequiredKills = 2;
            fight.SpawnCount = 2;
            fight.SpawnDistance = 9f;
            EditorUtility.SetDirty(fight);

            var outro = GetOrCreate<DialogueStep>(TutorialPath, "TS_09_Outro");
            outro.ObjectiveText = "준비 완료";
            outro.DialogueLine = "훌륭하다. 이제 동료를 모아 던전으로 향하라.";
            outro.DialogueDuration = 5f;
            EditorUtility.SetDirty(outro);

            var sequence = GetOrCreate<TutorialSequence>(TutorialPath, "TUT_Basics");
            sequence.DisplayName = "기본 조작 배우기";
            sequence.Intro = "무너진 성채의 폐허에서 눈을 뜬다.";
            sequence.Steps = new TutorialStep[] { intro, move, look, dodge, attack, swap, skill, fight, outro };
            EditorUtility.SetDirty(sequence);
        }
    }
}
