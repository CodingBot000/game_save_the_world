---
title: Background Ally Army 개발계획서
document_id: TD-BACKGROUND-ALLY-ARMY
document_type: development-plan
status: "구현됨(공중) / 계획(지상)"
version: "0.3.0"
created: "2026-08-29"
last_verified: "2026-08-30"
verification_scope: "Unity 6000.4.0f1: 모델 전방 반전, 공격 기동 속도 0.5배, 순찰 개틀링 버스트, 단일 랜덤 연기 추락 구현. 집중 EditMode 7/7, 실제 BattleArena 기수/이동 최소 내적 0.911, 개틀링 화염 167회, 트레이서 3발, 추락 2.367m·누적 회전 368.07°·연기 및 전투 불변식 통과. 직전 전체 110/110 유지; 신규 전체 MCP 작업은 95/112에서 상태 갱신 손실로 미확정. 탱크·모바일은 미구현/미검증."
implementation_baseline: "git a6554a7 + 사용자 작업 트리 보존"
unity_version: 6000.4.0f1
---

# Background Ally Army 개발계획서

## 1. 목적

BattleArena의 거대 몬스터 주변에 **전투에 참가하는 것처럼 보이는 배경 아군 부대**를 배치해 전장의 규모감과 생동감을 높인다.

1차에서는 공중 아군 헬기를 구현한다. 한두 대는 단독으로 비행하고, 두세 대는 편대를 이루어 몬스터 뒤쪽과 주변을 순찰한다. 일정 간격으로 몬스터를 향해 진입하고 기관총을 쏘는 시늉을 한 뒤 이탈·복귀한다.

2차에서는 같은 시스템에 지상 탱크 부대를 추가한다. 탱크는 StageVisualRoot에 고정된 지상 경로를 따라 종대로 이동하고, 가끔 정지하거나 속도를 낮춘 뒤 몬스터를 향해 포격하는 시늉을 한다.

두 연출은 모두 **순수 배경 연출**이다. 보스 체력, 플레이어 체력, 락온 타깃, 보스 패턴, 승패 조건에 영향을 주지 않는다.

## 2. 현재 상태

상태: **공중 구현됨 / 지상 계획**

BattleArena에는 공중 배경 아군 부대가 연결되어 있다. 탱크 에셋 분리와 지상 경로·포격은 아직 구현하지 않았다.

| 항목 | 현재 확인 결과 | 상태 |
| --- | --- | --- |
| 전투 흐름 | `BattleController`가 보스·플레이어 생존과 `IsBattleActive`를 소유한다. | 구현됨 |
| 배경 초기화 | `BattleController.ConfigureBackground()`가 기존 배경 회전 소스와 `BackgroundAllyArmyController`를 함께 구성한다. | 구현됨 |
| 보스 기준점 | `BossController`가 `OrbitCenter`, `AimPoint`, `HitPoint`를 제공한다. | 구현됨 |
| Base 카메라 | BattleArena의 `ArenaCamera`가 MainCamera다. `ArenaCameraRig` 컴포넌트는 현재 비활성이고 코드의 카메라 궤도도 임시 중단 상태다. | 구현됨 / 비활성 |
| 지형 회전 | `StageVisualRoot`에 `MoonOrbitController`가 있고 4°/s로 `Time.unscaledDeltaTime` 회전한다. | 구현됨 |
| 플레이어 표시 | 플레이어 헬기는 PlayerVisual 전용 오버레이 카메라에 표시된다. | 구현됨 |
| 배경 헬기 에셋 | 500 tris FBX, Base Color 256, Normal/Metallic-Smoothness 128을 프로젝트 전용 경로에 반입하고 shared Material/Prefab을 생성했다. | 구현됨 |
| 공중 순찰 | 단독기 1대와 3대 편대가 카메라 기준 타원 궤도를 돌며 높이·지연·뱅크 차이를 사용한다. | 구현됨 |
| 공중 가짜 공격 | 한 번에 한 그룹만 Approach → AttackRun → BreakAway → Rejoin을 실행하고 2~4발의 피해 없는 트레이서를 사용한다. | 구현됨 |
| 헬기 로터 | 단일 메시 위에 Main/Tail 로터 블러 평면을 추가하고 한 관리자가 회전시킨다. | 구현됨 |
| 탱크 에셋 | 외부 `small_tanks` 파일은 여러 탱크가 한 메시/파일에 포함된 묶음이다. 개별 탱크 기준 분리·최적화가 아직 필요하다. | 준비 필요 |

게임 시스템의 단일 기준은 [Titan Destroyer 게임 시스템 중심 문서](cur_state/titan-destroyer-game-system-master.md)다. 이 계획을 구현할 때 실제 연결 상태와 검증 범위를 중심 문서에 `구현됨` 또는 `프로토타입/디버그`로 갱신한다.

### 2.1 공중 Phase 0~2 구현 결과 — 2026-08-29

