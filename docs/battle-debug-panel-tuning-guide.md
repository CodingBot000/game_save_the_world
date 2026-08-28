# Battle Debug Panel Tuning Guide

Last updated: 2026-06-05

이 문서는 BattleArena 오른쪽 `Battle Tuning` 디버그창에 표시되는 수치와 토글의 의미를 설명한다. 항목명은 가능하면 코드에서 사용하는 영문 변수명 또는 tuning key를 기준으로 적고, 괄호 안에 한국어 설명을 붙인다.

## 기본 조작

- `<` 버튼: 값을 감소시킨다.
- `>` 버튼: 값을 증가시킨다.
- 모든 숫자값은 0 미만으로 내려가지 않는다.
- bool 값은 `<`, `>` 어느 쪽을 눌러도 ON/OFF가 전환된다.
- 변경값은 `BattleDebugTuningState`에 저장되어 BattleArena Retry 후에도 다시 적용된다.
- Play 중 조작은 runtime instance에만 적용된다. prefab, ScriptableObject asset은 자동 저장하지 않는다.

## 상단 버튼

| UI | 영문명 | 설명 |
| --- | --- | --- |
| `Apply` | `ApplyAllOverrides` | 현재 저장된 모든 디버그 override를 현재 씬의 플레이어, 보스, 패턴 컴포넌트에 다시 적용한다. 값이 꼬였거나 Retry 직후 강제 재적용하고 싶을 때 사용한다. |
| `Refill` | `RefillPlayerForDebug` | 플레이어 `currentHull`, `currentArmor`를 현재 최대치까지 채운다. Armor broken 상태도 현재 armor 값에 맞춰 복구한다. |
| `Boss HP` | `FullHealBossForDebug` | 보스 현재 체력을 현재 `BossMaxHealth`까지 채운다. |
| `Clear` | `ClearOverrides` | 디버그 override 저장값을 모두 지운다. 플레이어 기본 런타임 스탯은 즉시 다시 적용한다. 씬 기본값으로 완전히 되돌리는 것은 Retry 후 가장 안정적이다. |

## Debug Toggles

| UI | 영문명 | 설명 |
| --- | --- | --- |
| `Missile Cooldown` | `IgnoreMissileCooldown` | ON이면 플레이어 미사일 쿨다운을 무시하고 발사 입력을 허용한다. OFF이면 `PlayerMissileCooldown`과 현재 남은 쿨다운을 따른다. |
| `Undead` | `Undead` | ON이면 플레이어가 피해를 받아도 `currentHull`, `currentArmor`가 감소하지 않는다. 이미 죽음 처리까지 들어온 경우에도 플레이어를 refill하고 조작/전투를 다시 켠다. 원래 Environment 디버그창의 `Undead ON/OFF`도 같은 값이다. |
| `Hurtbox Visual` | `ShowDamageHurtbox` | ON이면 플레이어 피격 판정 collider 주변에 디버그 시각화 proxy를 표시한다. 실제 판정 크기 자체를 바꾸지는 않는다. |
| `Move Bounds` | `ShowMovementBoundsGuide` | ON이면 플레이어 이동 가능 영역 가이드를 표시한다. 이동 범위 수치는 `MovementBoundsX/Y/Z` 항목에서 조정한다. |

## Player Attack

| UI | 영문명 | 증감 | 설명 |
| --- | --- | ---: | --- |
| `Fire Cooldown` | `PlayerFireCooldown` / `fireCooldown` | 0.05 | 플레이어 기본 탄 발사 간격이다. 낮을수록 연사가 빨라진다. 이미 발사된 탄에는 영향 없고 다음 발사부터 반영된다. |
| `Bullet Speed` | `PlayerProjectileSpeed` / `projectileSpeed` | 5 | 플레이어 기본 탄 이동 속도다. 높을수록 탄이 빠르게 날아간다. 이미 날아간 탄에는 적용되지 않는다. |
| `Bullet Damage` | `PlayerProjectileDamage` / `projectileDamage` | 5 | 플레이어 기본 탄이 보스에게 주는 피해량이다. 다음 발사 탄부터 반영된다. |
| `Invulnerable` | `PlayerInvulnerabilityDuration` / `invulnerabilityDuration` | 0.1 | 플레이어가 피해를 받은 뒤 다시 피해를 받을 수 있기까지의 무적 시간이다. `Undead`와 다르게 체력 감소 자체를 막는 기능은 아니다. |
| `Hit Radius` | `PlayerHitRadius` / `hitRadius` | 0.1 | 플레이어 fallback 피격 반경이다. damage hurtbox collider 판정이 없거나 실패할 때 보조 판정으로 사용된다. 값이 클수록 더 쉽게 맞는다. |

