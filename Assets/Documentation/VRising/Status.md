# 현재 개발 상태 (2026-10-14 기준)

V Rising 리버스 데모 `BLOODLINE` 의 현재 상태입니다. 구현 세부는 각 문서를 참고하세요.

| 문서 | 내용 |
|---|---|
| [README.md](README.md) | 실행 방법, 조작, 폴더 구조, 데이터 테이블, 툴 |
| [CodeStructure.md](CodeStructure.md) | 코드 폴더 / 어셈블리 구조 (2026-10-13 재정리) |
| [World.md](World.md) | **이번 작업** — 외부 에셋 교체, 512m 오픈월드, WeatherMaker 햇빛 |
| [StateTree.md](StateTree.md) | 언리얼 StateTree 구현, 몬스터 AI 적용, StateTree 에디터 |
| [GAS.md](GAS.md) / [URP.md](URP.md) / [DesignPatterns.md](DesignPatterns.md) | 시스템 설명 |
| [Issues.md](Issues.md) | 문제와 해결 (1~17, 19~32) + 성능 관찰 포인트 (18) |
| [AssetRequests.md](AssetRequests.md) / [Multiplayer.md](Multiplayer.md) | 에셋 요청, 협동 확장 계획 |

## 1. 규모

| 어셈블리 | 파일 | 줄 수 | 내용 |
|---|---|---|---|
| Framework.Runtime | 29 | 약 4,400 | GAS, StateTree, 매니저/이벤트/풀, Addressables 로더, CSV 테이블, UIFactory |
| Framework.Editor | 4 | 약 1,100 | StateTree 에디터, CSV 변환, MaterialUpgradeUtil |
| Game.Runtime | 74 | 약 8,800 | 액터, 어빌리티, 몬스터 AI(StateTree 노드), 월드, 인벤토리, 건축, 치트, UI |
| Game.Editor | 16 | 약 3,400 | 테이블 임포트/검증, 콘텐츠/월드 빌더, Addressables, 밸런스 툴, UI Factory |
| Assembly-CSharp (Game/Integration) | 1 | 약 120 | WeatherMakerSkyBridge |
| 합계 | 124 | 약 17,800 | |

데이터: CSV 18종, StateTree 에셋 3종, AssetTable 61행.

## 2. 기능별 상태

| 영역 | 상태 | 비고 |
|---|---|---|
| 오프라인 1인 플레이 | ✅ 동작 | 타이틀 → 싱글 플레이 |
| 4인 협동 | ❌ 미구현 | 설계만 (Multiplayer.md) |
| 이동/카메라/입력 | ✅ | 키 바인딩은 InputTable.csv |
| 전투 (GAS) | ✅ | 콤보 3타, 무기 스킬, 주문, 궁극기, 대시, 상태이상, 보호막, 치명타, 레벨 보정 |
| **플레이어 캐릭터** | ✅ **교체** | unity-chan! + ExplosiveLLC RPG 애니메이션 (Idle/이동/공격 3타/회피/피격/사망) |
| **건축·채집 애니메이션** | ✅ **신규** | Crafting Pack: 건축(망치), 벌목, 채광, 채집 |
| **몬스터 모델** | ✅ **교체** | Magic Pig: 메두사, 코브라, 트롤, 미노타우로스(보스) |
| **성 건축 모듈** | ✅ **교체** | ASTROFISH 10종, 3m 그리드, 벽은 가장자리·기둥/램프는 모서리 스냅 |
| **월드** | ✅ **신규** (일부 미검증) | 512m 오픈월드, 지역 9개 (World.md) |
| **하늘 / 햇빛** | ✅ **WeatherMaker** | 게임 시간 → WeatherMaker, 태양 방향 → 햇빛 판정 |
| 지형 풀 (Detail) | ⚠️ 조사 중 | 편집 모드에서는 보이고 Play 에서 안 보임 (Issues 19) |
| 햇빛 / 낮밤 | ✅ | 그늘 판정 = 그림자 메시와 같은 모양의 콜라이더 레이캐스트 (지형 나무 포함, Issues 26). 밤 밝기 조정 (Issues 27) |
| 흡혈 / 혈액 / V Blood | ✅ | 혈액 5종 × 3단계, 보스 흡혈 → 궁극기 해금 |
| 채집 / 제작 / 건축 | ✅ | 제작은 즉시 완료, 성 심장 연료/붕괴 없음 |
| 상점 (전부 무료) | ✅ | 가격 0 검증 포함 |
| **입력 버퍼 / 콤보** | ✅ 신규 | 공격 중 입력을 순서대로 처리, 늦으면 콤보 초기화 (Issues 30) |
| **V Rising 상태이상** | ✅ 신규 | Leech·Ignite·Chill/Freeze·Static·Condemn·Weaken, Consume, Immaterial (GAS.md) |
| **스킬 이펙트** | ✅ 신규 | SpecialSkillsEffectsPack (GAPH 셰이더) 21종 + 발사체 5종 |
| 몬스터 AI | ✅ StateTree | 근접/원거리/보스(2페이즈) 3종 트리 |
| 몬스터 체력바 | ✅ | 레벨 배지 포함 |
| 무기 손 위치 | ✅ | unity-chan 오른손 소켓 자동 계산 (Issues 25) |
| 세이브/로드 | ❌ | |
| 사운드 | ❌ | 에셋 없음 |
| 한글 UI | ❌ | 한글 TMP 폰트 필요 (AssetRequests) |