| 항목 | 실제 결과 |
| --- | --- |
| 구성 | 단독기 1대 + 3대 V자 편대, 총 4대 |
| 메시 | 1대당 500 tris, 모든 인스턴스가 같은 Mesh 사용 |
| Material | 헬기/로터/트레이서 shared Material, 헬기 GPU Instancing 활성 |
| Collider/Rigidbody | 0 / 0 |
| 순찰 | X 11m / Y 5m 타원, 카메라 Up +5.5m, 카메라 쪽 깊이 -2m, 28초 주기 |
| 표시 크기 | 1280×720 검증에서 로터 포함 약 59~73×43~50px |
| 편대 | 1.5m 후행, 0.85m 좌우, 0.28초 SmoothDamp, 최대 8° 뱅크 |
| 자세 제한 | 월드 수평 기준 최대 7° 피치 + 8° 뱅크. 카메라 평면의 수직 접선을 그대로 바라보지 않음 |
| 모델 전방 | FBX VisualRoot Y=-90°. 기수와 Muzzle이 런타임 +Z 이동 방향을 향함 |
| 공격 | 9~16초 간격, 시도 확률 38%, 2~4발, 동시 공격 최대 1, 기동 속도 배율 0.5 |
| VFX | 고정 LineRenderer 풀 12개, 0.18초 트레이서, 풀 고갈 시 생략 |
| 순찰 개틀링 | 기체별 0.55~0.9초 버스트, 0.075~0.11초 화염 간격, 1.25~2.2초 쿨타임 |
| 랜덤 추락 | 20~34초 간격, 동시에 1대, 연기·중력 낙하·240~420°/s 자회전, 4~7초 뒤 재보충 |
| 실제 격리 실행 | 4대 생성 → 강제 단독기 공격 → 트레이서 3발 → 전원 순찰 복귀 |
| 전투 불변식 | 보스 HP 2000, 플레이어 Hull/Armor 100/120, 락온 유효 타깃 5개 유지 |
| 자동 테스트 | 2026-08-29 전체 EditMode 110/110, 2026-08-30 자세 보정 집중 7/7 통과 |
| 최종 실행 검증 | 최대 Up 편차 10.053°, 실제 이동/기수 최소 내적 0.911, 개틀링 167회, 추락 2.367m·368.07° |

검증 산출물은 로컬 `Logs/BackgroundAllyArmy/`에 보존한다. `patrol.png`, `attack.png`, `rejoined.png`, `army-isolated.png`, `army-attack-isolated.png`, `runtime.txt`를 생성했다. Unity MCP 원격 세션은 연결되지 않아 Unity 6000.4.0f1 배치 모드 Builder·테스트·Play Mode 검증으로 대체했다.

2026-08-30 자세 보정에서는 Unity MCP 연결을 사용했다. 헬기가 카메라 평면 타원의 수직 접선을 그대로 바라보며 꼬리를 아래로 세우거나 이동 반대 방향으로 보일 수 있던 경로를 제거했다. 이동 위치는 기존 타원을 유지하고, 기수는 월드 수평 방향을 사용하며 수직 이동량은 최대 7° 피치로만 표현한다. 전체 회귀 MCP 작업은 99/112에서 도구 작업 상태가 갱신되지 않아 통과로 간주하지 않았으며, 직전 전체 110/110과 이번 변경 집중 7/7·실제 자세 검증을 별도 근거로 사용한다.

같은 날 후속 수정에서 FBX VisualRoot를 Y=-90°로 반전해 보이는 기수와 런타임 +Z 이동 방향을 일치시켰다. 위치 보간 뒤 실제 프레임 이동 벡터를 자세 입력으로 사용하고 큰 방향 오차에만 회전 응답을 높여 최종 최소 이동/기수 내적 0.911을 확인했다. 공격 상태 시간은 `1 / attackMotionSpeedScale`로 늘리며 현재 배율 0.5이므로 기존 순간 기동의 절반 속도다. 각 기체는 독립 개틀링 버스트/쿨타임 화염을 사용하고, 전역 추락 디렉터는 한 번에 한 대만 연기·자회전 낙하시킨 뒤 숨김 대기 후 궤도에 재보충한다. 최종 격리 실행에서 개틀링 화염 167회, 트레이서 3발, 추락 1회·2.367m·368.07°가 기록됐고 보스/플레이어/락온 값은 변하지 않았다. 신규 전체 MCP 작업은 실패 0 상태로 95/112까지 진행한 뒤 도구 상태 갱신을 잃어 최종 통과로 기록하지 않는다.

## 3. 범위

### 3.1 1차 필수 범위: 공중 헬기 부대

- 단독 헬기 1~2대.
- 편대 헬기 2~3대.
- 기본 총 3~5대. 최종 수는 Inspector에서 조정 가능.
- 보스 뒤쪽과 주변의 카메라 기준 타원 궤도 순찰.
- 편대장과 편대원의 V자 대형.
- 기체별 미세한 속도·고도·반응 지연 차이.
- 진행 방향 회전과 선회 뱅크.
- 가끔 몬스터를 향한 Approach → AttackRun → BreakAway → Rejoin 연출.
- 총구 섬광과 짧은 기관총 트레이서.
- 전투 종료 시 새 공격 중단 및 활성 연출 정리.
- 동일 Mesh/Material 공유와 GPU Instancing.

### 3.2 2차 필수 범위: 지상 탱크 부대

- 묶음 탱크 파일을 개별 탱크 단위로 분리.
- 개별 탱크 메시·텍스처 최적화.
- StageVisualRoot 로컬 좌표로 작성한 지상 경로.
- 2~4대 단위 종대 또는 소대 이동.
- 차량별 간격·속도·정지 시간 차이.
- 몬스터 방향 포격 시늉과 포구 섬광·포탄 궤적·원거리 폭발 연출.
- 공중 부대와 공유하는 전역 가짜 공격 예산.
- 탱크가 지면에서 뜨거나 StageVisualRoot 회전과 분리되지 않도록 로컬 좌표 유지.

### 3.3 제외 범위

