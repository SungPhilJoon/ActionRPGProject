using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ActionRPG.Core.Save
{
    /// <summary>
    /// 클라우드 세이브를 실제로 붙이기 전까지의 자리 표시자(seam).
    ///
    /// 지금 당장 동작하는 코드는 아니지만, ISaveService 를 구현해 두면
    /// - SaveManager.Configure(new CloudSaveServiceStub()) 로 즉시 교체 가능하다는 것을 보여 주고
    /// - 실제 클라우드 SDK(Unity Cloud Save / PlayFab / 자체 서버)가 정해지면
    ///   이 파일 하나만 채워 넣으면 된다는 확장 지점을 명확히 남겨 둔다.
    ///
    /// 실제 구현 시 참고할 패턴:
    ///   1) 로그인/인증은 별도 AuthService 에서 처리하고 여기서는 토큰만 받아 쓴다.
    ///   2) 로컬(LocalFileSaveService)에도 항상 같이 쓰고, 클라우드 저장이 실패하면
    ///      로컬 데이터로 폴백하는 CompositeSaveService 로 감싸는 것을 권장한다.
    ///   3) LoadAsync 는 "클라우드가 최신인지 로컬이 최신인지" 타임스탬프 비교가 필요할 수 있다
    ///      (SaveEnvelope.SavedAtUnixSeconds 를 그 용도로 이미 넣어 두었다).
    /// </summary>
    public sealed class CloudSaveServiceStub : ISaveService
    {
        public UniTask<SaveResult> SaveAsync<T>(string slot, T data)
        {
            Debug.LogWarning($"[CloudSaveServiceStub] SaveAsync('{slot}') 호출됨 — 클라우드 백엔드가 아직 구현되지 않았습니다.");
            throw new NotImplementedException("클라우드 세이브 SDK 연동 후 구현하세요 (com.actionrpg.foundation/Runtime/Save/CloudSaveServiceStub.cs).");
        }

        public UniTask<SaveLoadResult<T>> LoadAsync<T>(string slot) =>
            throw new NotImplementedException("클라우드 세이브 SDK 연동 후 구현하세요.");

        public UniTask<bool> ExistsAsync(string slot) =>
            throw new NotImplementedException("클라우드 세이브 SDK 연동 후 구현하세요.");

        public UniTask DeleteAsync(string slot) =>
            throw new NotImplementedException("클라우드 세이브 SDK 연동 후 구현하세요.");
    }
}
