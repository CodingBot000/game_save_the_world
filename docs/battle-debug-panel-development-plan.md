# Battle Debug Panel Development Plan

Last updated: 2026-06-05

이 문서는 BattleArena에서 전투 밸런싱 값을 플레이 중 조정할 수 있는 접기/펼치기 디버그창 구현 계획이다. 디버그창은 화면 오른쪽에 반투명 패널로 열리고, 값 변경은 좌우 버튼으로 처리한다. Retry 후에는 마지막 입력값을 다시 적용한다.

## 목표

- 전투 중 주요 밸런스 값을 바로 조정한다.
- 모든 수치는 0 미만으로 내려가지 않는다.
- 좌측 버튼은 감소, 우측 버튼은 증가.
- 디버그창은 접기/펼치기가 가능하다.
- 펼치면 화면 오른쪽에 반투명 패널로 표시한다.
- 화면 높이를 넘는 항목은 스크롤 리스트로 접근한다.
- Retry 후에도 마지막 입력값을 유지한다.
- Play 중 변경은 런타임 인스턴스에만 적용하고 asset/prefab은 자동 저장하지 않는다.

## UI 요구사항

### 배치

- 접힘 상태:
  - 오른쪽 화면 가장자리에 작은 탭 버튼 표시.
  - 예: `Tuning` 또는 `Debug`.

- 펼침 상태:
  - 화면 오른쪽에 고정.
  - 패널 폭: 360-440px 권장.
  - 배경: 반투명 어두운색. 예: `rgba(5, 8, 12, 0.72)`.
  - 패널 내부는 `ScrollRect`.
  - Safe Area를 고려해 top/bottom margin 확보.

### 행 구성

각 수치 행:

```text
Label        [<]   123.45   [>]
```

권장 구성:

- Label: 왼쪽 정렬.
- 감소 버튼: `<` 또는 `-`.
- 값 표시: 중앙 정렬, 소수 자리 포맷.
- 증가 버튼: `>` 또는 `+`.
- 버튼 반복 입력은 처음에는 클릭 단위만 구현하고, 필요 시 press-and-hold 반복을 추가한다.

### 그룹

초기 그룹:

- Debug Toggles
- Player Attack
- Player Missile
- Player Defense
- Player Movement
- Boss
- Boss Attack
- Boss Patterns

각 그룹은 접기/펼치기 가능하면 좋지만, 1차 구현에서는 전체 패널 하나만 접기/펼치고 내부는 스크롤로 충분하다.

## 조작 정책

### 수치 변경

- 좌 버튼: `value -= step`
- 우 버튼: `value += step`
- 적용 전 clamp: `value = Mathf.Max(0f, value)`
- int 값은 `Mathf.Max(0, value)`
- bool 값은 toggle 버튼 사용.

### 증감 단위

초기 권장값:

| 값 유형 | step |
| --- | ---: |
| HP/Armor | 50 |
| Damage | 5 |
| Cooldown/Interval | 0.1 |
| Speed | 5 |
| Acceleration | 10 |
| TurnRate | 10 |
| Duration | 0.1 |
| Radius/Hitbox | 0.1 |
| HealthRatio | 0.05 |
| ProjectileCount/BurstCount | 1 |
| Angle | 5 |
| Multiplier | 0.1 |

주의:

- 쿨다운 0 허용 여부는 별도 결정이 필요하다.
- 요청 조건은 “0 미만 금지”이므로 1차 구현에서는 0을 허용할 수 있다.
- 안정성을 우선하면 쿨다운/interval/lifetime 계열은 최소 0.01 또는 0.1로 제한한다.

### 즉시 반영 정책

즉시 반영:

- 플레이어 이동 속도.
- 플레이어 방어 최대치와 회복값. 변경 시 체력/방어구 full refill.
- 디버그 토글.
- 보스 hit radius, idle bob.

다음 액션부터 반영:

- 플레이어 기본 탄 공격력/속도/쿨다운: 다음 발사부터.
- 미사일 튜닝값: 다음 미사일부터.
- 보스 공격 공통값: 다음 공격/다음 패턴부터.
- 보스 패턴 수치: 현재 실행 중인 패턴은 유지하고 다음 패턴부터.

명시적으로 유지하지 않을 값:

- 이미 날아간 투사체의 속도/피해량.
- 현재 실행 중인 보스 패턴 코루틴 내부의 Wait 시간.
- 현재 missileCooldownRemaining.
- 현재 HP/Armor를 낮은 상태로 보존하는 것.

## 튜닝 항목

### Debug Toggles

- `IgnoreMissileCooldown`
- `Undead`
- `Show Damage Hurtbox Debug Visual`
- `Show Movement Bounds Guide`

### Player Attack

- `fireCooldown`
- `projectileSpeed`
- `projectileDamage`

### Player Missile

