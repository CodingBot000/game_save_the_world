---
title: Stage 1 Kaiju 전역 에셋 이전 작업계획서
document_id: TD-STAGE01-KAIJU-CONTENT-RELOCATION
document_type: development-plan
status: 구현 완료 / 수동 PlayMode 스모크 대기
version: 1.1.0
created: 2026-09-13
last_reviewed: 2026-09-13
unity_version: 6000.4.0f1
target_branch: feature/stage1-kaiju-content-relocation
implementation_baseline: origin/main e3650a9
related_documents:
  - saratan-stage2-boss-integration-plan.md
  - kaiju-animation-integration-plan.md
  - kaiju-sweep-beam-alignment-development-plan.md
---

# Stage 1 Kaiju 전역 에셋 이전 작업계획서

## 1. 목적과 결론

전역 레거시 경로에 흩어진 Stage 1 Kaiju 전용 모델, 텍스처, 머티리얼, 애니메이션, Animator 구성과 전용 도구를 `Assets/_Project/Content/Bosses/Kaiju` 아래로 이전한다.

이 작업은 새 에셋을 복사하거나 GUID를 재발급하는 작업이 아니다. 기존 `.meta`를 유지한 채 에셋을 이동하여 프리팹과 ScriptableObject의 직렬화 참조를 보존하고, 문자열로 하드코딩된 경로만 새 위치로 변경한다. Stage 2 Saratan은 Stage 1 Kaiju 에셋을 참조하지 않는 현재 원칙을 유지한다.

작업 완료 후 다음 전역 경로에는 Kaiju 소유 에셋이 남아 있지 않아야 한다.

```text
Assets/Invader/
Assets/Animation/Invader/
Assets/Materials/Invader/
Assets/Textures/Invader/
Assets/Materials/Debug/
Assets/Editor/
```

마지막 두 경로는 폴더 전체를 제거한다는 뜻이 아니라 Kaiju 전용 파일만 해당 보스 소유 폴더로 옮긴다는 뜻이다. 공용 에셋과 공용 Editor 도구는 기존 위치를 유지한다.

## 2. 확정 원칙

### 2.1 소유권

- Kaiju의 모델, 텍스처, 머티리얼, 애니메이션, Controller, Mask, Prefab, Definition은 `Bosses/Kaiju`가 소유한다.
- Saratan의 모든 에셋과 GUID는 `Bosses/Saratan`이 독립적으로 소유한다.
- 보스 폴더와 Prefab 이름에는 스테이지 번호를 넣지 않으며, 실제 등장 스테이지는 `StageDefinition`에서 조합한다.
- HP, 피해 처리, 전투 흐름, HUD, 스테이지 로딩처럼 보스에 종속되지 않는 코드는 공용으로 유지한다.
- Kaiju 전용 Animation Driver, State Relay, 디버그 컴포넌트와 Editor 도구는 `Bosses/Kaiju` 소유 경로에 둔다.
- 테스트 코드는 Unity 테스트 관례에 맞춰 `_Project/Tests` 아래에 유지하되 보스 이름별 하위 폴더로 정리한다.
- Saratan Import Manifest의 대상 경로도 `Bosses/Saratan` 기준으로 유지한다.

### 2.2 GUID와 파일명

- 모든 이동 대상은 원본 `.meta`와 함께 이동한다.
- `AssetDatabase.MoveAsset` 또는 `.meta`를 포함한 Git 이동을 사용한다.
- 모델, Clip, Material의 GUID를 재발급하지 않는다.
- 이번 작업에서는 파일 이름을 바꾸지 않는다. 경로 이동과 이름 변경을 동시에 수행하지 않아 문제 원인을 분리한다.
- 이동 전후 GUID 목록을 저장하고 일치 여부를 검증한다.
- GUID가 달라지거나 Missing Reference가 발생하면 해당 단계에서 중단하고 복구한다.

### 2.3 삭제 원칙

