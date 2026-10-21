using Framework;
using Framework.UI;
using UnityEngine;

/// <summary>
/// 지어진 건축물. 종류(StructureTable.Type)에 따라 성 심장(영역), 관(리스폰), 작업대(제작) 역할을 한다.
/// 벽/바닥은 Structure 레이어라 햇빛을 가려 그늘을 만든다.
/// </summary>
public class Structure : MonoBehaviour, IInteractable
{
    public StructureRow Row { get; private set; }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    public int PromptTextId
    {
        get { return Row != null ? Row.NameId : 0; }
    }

    public void Initialize(StructureRow InRow)
    {
        Row = InRow;
        ComponentUtil.SetLayerRecursive(gameObject, LayerId.Structure);
        if (isInteractable() == true)
        {
            InteractableRegistry.Add(this);
        }
    }

    private void OnDestroy()
    {
        InteractableRegistry.Remove(this);
    }

    public bool CanInteract(PlayerCharacter InPlayer)
    {
        return Row != null && isInteractable() == true;
    }

    public void Interact(PlayerCharacter InPlayer)
    {
        switch (Row.Type)
        {
            case EStructureType.Station:
                var craft = Managers.Get<UIManager>().Get<CraftWindow>();
                if (craft != null)
                {
                    craft.OpenFor(Row.Station);
                }

                break;
            case EStructureType.Coffin:
                InPlayer.RespawnPoint = transform.position + transform.forward * 1.5f;
                EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = Row.NameId, Color = new Color(0.7f, 0.2f, 0.25f) });
                break;
        }
    }

    private bool isInteractable()
    {
        return Row.Type == EStructureType.Station || Row.Type == EStructureType.Coffin;
    }
}
