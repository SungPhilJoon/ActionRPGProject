using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ActionRPG.Networking
{
    /// <summary>
    /// NetworkManager 는 시작하면 DontDestroyOnLoad 로 살아남지만 중복을 스스로 정리하지는 않는다.
    /// 그래서 씬마다 NetworkManager 를 두면 메뉴 → 던전으로 넘어갈 때 두 개가 공존해 버린다.
    ///
    /// 이 컴포넌트를 씬의 NetworkManager 오브젝트에 붙이면
    ///  - 이미 살아 있는 NetworkManager 가 있으면 이쪽을 조용히 없애고
    ///  - 없으면 그대로 두어, 게임플레이 씬을 단독으로 재생하는 것도 가능하게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public class NetworkBootstrapGuard : MonoBehaviour
    {
        [Tooltip("메뉴를 거치지 않고 이 씬을 바로 재생했을 때 호스트를 자동으로 시작할지. 개발 중 반복 테스트용.")]
        [SerializeField] private bool autoStartHostWhenAlone = true;

        private NetworkManager _mine;

        private void Awake()
        {
            _mine = GetComponent<NetworkManager>();

            // 다른 씬에서 넘어온 NetworkManager 가 이미 있다면 이 오브젝트는 필요 없다.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton != _mine)
            {
                Destroy(gameObject);
                return;
            }
        }

        private IEnumerator Start()
        {
            if (_mine == null || !autoStartHostWhenAlone) yield break;

            // 메뉴에서 넘어오는 중일 수도 있으니 한 프레임 기다렸다가 판단한다.
            yield return null;

            if (NetworkManager.Singleton == null || NetworkManager.Singleton != _mine) yield break;
            if (_mine.IsListening) yield break;

            _mine.NetworkConfig.ConnectionApproval = false;
            _mine.StartHost();
            Debug.Log("[NetworkBootstrapGuard] 단독 재생이라 호스트를 자동으로 시작했습니다.");
        }
    }
}
