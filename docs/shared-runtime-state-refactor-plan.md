# Shared Runtime State Refactor Plan

Last updated: 2026-06-05

이 문서는 플레이어 상태값을 BattleArena, Garage, Upgrade, Character/Inventory 계열 화면에서 공용으로 접근하기 위한 리팩토링 계획이다. 목표는 `static`으로 어디서나 접근 가능한 공용 진입점을 만들되, 씬 오브젝트 참조를 들고 다니지 않고 순수 데이터만 공유하는 것이다.

## 목표

- 플레이어 기본 스탯, 업그레이드 결과, 전투용 최종 스탯을 한 곳에서 계산한다.
- BattleArena는 공용 계산 결과를 받아 런타임 컴포넌트에 적용한다.
- Garage/Upgrade/Character 화면은 같은 계산 결과를 읽어 표시한다.
- Retry 후에도 디버그 튜닝값을 유지할 수 있게 한다.
- 씬 재로드, Retry, Quit, 다른 씬 이동에서 파괴된 GameObject/Component 참조가 남지 않게 한다.
- ScriptableObject/prefab asset은 디버그 조작으로 자동 수정하지 않는다.

## 핵심 원칙

공용 접근은 필요하지만 모든 값을 전역 mutable field로 흩뿌리면 안 된다. `static`은 진입점으로만 사용하고, 실제 데이터는 명확한 모델에 담는다.

금지:

- `static PlayerCombatController CurrentPlayer`
- `static BossController CurrentBoss`
- `static Transform PlayerTransform`
- `static Collider[] DamageHurtboxes`
- Retry 후에도 살아 있는 객체가 씬 컴포넌트 이벤트를 계속 구독하는 구조

허용:

- `static PlayerRuntimeState.Current`
- `static BattleDebugTuningState.Current`
- `static PlayerRuntimeStats ResolveCurrentStats()`
- `float`, `int`, `bool`, enum, string id 같은 순수 값 저장

## 제안 구조

### 1. PlayerBaseStats

역할: 차량/플레이어의 원본 기본 스탯.

소스 후보:

- 기존 `VehicleCatalog`
- 기존 `VehiclePlayerStateCatalog`
- 추후 무기/장비/파일럿/업그레이드 카탈로그

포함 값:

- `vehicleId`
- `hullHp`
- `armorHp`
- `repairRate`
- `repairDelay`
- `brokenRecoverThreshold`
- `hullDamageMultiplierWhenBroken`
- 기본 공격력/쿨다운/탄속이 차량별로 달라질 경우 여기에 추가 가능

주의:

- 현재 `VehiclePlayerStateCatalog`는 방어 스탯만 갖고 있다.
- 이 asset을 Play 중 디버그창에서 바로 수정하지 않는다.

### 2. PlayerProgressState

역할: 업그레이드, 해금, 장비 선택처럼 씬을 넘어 유지되어야 하는 플레이어 진행 상태.

저장 방식:

- 1차: 런타임 static singleton
- 이후 저장 기능이 필요하면 JSON/PlayerPrefs/세이브 파일로 확장

예시 필드:

```csharp
public sealed class PlayerProgressState
{
    public string SelectedVehicleId;
    public int HullUpgradeLevel;
    public int ArmorUpgradeLevel;
    public int WeaponUpgradeLevel;
    public int MissileUpgradeLevel;
}
```

주의:

- 여기도 씬 오브젝트 참조를 저장하지 않는다.
- Garage/Upgrade UI는 이 상태를 수정하고, 수정 후 계산 레이어에 갱신을 요청한다.

### 3. PlayerRuntimeStats

역할: 기본 스탯 + 업그레이드 + 장비 + 디버그 override가 반영된 최종 읽기 모델.

예시 필드:

```csharp
public struct PlayerRuntimeStats
{
    public float MaxHull;
    public float MaxArmor;
    public float RepairRate;
    public float RepairDelay;
    public float BrokenRecoverThreshold;
    public float HullDamageMultiplierWhenBroken;

    public float FireCooldown;
    public float ProjectileSpeed;
    public float ProjectileDamage;

    public float MissileCooldown;
    public float MissileDamage;
    public float MissileLaunchSpeed;
    public float MissileCruiseSpeed;
    public float MissileAcceleration;
    public float MissileTurnRate;
    public float MissileLifetime;
    public float MissileHitRadius;

    public float StrafeSpeed;
    public float AltitudeSpeed;
    public float ForwardSpeed;
}
```

특징:

- UI가 읽기 쉽다.
- BattleArena 적용이 쉽다.
- 디버그 override를 끄면 정상 게임 스탯으로 즉시 돌아갈 수 있다.

### 4. PlayerRuntimeState

역할: `static`으로 접근하는 공용 진입점.

