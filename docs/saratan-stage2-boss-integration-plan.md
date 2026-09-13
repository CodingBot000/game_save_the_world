---
title: Stage 2 Saratan 보스 통합 개발계획서
document_id: TD-STAGE02-SARATAN-BOSS-INTEGRATION
document_type: development-plan
status: "구현 완료"
version: "1.1.0"
created: "2026-09-12"
last_reviewed: "2026-09-12"
unity_version: 6000.4.0f1
target_branch: feature/stage2-saratan-integration
implementation_baseline: "origin/main fd97e88, 기존 작업 트리의 사용자 변경은 별도 worktree로 격리"
related_documents:
  - kaiju-animation-integration-plan.md
  - cur_state/titan-destroyer-game-system-master.md
  - cur_state/titan-destroyer-latest-design-plan.md
---

# Stage 2 Saratan 보스 통합 개발계획서

## 1. 목적

Stage 2 보스 `Saratan`을 현재 Unity 프로젝트에 추가한다. 첫 구현 범위는 Stage 2를 선택해 공용 `BattleArena`에 진입했을 때 Saratan 전용 모델과 머티리얼이 정상 표시되고 기본 Idle 애니메이션이 재생되는 데까지다.

Stage 1 Kaiju와 Stage 2 Saratan은 모델, 텍스처, 머티리얼, 애니메이션, Animator Controller, Avatar Mask, Prefab, 보스별 설정 데이터를 완전히 분리한다. 패키지 안에서 `Kaiju`라는 이름을 사용하는 파일도 모두 Saratan 제공물로 취급하며 Stage 1 파일을 재사용하거나 참조하지 않는다.

공유 대상은 HP 처리, 전투 진행, HUD, 피해 처리 등 보스에 종속되지 않는 런타임 코드다. 현재는 같은 배경을 사용하지만 스테이지별 환경 진입점을 미리 분리해 향후 Stage 2 배경을 독립적으로 교체할 수 있게 한다.

## 2. 전제와 확정 결정

### 2.1 입력 에셋

입력 경로:

```text
/Users/switch/Downloads/Saratan/
├── Invader Kaiju Saratan.unitypackage
└── 게임ani 사라탄.txt
```

패키지에는 다음 두 명칭 계열이 함께 포함되어 있다.

1. `Kaiju_001` 및 `Kaiju_*` 애니메이션 계열
2. `Saratan_001` 및 `Saratan_*` 애니메이션 계열

두 계열 모두 Stage 2 Saratan 소유 에셋으로 취급한다. 이름만으로 Stage 1 에셋과 동일하다고 판단하지 않는다.

### 2.2 에셋 분리 원칙

- Stage 2 Saratan 에셋은 Stage 1 Kaiju 에셋을 직접 참조하지 않는다.
- 파일 내용이 현재 Stage 1 파일과 바이트 단위로 같아도 별도 에셋과 별도 GUID를 사용한다.
- 패키지 원본 GUID는 본 프로젝트에 그대로 반입하지 않는다.
- 패키지 내부의 Kaiju 명칭은 최종 본 프로젝트에서 `Saratan_RigA_*` 계열로 변경한다.
- 패키지 내부의 Saratan 명칭은 `Saratan_RigB_*` 계열로 정규화한다.
- Rig A/B의 실제 역할은 임시 프로젝트에서 모델 계층과 AnimationCurve 경로 호환성을 검증한 뒤 확정한다.
- 최종 런타임 Prefab이 사용하는 계열만 `Runtime` 구성에 연결하되, 사용하지 않는 계열도 Saratan 소유 Art 폴더 안에 독립 보존한다.

### 2.3 공유 원칙

다음은 Stage 1과 Stage 2가 공유한다.

- `BattleController`
- `BossController`의 체력, 피격, 사망 처리
- 공통 투사체 및 피해 처리
- HUD와 보스 체력 정보 표시
- 플레이어 이동, 락온, 승패 처리
- `StageSelectionState`
- 현재 공용 배경 구현의 기반 Prefab과 코드

다음은 공유하지 않는다.

- 보스 FBX
- 보스 Texture와 Material
- Animation Clip
- Animator Controller와 Avatar Mask
- 보스 완성 Prefab
- 보스 전용 Animation Driver
- 보스별 Socket, Hurtbox, AimPoint 설정
- 보스별 수치와 식별 데이터

## 3. 현재 프로젝트 확인 결과

### 3.1 스테이지 선택과 전투 진입

