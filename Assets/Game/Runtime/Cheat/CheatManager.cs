using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// 치트 명령. 명령어 이름은 이 enum 의 멤버 이름이며, 등록 시 FNV 해시(int)로 바뀐다.
/// 콘솔 입력도 첫 단어를 해시해서 int 로 찾기 때문에 문자열 비교가 없다 (Command 패턴).
/// </summary>
public enum ECheat
{
    help,
    god,
    nocd,
    heal,
    give,
    giveall,
    equip,
    time,
    timescale,
    freeze,
    killall,
    spawn,
    speed,
    unlock,
    blood,
    stat,
    tp,
    hurt,
    clearlog,
    level,
    nosun,
    lowhp,
    spell,
    status,
}

public class CheatManager : IManager
{
    private struct Command
    {
        public ECheat Id;
        public Action<string[]> Handler;
        public string Usage;
    }

    private readonly Dictionary<int, Command> Commands = new Dictionary<int, Command>();
    private readonly List<string> History = new List<string>();

    public event Action<string> OnOutput;

    public IReadOnlyList<string> Lines
    {
        get { return History; }
    }

    void IManager.Initialize()
    {
        regist(ECheat.help, help, "help");
        regist(ECheat.god, god, "god : toggle invulnerable + sun immune");
        regist(ECheat.nocd, noCooldown, "nocd : toggle no cooldown");
        regist(ECheat.heal, heal, "heal : full health/blood");
        regist(ECheat.give, give, "give <itemId> [count]");
        regist(ECheat.giveall, giveAll, "giveall [count] : all resources");
        regist(ECheat.equip, equip, "equip <itemId>");
        regist(ECheat.time, setTime, "time <hour 0-24>");
        regist(ECheat.timescale, timeScale, "timescale <x> : day/night speed");
        regist(ECheat.freeze, freeze, "freeze : toggle day/night freeze");
        regist(ECheat.killall, killAll, "killall [radius]");
        regist(ECheat.spawn, spawn, "spawn <characterId> [count]");
        regist(ECheat.speed, speed, "speed <moveSpeed>");
        regist(ECheat.unlock, unlock, "unlock : all V Blood abilities");
        regist(ECheat.blood, blood, "blood <type 1-5> <quality>");
        regist(ECheat.stat, stat, "stat <attribute> <value>  (e.g. stat PhysicalPower 50)");
        regist(ECheat.tp, teleport, "tp <x> <z>");
        regist(ECheat.hurt, hurt, "hurt <amount> : damage self");
        regist(ECheat.clearlog, clearLog, "clearlog : reset combat log / balance meter");
        regist(ECheat.level, level, "level <gearLevel>");
        regist(ECheat.nosun, noSun, "nosun : force night");
        regist(ECheat.lowhp, lowHp, "lowhp [radius] : set nearby enemies to 20% hp (feed test)");
        regist(ECheat.spell, spell, "spell <abilityId> : equip spell (301 Chaos Volley, 302 Blood Rite, 303 Ice Nova, 304 Ball Lightning, 305 Corrupted Skull, 306 Wraith Spear)");
        regist(ECheat.status, status, "status <effectId> [radius] : apply effect to nearby enemies (80 Leech, 82 Ignite, 84 Chill, 85 Freeze, 86 Static, 88 Condemn, 89 Weaken)");
    }

    void IManager.UnInitialize()
    {
        Commands.Clear();
    }

    void IManager.Update(float InDeltaTime)
    {
    }

    public void Execute(string InLine)
    {
        if (string.IsNullOrWhiteSpace(InLine) == true)
        {
            return;
        }

        print("> " + InLine);
        var tokens = InLine.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        Command command;
        if (Commands.TryGetValue(HashUtil.Fnv1a(tokens[0]), out command) == false)
        {
            print("unknown command. type 'help'");
            return;
        }

        try
        {
            command.Handler.Invoke(tokens);
        }
        catch (Exception e)
        {
            print($"error: {e.Message}  usage: {command.Usage}");
        }
    }

    private void regist(ECheat InId, Action<string[]> InHandler, string InUsage)
    {
        var command = new Command();
        command.Id = InId;
        command.Handler = InHandler;
        command.Usage = InUsage;
        Commands[HashUtil.Fnv1a(InId.ToString())] = command;
    }