- 참조가 없다는 이유만으로 즉시 삭제하지 않는다.
- 활성 런타임 에셋, Editor Preview 에셋, 테스트 Fixture, 완전 미사용 에셋으로 먼저 분류한다.
- 미사용으로 확정된 파일만 별도 커밋에서 삭제한다.
- 전역 폴더는 Kaiju 파일이 모두 빠지고 다른 에셋도 없을 때만 폴더와 `.meta`를 제거한다.
- 삭제한 파일은 커밋 전 Git 상태와 GUID 목록으로 복구 가능하게 유지한다.

## 3. 현재 상태 확인 결과

### 3.1 현재 Stage 1 보스 진입점

```text
Assets/_Project/Content/Bosses/Kaiju/
├── Data/
│   └── KaijuBossDefinition.asset
└── Prefabs/
    └── Kaiju.prefab
```

Stage 1 스테이지 정의는 다음 경로에 있다.

```text
Assets/_Project/Content/Stages/Stage01/
├── Environment/
│   └── Stage01Environment.prefab
└── Stage01Definition.asset
```

### 3.2 Unity에서 확인한 활성 전역 의존성

Unity `AssetDatabase.GetDependencies`로 다음 세 에셋을 확인했다.

- `Kaiju.prefab`
- `KaijuBossDefinition.asset`
- `Stage01Definition.asset`

세 진입점은 동일한 전역 Kaiju 의존성 집합을 가진다.

| 분류 | 현재 경로 | 수량 | 상태 |
|---|---|---:|---|
| 모델 | `Assets/Invader/Kaiju_001.fbx` | 1 | 활성 런타임 의존성 |
| 애니메이션 Clip | `Assets/Animation/Invader/Clips/Kaiju_*.anim` | 12 | 활성 런타임 의존성 |
| Animator Controller | `Assets/Animation/Invader/KaijuCombat.controller` | 1 | 활성 런타임 의존성 |
| Avatar Mask | `Assets/Animation/Invader/KaijuUpperBody.mask` | 1 | 활성 런타임 의존성 |
| Material | `Assets/Materials/Invader/Kaiju_*.mat` | 3 | 활성 런타임 의존성 |
| Texture | `Assets/Textures/Invader/Kaiju_*.png` | 3 | 활성 런타임 의존성 |

활성 런타임 의존성은 총 21개다. 이 파일들은 삭제 후보가 아니며 GUID를 유지해 반드시 이동한다.

### 3.3 활성 의존성 밖의 Kaiju 파일

다음 파일은 현재 Stage 1 Prefab 의존성 폐쇄에는 포함되지 않지만 Editor 도구, 테스트 씬 또는 과거 제작 흐름에서 사용할 수 있다.

```text
Assets/Invader/Test_Kaiju_001.fbx

Assets/Animation/Invader/
├── KaijuBoss.controller
├── Kaiju_Turn_Idle_001.anim
├── Kaiju_Turn_Attack_Idle_001.anim
├── Kaiju_Turn_Attack_001.anim
└── Kaiju_Turn_Attack_002.anim

Assets/Materials/Debug/
├── KaijuAnimationTestGround.mat
├── Kaiju_001_AnimationPreview.mat
├── Kaiju_Eye_AnimationPreview.mat
└── Kaiju_HeadSail_AnimationPreview.mat
```

이 파일들은 구현 시작 시 전체 참조를 다시 검사한다. 사용 중이면 Stage 1의 `Editor/Preview` 또는 테스트 Fixture로 이동하고, 사용하지 않으면 별도 삭제 커밋으로 정리한다.

### 3.4 경로가 하드코딩된 코드

