# ActionRPG Foundation

게임플레이 코드(Assets/Scripts)와 분리해 둔, 자주 바뀌지 않는 기반 계층.
독립 패키지로 만들어 둔 이유는 세 가지다.

1. **버전 관리 단위 분리** — 전투 기초 타입(IDamageable, Faction, DamageInfo 등)은
   한 번 자리잡으면 거의 바뀌지 않는다. 매일 바뀌는 게임플레이 코드와 같은 폴더에
   있으면 diff 가 섞여서 리뷰하기 어렵다.
2. **재사용성** — 다음 프로젝트에서도 이 폴더만 복사하면 스탯/전투/세이브/직렬화
   기반이 그대로 따라온다.
3. **컴파일 단위 분리** — Assets/Scripts 를 고칠 때 이 어셈블리는 다시 컴파일되지 않는다
   (asmdef 경계 덕분에 반대 방향으로는 의존하지 않으므로).

## 구성

| 폴더 | 내용 |
|---|---|
| `Runtime/Core` | IDamageable, Faction, DamageInfo, StatBlock, GameEventBus, ActorRegistry, GameConstants |
| `Runtime/Serialization` | protobuf-net 기반 `IBinarySerializer` / `ProtoBinarySerializer`, `SaveEnvelope<T>` |
| `Runtime/Lifecycle` | `LifecycleTracker` — 오브젝트 생성/소멸 추적 (에디터 툴 연동) |
| `Runtime/Save` | `ISaveService`, `LocalFileSaveService`, `CompositeSaveService`, `CloudSaveServiceStub`, `SaveManager` |
| `Runtime/DataDriven` | `CsvColumnAttribute`, `CsvParser` — CSV 데이터 드리븐의 런타임 쪽 |
| `Runtime/Assets` | `IAssetCatalog`, `AddressableAssetCatalog`, `CatalogService` — Addressables 래퍼 |
| `Editor/DataDriven` | `CsvImportProfile`, `CsvScriptableObjectImporter` — CSV → ScriptableObject 엔진 |
| `Editor/LifecycleTrackerWindow.cs` | Lifecycle Tracker 뷰어 (`ActionRPG > Lifecycle Tracker`) |

## 의존성

`com.unity.netcode.gameobjects`, `com.cysharp.unitask`, `org.nuget.protobuf-net`.
전부 `Packages/manifest.json` 에 등록되어 있으며 OpenUPM scoped registry 를 통해 받는다.

## 확장 시나리오

- **클라우드 세이브 붙이기**: `CloudSaveServiceStub` 을 실제 SDK 호출로 채우고
  `SaveManager.Configure(new CompositeSaveService(cloud, new LocalFileSaveService()))`.
- **직렬화 백엔드 교체**: `IBinarySerializer` 를 구현하는 새 클래스를 만들고
  `LocalFileSaveService` 생성자에 주입.
- **원격 카탈로그로 전환**: Addressables 그룹의 Build/Load Path 를 Remote 로 바꾸기만
  하면 `IAssetCatalog` 쪽 코드는 그대로 유지된다.
