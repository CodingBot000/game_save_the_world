# AimPoint Targeting System Spec

최종 갱신일: 2026-06-29

이 문서는 보스의 여러 `AimPoint` 위에 2D 타겟 이미지를 표시하고, 플레이어가 선택한 타겟으로 발사하며, 랜덤 크리티컬 타이밍을 적용하기 위한 기준 규칙이다.

## 기준 에셋

- `Assets/Art/UI/Battle/Targeting/targeting_normal_base.png`
- `Assets/Art/UI/Battle/Targeting/targeting_normal_inner.png`
- `Assets/Art/UI/Battle/Targeting/targeting_alert_base.png`
- `Assets/Art/UI/Battle/Targeting/targeting_alert_inner.png`

네 이미지는 같은 캔버스 크기 `198x178`로 정렬되어 있으며, 각 `base`와 `inner`는 같은 위치에 겹쳐서 사용한다.

## 현재 코드 기준

- `Assets/_Project/Scripts/Gameplay/BossController.cs`
  - 현재 `AimPoint`, `AimPoint2`, `AimPoint3`, `AimPoint4`, `AimPoint5`를 찾는다.
  - 현재 `aimPointRetargetInterval` 기본값 `5초`마다 `BossController.AimPoint`가 랜덤으로 바뀐다.
- `Assets/_Project/Scripts/Gameplay/PlayerCombatController.cs`
  - 현재 기본 탄과 미사일이 `bossController.AimPoint`를 향해 발사된다.
  - 따라서 현재 구조에서는 랜덤으로 바뀐 `AimPoint`가 곧 플레이어 발사 목표가 된다.
- `Assets/_Project/Scripts/UI/BattleDamageNumberPresenter.cs`
  - `ShowDamage(worldPosition, damage, critical)` 형태로 이미 크리티컬 숫자 이미지 표시를 지원한다.
- `Assets/_Project/Scripts/UI/DamageNumberSpriteCatalog.cs`
  - `normalDigits`, `criticalDigits` 배열이 이미 분리되어 있다.

## 표시 방식

타겟 이미지는 월드 스페이스 오브젝트가 아니라 `BattleCanvas` 하위의 Screen Space UI로 표시한다.

- 각 `AimPoint`마다 UI 마커 루트를 하나씩 만든다.
- 매 프레임 `Camera.WorldToScreenPoint(aimPoint.position)`로 3D 위치를 화면 좌표로 변환한다.
- `RectTransformUtility.ScreenPointToLocalPointInRectangle`로 Canvas 좌표로 변환한다.
- 카메라 뒤쪽에 있는 `AimPoint`는 숨긴다.
- 마커는 `LateUpdate`에서 갱신해 보스, 카메라 이동 이후 위치를 맞춘다.

## 타겟 상태

| 상태 | 의미 | Base | Inner | 깜빡임 |
| --- | --- | --- | --- | --- |
| 상태1 | 기본 표시 | `targeting_normal_base` | `targeting_normal_inner` | 없음 |
| 상태2 | 랜덤 크리티컬 타이밍 표시 | `targeting_normal_base` | `targeting_normal_inner` | Inner만 깜빡임 |
| 상태3 | 사용자가 선택한 발사 목표 | `targeting_alert_base` | `targeting_alert_inner` | Inner만 깜빡임 |

상태 우선순위는 다음과 같다.

1. 사용자가 선택한 `AimPoint`는 항상 상태3으로 보인다.
2. 현재 랜덤 크리티컬 `AimPoint`는 상태2로 보인다.
3. 나머지는 상태1로 보인다.

사용자 선택과 랜덤 크리티컬 대상이 같은 경우, 화면 표시는 상태3이 우선한다. 단, 그 5초 윈도우 안에서는 크리티컬 판정 보너스는 유지된다.

## 랜덤 크리티컬 타이밍

- 전투 중 5초마다 하나의 `AimPoint`를 랜덤 크리티컬 대상으로 지정한다.
- 새 랜덤 대상이 지정되면 이전 랜덤 대상은 상태1로 돌아간다.
- 이전 랜덤 대상이 사용자가 선택한 대상이면 상태3을 유지한다.
- 현재 사용자가 선택한 `AimPoint`는 다음 랜덤 대상 후보에서 제외한다.
- 후보가 없으면 해당 주기에는 랜덤 크리티컬 대상을 만들지 않는다.