| 파일 | 현재 문제 | 계획 |
|---|---|---|
| `Assets/Editor/ApplyKaijuBossVisual.cs` | 모델, 구형 Clip, Controller, Material, Texture 경로가 전역 경로로 고정됨 | 이동 후 Stage 1 경로로 변경하거나 도구가 폐기 대상이면 삭제 검토 |
| `Assets/Editor/KaijuAnimationTestBuilder.cs` | 모델, Clip 폴더, Texture 경로가 고정됨 | 새 Art 경로로 변경 |
| `Assets/Editor/KaijuCombatAnimationBuilder.cs` | 모델, Clip, Controller, Mask, Material, Texture 경로가 고정됨 | 새 Stage 1 경로로 변경 |
| `Assets/Editor/KaijuCombatAnimationVerification.cs` | Clip 폴더가 고정됨 | 새 Art 경로로 변경 |
| `Assets/_Project/Tests/EditMode/KaijuSweepBeamAlignmentTests.cs` | 모델, Controller, Clip 경로가 고정됨 | 새 경로로 변경 |
| `Assets/Editor/BuildSaratanStageIntegration.cs` | 전역 Stage 1 경로를 Saratan 금지 의존성으로 검사함 | 새 `Kaiju` 경로를 금지 대상으로 추가하고 기존 경로도 회귀 방지용으로 유지 |
| `Assets/_Project/Tests/EditMode/StageContentIntegrationTests.cs` | 전역 Stage 1 경로만 Saratan 금지 의존성으로 검사함 | 새 Stage 1 루트 검사 추가 |

`KaijuCombatAnimationBuilder`는 삭제된 `Kaiju_*_Combat.mat`을 이전 전역 경로에 다시 생성할 수 있다. 이 동작은 반드시 제거한다. Builder는 새 경로의 `Kaiju_001.mat`, `Kaiju_Eye.mat`, `Kaiju_HeadSail.mat`만 사용해야 한다.

## 4. 목표 폴더 구조

```text
Assets/_Project/Content/Bosses/Kaiju/
├── Art/
│   └── RigA/
│       ├── Models/
│       │   └── Kaiju_001.fbx
│       ├── Textures/
│       │   ├── Kaiju_001.png
│       │   ├── Kaiju_Eye.png
│       │   └── Kaiju_HeadSail.png
│       ├── Materials/
│       │   ├── Kaiju_001.mat
│       │   ├── Kaiju_Eye.mat
│       │   └── Kaiju_HeadSail.mat
│       └── Animations/
│           └── Kaiju_*.anim
├── Data/
│   └── KaijuBossDefinition.asset
├── Runtime/
│   ├── Animation/
│   │   ├── Controllers/
│   │   │   └── KaijuCombat.controller
│   │   └── Masks/
│   │       └── KaijuUpperBody.mask
│   ├── Prefabs/
│   │   └── Kaiju.prefab
│   ├── Scripts/
│   │   ├── KaijuBossAnimationDriver.cs
│   │   └── KaijuCombatStateRelay.cs
│   └── Debug/
│       └── KaijuAnimationTester.cs
├── Editor/
│   ├── Tools/
│   │   ├── ApplyKaijuBossVisual.cs
│   │   ├── KaijuAnimationTestBuilder.cs
│   │   ├── KaijuCombatAnimationBuilder.cs
│   │   └── KaijuCombatAnimationVerification.cs
│   └── Preview/
│       ├── Animation/
│       ├── Materials/
│       └── Models/
└── Tests/
    └── Fixtures/
```

Stage 1은 현재 한 개의 Rig만 사용하지만 Stage 2 구조와 규칙을 맞추기 위해 `RigA`를 사용한다. 향후 모델 교체나 Rig 추가가 발생해도 기존 경로 의미가 흔들리지 않게 한다.

Unity 테스트 스크립트 자체는 다음 위치에 둔다.

```text
Assets/_Project/Tests/EditMode/Bosses/Kaiju/
└── KaijuSweepBeamAlignmentTests.cs
```

## 5. 이동 매핑

### 5.1 활성 런타임 에셋