    private void print(string InLine)
    {
        History.Add(InLine);
        if (History.Count > 200)
        {
            History.RemoveAt(0);
        }

        OnOutput?.Invoke(InLine);
    }

    private static PlayerCharacter player()
    {
        var local = PlayerCharacter.Local;
        if (local == null)
        {
            throw new InvalidOperationException("no player");
        }

        return local;
    }

    private static int argInt(string[] InTokens, int InIndex, int InDefault)
    {
        if (InIndex >= InTokens.Length)
        {
            return InDefault;
        }

        return int.Parse(InTokens[InIndex], CultureInfo.InvariantCulture);
    }

    private static float argFloat(string[] InTokens, int InIndex, float InDefault)
    {
        if (InIndex >= InTokens.Length)
        {
            return InDefault;
        }

        return float.Parse(InTokens[InIndex], CultureInfo.InvariantCulture);
    }

    private bool toggleTag(Actor InActor, ETag InTag)
    {
        var tags = InActor.Asc.Tags;
        if (tags.HasTagExact((int)InTag) == true)
        {
            tags.RemoveTag((int)InTag);
            return false;
        }

        tags.AddTag((int)InTag);
        return true;
    }

    // ------------------------------------------------------------------ commands

    private void help(string[] InTokens)
    {
        var builder = new StringBuilder();
        foreach (var pair in Commands)
        {
            builder.AppendLine(pair.Value.Usage);
        }

        print(builder.ToString().TrimEnd());
    }

    private void god(string[] InTokens)
    {
        bool on = toggleTag(player(), ETag.Cheat_God);
        print("god " + (on == true ? "ON" : "OFF"));
    }

    private void noCooldown(string[] InTokens)
    {
        var local = player();
        bool on = toggleTag(local, ETag.Cheat_NoCooldown);
        local.Asc.ClearCooldowns();
        print("nocd " + (on == true ? "ON" : "OFF"));
    }

    private void heal(string[] InTokens)
    {
        var local = player();
        local.Attributes.Set(EAttribute.Health, local.Attributes.Get(EAttribute.MaxHealth));
        local.Attributes.Set(EAttribute.BloodPool, local.Attributes.Get(EAttribute.MaxBloodPool));
        print("healed");
    }

    private void give(string[] InTokens)
    {
        int itemId = argInt(InTokens, 1, 0);
        int count = argInt(InTokens, 2, 1);
        int left = player().Inventory.Add(itemId, count);
        print($"give {itemId} x{count - left}");
    }

    private void giveAll(string[] InTokens)
    {
        int count = argInt(InTokens, 1, 100);
        var items = Tables.Root.Item.All;
        for (int i = 0; i < items.Count; ++i)
        {
            if (items[i].Type == EItemType.Resource)
            {
                player().Inventory.Add(items[i].Index, count);
            }
        }

        print($"all resources x{count}");
    }

    private void equip(string[] InTokens)
    {
        int itemId = argInt(InTokens, 1, 0);
        player().Equipment.Equip(itemId);
        print($"equipped {itemId}");
    }

    private void setTime(string[] InTokens)
    {
        float hour = argFloat(InTokens, 1, 12.0f);
        Managers.Get<DayNightManager>().SetHour(hour);
        print($"time {hour:0.0}");
    }

    private void timeScale(string[] InTokens)
    {
        float scale = argFloat(InTokens, 1, 1.0f);
        Managers.Get<DayNightManager>().TimeScale = scale;
        print($"timescale {scale}");
    }

    private void freeze(string[] InTokens)
    {
        var dayNight = Managers.Get<DayNightManager>();
        dayNight.Frozen = dayNight.Frozen == false;
        print("freeze " + (dayNight.Frozen == true ? "ON" : "OFF"));
    }

    private void killAll(string[] InTokens)
    {
        float radius = argFloat(InTokens, 1, 30.0f);
        var local = player();
        var targets = new List<Actor>();
        Managers.Get<ActorRegistry>().FindHostiles(local, local.transform.position, radius, targets);
        for (int i = 0; i < targets.Count; ++i)
        {
            targets[i].Die(local);
        }

        print($"killed {targets.Count}");
    }

