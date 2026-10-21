using Unity.Netcode.Components;
using UnityEngine;

namespace ActionRPG.Character
{
    /// <summary>
    /// 소유자(클라이언트)가 위치를 직접 정하는 NetworkTransform.
    /// PvE 협동 게임이라 이동 치팅 위험보다 조작 반응성이 훨씬 중요해서 이 방식을 택했다.
    /// 피해 판정은 여전히 서버에서만 하므로 체력/전투는 안전하다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
