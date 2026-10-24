using UnityEngine;

public class BaseBehaviour : Behaviour
{
    private Transform cachedTransform = null;
    private GameObject cachedGameObject = null;

    public new Transform transform
    {
        get
        {
            if (cachedTransform == null)
            {
                return null;
            }

            return cachedTransform;
        }
    }
    
    public new GameObject gameObject
    {
        get
        {
            if (cachedGameObject == null)
            {
                return null;
            }
            
            return cachedGameObject;
        }
    }
    
    protected sealed override void Awake()
    {
        cachedTransform = base.transform;
        cachedGameObject = cachedTransform.gameObject;
        
        OnUnityAwake();
    }

    protected sealed override void OnDestroy()
    {
        OnUnityDestroy();

        cachedGameObject = null;
        cachedTransform = null;
    }

    protected virtual void OnUnityAwake()
    {
        
    }
    
    protected virtual void OnUnityDestroy()
    {
        
    }

    public virtual void SetName(string name)
    {
        cachedGameObject.name = name;
    }

    public virtual string GetName()
    {
        return cachedGameObject.name;
    }

    public virtual void SetParent(Transform inParent)
    {
        if (cachedTransform.parent == inParent)
        {
            return;
        }
        
        cachedTransform.SetParent(inParent);
    }
}