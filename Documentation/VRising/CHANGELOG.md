# 개발 기록 (BLOODLINE)

"언제 무엇을 왜 바꿨는가" 를 시간 순으로 남깁니다. 현재 상태는 [Status.md](Status.md), 문제와 해결의 상세는 [Issues.md](Issues.md).
이전 프로젝트(Netcode 덱/던전 RPG)의 기록은 [../Legacy/CHANGELOG.md](../Legacy/CHANGELOG.md).

---

## 2026-10-24 · 문서 검토 + Documentation 을 Assets 밖으로

- `Assets/Documentation` → 저장소 루트 `Documentation/` (Unity 임포트 대상에서 제외, `.meta` 삭제). 코드 주석의 `Documentation/VRising/…` 경로는 그대로 유효.
- 문서 내용을 코드와 대조해 갱신: Architecture 폴더 트리에 빠진 파일(`Core/Behaviours`, `PooledEffect`, `AbilityInputBuffer`, `ContentBuilder.SkillVfx`) 추가,
  `EExecution` 목록(PercentHeal), `EAssetKind`(Material/Data), Issues·URP 의 옛 문서 링크 수정. 메뉴 경로·치트 명령 24개·키 바인딩은 일치 확인.

## 2026-10-21 · Git 사고 복구 + 저장소 업로드 + 문서 정리

**복구** — 저장소를 새로 만들며 이전 `.git` 을 휴지통으로 버렸고, 이어서 `Revert "프로젝트 로드 Part4"` 가 Part4 로 추가한 파일을 디스크에서 지웠다.
- Part4 커밋(aee10b8)에서 외부 에셋 2,694개 (Magic Pig, SpecialSkillsEffectsPack, RPG Fantasy Items) 복원
- 휴지통의 이전 `.git` 스냅샷(18:15)에서 3,773개 복원: ASTROFISH_GAMES, `Assets/Documentation`, `Assets/Data`, `Assets/Editor`, `AddressableAssetsData`(원래 GUID), `InputSystem_Actions.inputactions` 등
- 모두 디스크에 없는 파일만 원본 그대로 되살렸고, 새로 만든 파일은 없다

**수정**
- `Tables.Bind(null)` NRE — `GameBootstrap.OnDestroy` 가 해제용으로 null 을 넘기는데 `Root.OnBind()` 를 무조건 호출했다. null 이면 해제만 한다.
- `AnimationInPlace` 삭제(사용자 결정, 07:23) 후 몬스터 프리팹 4개에 남은 빈 컴포넌트 제거. 제자리 처리는 FBX 임포트 설정(`Game/Setup/3c`)만 남음 → Issues 35 참고.
- Play 확인: 타이틀 → 월드 스폰(액터 20) → 싱글 플레이 → 평타·Q·E·Space·R·C·T → 종료까지 콘솔 에러·경고 0.

**업로드** — 9.3GB 를 GitHub 에 올리기 위해 `Tools/GitBatchPush` 추가 (용량 기준 배치 → 배치마다 커밋·푸시). 100MB 초과 데모 라이트맵 3개는 Git LFS.

**문서 정리** — 이전 프로젝트 문서를 `Legacy/` 로 분리, CodeStructure + DesignPatterns → [Architecture.md](Architecture.md), 이 CHANGELOG 신설,
중복 설명은 한 곳(원본)에만 두고 나머지는 링크로 바꿈.

## 2026-10-20 · AnimationTable — 애니메이션 재생을 데이터로

- `AnimationTable.csv` 신설: `Index, Speed, Parameters(Type;Name;Value 의 배열), ApplyRootMotion`.
- 재생은 `ActorAnimator.Play(AnimationTable Index, 속도)` 하나로 통일. `EAnim` 은 에디터 AnimatorController 빌더의 상태/트리거 슬롯 이름으로만 남음.
- `AbilityTable.Anim(EAnim)` → `Anims(int[])` (MeleeCombo 는 콤보 단계 순서 `1|2|7`), `ResourceNodeTable.Anim`, `ConstTable.BuildAnim/InteractAnim` 추가.
- `CsvConvert` 가 `[Serializable]` struct 를 `;` 구분(필드 선언 순서)으로 읽도록 확장 (Framework).
- 컨트롤러 빌더가 AnimationTable 에만 있는 파라미터도 생성, Validate 가 없는 애니메이션 Index·Speed≤0 을 검사.

