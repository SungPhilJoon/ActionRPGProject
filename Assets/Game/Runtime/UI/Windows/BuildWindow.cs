using Framework;
using UnityEngine;

/// <summary>건축 목록. 선택하면 창을 닫고 BuildManager 고스트 배치 모드로 들어간다.</summary>
public class BuildWindow : ListWindow
{
    protected override int TitleTextId
    {
        get { return (int)EText.Build; }
    }

    protected override void Fill()
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        var build = Managers.Get<BuildManager>();
        var rows = Tables.Root.Structure.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            var row = rows[i];
            bool canAfford = player.Inventory.Has(row.Costs, row.CostCounts);
            bool needsHeart = row.RequiresHeart == true && build.HasHeart == false;
            bool heartDone = row.Type == EStructureType.CastleHeart && build.HasHeart == true;

            string detail = $"{Tables.Text(EText.Requires)}: {UIUtil.CostText(player.Inventory, row.Costs, row.CostCounts)}";
            if (needsHeart == true)
            {
                detail += $"\n<color=#E05050>{Tables.Text(EText.NeedCastleHeart)}</color>";
            }

            int id = row.Index;
            AddRow(UIUtil.LoadSprite(row.Icon), Color.gray, Tables.Text(row.NameId), detail, Tables.Text(EText.Place), () => begin(id), canAfford == true && needsHeart == false && heartDone == false);
        }
    }

    private void begin(int InStructureId)
    {
        Close();
        Managers.Get<BuildManager>().Begin(InStructureId);
        EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.BuildHint, Color = Color.white });
    }
}