| 현재 경로 | 목표 경로 |
|---|---|
| `Assets/Invader/Kaiju_001.fbx` | `Assets/_Project/Content/Bosses/Kaiju/Art/RigA/Models/Kaiju_001.fbx` |
| `Assets/Textures/Invader/Kaiju_*.png` | `Assets/_Project/Content/Bosses/Kaiju/Art/RigA/Textures/` |
| `Assets/Materials/Invader/Kaiju_*.mat` | `Assets/_Project/Content/Bosses/Kaiju/Art/RigA/Materials/` |
| `Assets/Animation/Invader/Clips/Kaiju_*.anim` | `Assets/_Project/Content/Bosses/Kaiju/Art/RigA/Animations/` |
| `Assets/Animation/Invader/KaijuCombat.controller` | `Assets/_Project/Content/Bosses/Kaiju/Runtime/Animation/Controllers/KaijuCombat.controller` |
| `Assets/Animation/Invader/KaijuUpperBody.mask` | `Assets/_Project/Content/Bosses/Kaiju/Runtime/Animation/Masks/KaijuUpperBody.mask` |
| `Assets/_Project/Content/Bosses/Kaiju/Prefabs/Kaiju.prefab` | `Assets/_Project/Content/Bosses/Kaiju/Runtime/Prefabs/Kaiju.prefab` |

### 5.2 Kaiju 전용 코드와 도구

| 현재 경로 | 목표 경로 |
|---|---|
| `Assets/_Project/Scripts/Gameplay/KaijuBossAnimationDriver.cs` | `Assets/_Project/Content/Bosses/Kaiju/Runtime/Scripts/KaijuBossAnimationDriver.cs` |
| `Assets/_Project/Scripts/Gameplay/KaijuCombatStateRelay.cs` | `Assets/_Project/Content/Bosses/Kaiju/Runtime/Scripts/KaijuCombatStateRelay.cs` |
| `Assets/_Project/Scripts/Debug/KaijuAnimationTester.cs` | `Assets/_Project/Content/Bosses/Kaiju/Runtime/Debug/KaijuAnimationTester.cs` |
| `Assets/Editor/ApplyKaijuBossVisual.cs` | `Assets/_Project/Content/Bosses/Kaiju/Editor/Tools/ApplyKaijuBossVisual.cs` |
| `Assets/Editor/KaijuAnimationTestBuilder.cs` | `Assets/_Project/Content/Bosses/Kaiju/Editor/Tools/KaijuAnimationTestBuilder.cs` |
| `Assets/Editor/KaijuCombatAnimationBuilder.cs` | `Assets/_Project/Content/Bosses/Kaiju/Editor/Tools/KaijuCombatAnimationBuilder.cs` |
| `Assets/Editor/KaijuCombatAnimationVerification.cs` | `Assets/_Project/Content/Bosses/Kaiju/Editor/Tools/KaijuCombatAnimationVerification.cs` |
| `Assets/_Project/Tests/EditMode/KaijuSweepBeamAlignmentTests.cs` | `Assets/_Project/Tests/EditMode/Bosses/Kaiju/KaijuSweepBeamAlignmentTests.cs` |

클래스명, 네임스페이스와 Assembly 이름은 이번 작업에서 변경하지 않는다. 경로 이동만으로 런타임 타입 참조가 달라지지 않게 한다.

### 5.3 Preview와 테스트 후보

- `Test_Kaiju_001.fbx`가 사용 중이면 `Tests/Fixtures/Models`로 이동한다.
- 구형 `Kaiju_Turn_*.anim`과 `KaijuBoss.controller`가 Editor 도구에 필요하면 `Editor/Preview/Animation`으로 이동한다.
- `Kaiju*_AnimationPreview.mat`과 테스트 Ground Material이 필요하면 `Editor/Preview/Materials`로 이동한다.
- 사용처가 없으면 삭제 후보 목록과 근거를 먼저 제시하고 별도 커밋에서 삭제한다.

## 6. 구현 단계

