using System;
using ProtoBuf;

namespace ActionRPG.Core.Serialization
{
    /// <summary>
    /// 모든 세이브 파일이 공통으로 갖는 헤더.
    /// 스키마 버전을 넣어 두면, 나중에 SaveData 구조가 바뀌었을 때
    /// LocalFileSaveService 가 마이그레이션 여부를 판단할 수 있다.
    /// </summary>
    [ProtoContract]
    public sealed class SaveEnvelope<T>
    {
        [ProtoMember(1)] public int SchemaVersion;
        [ProtoMember(2)] public long SavedAtUnixSeconds;
        [ProtoMember(3)] public T Payload;

        public static SaveEnvelope<T> Wrap(T payload, int schemaVersion) => new SaveEnvelope<T>
        {
            SchemaVersion = schemaVersion,
            SavedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Payload = payload
        };
    }
}
