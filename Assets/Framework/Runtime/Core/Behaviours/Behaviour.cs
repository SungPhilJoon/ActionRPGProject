using UnityEngine;

namespace Framework
{
    /// <summary>
    /// 프레임워크 MonoBehaviour 의 최상위 부모. Unity 메시지(Awake/OnDestroy)를 virtual 로 열어
    /// BaseBehaviour 가 봉인(sealed)하고 OnUnity* 훅으로 넘길 수 있게 한다.
    /// UnityEngine.Behaviour 와 이름이 같으므로 Framework 네임스페이스 밖에서는 직접 참조하지 않는다.
    /// </summary>
    public abstract class Behaviour : MonoBehaviour
    {
        protected virtual void Awake()
        {
        }

        protected virtual void OnDestroy()
        {
        }
    }
}
