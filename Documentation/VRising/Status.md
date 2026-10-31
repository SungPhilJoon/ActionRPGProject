# 현재 개발 상태 (2026-10-28 기준, 규모 표는 10-21 측정)

V Rising 리버스 데모 `BLOODLINE` 의 현재 상태입니다. **알려진 문제와 미구현 범위는 이 문서가 원본**이고, 다른 문서는 여기를 링크합니다.
실행 방법·조작·툴은 [README.md](README.md), 변경 이력은 [CHANGELOG.md](CHANGELOG.md).

## 1. 규모

| 어셈블리 | 파일 | 줄 수 | 내용 |
|---|---|---|---|
| Framework.Runtime | 30 | 약 4,700 | GAS, StateTree, 매니저/이벤트/풀, Addressables 로더, CSV 테이블, UIFactory |
| Framework.Editor | 5 | 약 1,300 | StateTree 에디터, CSV 변환, MaterialUpgradeUtil, ShadeColliderUtil |
| Game.Runtime | 77 | 약 9,500 | 액터, 어빌리티, 몬스터 AI(StateTree 노드), 월드, 인벤토리, 건축, 치트, UI |
| Game.Editor | 17 | 약 3,700 | 테이블 임포트/검증, 콘텐츠/월드 빌더, Addressables, 밸런스 툴, UI Factory |
| 합계 | 130 | 약 19,300 | |

데이터: CSV 19종, StateTree 에셋 3종, AssetTable 84행.

## 2. 기능별 상태

