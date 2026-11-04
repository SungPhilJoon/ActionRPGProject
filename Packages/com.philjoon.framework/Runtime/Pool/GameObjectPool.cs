using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    /// <summary>
    /// 프리팹 InstanceID(int) 를 키로 하는 GameObject 풀 (Object Pool 패턴).
    /// 데미지 숫자, 발사체, 이펙트처럼 자주 생성/파괴되는 오브젝트에 사용한다.
    /// </summary>
    public class GameObjectPool : IManager
    {
        private readonly Dictionary<int, Stack<GameObject>> Pools = new Dictionary<int, Stack<GameObject>>();
        private readonly Dictionary<int, int> InstanceToPrefab = new Dictionary<int, int>();
        private readonly List<IPoolable> PoolableBuffer = new List<IPoolable>();
        private Transform Root;

        void IManager.Initialize()
        {
            var rootObject = new GameObject("[Pool]");
            Root = rootObject.transform;
        }

        void IManager.UnInitialize()
        {
            Pools.Clear();
            InstanceToPrefab.Clear();
            if (Root != null)
            {
                Object.Destroy(Root.gameObject);
            }
        }

        void IManager.Update(float InDeltaTime)
        {
        }

        public GameObject Spawn(GameObject InPrefab, Vector3 InPosition, Quaternion InRotation)
        {
            if (InPrefab == null)
            {
                return null;
            }

            int key = InPrefab.GetInstanceID();
            Stack<GameObject> stack;
            if (Pools.TryGetValue(key, out stack) == false)
            {
                stack = new Stack<GameObject>();
                Pools.Add(key, stack);
            }

            GameObject instance = null;
            while (stack.Count > 0 && instance == null)
            {
                instance = stack.Pop();
            }

            if (instance == null)
            {
                instance = Object.Instantiate(InPrefab, InPosition, InRotation);
                InstanceToPrefab[instance.GetInstanceID()] = key;
            }
            else
            {
                instance.transform.SetParent(null, false);
                instance.transform.SetPositionAndRotation(InPosition, InRotation);
                instance.SetActive(true);
            }

            notify(instance, true);
            return instance;
        }

        public void Despawn(GameObject InInstance)
        {
            if (InInstance == null)
            {
                return;
            }

            int prefabKey;
            if (InstanceToPrefab.TryGetValue(InInstance.GetInstanceID(), out prefabKey) == false)
            {
                Object.Destroy(InInstance);
                return;
            }

            notify(InInstance, false);
            InInstance.SetActive(false);
            InInstance.transform.SetParent(Root, false);
            Pools[prefabKey].Push(InInstance);
        }

        private void notify(GameObject InInstance, bool InSpawn)
        {
            PoolableBuffer.Clear();
            InInstance.GetComponentsInChildren(true, PoolableBuffer);
            for (int i = 0; i < PoolableBuffer.Count; ++i)
            {
                if (InSpawn == true)
                {
                    PoolableBuffer[i].OnSpawn();
                }
                else
                {
                    PoolableBuffer[i].OnDespawn();
                }
            }
        }
    }
}