- 배경 아군 공격의 실제 보스 피해.
- 아군이 보스 공격에 피격되거나 파괴되는 게임플레이.
- 플레이어의 아군 선택·명령·지원 요청 UI.
- 아군 체력, AI 전투 판단, NavMesh 전술 이동.
- 락온 대상 또는 약점 후보 등록.
- 난이도별 아군 화력·생존력 밸런스.
- 멀티플레이 동기화.
- 원본 보스 패턴, 피해량, 전투 타이밍 변경.

## 4. 설계 원칙

### 4.1 전투 시스템과 완전 분리

배경 아군의 총탄·포탄·폭발은 시각 오브젝트만 사용한다.

- `BattleController.TryHitBoss(...)`를 호출하지 않는다.
- `ProjectileController`와 실제 플레이어/보스 투사체 프리팹을 재사용하지 않는다.
- Collider, Rigidbody, Damage 필드를 두지 않는다.
- `BossLockOnTarget`과 락온 관련 컴포넌트를 붙이지 않는다.
- 보스 피격 틴트, 피해 숫자, 최근 공격 타깃 기록을 발생시키지 않는다.
- 배경 아군 레이어가 플레이어/보스 물리 레이어와 충돌하지 않게 한다.

가짜 공격 중 보스 HP와 플레이어 HP/Armor가 변하면 실패로 판정한다.

### 4.2 공중과 지상의 좌표 소유권 분리

공중 부대와 지상 부대는 같은 최상위 시스템에서 관리하지만 좌표 기준은 분리한다.

```text
BattleArenaRoot
└─ AmbientAllyArmyRoot
   ├─ AirRoot                 # BattleArenaRoot 기준, 카메라/보스 상대 좌표
   ├─ GroundBinding           # StageVisualRoot 참조
   │  ├─ GroundRoutes         # StageVisualRoot 로컬 경로
   │  └─ GroundUnits
   └─ CosmeticVfxRoot         # 트레이서·섬광·폭발 풀
```

- **공중 헬기:** StageVisualRoot의 자동 회전에 끌려가지 않는다. Base 카메라와 보스 기준으로 매 프레임 위치를 계산한다.
- **지상 탱크:** 도로·지면과 함께 움직여야 하므로 StageVisualRoot 로컬 공간에 경로와 차량을 둔다.
- **PlayerVisual 오버레이:** 배경 아군을 넣지 않는다. Base 카메라가 월드와 함께 렌더하도록 한다.

### 4.3 한 관리자가 전체 부대를 갱신

기체·차량마다 독립 Update를 붙이지 않는다. `BackgroundAllyArmyController`가 공중·지상 분대를 소유하고 한 번의 Update/LateUpdate에서 전체 Transform을 갱신한다.

- 유닛 데이터는 클래스/구조체로 보관.
- 매 프레임 LINQ, `Find*`, 배열 생성, 문자열 조합 금지.
- 프리팹 생성은 전투 시작 시 1회.
- 트레이서·섬광·폭발은 고정 크기 풀로 재사용.
- 동일 종류 유닛은 shared Mesh와 shared Material 사용.

## 5. 제안 코드 구조

### 5.1 런타임 컴포넌트

| 파일 | 역할 |
| --- | --- |
| `BackgroundAllyArmyController.cs` | 공중·지상 부대 초기화, 전투 상태, 전역 가짜 공격 슬롯과 수명 관리 |
| `BackgroundAirSquadRuntime.cs` | 단독기·편대 궤도, 편대장/편대원 위치, 공격 상태 머신 |
| `BackgroundGroundColumnRuntime.cs` | StageVisualRoot 로컬 경로, 탱크 간격·속도·정지·포격 상태 |
| `BackgroundAllyUnitView.cs` | Transform/Renderer/포구 앵커 캐시. 자체 Update 없음 |
| `BackgroundCosmeticAttackPool.cs` | 총구 섬광, 기관총 트레이서, 포탄 궤적, 원거리 폭발 풀 |
| `BackgroundGroundRoute.cs` | StageVisualRoot 로컬 웨이포인트와 폐회로/왕복 경로 설정 |

초기 구현에서는 범용 AI 프레임워크나 ScriptableObject 데이터 계층을 만들지 않는다. BattleArena 한 곳에서 검증한 뒤 스테이지별 설정이 실제로 달라질 때 `BackgroundAllyArmyProfile` ScriptableObject를 도입한다.

실제 공중 Phase 0~2에서는 파일 수와 컴포넌트별 Update를 줄이기 위해 공중 그룹·공격 상태·고정 트레이서 풀을 `BackgroundAllyArmyController`의 내부 런타임 타입으로 통합했다. `BackgroundAirSquadRuntime.cs`와 `BackgroundCosmeticAttackPool.cs`는 별도 파일로 만들지 않았다. 지상 Phase에서 공중/지상 공용 API가 실제로 필요해질 때만 분리한다.

### 5.2 프리팹과 에셋

```text
Assets/_Project/Art/Environment/BackgroundAllyArmy/
├─ Air/
├─ Ground/
├─ Materials/
└─ VFX/

Assets/Prefabs/Environment/BackgroundAllyArmy/
├─ BackgroundChopper_500.prefab
├─ BackgroundTank_*.prefab
├─ BackgroundCosmeticTracer.prefab
└─ BackgroundCosmeticImpact.prefab
```

### 5.3 씬 연결

`BattleController.ConfigureBackground()`에서 아래 참조를 전달한다.

```csharp
backgroundAllyArmy.Configure(
    battleController: this,
    boss: bossController,
    baseCamera: Camera.main,
    stageVisualRoot: stageVisualRoot);
```

`BackgroundAllyArmyController`가 반드시 받아야 하는 값:

