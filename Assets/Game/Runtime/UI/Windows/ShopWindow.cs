using Framework;
using UnityEngine;

/// <summary>
/// 상점. 프로젝트 규칙: 수익 모델은 고려하지 않고 모든 상품은 무료 (ShopTable.Price = 0).
/// 가격 컬럼이 0 이 아니어도 지불 로직이 없으므로 항상 무료로 지급된다.
/// </summary>
public class ShopWindow : ListWindow
{
    protected override int TitleTextId
    {
        get { return (int)EText.Shop; }
    }

    protected override void Fill()
    {
        var rows = Tables.Root.Shop.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            var row = rows[i];
            ItemRow item;
            if (Tables.Root.Item.TryGet(row.Item, out item) == false)
            {
                continue;
            }

            string title = row.Count > 1 ? $"{Tables.Text(item.NameId)} x{row.Count}" : Tables.Text(item.NameId);
            string detail = $"<color=#7CE07C>{Tables.Text(EText.Free)}</color>   {Tables.Text(item.DescId)}";
            var captured = row;
            AddRow(UIUtil.LoadSprite(item.Icon), Color.gray, title, detail, Tables.Text(EText.Buy), () => buy(captured), true);
        }
    }

    private void buy(ShopRow InRow)
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        int left = player.Inventory.Add(InRow.Item, Mathf.Max(1, InRow.Count));
        if (left < InRow.Count)
        {
            EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.Buy, Arg = UIUtil.ItemName(InRow.Item), Color = new Color(0.5f, 0.9f, 0.5f) });
        }
    }
}