## Player Missile

| UI | 영문명 | 증감 | 설명 |
| --- | --- | ---: | --- |
| `Cooldown` | `PlayerMissileCooldown` / `missileCooldown` | 0.1 | 미사일 발사 후 다음 미사일까지의 쿨다운 시간이다. `IgnoreMissileCooldown`이 ON이면 입력 제한은 무시된다. |
| `Damage` | `PlayerMissileDamage` / `missileDamage` | 10 | 미사일이 보스에게 주는 피해량이다. 다음 미사일부터 반영된다. |
| `Launch Speed` | `PlayerMissileLaunchSpeed` / `missileLaunchSpeed` | 2 | 미사일 발사 직후 초기 속도다. |
| `Cruise Speed` | `PlayerMissileCruiseSpeed` / `missileCruiseSpeed` | 5 | 미사일이 boost 후 도달하려는 순항 속도다. |
| `Acceleration` | `PlayerMissileAcceleration` / `missileAcceleration` | 10 | 미사일 boost 단계에서 속도가 증가하는 정도다. 높을수록 순항 속도에 더 빨리 도달한다. |
| `Turn Rate` | `PlayerMissileTurnRate` / `missileTurnRate` | 10 | 미사일이 목표 방향으로 회전할 수 있는 각속도다. 높을수록 더 강하게 추적한다. |
| `Lock Delay` | `PlayerMissileLockOnDelay` / `missileLockOnDelay` | 0.1 | 미사일이 발사 후 목표 추적을 시작하기 전까지의 지연 시간이다. 현재 구현에서는 straight phase duration과 함께 straight 단계 길이에 영향을 준다. |
| `Straight Time` | `PlayerMissileStraightPhaseDuration` / `missileStraightPhaseDuration` | 0.1 | 미사일이 처음 직진하는 시간이다. |
| `Straight Dist` | `PlayerMissileStraightPhaseDistance` / `missileStraightPhaseDistance` | 0.1 | 직진 단계에서 이동할 거리다. |
| `Turn Time` | `PlayerMissileTurnPhaseDuration` / `missileTurnPhaseDuration` | 0.1 | 직진 후 목표 방향으로 회전 보간하는 시간이다. |
| `Boost Time` | `PlayerMissileBoostPhaseDuration` / `missileBoostPhaseDuration` | 0.1 | boost 가속을 적용하는 시간이다. |
| `Lifetime` | `PlayerMissileLifetime` / `missileLifetime` | 0.5 | 미사일이 자동 제거되기까지의 생존 시간이다. 너무 짧으면 목표에 도달하기 전에 사라질 수 있다. |
| `Hit Radius` | `PlayerMissileHitRadius` / `missileHitRadius` | 0.1 | 미사일 충돌 판정 반경이다. 값이 클수록 보스에게 더 쉽게 맞는다. |

## Player Defense

이 그룹 값은 변경 시 플레이어 체력/아머를 즉시 최대치까지 채운다.

| UI | 영문명 | 증감 | 설명 |
| --- | --- | ---: | --- |
| `Max Hull` | `PlayerMaxHull` / `maxHull` | 50 | 플레이어 hull 최대 체력이다. 1 이상으로 유지된다. |
| `Max Armor` | `PlayerMaxArmor` / `maxArmor` | 50 | 플레이어 armor 최대치다. 0이면 armor가 없는 상태로 볼 수 있다. |
| `Repair Rate` | `PlayerRepairRate` / `armorRepairRate` | 1 | armor가 자동 회복될 때 초당 회복되는 양이다. |
| `Repair Delay` | `PlayerRepairDelay` / `armorRepairDelay` | 0.1 | 피해를 받은 뒤 armor 회복이 다시 시작되기까지의 대기 시간이다. |
| `Recover Thres.` | `PlayerBrokenRecoverThreshold` / `brokenRecoverThreshold` | 5 | armor broken 상태에서 이 값 이상으로 회복되면 broken 상태가 풀린다. 값은 `MaxArmor` 범위 안으로 clamp된다. |
| `Broken Mult.` | `PlayerHullDamageMultiplierWhenBroken` / `hullDamageMultiplierWhenBroken` | 0.1 | armor가 broken 상태일 때 hull에 들어가는 피해 배율이다. 높을수록 armor 파괴 후 더 위험하다. |

## Player Movement