- `StageSelectionState`는 `SelectedStageId`, `SelectedStageName`, 난이도를 보존한다.
- `StageStepSelectScenePresenter`가 두 번째 페이지를 선택하면 현재 규칙상 `stage_02_seoul`을 저장한다.
- `GameFlowController.StartBattle()`은 선택 스테이지와 무관하게 항상 `BattleArena`를 로드한다.
- `BattleController`는 선택한 StageDefinition을 읽지 않고 씬에 직렬화되거나 배치된 `BossController`를 사용한다.

따라서 폴더와 에셋만 추가해서는 Stage 2 Saratan이 선택되지 않는다. 전투 초기화 전에 선택 ID를 StageDefinition으로 해석하고 보스 Prefab을 생성하는 로더가 필요하다.

### 3.2 현재 Stage 1 보스 결합 상태

- `BattleArena`에는 `BossPlaceholder` Prefab 인스턴스와 Kaiju Visual이 직접 연결되어 있다.
- `BattleController`는 현재 보스와 공격 컨트롤러를 직렬화 참조하고, 없으면 씬에서 검색한다.
- `BossController`와 `BossAttackController`는 `KaijuBossAnimationDriver`를 구체 타입으로 조회한다.
- `KaijuBossAnimationDriver`는 Kaiju 뼈 경로, Beam/Tail, `JumpTurnR` 상태를 전제로 한다.
- Saratan 설명은 별도 뼈 구조, Breath 공격, `JumpTurnL`을 사용한다.

최초 표시 통합(Phase 6)에서는 Stage 1의 기존 전투 동작을 그대로 보존하고 Stage 2 공격 컴포넌트를 비활성화한다. 이후 Phase 9에서 Kaiju 전용 자산을 참조하지 않는 Saratan 독립 임시 공격 구성을 추가한다. Saratan 전용 전투 Animation Driver의 완성은 후속 범위로 둔다.

### 3.3 패키지 충돌 위험

패키지의 Kaiju 명칭 파일은 현재 Stage 1 에셋과 같은 GUID를 다수 포함한다. 애니메이션은 같은 GUID인데 패키지 경로와 현재 프로젝트 경로가 다르고, `Kaiju_Eye.mat`은 같은 GUID이면서 직렬화 내용도 다르다.

따라서 본 프로젝트에 패키지를 직접 전체 Import하지 않는다. 임시 프로젝트에서 신규 GUID를 발급한 독립 Saratan Export 세트를 만든 뒤 반입한다.

### 3.4 애니메이션 입력 상태

Saratan 명칭 계열에는 다음 12개 Clip이 있다.

- `BasicIdle`
- `IdleFront`, `IdleLeft45`, `IdleRight45`
- `Attack_FiringFront`, `Attack_FiringLeft45`, `Attack_FiringRight45`
- `Attack_BreathFront`, `Attack_BreathLeft45`, `Attack_BreathRight45`
- `JumpTurnL`
- `Death`

FBX는 Generic Rig 설정이다. 제공 TXT는 Basic/방향 Idle을 Loop로 지정하지만 패키지의 `.anim` 직렬화 상태에서는 Loop가 꺼져 있으므로 본 프로젝트 반입 후 명시적으로 수정하고 검증한다.

## 4. 목표 폴더 구조

```text
Assets/_Project/Content/
├── Bosses/
│   ├── Kaiju/
│   │   ├── Prefabs/
│   │   │   └── Kaiju.prefab
│   │   └── Data/
│   │       └── KaijuBossDefinition.asset
│   │
│   └── Saratan/
│       ├── Art/
│       │   ├── RigA/
│       │   │   ├── Models/
│       │   │   ├── Textures/
│       │   │   ├── Materials/
│       │   │   └── Animations/
│       │   └── RigB/
│       │       ├── Models/
│       │       ├── Textures/
│       │       ├── Materials/
│       │       └── Animations/
│       ├── Runtime/
│       │   ├── Animation/
│       │   │   ├── Controllers/
│       │   │   │   └── Saratan.controller
│       │   │   └── Masks/
│       │   │       └── SaratanUpperBody.mask
│       │   └── Prefabs/
│       │       └── Saratan.prefab
│       └── Data/
│           └── SaratanBossDefinition.asset
│
└── Stages/
    ├── Stage01/
    │   ├── Stage01Definition.asset
    │   └── Environment/
    │       └── Stage01Environment.prefab
    └── Stage02/
        ├── Stage02Definition.asset
        └── Environment/
            └── Stage02Environment.prefab
```