### Phase 0 작업 격리와 기준선 기록

1. `origin/main`을 갱신하고 기준 커밋을 기록한다.
2. `feature/stage1-kaiju-content-relocation` 브랜치와 별도 worktree를 만든다.
3. 현재 메인 체크아웃의 사용자 변경을 stash, reset 또는 덮어쓰지 않는다.
4. 이동 대상의 Path, GUID, Type, 직접 Dependency를 Manifest로 저장한다.
5. Unity Editor가 새 worktree를 열고 MCP가 올바른 인스턴스를 가리키는지 확인한다.

현재 메인 체크아웃에는 이 계획서와 무관한 다음 변경이 있으므로 구현 브랜치에 섞지 않는다.

```text
M  ProjectSettings/QualitySettings.asset
?? Assets/InitTestSceneea7518fd-c2e1-48d3-b388-6c0d0909dc25.unity
?? Assets/InitTestSceneea7518fd-c2e1-48d3-b388-6c0d0909dc25.unity.meta
?? docs/saratan-stage2-boss-integration-plan.pre-pull-local.md
```

### Phase 1 참조와 사용 여부 확정

1. Stage 1 Prefab, BossDefinition, StageDefinition의 전체 Dependency를 수집한다.
2. 현재 씬, Prefab, ScriptableObject, Animator Controller의 GUID 참조를 검색한다.
3. C#의 `AssetDatabase.LoadAssetAtPath` 및 문자열 경로를 검색한다.
4. 활성 에셋과 Preview, Fixture, 미사용 후보를 확정한다.
5. 이동 전 EditMode 테스트와 Stage 1 진입을 실행해 기준 결과를 기록한다.

### Phase 2 폴더 생성과 활성 에셋 이동

1. 목표 폴더를 Unity `AssetDatabase`로 생성한다.
2. FBX와 `.meta`를 `Art/RigA/Models`로 이동한다.
3. Texture와 Material을 각각 전용 폴더로 이동한다.
4. 활성 AnimationClip을 `Art/RigA/Animations`로 이동한다.
5. Controller와 Mask를 `Runtime/Animation` 아래로 이동한다.
6. Stage 1 Prefab을 `Runtime/Prefabs`로 이동한다.
7. 각 묶음 이동 후 Unity Import와 GUID 일치 검사를 수행한다.

### Phase 3 Kaiju 전용 코드와 제작 도구 이동

1. `KaijuBossAnimationDriver`와 `KaijuCombatStateRelay`를 `Runtime/Scripts`로 이동한다.
2. `KaijuAnimationTester`를 `Runtime/Debug`로 이동한다.
3. Kaiju 전용 Editor 도구를 `Kaiju/Editor/Tools`로 이동한다.
4. 테스트 스크립트를 Stage 1 전용 테스트 하위 폴더로 이동한다.
5. `.meta`를 유지하고 Assembly와 직렬화 타입명이 유지되는지 확인한다.

### Phase 4 하드코딩 경로와 Builder 수정

1. 모든 Kaiju 경로 상수를 새 루트로 변경한다.
2. `KaijuCombatAnimationBuilder`가 `_Combat.mat`을 생성하는 코드를 제거한다.
3. Builder가 새 기본 Material 세 개를 로드하도록 변경한다.
4. Saratan 독립성 검사에 새 Stage 1 루트를 추가한다.
5. 기존 전역 경로 검사는 회귀 방지용으로 유지한다.
6. Stage 1 Prefab 재생성 도구를 실행해도 전역 폴더나 `*_Combat.mat`이 생기지 않게 검증한다.

### Phase 5 Preview와 미사용 에셋 정리

1. Preview와 테스트에 필요한 파일을 Stage 1 소유 경로로 이동한다.
2. 참조가 없는 구형 Controller, Clip, Test FBX, Preview Material은 삭제 후보로 보고한다.
3. 삭제가 확정된 항목만 별도 커밋에서 제거한다.
4. 비어 있는 전역 `Invader` 관련 폴더와 `.meta`를 정리한다.
5. 공용 파일이 남아 있는 상위 폴더는 삭제하지 않는다.