| UI | 영문명 | 증감 | 설명 |
| --- | --- | ---: | --- |
| `Strafe Speed` | `PlayerStrafeSpeed` / `strafeSpeed` | 1 | A/D 좌우 이동 속도다. 즉시 반영된다. |
| `Altitude Speed` | `PlayerAltitudeSpeed` / `altitudeSpeed` | 1 | W/S 상하 이동 속도다. 즉시 반영된다. |
| `Forward Speed` | `PlayerForwardSpeed` / `forwardSpeed` | 1 | 전후 방향 이동 속도다. 현재 이동 컨트롤 구조에서 forward/back 축에 쓰인다. |
| `Bounds X` | `MovementBoundsX` / `halfExtents.x` | 0.25 | 플레이어 이동 가능 영역의 X 반경이다. 값이 클수록 좌우 이동 범위가 넓어진다. |
| `Bounds Y` | `MovementBoundsY` / `halfExtents.y` | 0.25 | 플레이어 이동 가능 영역의 Y 반경이다. 값이 클수록 상하 이동 범위가 넓어진다. |
| `Bounds Z` | `MovementBoundsZ` / `halfExtents.z` | 0.25 | 플레이어 이동 가능 영역의 Z 반경이다. 값이 클수록 전후 이동 범위가 넓어진다. |

## Boss

| UI | 영문명 | 증감 | 설명 |
| --- | --- | ---: | --- |
| `Max HP` | `BossMaxHealth` / `maxHealth` | 100 | 보스 최대 체력이다. 현재 체력을 자동으로 full heal하지는 않는다. 보스 체력을 채우려면 `Boss HP` 버튼을 사용한다. |
| `Current HP` | `BossCurrentHealth` / `currentHealth` | 100 | 보스 현재 체력이다. 0으로 내리면 보스 사망 처리에 가까운 테스트가 가능하다. 값은 `Max HP` 이하로 clamp된다. |
| `Hit Radius` | `BossHitRadius` / `hitRadius` | 0.1 | 보스 피격 반경이다. 값이 클수록 플레이어 탄/미사일이 더 쉽게 맞는다. |
| `Bob Amp` | `BossIdleBobAmplitude` / `idleBobAmplitude` | 0.05 | 보스 idle 상하 흔들림의 진폭이다. |
| `Bob Speed` | `BossIdleBobSpeed` / `idleBobSpeed` | 0.1 | 보스 idle 상하 흔들림 속도다. |

## Boss Attack

| UI | 영문명 | 증감 | 설명 |
| --- | --- | ---: | --- |
| `Base Interval` | `BossBaseAttackInterval` / `baseAttackInterval` | 0.1 | 보스 기본 공격 간격이다. 다음 공격 또는 다음 패턴 선택부터 안정적으로 반영된다. |
| `Enraged Int.` | `BossEnragedAttackInterval` / `enragedAttackInterval` | 0.1 | 보스 enraged 상태의 공격 간격이다. 낮을수록 보스 공격이 더 빨라진다. |
| `Bullet Speed` | `BossProjectileSpeed` / `baseProjectileSpeed` | 2 | 보스 탄 기본 속도다. 다음에 생성되는 보스 탄부터 반영된다. |
| `Bullet Damage` | `BossProjectileDamage` / `baseProjectileDamage` | 2 | 보스 탄 기본 피해량이다. `Undead`가 ON이면 플레이어 HP/Armor는 감소하지 않는다. |

## Pattern Timing

| UI | 영문명 | 증감 | 설명 |
| --- | --- | ---: | --- |
| `Startup Delay` | `BossPatternStartupDelay` / `startupDelay` | 0.1 | 보스 패턴 컨트롤러가 전투 시작 후 첫 패턴을 실행하기 전 대기 시간이다. |
| `Aimed Interval` | `BossPatternAimedBurstShotInterval` / `aimedBurstShotInterval` | 0.05 | aimed burst 계열 패턴에서 연속 발사 간격의 기본값으로 쓰인다. |
| `Warn Line` | `BossPatternWarningLineThickness` / `warningLineThickness` | 0.01 | 경고선/telegraph line 두께다. 값이 클수록 경고선이 두꺼워진다. |

## Boss Patterns

아래 항목은 보스 패턴 리스트의 각 패턴마다 반복 표시된다. `patternIndex`는 현재 패턴 순서 인덱스이며, UI에는 각 패턴의 `displayName`이 헤더로 표시된다.

