# Addressables 의존성 이슈 (자동 생성)

> 자동 생성 문서 — 의존성 툴(Game > Tools > Dependency Map)이 덮어씁니다. 직접 고치지 마세요. 마지막 갱신 2026-10-31 13:35

## 요약

| 항목 | 값 |
|---|---|
| 열린 이슈 | 49 (오류 0 · 경고 40 · 참고 9) |
| 이번에 새로 생긴 이슈 | 1 |
| 해결된 이슈 (기록) | 3 |
| Addressables 에셋 | 114 |
| 딸려 오는 에셋 | 454 |
| 테스트 시나리오 기록 | 5 |

## 이슈

### ⚠️ 경고 반납 안 된 참조: AI/ST_MeleeMonster (3회 반복에서 +6 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Monster_SpawnAndRemove · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Monster_SpawnAndRemove' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(MonsterBrain.loadTree)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `AI/ST_MeleeMonster`

### ⚠️ 경고 반납 안 된 참조: AI/ST_RangedMonster (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Monster_SpawnAndRemove · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Monster_SpawnAndRemove' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(MonsterBrain.loadTree)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `AI/ST_RangedMonster`

### ⚠️ 경고 반납 안 된 참조: Characters/BanditArcher (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Monster_SpawnAndRemove · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Monster_SpawnAndRemove' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(SpawnManager.spawnMonsterAsync)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Characters/BanditArcher`

### ⚠️ 경고 반납 안 된 참조: Characters/BanditThug (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Monster_SpawnAndRemove · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Monster_SpawnAndRemove' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(SpawnManager.spawnMonsterAsync)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Characters/BanditThug`

### ⚠️ 경고 반납 안 된 참조: Characters/OrcBrute (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Monster_SpawnAndRemove · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Monster_SpawnAndRemove' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(SpawnManager.spawnMonsterAsync)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Characters/OrcBrute`

### ⚠️ 경고 반납 안 된 참조: Icons/Banner (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Banner`

### ⚠️ 경고 반납 안 된 참조: Icons/Barrel (3회 반복에서 +6 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Barrel`

### ⚠️ 경고 반납 안 된 참조: Icons/CastleHeart (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/CastleHeart`

### ⚠️ 경고 반납 안 된 참조: Icons/Chest (3회 반복에서 +15 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Chest`

### ⚠️ 경고 반납 안 된 참조: Icons/Coin (3회 반복에서 +6 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Coin`

### ⚠️ 경고 반납 안 된 참조: Icons/CopperOre (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/CopperOre`

### ⚠️ 경고 반납 안 된 참조: Icons/Floor (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Floor`

### ⚠️ 경고 반납 안 된 참조: Icons/Lumber (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Lumber`

### ⚠️ 경고 반납 안 된 참조: Icons/PlantFiber (3회 반복에서 +6 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/PlantFiber`

### ⚠️ 경고 반납 안 된 참조: Icons/Potion (3회 반복에서 +12 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Potion`

### ⚠️ 경고 반납 안 된 참조: Icons/Shield (3회 반복에서 +12 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Shield`

### ⚠️ 경고 반납 안 된 참조: Icons/Spear (3회 반복에서 +6 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Spear`

### ⚠️ 경고 반납 안 된 참조: Icons/Stone (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Stone`

### ⚠️ 경고 반납 안 된 참조: Icons/Sword (3회 반복에서 +6 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Sword`

### ⚠️ 경고 반납 안 된 참조: Icons/Wall (3회 반복에서 +12 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Wall`

### ⚠️ 경고 반납 안 된 참조: Icons/Workbench (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: UI_OpenCloseWindows · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'UI_OpenCloseWindows' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(UIUtil.LoadSprite)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Icons/Workbench`

### ⚠️ 경고 반납 안 된 참조: Projectiles/FireBall (3회 반복에서 +6 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Ability_CastAllSlots · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Ability_CastAllSlots' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(ProjectileSystem.Fire)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Projectiles/FireBall`

### ⚠️ 경고 반납 안 된 참조: Structures/Banner (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/Banner`

### ⚠️ 경고 반납 안 된 참조: Structures/CastleHeart (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/CastleHeart`