`Bosses/Kaiju`와 `Bosses/Saratan`은 보스 자체의 소유 경로이며 스테이지에 귀속되지 않는다. 현재 Stage 1/2 배치는 `StageDefinition`의 조합일 뿐이며 이후 다른 스테이지에서도 같은 보스를 선택할 수 있다.

Stage 1 원본 파일은 첫 작업에서 무리하게 이동하지 않는다. 기존 Editor Builder가 경로를 하드코딩하고 있으므로 먼저 Stage 1 완성 보스 Prefab과 Definition을 만들고, 원본 파일 이동은 하드코딩 제거 후 별도 커밋으로 수행한다.

## 5. Git 격리 전략

### 5.1 기본 방식

작업 브랜치:

```text
feature/stage2-saratan-integration
```

현재 체크아웃에는 다음 사용자 변경이 존재한다.

```text
M  ProjectSettings/QualitySettings.asset
?? Assets/_Recovery/0 (3).unity
?? Assets/_Recovery/0 (3).unity.meta
```

이 변경을 stash, reset, checkout하지 않는다. 현재 작업공간을 그대로 둔 채 `origin/main` 기준의 별도 Git worktree를 만든다.

권장 작업 위치:

```text
/Users/switch/Development/Game/Unity/TitanSlayer/game_save_the_world-saratan
```

실행 원칙:

1. 원격 상태와 현재 기준 커밋을 확인한다.
2. 동일 이름 브랜치나 worktree가 이미 있는지 확인한다.
3. 없을 때만 `origin/main`에서 새 브랜치/worktree를 만든다.
4. 기존 체크아웃의 사용자 변경이 그대로 유지되는지 다시 확인한다.
5. 이후 모든 코드·씬·에셋 변경은 새 worktree에서만 수행한다.

### 5.2 커밋 경계

권장 커밋 분리는 다음과 같다.

1. `docs: add Saratan integration plan`
2. `assets: import isolated Saratan content`
3. `refactor: load stage-specific boss prefabs`
4. `content: add Stage 1 and Stage 2 definitions`
5. `content: integrate Saratan idle preview`
6. `test: add stage boss loading verification`

에셋 GUID 재생성, 런타임 구조 변경, 씬 변경을 한 커밋에 모두 섞지 않는다.

## 6. 임시 Unity 프로젝트 계획

### 6.1 생성

- Unity 버전은 본 프로젝트와 동일한 `6000.4.0f1`을 사용한다.
- 임시 프로젝트는 Git 저장소 밖에 생성한다.
- 프로젝트 이름은 `SaratanImportSandbox`로 한다.
- 필요한 렌더 파이프라인과 셰이더 의존성은 본 프로젝트의 `Packages/manifest.json`을 기준으로 맞춘다.
- 패키지 원본과 제공 TXT는 수정하지 않는다.

### 6.2 패키지 Import와 인벤토리

임시 프로젝트에는 `.unitypackage` 전체를 Import한다. Import 후 다음 정보를 Manifest로 기록한다.

- 원본 Path
- 원본 GUID
- 타입(FBX, Texture, Material, AnimationClip, Folder)
- 직접 Dependency
- 대응 Rig 계열
- 목표 Path
- 목표 이름
- 새 GUID
- 런타임 사용 여부

### 6.3 Rig A/B 판별

파일명 대신 실제 호환성으로 판별한다.

1. 두 FBX의 Transform 계층을 수집한다.
2. 두 AnimationClip 계열의 바인딩 경로를 수집한다.
3. 각 Clip 계열을 각 FBX에 샘플링한다.
4. Missing Transform binding과 비정상 자세를 확인한다.
5. 제공 TXT의 Saratan 동작과 일치하는 계열을 런타임 후보로 선정한다.

패키지의 Kaiju 명칭 계열은 결과와 관계없이 Stage 2 Saratan의 `RigA` 자산으로 보존한다. 기존 Stage 1 경로로 되돌리거나 Stage 1 파일을 참조하지 않는다.

### 6.4 신규 GUID 생성

단순 Move는 GUID를 유지하므로 사용하지 않는다. 임시 프로젝트 안에서 `AssetDatabase.CopyAsset` 기반으로 `SaratanExport` 세트를 복제해 신규 GUID를 만든다.

```text
Assets/SaratanExport/
└── Saratan/
    ├── Art/RigA/...
    ├── Art/RigB/...
    └── import-manifest.json
```

복제 후 다음 참조를 Stage 2 신규 GUID로 다시 연결한다.

- Material → Texture
- FBX Material Remap → Saratan Material
- Prefab → FBX, Material, Animator Controller
- Animator Controller → AnimationClip, Avatar Mask