    private void spawn(string[] InTokens)
    {
        int characterId = argInt(InTokens, 1, 0);
        int count = argInt(InTokens, 2, 1);
        var local = player();
        for (int i = 0; i < count; ++i)
        {
            Vector3 offset = Quaternion.Euler(0.0f, i * 360.0f / count, 0.0f) * Vector3.forward * 6.0f;
            Managers.Get<SpawnManager>().SpawnMonster(characterId, local.transform.position + offset);
        }

        print($"spawn {characterId} x{count}");
    }

    private void speed(string[] InTokens)
    {
        float value = argFloat(InTokens, 1, 6.0f);
        player().Attributes.Set(EAttribute.MoveSpeed, value);
        print($"speed {value}");
    }

    private void unlock(string[] InTokens)
    {
        var characters = Tables.Root.Character.All;
        for (int i = 0; i < characters.Count; ++i)
        {
            if (characters[i].IsVBlood == true)
            {
                player().UnlockAbilities(characters[i].VBloodUnlocks);
            }
        }

        print("unlocked all");
    }

    private void spell(string[] InTokens)
    {
        int abilityId = argInt(InTokens, 1, 301);
        var ability = player().GiveAbility(abilityId);
        print(ability != null ? $"equipped {abilityId} → {ability.Row.Slot}" : $"ability {abilityId} not found");
    }

    private void status(string[] InTokens)
    {
        int effectId = argInt(InTokens, 1, 84);
        float radius = argFloat(InTokens, 2, 15.0f);
        var self = player();
        var targets = new List<Actor>();
        Managers.Get<ActorRegistry>().FindHostiles(self, self.transform.position, radius, targets);
        for (int i = 0; i < targets.Count; ++i)
        {
            targets[i].ApplyEffect(effectId, self, 0);
        }

        print($"effect {effectId} → {targets.Count} enemies");
    }

    private void blood(string[] InTokens)
    {
        var type = (EBloodType)argInt(InTokens, 1, 1);
        float quality = argFloat(InTokens, 2, 100.0f);
        player().Blood.SetBlood(type, quality);
        print($"blood {type} {quality}%");
    }

    private void stat(string[] InTokens)
    {
        var attribute = (EAttribute)Enum.Parse(typeof(EAttribute), InTokens[1], true);
        float value = argFloat(InTokens, 2, 0.0f);
        player().Attributes.Set(attribute, value);
        print($"{attribute} = {player().Attributes.Get(attribute)}");
    }

    private void teleport(string[] InTokens)
    {
        float x = argFloat(InTokens, 1, 0.0f);
        float z = argFloat(InTokens, 2, 0.0f);
        player().Motor.Teleport(CombatUtil.SnapToGround(new Vector3(x, 0.0f, z)) + Vector3.up * 0.2f);
        print($"tp {x},{z}");
    }

    private void hurt(string[] InTokens)
    {
        float amount = argFloat(InTokens, 1, 50.0f);
        var local = player();
        var spec = new GameplayEffectSpec(EffectLibrary.Get(EffectLibrary.DamageEffectId), null, 0);
        spec.SetByCaller((int)ESetByCaller.Damage, amount);
        spec.SetByCaller((int)ESetByCaller.DamageType, (float)EDamageType.True);
        local.Asc.ApplyGameplayEffectSpecToSelf(spec);
        print($"hurt {amount}");
    }

    private void clearLog(string[] InTokens)
    {
        CombatLog.Clear();
        print("combat log cleared");
    }

    private void level(string[] InTokens)
    {
        float value = argFloat(InTokens, 1, 10.0f);
        player().Attributes.Set(EAttribute.Level, value);
        print($"level {value}");
    }

    private void noSun(string[] InTokens)
    {
        var dayNight = Managers.Get<DayNightManager>();
        dayNight.SetHour(0.0f);
        dayNight.Frozen = true;
        print("night locked (use 'freeze' to resume)");
    }

    private void lowHp(string[] InTokens)
    {
        float radius = argFloat(InTokens, 1, 15.0f);
        var local = player();
        var targets = new List<Actor>();
        Managers.Get<ActorRegistry>().FindHostiles(local, local.transform.position, radius, targets);
        for (int i = 0; i < targets.Count; ++i)
        {
            targets[i].Attributes.Set(EAttribute.Health, targets[i].Attributes.Get(EAttribute.MaxHealth) * 0.2f);
        }

        print($"lowhp {targets.Count}");
    }
}
