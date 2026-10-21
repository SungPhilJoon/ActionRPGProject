# 에셋 현황과 요청 목록 (2026-10-14 갱신)

## 1. 사용 중인 외부 에셋

2026-10-14 작업에서 프로젝트의 `Assets/ExternalAssets` 에셋으로 교체했습니다. **외부 에셋 원본 파일은 수정하지 않았습니다.** 머티리얼은 URP 복사본을 만들고, 프리팹은 `Art/Generated` / `Content` 에 새로 생성했습니다.

| 에셋 | 사용처 | 비고 |
|---|---|---|
| **unity-chan!** | 플레이어 캐릭터 | 툰 셰이더 → URP Lit 변환 (툰 룩 손실), 구 입력 스크립트 제거 |
| **ExplosiveLLC RPG Character Mecanim Animation Pack FREE** | 플레이어 Idle/Walk/Run, 2Hand-Sword 콤보 3타, 회피, 피격, 사망, 검 모델 | Humanoid 리타겟 |
| **ExplosiveLLC Crafting Mecanim Animation Pack** | 건축(Hammer-Wall), 벌목(Chop), 채광(Swing-Vertical), 채집(Gather), 줍기, 상인(Crafter), 창 모델 | |
| **Magic Pig Games (Infinity PBR)** | 몬스터: Medusa, CobraSnake, Troll, Minotaur(보스) | 모델 Generic 클립, AnimationEvent 끔 (Issues 20) |
| **ASTROFISH_GAMES** | 성 건축 모듈(바닥/벽/창벽/문벽/기둥/램프/작업대/제련소/관), 폐허 마을 건물, 횃불 기둥 | |
| **TerrainSampleAssets** | 침식 높이맵, Terrain Layer 8종, 풀/고사리 메시, 덤불 | 풀 머티리얼은 HDRP 용이라 URP 로 재생성 (Issues 19) |
| **TheTalesFactory** | 포토스캔 산악 바위 (산맥, 절벽, 아레나, 채집 바위) | |
| **SpecialSkillsEffectsPack (GAPH)** | 스킬·상태이상·발사체 이펙트 (Issues 32) | 원본 무수정, PooledEffect 래퍼 |
| **WeatherMaker 8.1.2** | 하늘, 태양/달, 구름, 햇빛 | URP 수동 통합 (URP.md 2절) |
| Kenney (CC0) | 지형 나무(재채색), 아이템 아이콘 | 이전 단계에서 사용, 나무는 대체재 |

## 2. 부족한 에셋 (조율 요청)

| 우선순위 | 필요 에셋 | 현재 대체 | 비고 |
|---|---|---|---|
| ★★★ | **사실적 나무 (SpeedTree / 빌보드 지원)** | Kenney 로우폴리 나무 재채색 | 대자연 오픈월드와 스타일 차이가 가장 큼. 프로젝트 안에 해당 에셋을 찾지 못함 |
| ★★☆ | **흡혈/시전 애니메이션** | 시전 = Unarmed-Attack-R3, 킥 = Unarmed-Attack-L2 | FREE 팩에 주문 시전 모션이 없음 |
| ★★☆ | **URP 용 UnityChanToonShader** | URP Lit | unity-chan 툰 룩 복원 |
| ★★☆ | **스킬 아이콘** (최소 15개) | 색 + 이름 텍스트 | AbilityTable.Icon |
| ★★☆ | **아이템 아이콘** | Kenney 3D 프리뷰 이미지 | ItemTable.Icon |
| ★★☆ | **한글 폰트 (TMP SDF)** | 영문 LiberationSans | 있으면 StringTable 만 번역하면 됨 |
| ★★☆ | 성 모듈 추가: 계단, 지붕, 관(전용 모델) | 관 = Crafting Box | ASTROFISH 에 맞는 관 모델 없음 |
| ★☆☆ | 사운드 (타격, 스킬, 앰비언트, BGM) | 없음 | 사운드 시스템 미구현. Magic Pig 클립의 PlayAudio 이벤트도 꺼 둠 |
| ★☆☆ | 물 셰이더 (호수용, 유한 크기) | URP 반투명 평면 | WeatherMaker 물은 무한 바다 프리팹 |