복제된 Export 폴더가 원본 패키지 GUID를 참조하지 않는지 Dependency 검사를 수행한다.

### 6.5 본 프로젝트 반입과 임시 프로젝트 폐기

1. Export 세트의 모든 GUID를 본 프로젝트 전체 GUID와 대조한다.
2. 충돌이 0개일 때만 Export 폴더를 작업 worktree에 복사한다.
3. Unity에서 Import와 재직렬화를 완료한다.
4. Missing Reference와 셰이더 오류가 없는지 확인한다.
5. Saratan Prefab 및 Stage 2 런타임 검증이 완료된 후에만 임시 프로젝트를 삭제한다.

임시 프로젝트 삭제는 사용자 승인 범위에 포함되지만, 최종 반입물과 Manifest가 작업 브랜치에 커밋되기 전에는 삭제하지 않는다.

## 7. 런타임 데이터 구조

### 7.1 BossDefinition

신규 `BossDefinition` ScriptableObject의 최소 필드:

```text
bossId
displayName
bossPrefab
maxHealth
```

첫 범위에서는 공격 패턴과 애니메이션 세부 설정을 무리하게 데이터화하지 않는다. Stage 1과 Stage 2의 HP 로직은 동일한 `BossController`를 사용하고, 실제 수치만 각 BossDefinition이 제공한다.

### 7.2 StageDefinition

신규 `StageDefinition` ScriptableObject의 최소 필드:

```text
stageId
displayName
bossDefinition
environmentPrefab
environmentTheme
```

초기 ID:

| Stage | stageId | Boss |
| --- | --- | --- |
| Stage 1 | `stage_01_tokyo` | `boss_stage01_kaiju` |
| Stage 2 | `stage_02_seoul` | `boss_stage02_saratan` |

현재 UI가 생성하는 ID와 호환하되, 향후 UI 표시명이나 지역명이 바뀌어도 ID가 변하지 않도록 StageDefinition을 단일 기준으로 삼는다.

### 7.3 StageCatalog와 BattleStageLoader

`StageCatalog`는 사용 가능한 StageDefinition 목록과 기본 Stage 1을 가진다.

`BattleStageLoader`는 `BattleController.Start()`보다 먼저 다음을 수행한다.

1. `StageSelectionState.SelectedStageId` 조회
2. `StageCatalog`에서 StageDefinition 해석
3. ID가 없거나 잘못됐으면 Stage 1로 명시적 fallback
4. `BossSpawnPoint`에 해당 보스 Prefab 생성
5. `EnvironmentSpawnPoint`에 해당 환경 Prefab 생성 또는 현재 공용 환경 활성화
6. 생성된 `BossController`와 환경 참조 검증
7. 이후 `BattleController`가 기존 공통 런타임을 연결하도록 허용

실행 순서는 `BattleStageLoader` → `BattleBackgroundHost` → `BattleController`가 되도록 명시한다.

## 8. Stage 1 구조 변경 계획

### 8.1 Stage 1 보스 Prefab 추출

현재 `BattleArena`의 `BossPlaceholder` 인스턴스와 모든 Prefab override를 기준으로 `Kaiju.prefab`을 만든다.

반드시 보존할 항목:

- Boss Root 위치, 회전, 스케일
- `BossController` 설정
- `BossAttackController` 설정
- `BossBulletPatternController`와 전체 Pattern 수치
- `BossVisualRoot`와 현재 Kaiju Visual
- `KaijuBossAnimationDriver`
- Mouth/Tail Socket
- AimPoint
- Hurtbox와 Collider
- Lock-on 및 디버그 시스템이 런타임에 기대하는 계층명

Stage 1 Prefab 생성 전후의 직렬화 값을 비교하고 Play Mode 전투 동작이 동일한지 확인한다.

### 8.2 BattleArena 정리

기존 구체 보스 인스턴스를 제거하고 다음 구조로 바꾼다.

```text
BattleArenaRoot
├── BossSpawnPoint
├── EnvironmentSpawnPoint
└── Systems
    ├── BattleStageLoader
    └── BattleController
```

`BattleController`의 기존 Stage 1 보스 직렬화 참조는 제거한다. 스테이지 로더가 생성한 단 하나의 활성 `BossController`를 찾도록 한다. 두 보스가 동시에 존재하면 초기화 실패로 처리한다.

## 9. Stage 2 Saratan Prefab 계획

### 9.1 초기 Prefab 구조

```text
Saratan
├── AimPoint
├── BossHurtbox
└── BossVisualRoot
    └── SaratanVisual
        ├── Animator
        └── Saratan 모델 계층
```