## 3. 툴

| 툴 | 메뉴 | 상태 |
|---|---|---|
| 셋업 일괄 실행 | Game > Setup > 0. Run All | ✅ (외부 에셋 기반 콘텐츠·월드 생성 포함) |
| CSV 임포트/검증 | Game > Data | ✅ CSV 저장 시 자동 재변환 |
| 밸런스 툴 | Window > Game > Balance Tool / F8 | ✅ Live + Simulator |
| UI Factory | Window > Game > UI Factory | ✅ 스크립트 생성, 미리보기, 프리팹 굽기 |
| StateTree Editor | Window > Framework > StateTree Editor (또는 Game > AI > StateTree Editor, 에셋 더블클릭) | ✅ 트리 뷰 / Details / Validate / Live Debug |
| 치트 콘솔 | ` 키 | ✅ 22개 명령 (`tp <x> <z>` 로 지역 이동, `time <h>` 로 햇빛 확인) |

## 4. 이번 작업 요약 (외부 에셋 + 오픈월드 + WeatherMaker)

1. **캐릭터**: 플레이어를 unity-chan 으로 교체하고, RPG Character FREE 애니메이션을 Humanoid 로 리타겟. 검 소켓은 같은 포즈를 두 캐릭터에 샘플링해 자동으로 계산.
2. **건축/채집 모션**: Crafting Pack 으로 건축(설치 시 1.1초 이동 잠금), 벌목·채광·채집. 근접 공격 시 주변에 적이 없고 채집 노드가 있으면 채집 모션이 나온다.
3. **몬스터**: Magic Pig 4종을 기존 4개 슬롯(근접/원거리/브루트/보스)에 연결. 이름(StringTable), 사거리·크기(CharacterTable) 조정.
4. **성 건축**: ASTROFISH 모듈 10종. 구조물 종류별 스냅(중심/가장자리/모서리).
5. **월드**: 512m Terrain, 침식 높이맵, TerrainSampleAssets 레이어 8종·풀, TheTalesFactory 바위, ASTROFISH 폐허 마을, 호수, 트롤 능선, 보스 아레나. SpawnTable 을 새 지역에 맞춰 재작성.
6. **햇빛**: WeatherMaker 하늘/태양/달/구름. 게임 시간이 주인이며, 태양 방향을 판정에 역으로 사용. 태양 밝기 ×1.7, 볼륨 노출 조정.
7. **공통 유틸**: `MaterialUpgradeUtil` (외부 머티리얼 → URP Lit 복사본, 원본 무수정).
8. **Play 확인**: 몬스터 20 / 채집 노드 231 스폰, SkyBridge 활성, unity-chan URP 렌더 + 검 장착, 나무·구름 그림자, 폐허 마을·상인·횃불 기둥 표시.

## 5. 알려진 문제 / 다음 단계 후보

- **지형 풀이 Play 모드에서 보이지 않음** — Scatter Mode 를 고쳐 편집 모드에서는 보이지만, Play 중 (215,150) 의 Detail 값이 0. 원인 조사가 남아 있음 (Issues 19).
- 육안 확인을 못 한 부분: Troll Ridge / Minotaur Arena / 호수 근접 화면, 신규 몬스터 전투 애니메이션 전체, 건축 모듈 이음새, 밤 화면 톤.
- unity-chan 툰 셰이딩이 URP Lit 변환으로 사라짐 (URP 용 툰 셰이더 필요).
- 사실적 나무 에셋이 없어 Kenney 나무를 재채색해 사용 (AssetRequests ★★★).
- manifest 에 HDRP 패키지가 있음 (활성 파이프라인은 URP). 제거 여부는 판단 필요.
- 사용자 작성 `SunExposure.OnDrawGizmos` 에 null 체크가 없고, `DrawLine(origin, toSun)` 은 `origin + toSun * RayLength` 여야 함 (보고만 했고 수정하지 않음).
- 보스 2페이즈 격노(이펙트 23)는 8초 지속. 계속 유지하려면 Infinite 이펙트로 데이터만 바꾸면 됨.
- 다음 단계 후보: 풀 문제 마무리, 멀티플레이(NGO), 세이브, VFX/아이콘/한글 폰트 교체, StateTree Linked Subtree.
