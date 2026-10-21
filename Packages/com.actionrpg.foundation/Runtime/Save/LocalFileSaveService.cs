using System;
using System.IO;
using ActionRPG.Core.Serialization;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ActionRPG.Core.Save
{
    /// <summary>
    /// 로컬 디스크(Application.persistentDataPath)에 protobuf-net 바이너리로 저장하는 기본 구현.
    /// 실제 파일 I/O 는 스레드풀에서 하고, Unity API 를 만지는 부분만 메인 스레드로 돌아온다.
    ///
    /// 나중에 클라우드로 옮길 때: 이 클래스를 참고해서 같은 인터페이스로
    /// CloudSaveService 를 만들고, SaveManager.Configure(new CloudSaveService(...)) 한 줄로 교체한다.
    /// 로컬 세이브는 "오프라인 캐시" 용도로 계속 남겨 두고, 클라우드 실패 시 폴백하는
    /// CompositeSaveService(로컬 + 클라우드)로 감싸는 것도 이 구조에서 자연스럽게 확장된다.
    /// </summary>
    public sealed class LocalFileSaveService : ISaveService
    {
        private const int CurrentSchemaVersion = 1;
        private const string FileExtension = ".sav";

        private readonly string _rootDirectory;
        private readonly IBinarySerializer _serializer;

        public LocalFileSaveService(IBinarySerializer serializer = null, string rootDirectory = null)
        {
            _serializer = serializer ?? ProtoBinarySerializer.Default;
            _rootDirectory = rootDirectory ?? Path.Combine(Application.persistentDataPath, "Saves");
        }

        private string PathFor(string slot) => Path.Combine(_rootDirectory, SanitizeSlot(slot) + FileExtension);

        private static string SanitizeSlot(string slot)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) slot = slot.Replace(c, '_');
            return slot;
        }

        public async UniTask<SaveResult> SaveAsync<T>(string slot, T data)
        {
            string path = PathFor(slot);
            var envelope = SaveEnvelope<T>.Wrap(data, CurrentSchemaVersion);

            await UniTask.SwitchToThreadPool();
            try
            {
                Directory.CreateDirectory(_rootDirectory);

                // 쓰다가 죽어도 기존 파일이 깨지지 않도록 임시 파일에 쓴 뒤 교체한다.
                string tempPath = path + ".tmp";
                using (var stream = File.Create(tempPath))
                    _serializer.Serialize(stream, envelope);

                File.Copy(tempPath, path, overwrite: true);
                File.Delete(tempPath);
                return SaveResult.Success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocalFileSaveService] '{slot}' 저장 실패: {e}");
                return SaveResult.IoError;
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }
        }

        public async UniTask<SaveLoadResult<T>> LoadAsync<T>(string slot)
        {
            string path = PathFor(slot);

            await UniTask.SwitchToThreadPool();
            try
            {
                if (!File.Exists(path)) return SaveLoadResult<T>.Fail(SaveResult.NotFound);

                using var stream = File.OpenRead(path);
                var envelope = _serializer.Deserialize<SaveEnvelope<T>>(stream);

                if (envelope == null) return SaveLoadResult<T>.Fail(SaveResult.Corrupted);
                if (envelope.SchemaVersion > CurrentSchemaVersion) return SaveLoadResult<T>.Fail(SaveResult.VersionMismatch);

                return SaveLoadResult<T>.Ok(envelope.Payload);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocalFileSaveService] '{slot}' 불러오기 실패: {e}");
                return SaveLoadResult<T>.Fail(SaveResult.Corrupted);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
            }
        }

        public async UniTask<bool> ExistsAsync(string slot)
        {
            string path = PathFor(slot);
            await UniTask.SwitchToThreadPool();
            bool exists = File.Exists(path);
            await UniTask.SwitchToMainThread();
            return exists;
        }

        public async UniTask DeleteAsync(string slot)
        {
            string path = PathFor(slot);
            await UniTask.SwitchToThreadPool();
            try { if (File.Exists(path)) File.Delete(path); }
            finally { await UniTask.SwitchToMainThread(); }
        }
    }
}