| 영역 | 상태 | 비고 |
|---|---|---|
| 오프라인 1인 플레이 | ✅ | 타이틀 → 싱글 플레이 (2026-10-21 Play 확인) |
| 4인 협동 | ❌ 미구현 | 설계만 ([Multiplayer.md](Multiplayer.md)) |
| 이동/카메라/입력 | ✅ | 키 바인딩은 InputTable.csv |
| 전투 (GAS) | ✅ | 콤보 3타, 무기 스킬, 주문, 궁극기, 대시, 상태이상, 보호막, 치명타, 레벨 보정 ([GAS.md](GAS.md)) |
| 애니메이션 재생 | ✅ | AnimationTable Index 로 재생 (Speed·파라미터·루트모션이 데이터) |
| 입력 버퍼 / 콤보 | ✅ | 공격 중 입력을 순서대로 처리, 늦으면 콤보 초기화 (Issues 30) |
| V Rising 상태이상 | ✅ | Leech·Ignite·Chill/Freeze·Static·Condemn·Weaken, Consume, Immaterial |
| 플레이어 캐릭터 | ✅ | unity-chan! + ExplosiveLLC RPG 애니메이션 (Idle/이동/공격 3타/회피/피격/사망) |
| 건축·채집 애니메이션 | ✅ | Crafting Pack: 건축(망치), 벌목, 채광, 채집 |
| 몬스터 | ✅ | Magic Pig: 메두사, 코브라, 트롤, 미노타우로스(보스) / StateTree AI 근접·원거리·보스(2페이즈) ([StateTree.md](StateTree.md)) |
| 몬스터 체력바 | ✅ | 레벨 배지 포함 |
| 스킬 이펙트 | ✅ | SpecialSkillsEffectsPack 21종 + 발사체 5종 |
| 월드 | ✅ (일부 미검증) | 512m 오픈월드, 지역 9개 + 강 ([World.md](World.md)) |
| 나무 · 풀 | ✅ | 절차 나무 4종(가문비/활엽수/자작/고사목) 약 3,500그루 + 덤불, 풀 12종 + 바람 셰이더 (World.md 7절) |
| 강 · 호수 물 | ✅ | URP 물 셰이더 (깊이 색, 굴절, 흐름, 거품) — URP.md 9절 |
| 건축 시 나무 · 풀 제거 | ✅ | `StructurePlacedEvent` → FoliageManager (World.md 7.4) |
| 카메라 가림 숨김 | ✅ | 메시만 숨기고 그림자 유지, 전략 교체 가능 ([CameraOcclusion.md](CameraOcclusion.md)) |
| 하늘 / 햇빛 / 낮밤 | ✅ | URP 절차 스카이박스 + 안개 + 구운 하늘 반사 (2026-10-30 WeatherMaker 제거), 그늘 판정 = 그림자와 같은 모양의 콜라이더 레이캐스트 ([URP.md](URP.md)) |
| 포스트 프로세싱 | ✅ | 볼륨 6개(낮/밤/피사계 심도/햇빛 화상/저체력/사망) + 대기 풀스크린 패스, 렌즈 플레어 · 스플릿 토닝 · 모션 블러 (URP.md 3 · 10절) |
| 캐릭터 · 몬스터 셰이더 | ✅ | `Game/Character` — 플레이어 툰, 몬스터 PBR + 림라이트 + 외곽선, 피격 플래시 (URP.md 11절) |
| 지형 풀 (Detail) | ⚠️ 조사 중 | 아래 3절 |
| 흡혈 / 혈액 / V Blood | ✅ | 혈액 5종 × 3단계, 보스 흡혈 → 궁극기 해금 |
| 채집 / 제작 / 건축 | ✅ | 제작은 즉시 완료, 성 심장 연료/붕괴 없음 |
| 상점 (전부 무료) | ✅ | 가격 0 검증 포함 |
| 에디터로 어빌리티 · AI · 몬스터 만들기 | ✅ | GAS Tool / StateTree Tool / MonsterAuthoring 인스펙터 ([Tool.md](Tool.md)) |
| 자동 테스트 | ✅ | Play 모드 17개 (GAS · StateTree · 건물/몬스터/보스 시나리오 · 월드 식생/가림/물 · 의존성 기록 5) 전부 통과 (2026-10-30) ([README.md 7절](README.md#7-테스트)) |
| UI | ✅ | 프리팹 + Addressables 로드 (런타임 조립 없음), Kenney CC0 기반 고딕 스킨 ([UI.md](UI.md)) |
| Addressables 의존성 툴 | ✅ | 의존성 지도 (정적 검사 · 플레이 전 검사 · 실시간 · 테스트 기록 → 이슈 자동 갱신) ([Dependency.md](Dependency.md)) |
| 세이브/로드 | ❌ | |
| 사운드 | ❌ | 에셋 없음 ([AssetRequests.md](AssetRequests.md)) |
| 한글 UI | ✅ | Unity Localization (GameText en/ko) + Noto Sans KR 동적 폰트 (2026-10-31) |

## 3. 알려진 문제

| 문제 | 상태 | 참고 |
|---|---|---|
| **지형 풀이 Play 모드에서 보이지 않음** — 편집 모드 (256,100) 에서는 그려지지만 Play 중 (215,150) 의 Detail 합이 0. 시작 지점(256,96)은 의도적으로 흙바닥이라 0 이 맞음 | 2026-10-29 Detail 재작성(Game/Foliage 셰이더, 마스크 밀도) 후 Play 화면 (122,300) 강가에서 풀이 그려짐을 확인. (215,150) 은 재확인 필요 | Issues 19 |
| **메두사 걷기 중 메시가 앞서 나갈 수 있음** — 제자리 보정 런타임 컴포넌트(`AnimationInPlace`)를 2026-10-21 삭제. 코브라·트롤은 임포트 설정만으로 해결됐지만 메두사는 그것만으로 안 됐었음 | 재확인 필요 | Issues 35 |
| 나무는 절차 생성 (사실적 SpeedTree 급은 아님) — 잎 카드가 가까이서 평면으로 보일 수 있음 | 실제 나무 에셋을 받으면 `TreeFactory` 프리팹만 교체 | AssetRequests ★★☆ |
| **Addressables 참조를 반납하지 않음** — 로드 코드 전체에 `AssetLoader.Release` 호출이 없어 같은 동작을 반복할 때마다 참조 카운트가 쌓이고 에셋이 메모리에서 내려가지 않음 (몬스터 스폰, 아이콘, 스킬 VFX, 건축 고스트). 의존성 테스트가 발견 | 수정 안 함 (판단 필요) | [Dependency.md 5절](Dependency.md#5-첫-실행-결과-2026-10-30-수정하지-않고-기록만-함), [DependencyIssues.md](DependencyIssues.md) |
| 나무 GameObject 약 4,200개 (LODGroup, 그늘 MeshCollider) — 이전 Terrain Tree 2,300 보다 많음 | 최적화는 직접 진행 (Issues 18) | |
| manifest 에 HDRP 패키지가 있음 (활성 파이프라인은 URP) | 제거 여부 판단 필요 | URP.md |
| `SunExposure.OnDrawGizmos` 에 null 체크가 없고, `DrawLine(origin, toSun)` 은 `origin + toSun * RayLength` 여야 함 | 보고만 함 (사용자 작성 코드) | |
| Frame Debugger 를 켜면 화면이 깜빡이고 이벤트가 생겼다 사라짐 | 후보 3개 중 WeatherMaker 2개는 제거로 해당 없음, 남은 후보는 Scene 뷰 카메라의 횃불 그림자, 미확정 | Issues 37 |
| 보스 2페이즈 격노(이펙트 23)가 8초 뒤 끝남 | 계속 유지하려면 Infinite 이펙트로 데이터만 변경 | StateTree.md |

**육안 확인을 다 못 한 곳**: Troll Ridge, Minotaur Arena, 호수 근접 화면, 신규 몬스터 전투 애니메이션 전체, 건축 모듈 이음새(벽-기둥), 건축·상호작용 애니메이션(AnimationTable 전환 후).

## 4. 미구현 범위

- 멀티플레이(최대 4인) — 오프라인 우선 요구에 따라 설계만 ([Multiplayer.md](Multiplayer.md))
- 세이브/로드, 성 심장 혈액 연료/붕괴, 하인(Servant), 연구 트리
- 제작은 즉시 완료 (CraftTime 컬럼은 예약)
- 최적화/프로파일링은 요구사항대로 손대지 않음 — 의심 지점은 [Issues.md 18](Issues.md#18-성능-관찰-포인트-최적화는-직접-진행-예정), 측정 툴 · 방법은 [Optimization.md](Optimization.md)

## 5. 다음 단계 후보

풀 문제 마무리, 메두사 제자리 재확인, 멀티플레이(NGO), 세이브, VFX/아이콘 교체, StateTree Linked Subtree, 그래프 어빌리티 분기/입력 대기, 툴의 삭제 기능, 테스트 확장(흡혈·제작·사망/부활).