### Phase 6 검증과 문서 갱신

1. Unity 컴파일이 끝날 때까지 기다린다.
2. Console의 Error와 Missing Reference를 확인한다.
3. Stage 1 Kaiju의 모델 크기, Material, Idle, 공격, 회전, 사망 애니메이션을 확인한다.
4. Stage 2 Saratan 선택과 표시, 독립 공격 구성이 그대로 유지되는지 확인한다.
5. EditMode 테스트를 실행한다.
6. 현재 상태 문서에 최종 경로를 반영한다.
7. 과거 계획서와 Import Manifest는 이력 자료로 유지하고 런타임 검증에서 제외한다.

## 7. 검증 항목

### 7.1 정적 검증

- 이동 전후 GUID가 모두 동일하다.
- Stage 1 Prefab의 Material, Mesh, Animator Controller, Avatar Mask 참조가 유효하다.
- Stage 1 Definition이 새 Prefab 경로를 정상 참조한다.
- Stage 2 Prefab과 Definition은 `Kaiju` 하위 에셋을 참조하지 않는다.
- `Assets/Invader`, `Assets/Animation/Invader`, `Assets/Materials/Invader`, `Assets/Textures/Invader`에 Kaiju 에셋이 남지 않는다.
- 코드에서 이전 경로를 런타임 또는 Editor 로드 경로로 사용하지 않는다.
- `Kaiju_001_Combat.mat`, `Kaiju_Eye_Combat.mat`, `Kaiju_HeadSail_Combat.mat`이 다시 생성되지 않는다.
- `git diff --check`가 통과한다.

### 7.2 Unity 검증

- Unity Console에 컴파일 오류가 없다.
- Missing Script, Missing Prefab, Missing Material, Missing Motion이 없다.
- Stage 1 Kaiju가 분홍색으로 렌더링되지 않는다.
- 세 기본 Material이 `Universal Render Pipeline/Lit`을 사용한다.
- Stage 1 Kaiju의 크기, 소켓, Hurtbox, AimPoint가 이동 전과 동일하다.
- Animator의 모든 State Motion과 StateMachineBehaviour가 유지된다.
- BattleArena에서 Stage 1과 Stage 2 전환이 모두 정상이다.

### 7.3 테스트

- `StageContentIntegrationTests`
- `KaijuSweepBeamAlignmentTests`
- 보스 로딩 관련 EditMode 테스트 전체
- Stage 1 Kaiju 수동 PlayMode 스모크 테스트
- Stage 2 Saratan 수동 PlayMode 회귀 테스트

## 8. 커밋 계획

권장 커밋 경계는 다음과 같다.

1. `docs: add Stage 1 Kaiju relocation plan`
2. `refactor: relocate Stage 1 Kaiju runtime assets`
3. `refactor: colocate Kaiju-specific scripts and editor tools`
4. `fix: update Kaiju asset paths and prevent Combat material regeneration`
5. `test: update Stage 1 asset migration coverage`
6. `chore: remove obsolete Kaiju preview assets and empty legacy folders`

각 커밋에서 Unity 프로젝트가 Import 가능하고 C# 컴파일이 되는 상태를 유지한다. 대규모 에셋 이동과 삭제를 한 커밋에 섞지 않는다.

## 9. 롤백 전략

- 구현은 별도 브랜치와 worktree에서 수행한다.
- 이동 전 Path와 GUID Manifest를 보관한다.
- 각 Phase를 별도 커밋으로 만들어 필요한 단계만 `git revert`할 수 있게 한다.
- GUID 불일치나 Missing Reference가 발생하면 추가 수정 전에 해당 이동 커밋을 되돌린다.
- 사용자 작업이 있는 메인 체크아웃에는 reset, checkout, stash를 수행하지 않는다.