- `missileCooldown`
- `missileDamage`
- `missileLaunchSpeed`
- `missileCruiseSpeed`
- `missileAcceleration`
- `missileTurnRate`
- `missileLockOnDelay`
- `missileStraightPhaseDuration`
- `missileStraightPhaseDistance`
- `missileTurnPhaseDuration`
- `missileBoostPhaseDuration`
- `missileLifetime`
- `missileHitRadius`

### Player Defense

- `maxHull`
- `maxArmor`
- `repairRate`
- `repairDelay`
- `brokenRecoverThreshold`
- `hullDamageMultiplierWhenBroken`
- 버튼: `Refill Player`

정책:

- 위 값 변경 시 요청대로 플레이어 체력/방어구를 가득 채운다.
- `maxArmor > 0`이면 `ArmorBroken = false`.

### Player Movement

- `strafeSpeed`
- `altitudeSpeed`
- `forwardSpeed`
- movement bounds X/Y/Z half extents
- movement bounds guide on/off

### Boss

- `maxHealth`
- `currentHealth`
- `hitRadius`
- `idleBobAmplitude`
- `idleBobSpeed`
- 버튼: `Boss Full Heal`

권장 정책:

- `maxHealth` 변경만으로 currentHealth를 자동 full heal할지 결정 필요.
- 안전한 기본값은 `maxHealth`와 `currentHealth`를 분리하고, full heal은 버튼으로 제공.

### Boss Attack

- `baseAttackInterval`
- `enragedAttackInterval`
- `projectileSpeed`
- `projectileDamage`

### Boss Patterns

패턴별:

- `enabled`
- `minHealthRatio`
- `maxHealthRatio`
- `cooldownMultiplier`
- `projectileCount`
- `secondaryProjectileCount`
- `burstCount`
- `burstInterval`
- `spreadAngle`
- `speedMultiplier`
- `secondarySpeedMultiplier`
- `damageMultiplier`
- `secondaryDamageMultiplier`
- `ringRotationStep`
- `telegraphDuration`
- `flashingDuration`
- `warningWidth`
- `warningHeight`
- `warningDepth`
- `overheadHeight`
- `splitDistance`

선택 UI:

- 패턴 이름을 헤더로 표시.
- 항목이 많으므로 패턴별 foldout 권장.

## 코드 구조

### BattleDebugPanel

역할:

- UI 생성과 입력 처리.
- 튜닝 항목 리스트 구성.
- 좌/우 버튼 클릭 시 `BattleDebugTuningState`를 변경.
- `BattleDebugTuningApplier`에 적용 요청.

위치 후보:

- `Assets/_Project/Scripts/UI/BattleDebugPanel.cs`

조건부 빌드:

```csharp
#if UNITY_EDITOR || DEVELOPMENT_BUILD
// debug UI
#endif
```

또는 컴포넌트는 존재하되 release build에서 자동 비활성.

### BattleDebugTuningState

역할:

- Retry 후에도 유지할 마지막 입력값 저장.
- static 접근 제공.
- 순수 데이터만 저장.

위치 후보:

- `Assets/_Project/Scripts/Debug/BattleDebugTuningState.cs`

주의:

- GameObject, Component, Transform 참조 금지.
- ScriptableObject asset 직접 수정 금지.

### BattleDebugTuningApplier

역할:

- 현재 BattleArena 씬의 전투 컴포넌트를 찾아 튜닝값 적용.
- 씬 로드 후 다시 생성된 컴포넌트에 override 재적용.
- 이벤트 구독 해제.

위치 후보:

- `Assets/_Project/Scripts/Debug/BattleDebugTuningApplier.cs`

적용 대상:

- `PlayerCombatController`
- `PlayerOrbitController`
- `PlayerMovementBounds`
- `BossController`
- `BossAttackController`
- `BossBulletPatternController`

### BattleTuningItem

역할:

- UI row 하나를 표현하는 데이터.

예시:

```csharp
public sealed class BattleTuningItem
{
    public string Label;
    public BattleTuningValueType ValueType;
    public float Step;
    public Func<float> GetFloat;
    public Action<float> SetFloat;
}
```

주의:

- 이 객체가 scene component를 오래 들고 있으면 Retry 후 문제가 될 수 있다.
- 패널은 씬마다 새로 생성되므로 현재 씬 컴포넌트 참조를 가져도 된다.
- static state에는 넣지 않는다.

## Runtime 적용 API 계획

기존 private field를 reflection으로 직접 변경하지 않는다. 각 컴포넌트에 debug-safe setter를 추가한다.

필요 API:

- `PlayerCombatController.ApplyDebugAttackTuning(...)`
- `PlayerCombatController.ApplyDebugMissileTuning(...)`
- `PlayerCombatController.ApplyDebugDefenseTuning(..., bool refill)`
- `PlayerOrbitController.ApplyDebugMovementTuning(...)`
- `PlayerMovementBounds.ApplyDebugBoundsTuning(...)`
- `BossController.ApplyDebugHealthTuning(...)`
- `BossAttackController.ApplyDebugAttackTuning(...)`
- `BossBulletPatternController.ApplyDebugPatternTuning(...)`

