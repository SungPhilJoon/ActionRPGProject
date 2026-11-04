# Changelog

형식: [Keep a Changelog](https://keepachangelog.com/ko/1.1.0/), 버전: [SemVer](https://semver.org/lang/ko/).

## [0.2.0] - 2026-11-04

### Added
- `StaticBehaviour`: 움직이지 않는 물체. 에디터에서 Batching Static(+ Occlusion · GI 옵션) 자동, 런타임 생성분은 `StaticBatching` 이 프레임 끝에 일괄 Combine, 배칭 뒤 이동 경고.
- `DynamicBehaviour`: 사라지거나 생기는 물체. 정적 플래그 해제, `Show/Hide`(renderer.enabled), `SetTint`(`SharedMaterialVariants`, MaterialPropertyBlock 미사용 → SRP Batcher 유지), `IPoolable`, SRP Batcher 검증.
- `StaticBehaviourFlagsHook`(Editor): 씬 · 프리팹 저장 때 플래그 재적용, 메뉴.
- `Log.Warning(string, Object)` 오버로드.

## [0.1.0] - 2026-11-04

ActionRPGProject 의 `Assets/Framework` 를 UPM 패키지로 분리한 첫 버전. 이전 커밋 이력은 `git subtree split` 으로 보존.

### Added
- Ability(GAS), StateTree(+ 에디터), UI 레이어, Addressables 로더 · 의존성 분석, CSV 테이블, EventBus, 풀, 카메라 가림, BaseBehaviour 계열.
- `KindRegistry` + `[Kind]`: enum switch 대신 다형성 팩토리.
- `ModifierOperation` / `MagnitudeCalculation`: GE 모디파이어 연산 · 크기 계산을 클래스로 분리.
- `UILayer`: 레이어별 캔버스 · 입력 차단 · ESC 정책.