## 10. 완료 조건

다음 조건을 모두 만족하면 작업을 완료로 판단한다.

- Stage 1 Kaiju의 활성 에셋과 전용 도구가 `Kaiju` 소유 경로에 있다.
- 전역 레거시 위치에 Kaiju 소유 파일이 남아 있지 않다.
- 모든 이동 대상의 GUID가 보존된다.
- Stage 1 Prefab, BossDefinition, StageDefinition의 참조가 유효하다.
- Stage 1 Kaiju가 기존 크기와 외형으로 표시되고 공격 패턴과 애니메이션이 동작한다.
- Stage 2 Saratan이 Stage 1 에셋을 참조하지 않고 기존 동작을 유지한다.
- Builder 실행 후 삭제된 Combat Material이나 전역 Kaiju 폴더가 재생성되지 않는다.
- 관련 EditMode 테스트와 수동 스모크 테스트가 통과한다.
- 구현 커밋에 기존 사용자 변경이 포함되지 않는다.

## 11. 구현 결과

2026-09-13에 `feature/stage1-kaiju-content-relocation` 브랜치의 별도 worktree에서 구현했다. 메인 체크아웃의 사용자 변경은 수정하거나 포함하지 않았다.

### 11.1 완료한 작업

- 활성 런타임 에셋 21개를 `Kaiju/Art/RigA`와 `Runtime/Animation`으로 이동했다.
- Stage 1 Prefab을 `Runtime/Prefabs`로 이동했다.
- Kaiju 전용 런타임 스크립트, 디버그 컴포넌트, Editor 도구를 Stage 1 소유 경로로 이동했다.
- 구형 Controller와 Clip, Preview Material, Preview Scene은 사용 이력이 있어 삭제하지 않고 `Editor/Preview`로 이동했다.
- `Test_Kaiju_001.fbx`는 삭제하지 않고 `Tests/Fixtures/Models`로 이동했다.
- 모든 하드코딩된 활성 로드 경로를 새 위치로 변경했다. 기존 전역 경로 문자열은 Saratan 독립성 회귀 검사에만 남겼다.
- `KaijuCombatAnimationBuilder`가 삭제된 `*_Combat.mat`을 다시 생성하던 동작을 제거하고 Stage 1 기본 Material만 사용하게 했다.
- 세 기본 Material에 남아 있던 사용하지 않는 Viper 텍스처 참조를 제거했다.
- Stage 1 전용 경로, URP/Lit Material, 자체 Texture, 레거시 경로 부재와 Combat Material 부재를 검사하는 EditMode 테스트를 추가했다.

### 11.2 자동 검증 결과

| 검증 | 결과 |
|---|---|
| Unity 6000.4.0f1 전체 재임포트와 C# 컴파일 | 통과 |
| 이동된 `.meta` GUID 비교 | 47개 일치, 불일치 0개 |
| Stage 1 Prefab의 새 모델·Animator·Clip 경로 | 통과 |
| Kaiju Asset 검증(12 Clip, Bone Path, Loop 정책, 단일 FBX) | 통과 |
| Stage 2의 Stage 1 에셋 비참조 검사 | 통과 |
| 기본 Material URP/Lit 및 자체 Texture 검사 | 통과 |
| 레거시 Kaiju 폴더와 `*_Combat.mat` 잔존 검사 | 통과 |
| 전체 EditMode 테스트 | 139개 통과, 실패 0개 |
| `git diff --check` | 통과 |

### 11.3 남은 확인

GUI에서 기능 브랜치 worktree를 열어 Stage 1 Kaiju의 실제 렌더링, 크기, 공격·회전·사망 애니메이션과 Stage 2 Saratan 전환을 수동 PlayMode로 확인한다. 자동 테스트에서는 Prefab 참조, 지원되는 URP/Lit Shader, Animator와 공격 관련 EditMode 동작이 모두 통과했다.