각 setter의 공통 규칙:

- 입력값 clamp.
- null 참조 방어.
- runtime state와 UI 표시값이 어긋나지 않게 변경 후 current value 반환 또는 property 제공.

## Retry 지속 방식

현재 Retry는 같은 씬을 다시 `LoadScene`한다. 따라서 씬 컴포넌트는 새로 생성된다.

구현 흐름:

1. 디버그창에서 값 변경.
2. `BattleDebugTuningState`에 override 저장.
3. 현재 씬 컴포넌트에 즉시 적용.
4. Retry.
5. BattleArena 새 인스턴스의 `BattleDebugTuningApplier.Start()`가 override 확인.
6. 남아 있는 override를 새 컴포넌트에 적용.

버튼:

- `Reset To Scene Defaults`: 현재 override를 지우고 씬 기본값을 다시 적용. 구현 난도가 있으면 Retry 안내.
- `Clear Retry Overrides`: 다음 Retry부터 기본값 사용.
- `Apply Current`: 현재 override 재적용.

## UI 입력 사이드이펙트

현재 플레이어 좌클릭 발사는 `EventSystem.current.IsPointerOverGameObject()`일 때 막힌다. 따라서 패널 위를 클릭하면 사격이 되지 않는 것은 정상이다.

주의할 점:

- 패널 버튼 클릭이 전투 사격으로 동시에 들어가면 안 된다.
- 패널 위에서는 마우스 발사가 막혀야 한다.
- 키보드 이동/스페이스 발사는 UI 포커스 중에도 동작할 수 있다. 필요하면 디버그 입력 중에는 이동을 막는 옵션을 별도로 둔다.

## 구현 단계

### Phase 1. 상태 저장소

- `BattleDebugTuningState` 추가.
- tuning key enum 추가.
- override 저장/조회/삭제 API 추가.

검증:

- 값 변경 후 Retry 없이 state 유지.
- `ClearOverrides` 동작.

### Phase 2. Safe setter

- 주요 전투 컴포넌트에 debug-safe setter 추가.
- 음수 방지.
- 플레이어 방어 스탯 변경 시 full refill.

검증:

- 직접 setter 호출 테스트.
- 기존 전투 시작 동작 변화 없음.

### Phase 3. Applier

- BattleArena에서 대상 컴포넌트 resolve.
- 현재 override 적용.
- Retry 후 재적용.

검증:

- 값 변경 후 게임오버 Retry.
- 마지막 입력값으로 시작.

### Phase 4. UI 패널

- 오른쪽 탭 버튼.
- 반투명 패널.
- ScrollRect.
- 수치 row 좌/우 버튼.
- bool toggle.

검증:

- 항목이 화면 밖으로 넘치면 스크롤 가능.
- 버튼 클릭 시 값 표시 즉시 갱신.
- 패널 위 클릭이 전투 좌클릭 사격으로 전달되지 않음.

### Phase 5. 패턴 편집

- Boss Patterns foldout 또는 패턴 선택 리스트.
- 패턴별 수치 row 추가.
- 현재 실행 중인 패턴에는 다음 패턴부터 반영된다는 표시.

검증:

- HP 구간별 패턴 eligible 상태 확인.
- 값 변경 후 다음 패턴부터 반영.

### Phase 6. 리셋/프리셋

- `Clear Retry Overrides`.
- `Reset To Scene Defaults`.
- 선택적으로 JSON export/import 또는 clipboard export.

검증:

- 기본값 복귀.
- asset dirty 발생 없음.

## 검증 체크리스트

- Play Mode 진입 시 디버그 패널이 오른쪽 접힌 상태로 표시된다.
- 펼치면 반투명 패널이 오른쪽에 열린다.
- 긴 리스트는 스크롤된다.
- 모든 수치가 0 미만으로 내려가지 않는다.
- 플레이어 체력/방어/회복 관련 값을 바꾸면 HP/Armor가 가득 찬다.
- 미사일 쿨다운 무시 토글이 실제 발사 가능 여부에 반영된다.
- 기본 공격/미사일 값은 다음 발사부터 반영된다.
- 보스 패턴 값은 다음 패턴부터 안정적으로 반영된다.
- Retry 후 마지막 입력값이 유지된다.
- `Clear Retry Overrides` 후 Retry하면 기본값으로 돌아간다.
- Quit 후 MainMenu 이동에서 예외가 없다.
- Play 중 prefab/ScriptableObject asset이 dirty 되지 않는다.
- Unity console error가 없다.

## 결정 필요 사항

- 쿨다운/interval 0 허용 여부.
- 보스 MaxHP 변경 시 자동 full heal 여부.
- 디버그 패널 기본 상태: 접힘 또는 펼침.
- 디버그 패널을 Editor/Development Build에서만 노출할지.
- 패턴 편집 UI를 1차에 포함할지, 2차로 분리할지.
- 튜닝값 export/import가 필요한지.