선택된 대상이 현재 랜덤 크리티컬 대상인 경우에도 즉시 랜덤 대상을 바꾸지 않는다. 다음 5초 갱신 시점에만 새 랜덤 대상을 뽑고, 그때 현재 선택 대상을 후보에서 제외한다.

## 사용자 선택

- 모바일 터치와 마우스 클릭 모두 지원한다.
- 터치 가능한 대상은 상태1, 상태2 모두 가능하다.
- 사용자가 `AimPoint` 마커를 선택하면 해당 마커는 상태3으로 바뀐다.
- 한 번에 하나의 사용자 선택 대상만 존재한다.
- 다른 `AimPoint`를 선택하면 기존 선택 대상은 선택 해제되고, 현재 랜덤 크리티컬 상태에 따라 상태1 또는 상태2로 돌아간다.

터치 판정은 UI 마커에 직접 붙이는 방식이 우선이다. 즉, 3D `AimPoint`에 별도 콜라이더를 추가하기보다 Screen Space 마커에 클릭/터치 핸들러를 붙여 선택한다.

## 발사 목표 변경

현재 랜덤으로 바뀌는 `BossController.AimPoint`를 그대로 플레이어 발사 목표로 쓰는 동작은 제거한다.

새 기준은 다음과 같다.

- 플레이어 기본 탄은 사용자가 선택한 `AimPoint`를 향해 발사한다.
- 플레이어 미사일도 사용자가 선택한 `AimPoint`를 추적한다.
- 사용자 선택 전에는 기본 `AimPoint` 또는 보스 중심점을 fallback 목표로 사용한다.
- 랜덤 크리티컬 대상은 발사 목표를 강제로 바꾸지 않는다.

이 변경은 플레이어 무기 조준에만 적용한다. 보스 공격의 발사 위치나 패턴용 조준 로직은 별도 요구가 없으면 유지한다.

## 크리티컬 판정

기본 탄 피해량은 현재 `25`다.

| 상황 | 크리티컬 발생률 |
| --- | ---: |
| 일반 명중 | 5% |
| 현재 랜덤 크리티컬 대상에 맞춘 발사가 명중 | 20% |

크리티컬 판정은 발사 시점 기준으로 스냅샷을 잡는다. 발사 시점에 선택 대상이 현재 랜덤 크리티컬 대상이면 그 투사체는 명중 시 20% 판정을 사용한다. 발사 후 5초 주기가 지나 랜덤 대상이 바뀌어도 이미 발사된 투사체의 크리티컬 확률은 바뀌지 않는다.

크리티컬이 발생하면 `BattleDamageNumberPresenter.ShowDamage(..., critical: true)`를 사용해 `criticalDigits` 숫자 이미지를 표시한다.

## 미정 항목

현재 규칙에는 크리티컬 발생률은 정의되어 있지만, 크리티컬 발생 시 실제 피해 배율은 정의되어 있지 않다.

구현 전에 아래 값 중 하나를 결정해야 한다.

- 크리티컬은 숫자 이미지와 연출만 바꾸고 피해량은 `25` 그대로 유지한다.
- 크리티컬 피해량을 `25 * criticalDamageMultiplier`로 계산한다.
- 크리티컬 피해량을 별도 고정값으로 둔다.

피해 배율이 정해지기 전까지는 크리티컬 판정과 숫자 이미지 표시는 구현 가능하지만, 최종 데미지 공식은 완성되지 않은 상태로 본다.

## 구현 메모

- `BossController`는 모든 `AimPoint` 목록을 외부에서 읽을 수 있게 공개 API를 제공해야 한다.
- 랜덤 크리티컬 대상과 사용자 선택 대상은 `BossController.AimPoint` 하나에 섞지 않는다.
- 별도 타겟팅 컨트롤러가 `AllAimPoints`, `SelectedAimPoint`, `CriticalWindowAimPoint`를 관리하는 편이 안전하다.
- `PlayerCombatController`는 발사 시점에 선택된 목표와 크리티컬 확률을 투사체에 전달해야 한다.
- `ProjectileController` 또는 별도 데미지 컨텍스트가 명중 시 `BattleController.TryHitBoss`에 크리티컬 여부를 전달해야 한다.
- `BattleController.TryHitBoss`는 최종 피해량과 `critical` 플래그를 함께 받아 데미지 숫자 표시까지 연결해야 한다.