- `BattleController`: `IsBattleActive` 확인.
- `BossController`: `HitPoint`, `OrbitCenter`, 사망 상태 확인.
- Base `Camera`: 화면 평면과 뒤쪽 깊이 계산.
- `StageVisualRoot`: 지상 탱크 경로의 좌표계.
- 헬기/탱크 프리팹과 shared Material.

씬 참조 누락 시 전투를 막지 않는다. 해당 배경 연출만 비활성화하고 한 번의 명확한 경고를 남긴다.

## 6. 공중 헬기 설계

### 6.1 기본 구성

기본 프리셋:

| 항목 | 기본값 | 허용 범위 |
| --- | ---: | ---: |
| 단독기 | 1 | 1~2 |
| 편대 수 | 1 | 1 |
| 편대원 | 3 | 2~3 |
| 총 헬기 | 4 | 3~5 |
| 타원 X 반경 | 11m | 9~13m |
| 타원 Y 반경 | 5m | 4~6m |
| 보스 뒤 깊이 | 5m | 3~8m |
| 한 바퀴 시간 | 28초 | 22~34초 |
| 편대 간격 | 1.5m | 1.2~1.8m |
| 최대 뱅크 | 8° | 6~10° |

### 6.2 카메라 기준 타원 궤도

공중 중심점은 보스 뒤쪽에 둔다.

```text
center = boss.HitPoint + camera.forward × depthBehind
position = center
         + camera.right × cos(angle) × radiusX
         + camera.up    × sin(angle) × radiusY
```

카메라 `forward`의 양의 방향은 카메라에서 장면 안쪽이므로 `depthBehind > 0`이면 화면상 보스 뒤에 놓인다.

월드 X/Y에 고정하지 않고 카메라의 right/up/forward를 사용한다. 화면비 변경과 향후 카메라 궤도 복원에도 경로가 유지되어야 한다.

### 6.3 편대

편대장이 궤도를 직접 이동한다. 편대원은 편대장의 접선과 방사 방향으로 V자 목표점을 만든다.

```text
              Leader
                ▲
               / \
      WingLeft     WingRight
```

```text
wingTarget = leader
           - tangent × trailDistance
           ± radial  × lateralDistance
```

자연스러움을 위해 편대원은 목표점에 즉시 고정하지 않는다.

- `Vector3.SmoothDamp` 반응시간 0.2~0.35초.
- 기체별 속도 편차 ±5%.
- 높이 편차 ±0.15~0.3m.
- PerlinNoise 기반 저주파 흔들림.
- 위치·속도·노이즈 seed를 전투 시작에 1회 확정.

### 6.4 자세

- 기수는 경로 접선 방향을 본다.
- 회전은 지수 감쇠 Slerp로 보간한다.
- 선회 방향으로 최대 6~10° 뱅크한다.
- 공격 진입 중 기수를 조금 낮추고, 이탈 중 기수를 높인다.
- 모델 축이 Unity +Z 전방과 다르면 프리팹의 `VisualRoot` 회전 오프셋으로 해결한다. 런타임 이동 수학에 모델별 상수를 섞지 않는다.

### 6.5 로터 표현

500 tris 최적화 헬기는 단일 메시다. 1차 기본안은 다음과 같다.

- 기존 메시 실루엣은 유지.
- 메인 로터 위치에 저알파 블러 원판 또는 교차 평면 추가.
- 테일 로터 위치에 작은 블러 원판 추가.
- 별도 Transform 또는 UV 회전으로 애니메이션.
- 블러 원판은 그림자 생성/수신 비활성.
- 투명 오버드로를 줄이기 위해 화면상 크기에 맞는 최소 면적 사용.

실제 블레이드 분리·회전은 블러 원판이 육안상 부적합할 때만 2차 대안으로 수행한다.

## 7. 공중 가짜 공격 설계

### 7.1 상태 머신

```text
Patrol
  → Approach
  → AttackRun
  → BreakAway
  → Rejoin
  → Patrol
```

| 상태 | 역할 | 기본 시간 |
| --- | --- | ---: |
| Patrol | 정상 타원 순찰 | 9~16초 무작위 |
| Approach | 궤도에서 이탈해 보스 옆 진입점으로 이동 | 0.8~1.2초 |
| AttackRun | 보스를 스쳐 지나가며 트레이서 발사 | 1~1.5초 |
| BreakAway | 외곽·상단 방향으로 급선회 | 1.5~2.5초 |
| Rejoin | 가장 가까운 궤도 위상으로 부드럽게 복귀 | 거리 기반, 최대 3초 |

### 7.2 공격 디렉터

전체 부대에서 동시에 하나의 가짜 공격만 허용한다.

- 전역 공격 간격 9~16초.
- 공격 시도 확률 30~40%.
- 단독기 또는 편대장 중 하나 선택.
- 편대 공격에서는 편대원도 진입하되 트레이서는 편대장 중심으로 제한.
- 1회 기관총 버스트 2~4개.
- 보스 중심을 정확히 관통하지 않고 주변 오프셋을 사용해 실제 피격과 구분.
- 실제 플레이어 탄보다 채도·밝기·크기를 낮춘다.

`BattleController.IsBattleActive == false` 또는 보스 사망 시 새 공격을 시작하지 않는다. 진행 중인 트레이서는 즉시 또는 짧은 페이드로 정리하고, 헬기는 순찰만 계속하거나 설정에 따라 화면 밖으로 이탈한다. 1차 기본안은 **공격 중단 + 순찰 유지**다.