Phase 6 초기 표시 단계의 컴포넌트:

- `BossController`
- `BossAttackController` — 초기 비활성화
- `BossBulletPatternController` — 초기 비활성화 또는 미부착
- `Animator`
- Stage 2 전용 Collider/Hurtbox

초기에는 Saratan에 `KaijuBossAnimationDriver`를 붙이지 않는다.

### 9.2 표시와 좌표 보정

- 모델 축 보정은 FBX 원본을 수정하지 않고 `BossVisualRoot` 아래 Local Transform으로 관리한다.
- 월드 위치는 `BossSpawnPoint`가 소유한다.
- 모델 크기는 현재 Stage 1 보스의 화면 점유율과 HitPoint 높이를 기준으로 초기 정규화한다.
- `AimPoint`와 Hurtbox는 모델 Renderer Bounds를 확인한 뒤 Saratan 전용 값으로 배치한다.
- 모든 SkinnedMeshRenderer의 Material이 Stage 2 Saratan 폴더를 참조하는지 확인한다.

### 9.3 초기 Animator

Phase 6의 첫 Controller는 다음 상태만 필수로 한다. Phase 9에서는 Saratan 전용 Firing/Breath 상태와 Trigger를 추가한다.

```text
Base Layer
└── BasicIdle (Default, Loop)
```

Loop 정책:

| Clip | Loop |
| --- | --- |
| BasicIdle | On |
| IdleFront / Left45 / Right45 | On |
| Firing 3종 | Off |
| Breath 3종 | Off |
| JumpTurnL | Off |
| Death | Off |

상체 Blend Tree, Breath 이벤트, JumpTurnL 회전 구간, Death 연동은 초기 표시 검증 후 다음 단계에서 구현한다.

## 10. 스테이지 환경 분리 계획

Stage 1과 Stage 2는 현재 같은 배경을 보이게 하지만 서로 다른 환경 진입 Prefab을 갖는다.

```text
Stage01Environment.prefab
└── SharedCurrentBattleEnvironment

Stage02Environment.prefab
└── SharedCurrentBattleEnvironment
```

두 래퍼는 같은 공용 배경 기반을 사용할 수 있으나 StageDefinition은 서로 다른 래퍼를 참조한다. 향후 Stage 2 배경이 변경되면 `Stage02Environment.prefab` 내부만 교체하거나 독립 오브젝트를 추가한다.

현재 `StageVisualRoot`, 배경 아군, 지상 장갑차, `BattleBackgroundHost`의 좌표 및 참조 관계는 유지한다. 환경 분리 중 전투 시스템과 배경 연출의 설정 순서가 바뀌지 않게 한다.

## 11. 구현 단계

### Phase 0 — 안전 기준선

- 현재 저장소 상태와 기준 커밋 기록
- 사용자 변경 파일 목록 기록
- 원격 갱신 여부 확인
- `feature/stage2-saratan-integration` worktree 생성
- 원래 체크아웃이 변경되지 않았는지 확인

완료 조건: 작업 worktree가 깨끗하고 원래 작업공간의 사용자 변경이 그대로다.

### Phase 1 — Saratan Import Sandbox

- Unity 6000.4.0f1 임시 프로젝트 생성
- 본 프로젝트와 렌더링 패키지 정합
- `.unitypackage` 전체 Import
- 전체 에셋/GUID/Dependency Manifest 생성
- Rig A/B 모델·Clip 호환성 검사

완료 조건: 두 입력 계열의 실제 리그 대응 관계가 기록되고 Missing binding이 식별된다.

### Phase 2 — Saratan 독립 Export 생성

- 모든 파일을 Saratan 명칭 체계로 복제
- `AssetDatabase.CopyAsset`으로 신규 GUID 발급
- Material, Texture, FBX remap 재연결
- 원본 GUID 및 Stage 1 GUID 참조 0개 확인
- Export Manifest 확정

완료 조건: 임시 프로젝트 안에서 Saratan Export 세트만으로 모델과 Clip을 재생할 수 있다.

### Phase 3 — 본 프로젝트 에셋 반입

- GUID 충돌 사전 검사
- 작업 worktree의 Stage 2 전용 폴더로 반입
- Unity Import/Compile 완료 대기
- Missing Reference, Missing Shader, Material 오류 확인
- 에셋 반입 커밋

완료 조건: Stage 1 파일 변경 없이 모든 Saratan 에셋이 신규 GUID로 존재한다.

### Phase 4 — 데이터 기반 스테이지 로딩