| UI | 영문명 | 증감 | 설명 |
| --- | --- | ---: | --- |
| `Enabled` | `Enabled` / `enabled` | toggle | OFF이면 해당 패턴을 선택 대상에서 제외한다. |
| `Min HP Ratio` | `MinHealthRatio` / `minHealthRatio` | 0.05 | 이 패턴이 실행될 수 있는 최소 보스 체력 비율이다. 0은 0%, 1은 100%다. |
| `Max HP Ratio` | `MaxHealthRatio` / `maxHealthRatio` | 0.05 | 이 패턴이 실행될 수 있는 최대 보스 체력 비율이다. |
| `Cooldown x` | `CooldownMultiplier` / `cooldownMultiplier` | 0.1 | 패턴 실행 후 다음 패턴까지의 쿨다운 배율이다. 높을수록 다음 패턴까지 더 오래 기다린다. |
| `Projectile` | `ProjectileCount` / `projectileCount` | 1 | 주 탄환 개수다. fan spread, ring, burst 등 패턴별로 의미가 조금씩 다르다. 최소 1로 적용된다. |
| `Secondary` | `SecondaryProjectileCount` / `secondaryProjectileCount` | 1 | 보조 탄환 개수다. split shot 같은 보조 발사 구조에서 사용된다. 최소 1로 적용된다. |
| `Burst` | `BurstCount` / `burstCount` | 1 | burst 반복 횟수다. 최소 1로 적용된다. |
| `Burst Interval` | `BurstInterval` / `burstInterval` | 0.05 | burst 사이의 시간 간격이다. 낮을수록 연속 공격이 촘촘해진다. |
| `Spread Angle` | `SpreadAngle` / `spreadAngle` | 5 | 부채꼴/분산 발사의 전체 각도다. 높을수록 탄이 넓게 퍼진다. |
| `Speed x` | `SpeedMultiplier` / `speedMultiplier` | 0.1 | 보스 탄 기본 속도에 곱하는 주 탄환 속도 배율이다. |
| `Secondary Speed x` | `SecondarySpeedMultiplier` / `secondarySpeedMultiplier` | 0.1 | 보조 탄환 속도 배율이다. |
| `Damage x` | `DamageMultiplier` / `damageMultiplier` | 0.1 | 보스 탄 기본 피해량에 곱하는 주 탄환 피해 배율이다. |
| `Secondary Damage x` | `SecondaryDamageMultiplier` / `secondaryDamageMultiplier` | 0.1 | 보조 탄환 피해 배율이다. |
| `Ring Step` | `RingRotationStep` / `ringRotationStep` | 5 | spiral/ring 계열 패턴에서 다음 ring의 회전 각도 차이다. |
| `Telegraph` | `TelegraphDuration` / `telegraphDuration` | 0.1 | 공격 전 경고 표시가 유지되는 시간이다. |
| `Flashing` | `FlashingDuration` / `flashingDuration` | 0.1 | 경고 표시가 점멸하는 시간이다. |
| `Warn Width` | `WarningWidth` / `warningWidth` | 0.1 | warning 영역의 폭이다. falling bomb, line warning 등 패턴에서 사용된다. |
| `Warn Height` | `WarningHeight` / `warningHeight` | 0.5 | warning 영역의 높이다. |
| `Warn Depth` | `WarningDepth` / `warningDepth` | 0.1 | warning 영역의 깊이다. |
| `Overhead` | `OverheadHeight` / `overheadHeight` | 0.5 | 낙하형 패턴에서 탄/폭탄이 생성되는 머리 위 높이다. |
| `Split Dist` | `SplitDistance` / `splitDistance` | 0.5 | split shot 계열에서 분리 또는 추가 탄 생성이 일어나는 거리다. |

## 반영 타이밍 요약

즉시 반영되는 항목:

- `Undead`
- `ShowDamageHurtbox`
- `ShowMovementBoundsGuide`
- `PlayerMaxHull`, `PlayerMaxArmor`, `PlayerRepairRate`, `PlayerRepairDelay`, `PlayerBrokenRecoverThreshold`, `PlayerHullDamageMultiplierWhenBroken`
- `PlayerStrafeSpeed`, `PlayerAltitudeSpeed`, `PlayerForwardSpeed`
- `MovementBoundsX`, `MovementBoundsY`, `MovementBoundsZ`
- `BossMaxHealth`, `BossCurrentHealth`, `BossHitRadius`, `BossIdleBobAmplitude`, `BossIdleBobSpeed`

다음 액션부터 안정적으로 반영되는 항목:

- `PlayerFireCooldown`, `PlayerProjectileSpeed`, `PlayerProjectileDamage`
- 미사일 튜닝 전체
- `BossBaseAttackInterval`, `BossEnragedAttackInterval`, `BossProjectileSpeed`, `BossProjectileDamage`
- Boss pattern 항목 전체

이미 생성된 투사체와 현재 실행 중인 보스 패턴 coroutine 내부 대기 시간은 변경하지 않는다.