### ⚠️ 경고 반납 안 된 참조: Structures/Coffin (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/Coffin`

### ⚠️ 경고 반납 안 된 참조: Structures/Floor (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/Floor`

### ⚠️ 경고 반납 안 된 참조: Structures/Pillar (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/Pillar`

### ⚠️ 경고 반납 안 된 참조: Structures/Smelter (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/Smelter`

### ⚠️ 경고 반납 안 된 참조: Structures/Wall (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/Wall`

### ⚠️ 경고 반납 안 된 참조: Structures/WallDoor (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/WallDoor`

### ⚠️ 경고 반납 안 된 참조: Structures/WallWindow (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/WallWindow`

### ⚠️ 경고 반납 안 된 참조: Structures/Workbench (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Build_GhostBeginCancel · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Build_GhostBeginCancel' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(BuildManager.Begin)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `Structures/Workbench`

### ⚠️ 경고 반납 안 된 참조: VFX/DashTrail (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Monster_SpawnAndRemove · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Monster_SpawnAndRemove' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(CueSpawner.spawnInternal)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `VFX/DashTrail`

### ⚠️ 경고 반납 안 된 참조: VFX/Empower (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Ability_CastAllSlots · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Ability_CastAllSlots' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(CueSpawner.spawnInternal)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `VFX/Empower`

### ⚠️ 경고 반납 안 된 참조: VFX/Heal (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Ability_CastAllSlots · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Ability_CastAllSlots' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(CueSpawner.spawnInternal)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `VFX/Heal`

### ⚠️ 경고 반납 안 된 참조: VFX/Skill/BloodNova (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Ability_CastAllSlots · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Ability_CastAllSlots' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(CueSpawner.spawnInternal)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `VFX/Skill/BloodNova`

### ⚠️ 경고 반납 안 된 참조: VFX/Skill/BloodRiteAura (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Ability_CastAllSlots · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Ability_CastAllSlots' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(CueSpawner.spawnInternal)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `VFX/Skill/BloodRiteAura`

### ⚠️ 경고 반납 안 된 참조: VFX/Skill/SwordSlash (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Ability_CastAllSlots · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Ability_CastAllSlots' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(CueSpawner.spawnInternal)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `VFX/Skill/SwordSlash`

### ⚠️ 경고 반납 안 된 참조: VFX/Skill/VeilBlood (3회 반복에서 +3 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Ability_CastAllSlots · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Ability_CastAllSlots' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(CueSpawner.spawnInternal)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `VFX/Skill/VeilBlood`

### ⚠️ 경고 반납 안 된 참조: VFX/Skill/Whirlwind (3회 반복에서 +12 — 반복할수록 늘어남)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 테스트: Ability_CastAllSlots · 담당 프로그래밍 · `R-LEAK`
- 왜 문제인가: 'Ability_CastAllSlots' 를 끝내고 원래 상태로 돌아왔는데 이 에셋의 참조 카운트가 늘어난 채로 남았습니다. 계속 반복하면 메모리에서 내려가지 않습니다.
- 어떻게 고치나: 이 에셋을 로드한 곳(CueSpawner.spawnInternal)에서 다 쓴 뒤 AssetLoader.Release 를 호출하는지 확인하세요. 한 번 로드해 계속 쓰는 것이 의도라면 무시해도 됩니다.
- 관련: `VFX/Skill/Whirlwind`

### ℹ️ 참고 무거운 에셋: BanditThug (딸려 오는 원본 147MB)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Characters/BanditThug.prefab`

### ℹ️ 참고 무거운 에셋: Banner (딸려 오는 원본 129MB)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Structures/Banner.prefab`

### ℹ️ 참고 무거운 에셋: BossGrimgor (딸려 오는 원본 229MB)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Characters/BossGrimgor.prefab`

### ℹ️ 참고 무거운 에셋: Boulder (딸려 오는 원본 208MB)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Nodes/Boulder.prefab`

### ℹ️ 참고 무거운 에셋: CastleHeart (딸려 오는 원본 129MB)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Structures/CastleHeart.prefab`

### ℹ️ 참고 무거운 에셋: CopperVein (딸려 오는 원본 208MB)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Nodes/CopperVein.prefab`