## 2026-10-14 · 외부 에셋 교체 + 512m 오픈월드 + WeatherMaker

1. **캐릭터**: 플레이어를 unity-chan 으로 교체, RPG Character FREE 애니메이션을 Humanoid 리타겟. 검 소켓은 같은 포즈를 두 캐릭터에 샘플링해 자동 계산.
2. **건축/채집 모션**: Crafting Pack 으로 건축(설치 시 1.1초 이동 잠금), 벌목·채광·채집. 주변에 적이 없고 채집 노드가 있으면 근접 공격 대신 채집 모션.
3. **몬스터**: Magic Pig 4종을 근접/원거리/브루트/보스 슬롯에 연결. 이름(StringTable), 사거리·크기(CharacterTable) 조정.
4. **성 건축**: ASTROFISH 모듈 10종, 구조물 종류별 스냅(중심/가장자리/모서리).
5. **월드**: 512m Terrain, 침식 높이맵, TerrainSampleAssets 레이어·풀, TheTalesFactory 바위, 폐허 마을, 호수, 트롤 능선, 보스 아레나. SpawnTable 재작성.
6. **햇빛**: WeatherMaker 하늘/태양/달/구름. 게임 시간이 주인이고 태양 방향을 판정에 역으로 사용.
7. **공통 유틸**: `MaterialUpgradeUtil` (외부 머티리얼 → URP Lit 복사본, 원본 무수정).
8. 태그 비교를 언리얼과 같은 의미로 개편 (GAS.md), V Rising 상태이상 7종 + Consume + Immaterial, 입력 버퍼/콤보 초기화, 스킬 이펙트 팩 적용.
9. Play 확인: 몬스터 20 / 채집 노드 231 스폰, unity-chan URP 렌더 + 검 장착, 나무·구름 그림자, 폐허 마을·상인·횃불 표시.

## 2026-10-13 · 코드 폴더 / 어셈블리 재정리

| 이전 | 이후 |
|---|---|
| `Assets/Game/Framework` (`Game.Framework`) | `Assets/Framework/Runtime` (`Framework.Runtime`) |
| (없음) | `Assets/Framework/Editor` (`Framework.Editor`) 신설 |
| `Assets/Game/Scripts` | `Assets/Game/Runtime` |
| `Scripts/AI` | `Runtime/Monster` |
| `Scripts/Player/GameInput`, `TopDownCamera` | `Runtime/Input`, `Runtime/Camera` |
| `Scripts/Ability/CombatUtil` | `Runtime/Combat` |
| `Scripts/UI/*` (평평함) | `UI/Windows`, `UI/Widgets`, `UI/WorldSpace` |
| `MonsterCharacter.cs` 안의 `LootUtil` | `Runtime/Inventory/LootUtil.cs` |
| `Game/Editor/*` (평평함) | `Setup / Data / Content / World / AI / Tools` |
| `StateTreeEditorWindow` (Game.Editor, EBBKey·EditorPaths 직접 참조) | Framework.Editor 로 이동, 게임 의존 제거 (어트리뷰트 연결) |
| `CsvTableImporter` 의 범용 변환·자동 재변환·폴더 생성 | `TableImportUtility`, `TableImportPostprocessor`, `EditorAssetUtil` (Framework.Editor) |
| CSV 자동 재변환: 경로 규칙으로 테이블 탐색 | 테이블의 `Source` 참조로 탐색 → 폴더 구조와 무관 |

- 모든 이동은 `AssetDatabase.MoveAsset` → `.meta` GUID 유지 → 프리팹·씬의 스크립트 참조 유지 (Missing Script 0 확인).
- 어셈블리 이름 변경으로 StateTree 에셋의 `[SerializeReference]` 노드가 Missing → `[MovedFrom]` 으로 해결 (Issues 17).
- 2026-10-14: asmdef 없는 외부 에셋 어댑터용 `Game/Integration` 추가 (WeatherMakerSkyBridge).
