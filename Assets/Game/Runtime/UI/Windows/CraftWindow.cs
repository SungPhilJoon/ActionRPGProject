using Framework;
using UnityEngine;

/// <summary>제작 창. 손 제작(Hand)은 어디서나, 작업대 레시피는 작업대와 상호작용해야 보인다.</summary>
public class CraftWindow : ListWindow
{
    private EStation Station = EStation.Hand;

    protected override int TitleTextId
    {
        get { return (int)EText.Crafting; }
    }

    public void OpenFor(EStation InStation)
    {
        Station = InStation;
        Open();
    }

    protected override void OnClose()
    {
        Station = EStation.Hand;
    }

    private void OnEnable()
    {
        EventBus<InventoryChangedEvent>.Subscribe(onInventoryChanged);
    }

    private void OnDisable()
    {
        EventBus<InventoryChangedEvent>.Unsubscribe(onInventoryChanged);
    }

    private void onInventoryChanged(InventoryChangedEvent InEvent)
    {
        if (IsOpen == true && Content != null)
        {
            Refresh();
        }
    }

    protected override void Fill()
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        var recipes = Tables.Root.Recipe.All;
        for (int i = 0; i < recipes.Count; ++i)
        {
            var recipe = recipes[i];
            bool available = recipe.Station == EStation.Hand || recipe.Station == Station;
            if (available == false)
            {
                continue;
            }

            ItemRow result;
            if (Tables.Root.Item.TryGet(recipe.Result, out result) == false)
            {
                continue;
            }

            bool canCraft = player.Inventory.Has(recipe.Inputs, recipe.InputCounts);
            string title = recipe.ResultCount > 1 ? $"{Tables.Text(result.NameId)} x{recipe.ResultCount}" : Tables.Text(result.NameId);
            string detail = $"{Tables.Text(EText.Requires)}: {UIUtil.CostText(player.Inventory, recipe.Inputs, recipe.InputCounts)}";
            var captured = recipe;
            AddRow(UIUtil.LoadSprite(result.Icon), Color.gray, title, detail, Tables.Text(EText.Craft), () => craft(captured), canCraft);
        }
    }

    private void craft(RecipeRow InRecipe)
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        if (player.Inventory.Remove(InRecipe.Inputs, InRecipe.InputCounts) == false)
        {
            EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.NotEnoughMaterials, Color = Color.red });
            return;
        }

        player.Inventory.Add(InRecipe.Result, Mathf.Max(1, InRecipe.ResultCount));
        EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.Craft, Arg = UIUtil.ItemName(InRecipe.Result), Color = UIUtil.GoldColor });
    }
}
