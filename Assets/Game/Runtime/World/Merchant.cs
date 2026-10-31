using Framework;
using Framework.UI;
using UnityEngine;

/// <summary>떠돌이 상인. 상호작용하면 상점 창을 연다 (모든 상품 무료).</summary>
public class Merchant : BaseBehaviour, IInteractable
{
    public Vector3 Position
    {
        get { return transform.position; }
    }

    public int PromptTextId
    {
        get { return (int)EText.Merchant; }
    }

    private void OnEnable()
    {
        InteractableRegistry.Add(this);
        MapMarkers.Add(EMapMarker.Merchant, transform);
    }

    private void OnDisable()
    {
        InteractableRegistry.Remove(this);
        MapMarkers.Remove(transform);
    }

    public bool CanInteract(PlayerCharacter InPlayer)
    {
        return true;
    }

    public void Interact(PlayerCharacter InPlayer)
    {
        var window = Managers.Get<UIManager>().Get<ShopWindow>();
        if (window != null)
        {
            window.Open();
        }
    }
}
