using ActionRPG.Skills;
using ProtoBuf;

namespace ActionRPG.Save
{
    /// <summary>
    /// 로컬에 영구 저장되는 플레이어 프로필. Protobuf 로 직렬화되어
    /// <see cref="ActionRPG.Core.Save.SaveManager"/> 를 통해 저장/로드된다.
    ///
    /// PlayerPrefs 기반의 <see cref="LoadoutStorage"/> 와 병행해서 쓰는 이유:
    /// PlayerPrefs 는 UI 가 즉시(동기적으로) 읽어야 하는 "마지막으로 고른 덱" 같은
    /// 가벼운 값에 적합하고, 이 프로필은 나중에 클라우드 세이브로 옮겨 갈
    /// "진짜 영구 데이터"(해금한 스킬, 총 플레이 시간 등)를 담는다.
    /// 필드 번호(ProtoMember)는 한 번 배포하면 재사용하지 말 것 — 추가만 하고 삭제는 피한다.
    /// </summary>
    [ProtoContract]
    public sealed class PlayerProfileData
    {
        [ProtoMember(1)] public string PlayerName;
        [ProtoMember(2)] public SkillLoadout LastLoadout;
        [ProtoMember(3)] public ushort[] UnlockedSkillIds = System.Array.Empty<ushort>();
        [ProtoMember(4)] public float TotalPlaytimeSeconds;
        [ProtoMember(5)] public int TutorialCompletedFlag; // 0/1. bool 대신 int 로 둬서 나중에 단계별 플래그로 확장하기 쉽게.
    }
}