- `BossDefinition`, `StageDefinition`, `StageCatalog` 구현
- `BattleStageLoader` 구현
- 잘못된 ID의 Stage 1 fallback 구현
- EditMode 단위 테스트 작성

완료 조건: 코드 테스트에서 Stage 1/2 ID가 서로 다른 보스 Definition을 반환한다.

### Phase 5 — Stage 1 Prefab화와 회귀

- 현재 씬의 Stage 1 보스 전체 설정 캡처
- `Kaiju.prefab` 생성
- `Stage01Definition.asset` 생성
- BattleArena의 고정 보스를 SpawnPoint 방식으로 교체
- 기존 Kaiju 전투 회귀 검사

완료 조건: Stage 1 외형, HP, 공격, 락온, 피해, 사망이 변경 전과 동일하다.

### Phase 6 — Stage 2 Saratan 표시 통합

- `Saratan.controller` 최소 Idle 구성
- `Saratan.prefab` 생성
- Stage 2 전용 Material, AimPoint, Hurtbox 설정
- `SaratanBossDefinition.asset` 및 `Stage02Definition.asset` 생성
- Stage 2 공격 시스템 비활성화

완료 조건: `stage_02_seoul`에서 Saratan만 생성되고 Idle이 반복 재생된다.

### Phase 7 — 환경 진입점 분리

- Stage 1/2 환경 래퍼 Prefab 생성
- 두 StageDefinition에 각 래퍼 연결
- 초기에는 동일한 공용 환경 결과 확인
- 향후 Stage 2 교체를 위한 참조 경계 검증

완료 조건: 두 스테이지의 현재 배경은 같지만 Definition과 Environment Prefab은 별도다.

### Phase 8 — 통합 검증과 정리

- 전체 컴파일 및 Console 검사
- 집중 EditMode 테스트
- 전체 EditMode 테스트
- Stage 1 Play Mode 회귀
- Stage 2 Play Mode 표시 검증
- Retry, Quit, Stage 재진입 검증
- GUID/Dependency 최종 감사
- 문서 갱신 및 커밋 정리
- 검증 완료 후 임시 프로젝트 삭제

완료 조건: 아래 인수 조건을 모두 충족한다.

### Phase 9 — Saratan 임시 공격 패턴

최종 Saratan 공격 설계 전까지 Stage 1 Kaiju와 동일한 공격 순서와 수치를 Stage 2에 복제한다. 동일 동작은 기준값일 뿐이며 Stage 1 런타임 오브젝트나 보스 전용 애니메이션 자산을 참조하지 않는다.

- 공용 `BossAttackController`, `BossBulletPatternController`, 투사체 및 피해 처리 코드는 재사용한다.
- 패턴 목록과 수치는 `Saratan.prefab`의 별도 컴포넌트에 값으로 복제한다.
- Stage 2는 `SaratanDebrisFragmentCatalog.asset`을 별도 GUID로 소유한다.
- 발사점과 지면 파편 발사점은 Saratan 프리팹 내부에 새로 만든다.
- `Attack1`은 `Saratan_RigB_Attack_FiringFront`, `Attack2`는 `Saratan_RigB_Attack_BreathFront`를 사용한다.
- Kaiju Animator Controller, AnimationClip, `KaijuBossAnimationDriver`, Mouth/Tail Socket은 사용하지 않는다.
- 이후 Saratan 최종 패턴 개발은 Stage 2 프리팹과 전용 자산만 교체하며 Stage 1에는 영향을 주지 않는다.

완료 조건: 두 보스의 현재 패턴 값은 같지만, Stage 2 공격 관련 보스 전용 참조가 모두 Stage 2 폴더 또는 Stage 2 프리팹 내부를 가리킨다.

## 12. 테스트 계획

### 12.1 에셋 검사

- Stage 2 폴더의 GUID가 프로젝트 전체에서 중복되지 않는다.
- Stage 2 Dependency 목록에 Stage 1 보스 Art/Animation/Prefab 경로가 없다.
- 모든 Stage 2 Material이 Stage 2 Texture를 사용한다.
- 모든 Animator 상태가 Stage 2 AnimationClip을 사용한다.
- Missing Script, Missing Material, Missing Texture가 없다.

### 12.2 EditMode

- `stage_01_tokyo` → Stage 1 Definition
- `stage_02_seoul` → Stage 2 Definition
- 빈 ID → Stage 1 fallback
- 알 수 없는 ID → 경고 1회와 Stage 1 fallback
- 각 StageDefinition의 BossDefinition과 Environment Prefab이 유효함
- Stage 1/2 Boss Prefab에 BossController가 정확히 1개 존재함
- Stage 2 Prefab이 Stage 1 보스 전용 에셋을 참조하지 않음