### 7.3 순찰 개틀링 총구 화염

상태: **구현됨**

- 모든 활성 헬기는 서로 다른 초기 쿨타임으로 시작한다.
- 0.55~0.9초 동안 0.075~0.11초 간격으로 Muzzle 앞 Cross-Quad 화염을 점멸한다.
- 버스트 뒤 1.25~2.2초 쿨타임을 가진 뒤 다시 발사한다.
- 추락·전투 종료·컴포넌트 비활성화 중에는 즉시 화염을 끈다.
- 총구 화염은 실제 투사체·피해·Collider를 만들지 않는다.

### 7.4 랜덤 연기 추락

상태: **구현됨**

- 20~34초마다 활성 헬기 중 한 대를 무작위 선택한다.
- 동시에 추락 가능한 헬기는 한 대뿐이다.
- 추락 기체는 기존 진행 속도의 일부를 유지하면서 중력 2.4로 하강하고 로컬 혼합축으로 240~420°/s 자회전한다.
- World simulation 연기 ParticleSystem을 켜 기체가 회전해도 연기가 뒤에 남는다.
- 3.2~4.5초 또는 화면 아래 이탈 시 렌더를 숨기고, 4~7초 뒤 현재 궤도 위치에 재보충한다.
- 편대장 추락 시 진행 중인 편대 가짜 공격만 취소하고 다른 기체의 순찰은 유지한다.
- 추락은 피해·폭발 판정·보스/플레이어 상태를 변경하지 않는다.

## 8. 지상 탱크 설계

### 8.1 에셋 전처리

현재 `small_tanks`는 여러 차량이 한 메시/파일에 묶여 있으므로 먼저 아래 작업이 필요하다.

1. 개별 탱크의 공간 그룹을 식별한다.
2. 탱크 한 대씩 별도 MASTER/최적화 변형/내보내기를 만든다.
3. 원본 외형 비교 렌더를 남긴다.
4. 탱크 1대당 삼각형 목표를 정한다.
5. 공통 텍스처를 공유할 수 있으면 atlas/shared material을 유지한다.
6. Unity에서 각 탱크 프리팹의 전방축·바닥 피벗·포구 앵커를 통일한다.

초기 권장 예산:

| 항목 | 목표 |
| --- | ---: |
| 탱크 1대 삼각형 | 300~700 tris |
| Base Color | 256px |
| Normal | 128px 또는 생략 비교 |
| Metallic/Smoothness | 128px |
| Material | 가능하면 전 탱크 공용 1개 |

탱크가 화면에서 헬기보다 더 작다면 300~500 tris를 우선 비교한다. 차체·포탑·포신 실루엣이 무너지면 700 tris까지 허용한다.

### 8.2 지상 경로

탱크 경로는 `BackgroundGroundRoute` 컴포넌트와 StageVisualRoot 자식 웨이포인트로 작성한다.

```text
StageVisualRoot
└─ BackgroundGroundRoutes
   ├─ Route_A
   │  ├─ P0
   │  ├─ P1
   │  └─ P2
   └─ Route_B
```

- 웨이포인트는 StageVisualRoot 로컬 좌표.
- 폐회로 또는 왕복 모드 선택.
- 탱크도 StageVisualRoot 아래에서 로컬 위치·회전을 갱신.
- 경로 고도는 지면에서 약간 띄운 값으로 직접 저작.
- 초기 구현은 NavMesh와 매 프레임 지면 Raycast를 사용하지 않는다.
- 여러 지형에 재사용할 필요가 생기면 Editor 경로 스냅 도구 또는 제한적 Raycast 베이크를 검토한다.

### 8.3 탱크 종대

- 2~4대가 같은 경로를 일정 거리 차이로 이동.
- 선두 차량 진행률을 기준으로 후속 차량이 거리 오프셋을 사용.
- 차량별 속도 ±3%, 간격 ±0.2m.
- 곡선에서 `Quaternion.Slerp`로 부드럽게 방향 전환.
- 경로 급회전은 저작 단계에서 제거.
- 화면 밖 구간에서 시작해 자연스럽게 진입하고, 끝에서 재사용/순환.

### 8.4 탱크 가짜 포격

```text
Drive → SlowDown → AimPause → Fire → RecoilHold → Resume
```

- 공중 부대와 같은 전역 공격 슬롯을 사용.
- 포탑 분리가 가능하면 몬스터 방향으로 제한 회전.
- 포탑이 단일 메시라면 차량 전체 방향은 유지하고 포구 섬광·탄도만 몬스터 방향으로 표현.
- 포탄은 포물선 LineRenderer/Trail 연출이며 Collider와 피해가 없다.
- 보스 주변 원거리 폭발은 실제 피격 VFX보다 작고 어둡게 표시.
- 포격 중 탱크 종대가 모두 멈추지 않도록 공격 차량만 감속하거나 짧게 정지.

## 9. VFX·오디오 정책

### 9.1 공용 풀

초기 최대값:

| VFX | 풀 크기 |
| --- | ---: |
| 기관총 트레이서 | 12 |
| 헬기 총구 섬광 | 4 |
| 탱크 포탄 궤적 | 4 |
| 탱크 포구 섬광 | 4 |
| 원거리 폭발 | 6 |

풀 부족 시 새 인스턴스를 생성하지 않고 해당 연출을 생략한다. 배경 연출 누락은 전투 프레임 드롭보다 우선순위가 낮다.

### 9.2 오디오

1차 기본안은 배경 공격 전용 음원을 추가하지 않는다. 시각 연출의 가독성을 먼저 확인한다.