제안 API:

```csharp
public static class PlayerRuntimeState
{
    public static PlayerProgressState Progress { get; }
    public static PlayerRuntimeStats CurrentStats { get; }

    public static event Action StatsChanged;

    public static void SetSelectedVehicle(string vehicleId);
    public static void SetUpgradeLevel(PlayerUpgradeType type, int level);
    public static PlayerRuntimeStats ResolveStats();
    public static void Recalculate();
}
```

규칙:

- `CurrentStats`는 계산 결과다.
- 씬 컴포넌트는 `StatsChanged`를 구독할 수 있지만 `OnDestroy`에서 반드시 해제한다.
- BattleArena의 컴포넌트는 시작 시 `CurrentStats`를 적용하고, 필요 시 `StatsChanged`에서 다시 적용한다.

### 5. BattleDebugTuningState

역할: Retry 후에도 유지될 디버그 override 값 저장.

저장 대상:

- 공격력, 쿨다운, 속도, 체력 최대값, 회복 관련 수치, 보스 최대 체력, 보스 패턴 수치
- `IgnoreMissileCooldown`, `Undead`

저장하지 않을 대상:

- 현재 플레이어 HP
- 현재 Armor
- 현재 missile cooldown remaining
- 현재 보스 HP
- 현재 실행 중인 보스 패턴 코루틴
- 날아가고 있는 투사체
- GameObject/Transform/Component 참조

제안 API:

```csharp
public static class BattleDebugTuningState
{
    public static bool HasOverrides { get; }
    public static BattleDebugTuningOverrides Overrides { get; }

    public static event Action OverridesChanged;

    public static void SetFloat(BattleTuningKey key, float value);
    public static void SetInt(BattleTuningKey key, int value);
    public static void SetBool(BattleTuningKey key, bool value);
    public static void ClearOverrides();
}
```

## 데이터 흐름

정상 게임 흐름:

1. Garage/Upgrade가 `PlayerRuntimeState.Progress`를 변경한다.
2. `PlayerRuntimeState.Recalculate()`가 현재 차량/업그레이드 기준으로 `PlayerRuntimeStats`를 계산한다.
3. Garage/Upgrade UI는 `CurrentStats`를 읽어 표시한다.
4. BattleArena 시작 시 `PlayerCombatController`가 `CurrentStats`를 적용한다.

디버그 튜닝 흐름:

1. BattleArena 디버그창에서 값을 변경한다.
2. 변경값은 `BattleDebugTuningState`에 저장된다.
3. 현재 씬의 `BattleDebugTuningApplier`가 새 값을 런타임 컴포넌트에 적용한다.
4. Retry로 BattleArena가 다시 로드된다.
5. 새 `BattleDebugTuningApplier`가 `BattleDebugTuningState`에 남은 override를 다시 적용한다.

## 전투 컴포넌트 변경 계획

### PlayerCombatController

추가할 debug-safe API:

- `ApplyRuntimeStats(PlayerRuntimeStats stats, PlayerStatApplyMode mode)`
- `SetFireCooldown(float value)`
- `SetProjectileSpeed(float value)`
- `SetProjectileDamage(float value)`
- `SetMissileTuning(PlayerMissileTuning tuning)`
- `SetDefenseTuning(PlayerDefenseTuning tuning, bool refill)`

정책:

- 모든 수치는 `Mathf.Max(0f, value)`로 0 미만 방지.
- 쿨다운은 0 허용 여부를 명확히 정한다. 연사 테스트가 필요하면 0 허용, 안정성을 우선하면 0.01 이상.
- 방어 스탯 변경 시 요청 정책대로 체력/방어구를 가득 채운다.
- `ArmorBroken`은 방어구가 0보다 크면 false로 복구한다.

### BossController

추가할 debug-safe API:

- `SetMaxHealth(float value, bool refill)`
- `SetCurrentHealth(float value)`
- `SetHitRadius(float value)`
- `SetIdleBob(float amplitude, float speed)`

정책:

- MaxHP 변경 시 기본은 `refill = false`가 안전하다.
- 디버그창에는 `Boss Full Heal` 별도 버튼을 둔다.
- 사용자가 원하면 MaxHP 변경 시 refill 옵션을 켤 수 있다.

### BossAttackController

추가할 debug-safe API:

- `SetBaseAttackInterval(float value)`
- `SetEnragedAttackInterval(float value)`
- `SetProjectileSpeed(float value)`
- `SetProjectileDamage(float value)`

정책:

- 다음 공격/다음 패턴부터 반영되는 것으로 문서화한다.

### BossBulletPatternController

추가할 debug-safe API:

- `GetPatternDefinitionsForDebug()`
- `SetPatternEnabled(int index, bool enabled)`
- `SetPatternFloat(int index, BattlePatternTuningKey key, float value)`
- `SetPatternInt(int index, BattlePatternTuningKey key, int value)`
- `CancelActivePatternForDebug()` optional

정책:

- 기본은 현재 실행 중인 패턴을 강제 중단하지 않는다.
- 값은 다음 패턴부터 안정적으로 반영한다.
- 필요하면 `Restart Pattern` 버튼을 별도로 둔다.

### PlayerOrbitController

추가할 debug-safe API:

- `SetMovementSpeeds(float strafe, float altitude, float forward)`
- `SetVisualTilt(float maxAngle, float duration)`

정책:

- 이동 속도는 즉시 반영된다.
- screen-space visual 관련 값까지 디버그창에 넣을지는 별도 판단한다. 전투 밸런스 핵심 값은 이동 속도와 이동 범위다.

## Retry 지속 정책

Retry는 현재 `SceneManager.LoadScene(SceneManager.GetActiveScene().path)`로 BattleArena를 다시 로드한다. 따라서 씬 인스턴스 값은 기본값으로 돌아간다.

`Persist Through Retry`를 구현하려면:

- 디버그창 입력값을 `BattleDebugTuningState`에 저장한다.
- 새 씬 로드 후 `BattleDebugTuningApplier`가 override를 다시 적용한다.
- `BattleDebugTuningState`는 scene object reference를 저장하지 않는다.

이 방식이면 메모리 누수 위험은 낮다.

위험한 구현:

- static 상태가 `PlayerCombatController`를 직접 보관한다.
- Retry마다 새 singleton이 누적된다.
- persistent 객체가 이전 씬의 이벤트 구독을 해제하지 않는다.
- 코루틴/Invoke가 씬 전환 후에도 이전 오브젝트를 물고 있다.

## 구현 단계

### Phase 1. 데이터 모델 추가

- `PlayerRuntimeStats`
- `PlayerProgressState`
- `PlayerRuntimeState`
- `BattleDebugTuningOverrides`
- `BattleDebugTuningState`
- tuning key enum들

검증:

- 컴파일 통과.
- 기존 BattleArena 동작 변화 없음.

### Phase 2. 전투 컴포넌트에 safe setter 추가

- `PlayerCombatController`
- `BossController`
- `BossAttackController`
- `BossBulletPatternController`
- `PlayerOrbitController`

검증:

- setter 호출 전 기존 동작 동일.
- 음수 입력이 0 또는 최소값으로 clamp.
- 방어 스탯 변경 시 플레이어 체력/방어구 full refill.

### Phase 3. Applier 추가

- `BattleDebugTuningApplier` 컴포넌트 추가.
- BattleArena 시작 시 현재 씬 컴포넌트들을 resolve.
- `PlayerRuntimeState.CurrentStats`와 `BattleDebugTuningState.Overrides` 적용.
- `OnDestroy`에서 이벤트 해제.

검증:

- Retry 후 override가 다시 적용된다.
- Quit 후 MainMenu 이동에서 예외가 없다.

### Phase 4. Garage/Upgrade 연동

- Garage/Upgrade UI가 직접 카탈로그를 읽는 대신 `PlayerRuntimeState.CurrentStats`를 읽도록 점진 전환.
- 업그레이드 변경 시 `PlayerRuntimeState.Recalculate()`.

검증:

- 선택 차량 변경 후 BattleArena가 같은 최종 스탯을 사용한다.
- 차고 표시값과 전투 시작값이 일치한다.

## 검증 체크리스트

- BattleArena 최초 진입 시 기존 기본값과 동일하게 시작한다.
- 디버그 override가 없을 때 기존 gameplay가 바뀌지 않는다.
- 디버그 값 변경 후 Retry하면 마지막 입력값이 다시 적용된다.
- `Clear Overrides` 후 Retry하면 씬 기본값으로 돌아간다.
- 플레이어 방어 스탯 변경 시 HP/Armor가 가득 찬다.
- 보스 MaxHP 변경 정책이 UI 표기와 실제 체력에 맞게 동작한다.
- Play 중 prefab/ScriptableObject asset이 dirty 되지 않는다.
- static 상태에 scene object reference가 남지 않는다.
- Unity console error가 없다.

## 결정 필요 사항

- 쿨다운 0을 허용할지, 최소 0.01로 제한할지.
- 보스 MaxHP 변경 시 자동 full heal 여부.
- 디버그 override를 BattleArena Retry까지만 유지할지, 앱 실행 내내 유지할지.
- 디버그 override를 저장 파일로 내보내는 기능이 필요한지.
- Garage/Upgrade에서 디버그 override까지 반영된 값을 보여줄지, 정상 게임 계산값만 보여줄지.
