using System.Collections.Generic;
using System.Text;
using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// 런타임 밸런스 오버레이 (F8). CombatLog 를 읽어
///  - 어빌리티별 누적 피해 / DPS (플레이어가 가한 것)
///  - 플레이어가 받은 피해 총합과 어빌리티별 내역
///  - 최근 피해 기록
/// 을 보여 준다. 에디터의 Balance Tool 창과 같은 데이터를 쓴다.
/// </summary>
public class BalanceOverlayWindow : UIWindow
{
    private const float RefreshInterval = 0.25f;

    private TextMeshProUGUI Body;
    private float Timer;
    private readonly List<KeyValuePair<long, float>> Sorted = new List<KeyValuePair<long, float>>();

    public override bool BlocksGameInput
    {
        get { return false; }
    }

    protected override void Build()
    {
        var frame = UIFactory.CreatePanel(Rect, "Frame", new Color(0.0f, 0.0f, 0.0f, 0.65f));
        UIFactory.Place(frame, new Vector2(0.0f, 1.0f), new Vector2(20.0f, -20.0f), new Vector2(560.0f, 640.0f));
        Body = UIFactory.CreateText(frame, "Body", string.Empty, 16.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Stretch(Body.rectTransform, 12.0f);
        Body.raycastTarget = false;
        frame.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
    }

    private void Update()
    {
        Timer -= Time.unscaledDeltaTime;
        if (Timer > 0.0f)
        {
            return;
        }

        Timer = RefreshInterval;
        Body.text = buildText();
    }

    private string buildText()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"<b>{Tables.Text(EText.BalanceTitle)}</b>   <size=13>(cheat: clearlog)</size>");
        float elapsed = Mathf.Max(1.0f, Time.time - CombatLog.SessionStartTime);

        var player = PlayerCharacter.Local;
        builder.AppendLine($"<color=#F0D080>{Tables.Text(EText.BalanceDealt)}</color>");
        Sorted.Clear();
        foreach (var pair in CombatLog.GetDamageBySourceAbility())
        {
            Sorted.Add(pair);
        }

        Sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
        for (int i = 0; i < Sorted.Count && i < 10; ++i)
        {
            int character;
            int ability;
            CombatLog.SplitKey(Sorted[i].Key, out character, out ability);
            builder.AppendLine($"  {characterName(character)} · {abilityName(ability)} : {Sorted[i].Value:0} / {Sorted[i].Value / elapsed:0.0}");
        }

        if (player != null)
        {
            builder.AppendLine();
            builder.AppendLine($"<color=#F08080>{Tables.Text(EText.BalanceTaken)}: {CombatLog.GetDamageTaken(player.ActorId):0}</color>");
            var breakdown = CombatLog.GetTakenBreakdown(player.ActorId);
            if (breakdown != null)
            {
                foreach (var pair in breakdown)
                {
                    builder.AppendLine($"  {abilityName(pair.Key)} : {pair.Value:0}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine($"<color=#A0C0F0>{Tables.Text(EText.BalanceRecent)}</color>");
        int count = Mathf.Min(10, CombatLog.Count);
        for (int i = 0; i < count; ++i)
        {
            var record = CombatLog.GetRecent(i);
            string crit = record.IsCritical == true ? " <color=#FFD040>CRIT</color>" : string.Empty;
            string kind = record.IsHeal == true ? "+" : "-";
            builder.AppendLine($"  {characterName(record.SourceCharacter)} → {characterName(record.TargetCharacter)} [{abilityName(record.AbilityId)}] {kind}{record.Final:0} (raw {record.Raw:0}, abs {record.Absorbed:0}){crit}");
        }

        return builder.ToString();
    }

    private static string characterName(int InCharacterId)
    {
        CharacterRow row;
        if (InCharacterId == 0 || Tables.Root.Character.TryGet(InCharacterId, out row) == false)
        {
            return "World";
        }

        return Tables.Text(row.NameId);
    }

    private static string abilityName(int InAbilityId)
    {
        AbilityRow row;
        if (InAbilityId == 0 || Tables.Root.Ability.TryGet(InAbilityId, out row) == false)
        {
            return "Effect";
        }

        return Tables.Text(row.NameId);
    }
}