오디오를 추가할 경우:

- 3D Spatial Blend 사용.
- 낮은 볼륨과 짧은 최대 거리.
- `GlobalSoundSettings`와 AudioListener 제어를 따름.
- 플레이어 기관총·락온 효과음보다 낮은 우선순위.
- 공격마다 재생하지 않고 전체 배경 공격에 쿨다운 적용.

## 10. 성능 예산

목표 플랫폼에서 실제 프로파일링 전 사용하는 초기 예산이다.

| 항목 | 목표 |
| --- | ---: |
| 헬기 | 500 tris × 최대 5대 = 2,500 tris |
| 탱크 | 최대 700 tris × 최대 6대 = 4,200 tris |
| 전체 배경 아군 가시 메시 | 최대 8,000 tris |
| 배경 아군 Material | 공중 1개, 지상 1~2개, 로터/VFX 별도 최소화 |
| 동시 가짜 공격 | 1개 |
| 동시 활성 트레이서/포탄 | 12개 이하 |
| 프레임당 GC Alloc | 0 B 목표 |
| 배경 아군 Collider/Rigidbody | 0개 |

그림자 기본 정책:

- 헬기 Shadow Casting Off.
- 탱크는 Off로 시작하고 지면 접지감이 부족할 때만 저비용 blob shadow 또는 제한된 그림자 사용.
- 투명 로터 블러는 그림자 비활성.

## 11. 구현 단계

### Phase 0. 에셋 반입과 기준 고정

상태: **구현됨**

- 500 tris 헬기 FBX와 텍스처를 프로젝트 전용 아트 경로에 복사.
- URP Lit shared Material 생성.
- `BackgroundChopper_500.prefab` 생성.
- 전방축·스케일·보스 대비 화면 크기 확인.
- 로터 블러 프리팹 프로토타입.
- 원본 외부 파일과 최적화 레시피 경로 기록.

완료 조건:

- Unity가 FBX를 오류 없이 임포트.
- 1 Mesh, 1 Material, 500 tris 확인.
- 실제 BattleArena 카메라에서 목표 화면 크기 확인.
- PlayerVisual 레이어와 분리.

### Phase 1. 공중 순찰과 편대

상태: **구현됨**

- `BackgroundAllyArmyController`와 공중 런타임 추가.
- AirRoot 생성과 단독기·편대 스폰.
- 카메라 기준 타원 궤도.
- 편대 목표점·SmoothDamp·노이즈.
- 진행 방향 회전과 뱅크.
- 화면비별 경로 확인.

완료 조건:

- 3~5대가 서로 겹치지 않고 반복 경계가 보이지 않게 순찰.
- 보스 뒤쪽/주변 깊이가 유지됨.
- 16:9, 20:9, 4:3에서 주요 기체가 지나치게 잘리지 않음.
- 10분 순찰 동안 예외와 누적 위치 오차 없음.

### Phase 2. 공중 가짜 공격

상태: **구현됨**

- 공중 상태 머신과 전역 공격 슬롯.
- 보스 주변 접근점·공격 통과점·이탈점 생성.
- 총구 앵커와 트레이서 풀.
- 공격 종료 후 가장 가까운 궤도 위상으로 복귀.
- 승리·패배·Retry 정리.

완료 조건:

- 단독기와 편대 공격이 각각 자연스럽게 실행됨.
- 동시에 둘 이상의 공격이 겹치지 않음.
- 공격 전후 보스 HP, 플레이어 HP/Armor, 락온 후보 수 변화 0.
- 실제 플레이어 탄·보스 경고와 시각적으로 구분됨.

### Phase 3. 탱크 에셋 분리·최적화

상태: **계획**

- `small_tanks` 묶음 분석.
- 실제 포함 탱크 수와 개별 삼각형 수 기록.
- 300/500/700 tris 후보 비교.
- 저해상도 텍스처와 shared Material 준비.
- 탱크별 피벗, 전방축, 포구 앵커 통일.

완료 조건:

- 개별 탱크 프리팹을 독립 배치 가능.
- 실제 게임 거리에서 차체·포탑·포신 실루엣 유지.
- 원본 외형 비교 렌더와 Recipe 보존.

### Phase 4. 지상 경로와 종대

상태: **계획**

- `BackgroundGroundRoute`와 웨이포인트 작성.
- StageVisualRoot 로컬 좌표 이동.
- 종대 간격·속도·방향 보간.
- 화면 밖 진입·퇴장과 순환.

완료 조건:

- StageVisualRoot 회전 중 탱크가 도로/지면에서 미끄러지지 않음.
- 탱크가 뜨거나 지면을 관통하지 않음.
- 경로 반복 지점이 카메라 안에서 눈에 띄지 않음.

### Phase 5. 지상 가짜 포격

상태: **계획**

- 공중과 공유하는 공격 슬롯.
- 감속·조준 대기·포구 섬광·포탄 궤적·폭발·재가속.
- 활성 VFX 풀과 종료 정리.

완료 조건:

- 헬기 공격과 탱크 포격이 동시에 과밀하게 발생하지 않음.
- 포격 전후 모든 전투 수치 변화 0.
- 실제 보스 공격 경고·플레이어 탄과 혼동되지 않음.

### Phase 6. 통합·성능·문서화

상태: **계획** — 공중 단독 검증은 완료, 지상 통합 이후 전체 단계 수행

- 공중·지상 부대 동시 10분 soak.
- 해상도·화면비·Day/Night/Rain 확인.
- 5락 일제사격·월드 카메라 진동 중 배경 위치 확인.
- 승리·패배·Undead·Retry·Quit 확인.
- Profiler로 배경 아군 CPU/GC/렌더 비용 기록.
- 중심 문서와 docs 인덱스의 상태 갱신.

