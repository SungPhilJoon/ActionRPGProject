using System.IO;

namespace ActionRPG.Core.Serialization
{
    /// <summary>
    /// 바이너리 직렬화 백엔드 추상화.
    ///
    /// 지금은 <see cref="ProtoBinarySerializer"/>(protobuf-net) 하나뿐이지만,
    /// 세이브 포맷이나 네트워크 스냅샷 포맷을 바꿔야 할 때(예: 커스텀 wire-format,
    /// MemoryPack, 압축 추가 등) 이 인터페이스만 구현하면 SaveManager 나
    /// 스냅샷 코드를 건드리지 않고 교체할 수 있다.
    /// </summary>
    public interface IBinarySerializer
    {
        void Serialize<T>(Stream destination, T value);
        T Deserialize<T>(Stream source);
        byte[] Serialize<T>(T value);
        T Deserialize<T>(byte[] bytes);
    }
}