### 12.3 Play Mode — Stage 1

- Kaiju가 기존 위치와 크기로 표시됨
- 기존 Idle/공격/Beam/Tail/회전/Death 정상
- 보스 HP, 플레이어 피해, HUD 정상
- Lock-on 및 약점 시스템 정상
- 배경 아군과 지상 장갑차가 정상 연결됨
- Retry와 Quit 정상

### 12.4 Play Mode — Stage 2

- Saratan이 하나만 생성됨
- Stage 1 Kaiju Visual이 존재하지 않음
- Saratan Material과 Texture가 정상 표시됨
- `BasicIdle`이 끊김 없이 반복됨
- AimPoint가 모델 중심/상체의 적절한 위치에 있음
- Hurtbox가 모델 크기와 대체로 일치함
- 플레이어 궤도와 카메라가 Saratan 중심을 사용함
- 임시 공격 컨트롤러와 패턴 컨트롤러가 활성화됨
- 임시 패턴을 강제 실행했을 때 Coroutine과 공격 애니메이션 Trigger가 정상 동작함
- Kaiju 전용 Animation Driver 없이 Saratan 전용 Firing/Breath Clip을 사용함
- 기존 공용 배경이 Stage 1과 동일하게 표시됨
- Retry 시 Saratan이 중복 생성되지 않음

### 12.5 시각 검증

- Game View 정면 캡처
- 좌우 및 상단 Scene View 캡처
- Stage 1/2 동일 카메라에서 화면 점유율 비교
- SkinnedMesh 변형, 축 뒤집힘, 지면 관통 확인
- Emission과 Toon Shader 표시 확인

## 13. 인수 조건

다음 조건을 모두 만족해야 첫 통합을 완료로 판정한다.

1. 구현 작업이 `feature/stage2-saratan-integration` 브랜치/worktree에만 존재한다.
2. 원래 작업공간의 사용자 변경이 손실되거나 수정되지 않았다.
3. 패키지의 모든 제공물은 Stage 2 Saratan 소유로 기록된다.
4. Stage 2 보스 콘텐츠는 Stage 1 보스 콘텐츠를 참조하지 않는다.
5. Stage 1과 Stage 2 Prefab 및 Definition은 서로 다른 GUID를 가진다.
6. 공통 HP, 전투, HUD 코드는 복제하지 않고 공유한다.
7. Stage 1 선택 시 기존 Kaiju 전투가 회귀 없이 동작한다.
8. Stage 2 선택 시 Saratan이 정상 표시되고 BasicIdle이 재생된다.
9. 두 스테이지는 현재 같은 배경 결과를 사용하지만 환경 진입 Prefab은 분리되어 있다.
10. Console에 컴파일 오류, Missing Reference, 반복 Animator 오류가 없다.
11. 집중 테스트와 전체 EditMode 테스트 결과가 기록된다.
12. 최종 에셋과 Manifest가 커밋된 뒤 임시 Unity 프로젝트가 삭제된다.
13. Stage 2 임시 공격 설정은 Stage 1 패턴과 값이 같아도 독립 컴포넌트와 독립 보스 전용 자산으로 존재한다.

## 14. 위험과 대응

| 위험 | 영향 | 대응 |
| --- | --- | --- |
| 패키지 GUID가 Stage 1과 충돌 | Stage 1 에셋 덮어쓰기 또는 참조 변경 | 본 프로젝트 직접 Import 금지, Sandbox CopyAsset 신규 GUID, 반입 전 전수 비교 |
| Material이 원본 Texture GUID를 유지 | Stage 1 Texture 공유 또는 Missing | Stage 2 Material 재생성/재바인딩 후 Dependency 검사 |
| 두 Animation 계열의 리그 대응을 이름으로 오판 | 애니메이션 무반응 또는 뒤틀림 | Transform/Curve binding 및 실제 SampleAnimation 검증 |
| Stage 1 Prefab 추출 중 override 유실 | 기존 공격/충돌/연출 회귀 | 추출 전후 직렬화 스냅샷과 Play Mode 회귀 |
| 로더 실행 순서가 늦음 | BattleController가 보스를 찾지 못함 | 실행 순서 명시, 보스 생성 완료 후 BattleController 초기화 |
| Saratan에 Kaiju Driver 연결 | 뼈 경로 오류와 잘못된 공격 상태 | 미부착 유지, 공통 Trigger fallback과 Saratan 전용 Clip 사용 |
| 임시 동일 패턴을 Stage 1 자산 참조로 연결 | 향후 Saratan 변경이 Kaiju에 영향 | 수치만 복사하고 Stage 2 전용 컴포넌트·Controller·Catalog·Socket 사용을 테스트로 고정 |
| 환경 분리 중 StageVisualRoot 참조 유실 | 배경 회전·아군 경로 오류 | 환경 래퍼 생성 전후 BackgroundHost/StageVisualRoot 검증 |
| 임시 프로젝트 조기 삭제 | GUID/의존성 추적 자료 손실 | Export, Manifest, 본 프로젝트 검증 및 커밋 후 삭제 |