완료 조건:

- 아래 §12 합격 기준 충족.
- 구현 파일·씬·프리팹·문서 상태가 일치.
- 미검증 항목을 `불일치` 또는 `계획`으로 남김.

## 12. 검증 계획과 합격 기준

### 12.1 기능

- 기본 1개 단독기 + 3대 편대가 생성된다.
- Inspector 설정으로 단독기 2대, 편대 2대 구성이 가능하다.
- 편대원은 편대장과 충돌·중첩하지 않고 V자 대형을 유지한다.
- 공격 후 모든 기체가 순찰 궤도로 복귀한다.
- 탱크 종대가 StageVisualRoot 로컬 경로를 유지한다.
- 공중/지상 공격이 하나의 전역 슬롯을 공유한다.

### 12.2 전투 불변식

- 가짜 공격 전후 보스 HP 변화 0.
- 플레이어 Hull/Armor 변화 0.
- 보스 최근 공격 타깃 상태 변화 0.
- 락온 후보와 성공 락 수 변화 0.
- 보스 공격 패턴 순서·간격·피해 변화 0.
- 승패 조건 변화 0.

### 12.3 화면과 연출

- 배경 헬기가 플레이어 헬기보다 시각적 우선순위가 낮다.
- 보스 약점·빔 경고·투사체를 지속적으로 가리지 않는다.
- 트레이서와 폭발이 실제 플레이어 공격으로 오인되지 않는다.
- 16:9, 20:9, 4:3에서 궤도와 경로가 화면 밖으로 과도하게 잘리지 않는다.
- Side/oblique 시점에서 로터 블러가 기체를 가리지 않는다.
- Day/Night/Rain에서 Material이 과도하게 밝거나 검게 되지 않는다.

### 12.4 상태 전환

- 일시정지에서 순찰·공격 타이머가 의도대로 정지한다.
- 보스 사망 시 새 가짜 공격이 시작되지 않는다.
- 플레이어 사망 시 활성 VFX가 정리된다.
- Undead 디버그 복귀 시 중복 유닛·중복 이벤트가 생기지 않는다.
- Retry 후 이전 씬 유닛·풀·이벤트 참조가 남지 않는다.
- Quit 후 MainMenu에서 예외가 없다.

### 12.5 성능

- 최대 헬기 5대 + 탱크 6대에서 배경 아군 가시 메시 8,000 tris 이하.
- 유닛 이동 프레임당 GC Alloc 0 B.
- 트레이서 풀 부족 시 런타임 Instantiate 없음.
- shared Mesh/Material 참조 유지.
- Unity Console error 0.
- 10분 Play Mode soak에서 오브젝트 수가 증가하지 않음.

## 13. 자동 테스트 계획

### EditMode

- 카메라 기준 타원 위치 계산.
- 타원 접선과 기수 방향.
- V자 편대 오프셋 좌우 대칭.
- 편대원 SmoothDamp 목표점 범위.
- 공격 상태 전환과 시간 경계.
- 전역 공격 슬롯 동시성 1 보장.
- 공격 종료 후 궤도 위상 복귀.
- StageVisualRoot 로컬 경로 샘플링.
- 폐회로/왕복 경로 경계.
- 풀 고갈 시 생략 정책.

### Play Mode 또는 전용 런타임 진단

- BattleArena 기본 스폰 수와 Material 공유.
- 실제 보스 HP/플레이어 HP 스냅샷 불변.
- 락온 후보 수 불변.
- 보스 사망·플레이어 사망·Undead·Retry 정리.
- 카메라 화면비 변경 후 화면상 궤도 범위.
- StageVisualRoot 회전 중 탱크 로컬 접지.
- 10분 오브젝트 수/GC/Console 검사.

테스트 편의를 위한 강제 공격·강제 위상 API는 Editor/Development 진단으로만 제공한다. 정상 전투 랜덤값과 정적 전역 Random 상태를 영구 변경하지 않는다.

## 14. 예상 변경 파일

| 파일 | 계획한 변경 |
| --- | --- |
| `Assets/_Project/Scripts/Environment/BackgroundAllyArmyController.cs` | **구현됨:** 공중 부대 초기화·순찰·편대·공격 상태·고정 트레이서 풀. 지상 공용 슬롯은 후속 확장 |
| `Assets/_Project/Scripts/BackgroundAllyArmyCore/BackgroundAllyArmyMath.cs` | **구현됨:** 테스트 가능한 타원·접선·편대·보간 순수 수학 |
| `Assets/_Project/Scripts/Environment/BackgroundAllyUnitView.cs` | **구현됨:** Transform/Renderer/포구/로터 캐시, 자체 Update 없음 |
| `Assets/_Project/Scripts/Editor/BackgroundAllyArmyBuilder.cs` | **구현됨:** 임포터·Material·로터 텍스처·Prefab·BattleArena 연결 재생성 |
| `Assets/Editor/BackgroundAllyArmyVerification.cs` | **구현됨:** 실제 BattleArena 격리 실행, 전투 불변식과 캡처 검증 |
| `Assets/_Project/Scripts/Environment/BackgroundAirSquadRuntime.cs` | 별도 파일 미생성. 공중 Phase에서는 Controller 내부 런타임 타입으로 통합 |
| `Assets/_Project/Scripts/Environment/BackgroundGroundColumnRuntime.cs` | 탱크 종대·포격 상태 |
| `Assets/_Project/Scripts/Environment/BackgroundCosmeticAttackPool.cs` | 별도 파일 미생성. 공중 트레이서 풀은 Controller 내부에 구현, 지상 통합 시 분리 검토 |
| `Assets/_Project/Scripts/Environment/BackgroundGroundRoute.cs` | StageVisualRoot 로컬 경로 |
| `Assets/_Project/Scripts/Gameplay/BattleController.cs` | 배경 아군 시스템 참조 resolve와 Configure 호출 |
| `Assets/Scenes/BattleArena.unity/BattleArena.unity` | AmbientAllyArmyRoot, 경로, 직렬화 참조 |
| `Assets/_Project/Art/Environment/BackgroundAllyArmy/**` | 헬기/탱크 모델·텍스처·Material·VFX |
| `Assets/Prefabs/Environment/BackgroundAllyArmy/**` | 헬기·탱크·VFX 프리팹 |
| `Assets/_Project/Tests/EditMode/BackgroundAllyArmyTests.cs` | 이동·편대·공격·경로 순수 로직 테스트 |
| `Assets/Editor/BackgroundAllyArmyVerification.cs` | 실제 BattleArena 진단이 필요할 경우 추가 |
| 본 계획서, `docs/README.md`, 중심 문서 | 단계별 구현·검증 상태와 변경 이력 갱신 |

