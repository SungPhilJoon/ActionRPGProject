using System.IO;
using ProtoBuf;

namespace ActionRPG.Core.Serialization
{
    /// <summary>
    /// protobuf-net 기반 직렬화 구현.
    ///
    /// JSON 대비 크기가 훨씬 작고(필드 번호 기반 varint 인코딩), 파싱도 빠르다.
    /// 세이브 데이터와 네트워크 스냅샷(주기적으로 보내는 큰 상태 묶음 등,
    /// Netcode 의 NetworkVariable 로는 비효율적인 대용량 페이로드)에 사용한다.
    ///
    /// 사용법: 직렬화할 타입에 [ProtoContract] 를 붙이고 각 필드에
    /// [ProtoMember(1)], [ProtoMember(2)] ... 순서를 명시적으로 매긴다.
    /// 필드 번호는 한 번 배포하면 절대 재사용하지 말 것 (하위 호환 깨짐).
    ///
    /// ⚠ AOT / IL2CPP 주의:
    /// protobuf-net 의 기본 동작(RuntimeTypeModel.Default)은 최초 직렬화 시
    /// System.Reflection.Emit 으로 직렬화 코드를 동적 생성한다. 이는 Mono/CoreCLR
    /// 빌드(에디터, 스탠드얼론 개발 빌드)에서는 문제없이 동작하지만, IL2CPP 로
    /// 빌드하는 모바일/콘솔 타겟에서는 동적 코드 생성이 금지되어 있어 실패한다.
    /// 실제 IL2CPP 플랫폼에 배포하기 전에는 반드시
    ///   RuntimeTypeModel.Default.Compile("SerializerModel", "SerializerModel.dll")
    /// 로 정적 직렬화 어셈블리를 미리 생성해 Plugins 에 넣거나,
    /// <c>[ProtoContract(SkipConstructor = true)]</c> 조합 + AOT 사전 컴파일 모델을
    /// 사용해야 한다. 지금 단계(에디터/스탠드얼론 검증)에서는 기본 모델로 충분하다.
    /// </summary>
    public sealed class ProtoBinarySerializer : IBinarySerializer
    {
        public static readonly ProtoBinarySerializer Default = new ProtoBinarySerializer();

        public void Serialize<T>(Stream destination, T value) => Serializer.Serialize(destination, value);

        public T Deserialize<T>(Stream source) => Serializer.Deserialize<T>(source);

        public byte[] Serialize<T>(T value)
        {
            using var ms = new MemoryStream();
            Serializer.Serialize(ms, value);
            return ms.ToArray();
        }

        public T Deserialize<T>(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            return Serializer.Deserialize<T>(ms);
        }
    }
}