## 15. 이번 범위에서 제외

- Saratan의 최종 공격 패턴 설계(현재는 Kaiju와 동일한 임시 복제본 사용)
- Breath 공격 판정과 VFX
- 상체 3방향 Blend Tree 완성
- 플레이어 수평 추적과 공격 방향 연동
- `JumpTurnL`의 5프레임 제외 회전 보간
- Saratan Death와 전투 종료 연출 완성
- Stage 2 전용 배경 제작
- Stage 2 최종 밸런스와 난이도별 수치
- Stage 1 기존 원본 에셋의 전면 폴더 이동

위 항목은 Saratan이 Stage 2에서 독립 에셋으로 정상 표시되는 것을 확인한 뒤 후속 개발계획으로 분리한다.

## 16. 구현 및 검증 결과

2026-09-12 기준 본 계획의 첫 통합 범위를 `feature/stage2-saratan-integration` 전용 worktree에서 완료했다.

- 패키지의 `Kaiju` 명칭 계열은 `Saratan_RigA_*`, `Saratan` 명칭 계열은 `Saratan_RigB_*`로 복제했다.
- 신규 에셋 38개 모두 새 GUID를 사용하며 기존 본 프로젝트 GUID와 교집합이 없다.
- Rig A ↔ Rig A, Rig B ↔ Rig B의 AnimationCurve Transform binding 누락은 각각 0개다.
- 교차 리그 적용 시 Rig A → Rig B는 41개, Rig B → Rig A는 36개 Transform 경로가 불일치해 런타임에는 Rig B를 선택했다.
- 제공 Toon Shader는 현재 Unity/URP 조합에서 Metal 컴파일 오류가 발생해, Stage 2 전용 Material을 지원되는 `Universal Render Pipeline/Lit`로 변환하고 전용 Base/Emission Texture만 다시 연결했다.
- `BattleArena`의 기존 Kaiju 설정을 `Kaiju.prefab`으로 추출하고 고정 보스 인스턴스를 제거했다.
- `StageCatalog`와 `BattleStageLoader`가 `stage_01_tokyo` 및 `stage_02_seoul`을 각각 독립 Boss/Environment Prefab으로 해석한다.
- Stage 2 Saratan의 `BossAttackController`와 `BossBulletPatternController`는 Stage 1의 현재 수치와 순서를 독립 직렬화 값으로 복제해 활성화했다.
- `Saratan.controller`는 Stage 2 Rig B의 `BasicIdle`, `Attack_FiringFront`, `Attack_BreathFront`만 참조하며 `Attack1`/`Attack2` Trigger로 전환한다.
- 공격 발사점은 Saratan 턱 뼈의 `SaratanMouthFirePoint`와 Stage 2 Prefab 내부의 양쪽 발 파편 발사점으로 별도 구성했다.
- 파편 설정은 새 GUID의 `SaratanDebrisFragmentCatalog.asset`으로 분리했으며, `KaijuBossAnimationDriver` 및 Stage 1 보스 전용 에셋 의존성은 없다.
- 공격 실행 코드, 투사체 및 공용 VFX 기반은 몬스터에 종속되지 않는 공용 런타임으로 유지한다.
- Stage 1/2 환경 Prefab은 서로 다른 GUID를 사용하며 현재는 기존 공용 배경 결과를 유지한다.

검증 결과:

```text
Saratan 집중 EditMode: 18/18 Passed
전체 EditMode:          134/134 Passed
Stage 로딩 PlayMode:    2/2 Passed
Unity Console Error:    0
프로젝트 중복 GUID:     0
Stage 2/기존 프로젝트 GUID 교집합: 0
Stage 2 금지 Dependency: 0
```

프리뷰 렌더에서 모델 형상, 전용 Texture, Emission 및 지원 Shader 표시를 확인했다. 임시 Import 프로젝트는 최종 브랜치 커밋 이후 폐기한다.