관련 신규 Unity 에셋과 C# 파일에는 `.meta`를 함께 포함한다. 기존 사용자 씬·에셋·코드 변경을 보존하고, Builder를 사용하더라도 이미 연결된 수동 값을 무조건 덮어쓰지 않는다.

## 15. 위험과 대응

| 위험 | 대응 |
| --- | --- |
| 배경 공격이 실제 공격으로 오인됨 | 색·밝기·크기 낮춤, 피해 숫자/피격 틴트 없음, 발사 빈도 제한 |
| 보스 패턴 경고를 가림 | 보스 화면 중심 회피 오프셋, 공격 동시성 1, 불투명도/크기 제한 |
| 편대가 기계적으로 보임 | SmoothDamp 지연, 속도·높이 편차, 저주파 노이즈 |
| 헬기가 StageVisualRoot 회전에 끌려감 | AirRoot를 StageVisualRoot 밖에 두고 카메라/보스 상대 좌표 사용 |
| 탱크가 회전 지형에서 미끄러짐 | GroundRoutes와 탱크를 StageVisualRoot 로컬 공간에 배치 |
| 로터가 멈춰 보여 부자연스러움 | 저비용 로터 블러 평면 사용. 필요 시에만 원본 메시 분리 |
| 탱크 묶음을 그대로 써서 예산 초과 | 개별 탱크 분리 후 대당 300/500/700 tris 후보 검증 |
| Retry 후 유닛/이벤트 중복 | 씬 로컬 소유, OnDestroy 정리, static에 Transform 저장 금지 |
| VFX Instantiate 스파이크 | 고정 풀과 고갈 시 생략 정책 |
| 향후 카메라 궤도 복원 시 경로 붕괴 | 공중 이동을 카메라 right/up/forward 기준으로 계산 |

## 16. 중단·롤백 기준

아래 조건이면 다음 단계로 진행하지 않고 해당 단계의 연결을 비활성화한다.

- 가짜 공격이 보스 또는 플레이어 전투 수치를 변경한다.
- 보스 빔 경고나 락온 마커를 지속적으로 가린다.
- 배경 헬기가 PlayerVisual 오버레이에 들어간다.
- StageVisualRoot 회전 중 탱크 접지 오차를 해결하지 못한다.
- 최대 구성에서 프레임당 할당이나 런타임 Instantiate가 지속 발생한다.
- 씬 재시작 후 유닛·VFX·이벤트가 중복된다.

롤백 단위:

- `BackgroundAllyArmyController.enabled = false`로 전체 연출 비활성.
- `enableAir`/`enableGround`으로 공중·지상 개별 비활성.
- `enableCosmeticAttacks`로 순찰만 유지하고 공격 연출 제거.
- 씬의 AmbientAllyArmyRoot를 제거해 기존 전투를 즉시 복원 가능해야 한다.

## 17. 결정된 기본안과 추후 결정 항목

### 결정된 기본안

- 기본 공중 구성은 단독기 1대 + 3대 편대, 총 4대.
- 최대 공중 구성은 5대.
- 헬기는 500 tris 최적화 모델 사용.
- 공중 부대는 카메라/보스 상대 좌표, 지상 부대는 StageVisualRoot 로컬 좌표.
- 한 번에 하나의 가짜 공격만 실행.
- 가짜 공격은 피해·충돌·락온과 완전 분리.
- 전투 종료 후 새 공격 중단, 순찰은 유지.
- 헬기 로터는 블러 평면 기본안.
- 탱크는 묶음 파일을 개별 분리한 뒤 구현.

### 구현 중 결정할 항목

- 실제 BattleArena에서 헬기 최종 화면 크기와 depthBehind 값.
- 편대가 보스 앞쪽을 일부 통과할지, 항상 뒤에만 머물지.
- 탱크 1차 노출 수 4대 또는 6대.
- 탱크 경로 수와 스테이지별 경로 저작 방식.
- 배경 공격 전용 3D 음향의 필요 여부.
- 승리 후 부대가 계속 순찰할지 화면 밖으로 이탈할지의 최종 연출.

이 항목은 실제 Game View 비교와 프로파일링으로 결정한다. 임의 값을 구현 상태로 고정하지 않는다.
