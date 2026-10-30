# 에셋 현황과 요청 목록 (2026-10-14 갱신)

## 1. 사용 중인 외부 에셋

2026-10-14 작업에서 프로젝트의 `Assets/ExternalAssets` 에셋으로 교체했습니다. **외부 에셋 원본 파일은 수정하지 않았습니다.** 머티리얼은 URP 복사본을 만들고, 프리팹은 `Art/Generated` / `Content` 에 새로 생성했습니다.

| 에셋 | 사용처 | 비고 |
|---|---|---|
| **unity-chan!** | 플레이어 캐릭터 | 툰 셰이더 → URP Lit → 2026-10-30 자체 `Game/Character` 툰 모드로 툰 룩 복원 (URP.md 11절), 구 입력 스크립트 제거 |
| **ExplosiveLLC RPG Character Mecanim Animation Pack FREE** | 플레이어 Idle/Walk/Run, 2Hand-Sword 콤보 3타, 회피, 피격, 사망, 검 모델 | Humanoid 리타겟 |
| **ExplosiveLLC Crafting Mecanim Animation Pack** | 건축(Hammer-Wall), 벌목(Chop), 채광(Swing-Vertical), 채집(Gather), 줍기, 상인(Crafter), 창 모델 | |
| **Magic Pig Games (Infinity PBR)** | 몬스터: Medusa, CobraSnake, Troll, Minotaur(보스) | 모델 Generic 클립, AnimationEvent 끔 (Issues 20) |
| **ASTROFISH_GAMES** | 성 건축 모듈(바닥/벽/창벽/문벽/기둥/램프/작업대/제련소/관), 폐허 마을 건물, 횃불 기둥 | |
| **TerrainSampleAssets** | 침식 높이맵, Terrain Layer 8종, 풀/고사리 메시, 덤불 | 풀 머티리얼은 HDRP 용이라 URP 로 재생성 (Issues 19) |
| **TheTalesFactory** | 포토스캔 산악 바위 (산맥, 절벽, 아레나, 채집 바위) | |
| **SpecialSkillsEffectsPack (GAPH)** | 스킬·상태이상·발사체 이펙트 (Issues 32) | 원본 무수정, PooledEffect 래퍼 |
| ~~WeatherMaker 8.1.2~~ | (2026-10-30 제거 — 연산 대비 효과가 작음) | URP 기본 하늘 + 포스트 프로세싱으로 대체 (URP.md 1 · 3 · 10절) |
| Kenney (CC0) | 아이템 아이콘 | 나무는 2026-10-29 절차 나무로 교체 |
| **Kenney UI Pack: RPG Expansion (CC0)** | UI 판넬 · 버튼 · 슬롯 · 게이지 (흑백 + 고딕 틴트로 변환, UI.md 4절) | 2026-10-29 OpenGameArt 에서 받음. `Art/ThirdParty/Kenney/UI_RPG` |
| **Poly Haven (CC0)** | 나무껍질 텍스처 `pine_bark`, `bark_brown_02`, `bark_willow_02` (1k, `Art/ThirdParty/PolyHaven/Bark`) + 기존 지형 텍스처 | 2026-10-29 추가 |

## 2. 부족한 에셋 (조율 요청)

| 우선순위 | 필요 에셋 | 현재 대체 | 비고 |
|---|---|---|---|
| ★★☆ | **사실적 나무 (SpeedTree / 빌보드 지원)** | 절차 나무 4종 (2026-10-29, World.md 7.1) | 로우폴리보다는 오픈월드에 가깝지만 실사 에셋에는 못 미침. 받으면 `TreeFactory` 프리팹 교체만 하면 됨 (배치 · 가림 · 건축 제거는 그대로) |
| ★★☆ | **흡혈/시전 애니메이션** | 시전 = Unarmed-Attack-R3, 킥 = Unarmed-Attack-L2 | FREE 팩에 주문 시전 모션이 없음 |
| ☆☆☆ | ~~URP 용 UnityChanToonShader~~ | `Game/Character` 툰 모드 (2026-10-30) | 더 정교한 툰 룩이 필요할 때만 |
| ★★☆ | **고딕 · 다크 판타지 UI 팩** | Kenney RPG UI(CC0)를 흑백 + 검붉은 틴트로 변환 | 2026-10-29 My Assets(받아 둔 15개)에 UI 팩 없음. Asset Store 팩은 계정에서 My Assets 에 추가해 받아야 함 → 받으면 `UISkin` 스프라이트만 교체 |
| ★★☆ | **스킬 아이콘** (최소 15개) | 색 + 이름 텍스트 | AbilityTable.Icon |
| ★★☆ | **아이템 아이콘** | Kenney 3D 프리뷰 이미지 | ItemTable.Icon |
| ★★☆ | **한글 폰트 (TMP SDF)** | 영문 LiberationSans | 있으면 StringTable 만 번역하면 됨 |
| ★★☆ | 성 모듈 추가: 계단, 지붕, 관(전용 모델) | 관 = Crafting Box | ASTROFISH 에 맞는 관 모델 없음 |
| ★☆☆ | 사운드 (타격, 스킬, 앰비언트, BGM) | 없음 | 사운드 시스템 미구현. Magic Pig 클립의 PlayAudio 이벤트도 꺼 둠 |
| ~~★☆☆~~ | ~~물 셰이더~~ | **해결** — 직접 만든 `Game/Water` (URP.md 9절) | |