### ℹ️ 참고 무거운 에셋: OrcBrute (딸려 오는 원본 228MB)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Characters/OrcBrute.prefab`

### ℹ️ 참고 무거운 에셋: Rock (딸려 오는 원본 208MB)

- 상태: ⏳ 계속 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Nodes/Rock.prefab`

### ℹ️ 참고 아무도 쓰지 않는 Addressables 에셋 3개

- 상태: 🆕 새 이슈 · 처음 발견 2026-10-31 13:35 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 기획 · 프로그래밍 · `S-UNUSED`
- 왜 문제인가: 데이터 테이블에도 없고 다른 에셋도 참조하지 않습니다. 코드에서 주소를 직접 쓰는 경우일 수도 있습니다.
- 어떻게 고치나: 정말 안 쓰면 Content 폴더에서 지우고, 쓰는 것이라면 AssetTable 에 행을 추가하세요.
- 관련: `unused`, `Assets/Game/Data/Localization/Locales/Korean.asset`, `Assets/Game/Data/Localization/Tables/GameText_ko.asset`, `Assets/Game/Data/Localization/Tables/GameText_en.asset`

### ⚠️ 경고 Resources 폴더 에셋 2개가 Addressables 번들에도 들어감

- 상태: ✅ 해결됨 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-31 13:35 · 출처 정적 검사 · 담당 프로그래밍 · 아트 · `S-DUP-RESOURCES`
- 왜 문제인가: Assets/TextMesh Pro/Resources 의 에셋은 빌드 시 Resources 에 한 벌, 그 에셋을 쓰는 번들에 한 벌 들어갑니다 (용량 · 메모리 2배).
- 어떻게 고치나: 외부 에셋 원본은 고치지 않는 규칙이므로, 이 에셋들을 Addressables 에 직접 등록하거나 복사본을 Content 쪽으로 옮기는 방안을 검토하세요.
- 관련: `Assets/TextMesh Pro/Resources`, `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`, `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`

### ⚠️ 경고 멈칫(동기 로드 대기 45ms): Characters/Player

- 상태: ✅ 해결됨 · 처음 발견 2026-10-31 10:59 · 마지막 2026-10-31 13:35 · 출처 테스트: Boot · 담당 프로그래밍 · `R-SYNC`
- 왜 문제인가: 게임이 이 에셋이 로드될 때까지 그 자리에서 기다렸습니다. 화면이 순간 멈춘 것처럼 보입니다.
- 어떻게 고치나: 미리 로드(프리로드)하거나 비동기 로드로 바꾸세요.
- 관련: `Characters/Player`

### ℹ️ 참고 무거운 에셋: NewMonster (딸려 오는 원본 143MB)

- 상태: ✅ 해결됨 · 처음 발견 2026-10-30 00:03 · 마지막 2026-10-30 08:12 · 출처 정적 검사 · 담당 아트 · `S-HEAVY`
- 왜 문제인가: 이 에셋을 로드하면 함께 따라오는 텍스처 · 메시가 많아 로딩 시간과 메모리가 큽니다 (원본 파일 크기 기준).
- 어떻게 고치나: 텍스처 최대 크기 · 압축, 메시 LOD 를 확인하세요. 꼭 같이 올 필요가 없는 것은 별도 에셋으로 나누세요.
- 관련: `Assets/Game/Content/Characters/NewMonster.prefab`

## 테스트 시나리오 기록 (시나리오별 최신)

| 시나리오 | 실행 시각 | 로드 | 반납 | 남은 참조 증가 | 동기 대기(ms) | 실패 |
|---|---|---|---|---|---|---|
| Ability_CastAllSlots | 2026-10-31 13:33:47 | 36 | 0 | 36 | 0 | 0 |
| Boot | 2026-10-31 13:34:02 | 425 | 0 | 425 | 7 | 0 |
| Build_GhostBeginCancel | 2026-10-31 13:34:02 | 30 | 0 | 30 | 0 | 0 |
| Monster_SpawnAndRemove | 2026-10-31 13:34:03 | 21 | 0 | 21 | 0 | 0 |
| UI_OpenCloseWindows | 2026-10-31 13:34:09 | 102 | 0 | 102 | 0 | 0 |
