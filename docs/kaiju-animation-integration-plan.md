# Kaiju 애니메이션 적용 및 animTestScene 개발계획서

작성일: 2026-08-26  
대상: Unity 6000.4.0f1 / game_save_the_world

## 1. 목표와 범위

기존 프로젝트의 `Assets/Invader/Kaiju_001.fbx` 모델을 유지하고,
외부 `TitanSlayerNewAssets/FBX`의 동작별 FBX 12개에서 독립적인 `.anim` 파일을 추출한다.
각 애니메이션마다 모델을 배치하지 않고, 단일 모델에 선택한 클립을 재생한다.

완료된 A~C 범위 (`aafbfa3` 커밋):

- 리그 경로 호환성 확인 및 12개 `.anim` 추출.
- `Assets/Scenes/animTestScene.unity` 전용 테스트 씬 제작.
- Game 화면의 애니메이션 선택 버튼 12개와 재생 디버그 조작 제공.
- 실제 모델 재생, 씬 참조 독립성, 기존 전투 에셋 비변경 검증.

D단계 범위 (A~C 커밋 후 추가 요청으로 진행):

- 실제 전투용 Animator Controller, 상체 Mask와 Blend Tree 통합.
- AI 상태 전이, 점프 회전 각도 제어, 발사체·빔·파편·피격·사망 로직 연결.
- 위 동작에 필요한 Animation Event 연결 및 초기 프레임 지정. 최종 아트 타이밍은 후속 튜닝.

이벤트가 없거나 게임 기능이 아직 구현되지 않은 것은 A~C 클립 테스트의 실패 조건이 아니다.
테스트 씬은 애니메이션만 재생하며 공격 이펙트나 데미지를 발생시키지 않는다.

## 2. 확인한 기존 구조

| 구분 | 기존 자산 / 확인 사항 |
| --- | --- |
| 유지할 모델 | `Assets/Invader/Kaiju_001.fbx` |
| 리그 | Generic / NoAvatar. 별도 Avatar 서브에셋 없음 |
| 기존 애니메이션 | `Assets/Animation/Invader/Kaiju_Turn_*.anim` 4개 |
| 기존 Controller | `Assets/Animation/Invader/KaijuBoss.controller`: Idle, Attack1, Attack2 |
| 기존 전투 씬 | `Assets/Scenes/BattleArena.unity/BattleArena.unity` |
| 기존 Material / PNG | `Assets/Materials/Invader`, `Assets/Textures/Invader`의 기존 Kaiju 자산. 테스트는 기존 PNG + 전용 Lit Material 사용 |

`.anim` 파일은 FBX **밖에 별도로 저장된 클립**이다. Controller는 이를 참조한다.
따라서 이번 작업 결과도 “모델 FBX 하나 + 외부 `.anim` 여러 개”이지,
“하나의 FBX 내부에 여러 애니메이션을 합치는 작업”이 아니다.

외부 `TitanSlayerNewAssets/FBX/Kaiju.fbx`는 이번 씬 모델로 사용하지 않는다.
기존 모델 Import Settings, 기존 `Kaiju_Turn_*.anim`, `KaijuBoss.controller`는 계속 보존한다.
A~C에서는 BattleArena를 변경하지 않았으며 D단계에서만 새 Controller와 이벤트 수신기를 연결한다.

## 3. 자산 위치와 선정 이유

```text
game_save_the_world/
  Assets/
    Invader/Kaiju_001.fbx                       # 기존 모델 그대로
    Animation/Invader/Clips/Kaiju_*.anim        # 새 독립 클립 12개
    Animation/Invader/KaijuCombat.controller   # D단계 전투용 2레이어
    Animation/Invader/KaijuUpperBody.mask      # Generic 상체 Transform Mask
    Scenes/animTestScene.unity                 # 독립 테스트 씬
    _Project/Scripts/Debug/KaijuAnimationTester.cs
    Editor/KaijuAnimationTestBuilder.cs        # 추출/씬 생성/열기 메뉴
    Editor/KaijuCombatAnimationBuilder.cs      # 전투 자산 생성/연결/검사
    Editor/KaijuCombatAnimationVerification.cs # 격리 복제본 런타임 회귀 검사
    _Project/Scripts/Gameplay/KaijuBossAnimationDriver.cs
    _Project/Scripts/Gameplay/KaijuCombatStateRelay.cs
    Materials/Invader/Kaiju_*_Combat.mat        # 기존 PNG + 전투 전용 URP/Lit
    Materials/Debug/KaijuAnimationTestGround.mat
    Materials/Debug/Kaiju_*_AnimationPreview.mat # 기존 PNG를 참조하는 테스트용 Lit Material 3개
  docs/kaiju-animation-integration-plan.md
../TitanSlayerNewAssets/FBX/                    # 원본 보존, 런타임 참조 없음
```

- `Animation/Invader/Clips`: 기존 애니메이션 분류를 유지하면서 새 클립을 분리한다.
- `Scenes`: 독립 실행용 씬이며 실제 전투 씬의 테스트용 변경을 피할 수 있다.
- `Scripts/Debug`: 임시 디버그 기능을 전투 코드와 구분한다.
- `Editor`: Unity Editor에서만 사용하는 추출·생성 코드를 빌드 코드에서 분리한다.
- 원본 FBX는 Unity 프로젝트 밖에 보관한다. 추출 중에만 고유 임시 폴더로 복사하고,
  독립 참조를 검사한 후 해당 복사본을 삭제한다. 원본은 수정·삭제하지 않는다.

## 4. 추출 대상 및 재생 정책

각 파일명과 같은 이름의 `.anim`을 만든다. 원본의 전체 Take를 사용하고 임의로 구간을 자르지 않는다.

| 버튼 / 클립명 (Kaiju_ 접두사 생략) | 반복 | 향후 전투 적용 | 이번 테스트 |
| --- | --- | --- | --- |
| BasicIdle | O | Base Layer 대기 | 원본 클립 단독 재생 |
| IdleFront | O | 상체 정면 대기 | 동일 |
| IdleLeft45 | O | 상체 좌 45도 대기 | 동일 |
| IdleRight45 | O | 상체 우 45도 대기 | 동일 |
| Attack_FiringFront | X | 상체 정면 발사 | 동일 |
| Attack_FiringLeft45 | X | 상체 좌 45도 발사 | 동일 |
| Attack_FiringRight45 | X | 상체 우 45도 발사 | 동일 |
| Attack_BeamLeftToR | X | 좌→우 전신 빔 | 동일, 빔 이펙트 없음 |
| Attack_BeamRightToL | X | 우→좌 전신 빔 | 동일, 빔 이펙트 없음 |
| Attack_Tail | X | 꼬리 공격 | 동일, 파편 생성 없음 |
| JumpTurnR | X | 우측 점프 회전 | 동일, 별도 회전 스크립트 없음 |
| Death | X | 사망 | 마지막 포즈 유지 |

반복하지 않는 클립은 1회 재생 후 마지막 포즈에서 멈춘다.
버튼을 다시 누르면 0초부터 재생한다. 다른 클립을 선택할 때 본의 기준 포즈를 복구하여
이전 클립의 비키프레임 본 포즈가 남지 않게 한다.

A~C 테스트 씬에서는 모든 클립을 **마스크 없이 원본 그대로** 검사한다.
“향후 상체 적용”은 원본 FBX에 상체 본만 들어 있다는 뜻이 아니다.
상체 Mask와 BasicIdle을 합성한 결과는 D단계에서 별도 검증한다.

## 5. 개발 순서

### 단계 A — 호환성과 추출

1. 기존 모델과 신규 클립의 본 이름 및 계층 경로를 비교한다.
2. 기존 모델에 맞춰 임시 FBX를 Generic / NoAvatar로 임포트한다.
   없는 Avatar를 복사하거나 다른 FBX의 Avatar로 기존 모델을 교체하지 않는다.
3. material import를 끄고 애니메이션 압축 없이 커브를 추출한다.
4. 실제 `AnimationClip`만 복제하고 `__preview__` 클립은 제외한다.
5. 대기 4개만 Loop Time을 켠다. 파일명·Take·커브 경로·길이를 기록한다.
6. 누락된 본 경로, 빈 클립, 외부 객체 참조 커브가 있으면 자동 추출을 중단한다.
7. 독립 `.anim`에서 임시 FBX로 향하는 의존성이 없는지 검사한다.
8. 임시 복사본만 정리한다. 이미 추출된 `.anim`은 후속 수작업 보존을 위해 덮어쓰지 않는다.

### 단계 B — animTestScene 구현

1. 빈 전용 씬에 기존 모델 FBX 인스턴스를 **1개만** 생성한다.
2. 기존 Kaiju PNG를 참조하는 테스트용 URP/Lit Material을 연결한다.
   신규 클립의 본 포즈는 이미 Y-up이므로 테스트 모델의 회전은 (0, 0, 0)을 사용한다.
   기존 전투 시각화 스크립트의 X=270도를 이 평면 테스트 씬에 적용하면 모델이 눕기 때문에 그대로 복사하지 않는다.
3. 대기 포즈 기준 높이를 6 단위로 맞춘다. 모델 에셋 자체의 Scale은 수정하지 않는다.
4. 각 클립의 여러 포즈를 샘플링하여 카메라 범위를 정하고 조명·바닥을 추가한다.
   SkinnedMeshRenderer의 실제 렌더 경계 최저점 아래에 바닥을 두어 발 메시가 잘리지 않게 한다.
5. 기존 전투 Controller 대신 단일 `AnimationClipPlayable`로 선택 클립을 직접 재생한다.
   테스트용 상태 전이를 거치지 않으며 프로덕션 Controller도 변경하지 않는다.
6. Game 화면 좌측에 12개 클립 버튼과 선택 강조 표시를 만든다.
7. 다시 재생, 일시정지/재개, 0.25/0.5/1/2배속, 한 프레임 이동, 시간 슬라이더를 제공한다.
8. 현재 클립 이름, 재생 시간/길이, FPS, 반복 여부를 표시한다.
9. Input System용 EventSystem을 연결한다. UI 글자는 파일명과 영문 조작어를 사용한다.
10. `applyRootMotion=false`, `fireEvents=false`로 게임 이동 및 이벤트 호출을 분리한다.
    클립 자체에 기록된 본의 이동·회전은 재생하되 스크립트 기반 점프 턴은 추가하지 않는다.

### 단계 C — 검증

- Unity 컴파일 오류 없이 씬을 열고 Play할 수 있어야 한다.
- 12개 버튼 각각의 실제 클릭 콜백이 올바른 클립을 선택해야 한다.
- 커브에 대응하는 본이 존재하고, 클립 중간 포즈가 런타임 모델에 적용되어야 한다.
- 루프/비루프 종료, Death→Idle 전환, 재시작, 일시정지, 배속, 시간 이동을 검사한다.
- 씬의 FBX 의존성은 기존 `Kaiju_001.fbx` 하나여야 한다.
- 씬에 빠진 스크립트/클립/Material이 없어야 한다.
- 콘솔 및 화면 캡처로 렌더링과 UI 상태를 확인한다.
- 기존 전투 에셋과 외부 원본이 변경되지 않았는지 확인한다.

### 단계 D — 실제 게임 통합 (추가 요청, 구현 및 검증 완료)

1. 기존 보스 Animator 호출과 Attack1/Attack2 트리거의 호환 방식을 결정한다.
2. Base Layer: BasicIdle, Beam 2종, Tail, JumpTurnR, Death를 연결한다.
3. Generic Transform 기반 상체 Avatar Mask를 만들고 척추/팔/목/머리를 선택한다.
   골반/다리/꼬리는 요구 동작을 확인한 후 포함 여부를 결정한다.
4. UpperBody Layer에 Idle 3종 및 Firing 3종 1D Blend Tree를 구성한다.
   `TargetAngle`: -45 / 0 / +45, 전신 공격 중에는 상체 가중치를 제어한다.
5. 스크립트 회전과 클립 회전의 중복을 확인한 후 JumpTurn 이벤트와 각도를 확정한다.
6. 빔·꼬리·발사 이벤트를 실제 게임 로직과 연결한다.
7. 클립 단독 테스트 → 레이어 합성 → BattleArena 실제 공격 순으로 회귀 검증한다.

## 6. 사용 방법과 재현성

1. Unity에서 `Assets/Scenes/animTestScene.unity`를 연다.
2. Game 뷰를 가로 화면(권장 16:9)으로 두고 Play한다.
3. 좌측 버튼을 누르면 동일 몬스터가 해당 애니메이션을 재생한다.
4. 슬라이더를 움직이면 일시정지 상태로 포즈를 확인한다. Resume으로 계속 재생한다.
5. 비루프 클립의 재생이 끝나면 Replay 또는 해당 클립 버튼으로 다시 시작한다.

메뉴: `Tools > TitanDestroyer > Kaiju Animation Test`

- `1. Extract missing clips`: 누락된 클립만 추출한다. 형제 폴더 `TitanSlayerNewAssets/FBX`가 필요하다.
- `2. Create animTestScene (if missing)`: 씬이 없을 때만 생성한다. 기존 씬을 덮어쓰지 않는다.
- `Open animTestScene`: 수정 중인 씬의 저장 여부를 확인한 뒤 테스트 씬을 연다.
- `Reset preview framing`: 열린 테스트 씬에서 모델의 정립 자세와 카메라 프레이밍을 복구하여 저장한다.
- `3. Verify saved assets`: 커브 경로, 반복 설정, 독립 클립과 씬의 모델 의존성을 검사한다.
- `4. Verify buttons and poses (Play Mode)`: 테스트 씬 Play 중 12개 버튼의 클릭 이벤트를 실행하고,
  런타임 본 포즈를 원본 클립의 직접 샘플 결과와 비교한다. 재생 종료·루프·일시정지·프레임 이동·배속도 검사한다.
  검사가 끝나면 1배속 BasicIdle 재생으로 돌아간다.

추출이 완료된 `.anim`과 씬만으로 재생할 수 있으므로 원본 폴더 없이 프로젝트를 받아도
테스트는 가능하다. 원본은 재추출에만 필요하다. 테스트 씬은 기본 게임 빌드 씬 목록에 추가하지 않는다.

## 7. 문서 기준과 주의 사항

- `TitanSlayerNewAssets/kaiju_boss_design_doc.md`와 `게임ani.txt`의 갱신된 파일명 기준을 따른다.
- 이전 `Kaiju_JumpTurnL`은 실제 내부 이름에 맞춰 `Kaiju_JumpTurnR`로 정리되었다.
- 원본 PDF의 예전 표기와 계획상의 이벤트를 현재 구현 완료 상태로 해석하지 않는다.
- FBX 내부 타임스탬프를 임의 환산하지 않고 Unity가 임포트한 `clip.length`와 `frameRate`를 사용한다.
- 원본 모델이 아닌 기존 모델에서 육안으로 피부 변형·발 접지·좌우 방향을 최종 확인한다.
  본 경로 일치는 필수 조건이지만 최종 아트 품질 승인과 같지는 않다.

기술 참고: Unity의 [Playables 예제](https://docs.unity3d.com/6000.0/Documentation/Manual/Playables-Examples.html)와
[Manual 업데이트 모드](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Playables.DirectorUpdateMode.html),
[BakeMesh 스케일 보정](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html).

## 8. A~C 구현 및 검증 결과

### 추출 결과

12개 클립 모두 기존 모델의 본 경로와 일치했다. 별도 Avatar 생성이나 본 이름 변경은 필요하지 않았다.
원본 전체 길이를 유지했고, Unity가 보고한 FPS는 모두 30이다.

| 클립 | 길이 (초) | 커브 수 | Loop |
| --- | ---: | ---: | --- |
| Kaiju_BasicIdle | 2.666667 | 440 | O |
| Kaiju_IdleFront | 2.666667 | 440 | O |
| Kaiju_IdleLeft45 | 2.666667 | 440 | O |
| Kaiju_IdleRight45 | 2.666667 | 440 | O |
| Kaiju_Attack_FiringFront | 1.000000 | 440 | X |
| Kaiju_Attack_FiringLeft45 | 1.000000 | 440 | X |
| Kaiju_Attack_FiringRight45 | 1.000000 | 440 | X |
| Kaiju_Attack_BeamLeftToR | 2.666667 | 440 | X |
| Kaiju_Attack_BeamRightToL | 2.666667 | 420 | X |
| Kaiju_Attack_Tail | 3.000000 | 440 | X |
| Kaiju_JumpTurnR | 1.233333 | 440 | X |
| Kaiju_Death | 3.900000 | 440 | X |

### 환경 호환성 조정

기존 `Toon/Toon` 셰이더가 현재 Unity 6000.4.0f1 / Metal에서
`RemapFoveatedRenderingLinearToNonUniform` 식별자 관련 컴파일 오류를 냈다.
프로젝트 전체의 셰이더나 기존 Material은 수정하지 않고, 테스트 씬에서만
기존 Kaiju PNG를 참조하는 URP/Lit Material 3개를 사용한다.
따라서 테스트 화면의 조명/질감은 기존 Toon 표현과 다르며, 이 씬의 목적은 애니메이션 확인이다.

추출 클립은 커브 손실을 피하려고 압축을 끈 상태로 저장했다.
12개 텍스트 직렬화 `.anim`의 디스크 크기는 약 160 MiB이며, 이것이 빌드 후 메모리 크기를 뜻하지는 않는다.
커브 최적화/압축과 용량 측정은 동작 승인 후 별도 수행한다.

### Play Mode 자동 검증 결과

- 12/12 버튼: PointerClick 이벤트로 해당 클립을 선택하고 0초부터 재생하는 것을 확인.
- 12/12 포즈: 재생 길이의 37% 지점에서 런타임 Animator/Playable 결과를 `.anim` 직접 샘플과 비교.
  최대 본 위치 오차 0.000001, 최대 회전 오차 0.0000도로 통과.
- 12/12 클립: 반복 또는 종료 후 마지막 포즈 유지 규칙 통과.
- Death → BasicIdle 복귀, 다시 재생, 일시정지, 1프레임 이동, 2배속 검사 통과.
- 전용 검증 메뉴로 재실행 가능. 이 검증은 본 포즈 및 제어 기능 검사이며 전투 공격/VFX 검증은 아니다.

### 씬 구성과 원본 보존

- 저장된 씬의 FBX 의존성은 기존 `Assets/Invader/Kaiju_001.fbx` 하나뿐이다.
- 새 `.anim` 12개에는 원본/임시 FBX 의존성이 없다.
- Game 화면에서 12개 버튼, 현재 클립/시간, 재생 조작 UI 및 텍스처 적용을 확인했다.
- 버튼 중앙 12곳의 UI Raycast가 모두 해당 버튼을 가리키는 것을 확인했다.
- 씬의 누락 스크립트 0개, Kaiju SkinnedMeshRenderer 3개(몸/눈/머리 장식), 모델 인스턴스 1개를 확인했다.
- 최종 화면에서 머리 장식부터 발까지 전신이 보이고 바닥에 메시가 잘리지 않는 것을 확인했다.
- 런타임 본 좌표에서 Head의 Y가 Foot보다 높은 Y-up 구조임을 확인했고,
  모델 회전을 identity로 수정한 정립 자세를 화면에서 검증했다.
- 기존 모델, 기존 애니메이션, 기존 Controller, BattleArena 및 기존 Material 파일은 수정하지 않았다.
- 추출 과정의 임시 FBX 복사본은 정리했다. 최초 경로 호환성 확인용 BasicIdle 복사본과 메타는
  `/tmp/kaiju-animation-probe.zcNEql`에 이동해 일시적으로 복구 가능하며 원본과 SHA-256이 일치함을 확인했다.
  원본 `TitanSlayerNewAssets/FBX`는 이번 작업에서 수정하지 않았다.
- 개발계획서만 Git에 포함되도록 `.gitignore`의 기존 docs 제외 규칙에 좁은 예외를 추가했다.
  다른 로컬 docs 파일의 제외 정책은 유지했다.

## 9. D단계 구현

### 전투 연결 구조

- BattleArena의 기존 Kaiju 모델 인스턴스에 `KaijuBossAnimationDriver`를 추가한다.
  모델을 복제하거나 외부 FBX를 씬에 추가하지 않는다. 모델 위치/크기는 보존하고,
  신규 Y-up 포즈에 맞춰 모델 로컬 회전을 identity로 정리한다.
- 새 `KaijuCombat.controller`: Base에는 BasicIdle / BeamLeftToR / BeamRightToL / Tail / JumpTurnR / Death,
  UpperBody에는 AimIdle / Firing의 1D Blend Tree를 둔다. 기존 Controller와 옛 클립은 롤백용으로 보존한다.
- Mask는 `Root/Pelvis/Spine 01` 및 자식만 포함한다. Root/골반/다리/꼬리는 제외한다.
  TargetAngle은 -45 / 0 / +45이며 전신 동작 중 UpperBody 가중치는 0이다.
- 기존 `PlayQuickAttackAnimation` / `PlayHeavyAttackAnimation` 호출 API는 유지한다.
  새 드라이버가 있으면 Quick은 조준 사격, Heavy는 기본 좌→우 빔 포즈를 사용하고,
  명시적으로 연결한 빔/꼬리 패턴은 방향과 이벤트 타이밍을 지정한다.
  드라이버가 없는 옛 씬에서는 기존 Attack1/Attack2 호출이 그대로 동작한다.
  새 Controller에 직접 Attack1/Attack2를 보내는 경우도 `KaijuCombatStateRelay`로 상태와 이벤트를 연결한다.
- `BossController.FaceTarget`는 새 드라이버가 있을 때 상체 추적과 점프 턴에 위임한다.
  ±45도 밖의 타겟에 대해 공격 중이 아닐 때 45~90도 범위의 방향 보정 턴을 수행한다.
  타겟을 지나치지 않도록 상대 각도보다 큰 회전은 제한한다.
- JumpTurnR 클립은 Root 회전이 identity이며 골반 시작/끝 회전도 BasicIdle과 동일했다.
  누적 회전은 보스 루트에서만 처리하고, 기존 매 프레임 FaceTarget 회전과 idle bob은 겹치지 않게 한다.
  좌측 전용 클립은 없으므로 좌측에도 JumpTurnR 포즈 + 음의 루트 회전을 사용한다. 미러 클립을 새로 만든 것은 아니다.
- 발사 위치는 기존 Head 아래의 씬 전용 `KaijuMouthSocket`이다. 꼬리 파편은 기존 Tail004를 사용한다.
  발사점은 Inspector에서 조절 가능하며 원본 FBX는 변경하지 않는다.

### 실제 패턴 매핑

| 현재 전투 패턴 | 새 동작 / 발동 기준 | 유지하는 기존 기능 |
| --- | --- | --- |
| DebrisFragmentScatter | Tail → OnTailImpact 후 파편 배치 생성 | 파편 카탈로그, 산포, 피해, 카메라 흔들림. 임시 루트 점프는 새 드라이버 사용 시 생략 |
| DebrisSalvo | 매 발사마다 Firing Blend Tree → OnFireProjectile | 조준 경고, 발사 수, 가속/크기 변화, 피해 |
| AcceleratingSweepBeam | 방향에 맞는 BeamLeftToR / BeamRightToL → OnBeamStart | 경고, 느린/빠른 스윕, 기존 빔 판정 |
| TrackingResidualBeam | Firing Blend Tree의 발사 포즈를 유지 | 추적 빔, 지속 피해 틱. 전용 추적 빔 클립이 없어 사용하는 임시 매핑 |
| PressureSniperShot | 경고 후 Firing 이벤트로 발사 | 기존 압박 탄환 기능. 현재 저장된 4개 패턴 목록에는 없음 |

기존 LegacyBulletHell 모드의 다른 패턴은 기존 시각화 호출/판정을 유지하며,
개별 공격 연출의 정밀 동기화 대상으로 확대하지 않는다.

### 이벤트 초기값 (30fps, 0초 기준)

| 클립 | 이벤트 | 시간 / 프레임 |
| --- | --- | --- |
| Firing 3종 | OnFireProjectile / OnFiringEnd | 9 / 29프레임 |
| Beam 2종 | OnBeamStart / OnBeamEnd / OnBeamRecovered | 20 / 68 / 79프레임 |
| Tail | OnTailImpact / OnTailEnd | 48 / 89프레임 |
| JumpTurnR | OnJumpTurnStart / OnJumpTurnEnd / OnJumpTurnRecovered | 5 / 32 / 36프레임 |

이 시점은 원본 FBX에 있던 이벤트가 아니라 D단계에서 지정한 **초기 튜닝값**이다.
Firing/Beam 준비 속도는 기존 패턴의 경고 시간에 맞춘다. 빔 동작 구간은 기존 활성 시간에 맞춘다.
꼬리 공격은 실제 포즈를 보이기 위해 1배속의 48프레임까지 기다린다.
혼합 중 여러 클립이 같은 이벤트를 보내도 하나의 공격 번호당 한 번만 발동한다.
이벤트가 누락되면 시간 초과로 공격을 취소하며, 임의 시간으로 대신 발사하지 않는다.

원본 커브를 복제하지 않고 추출한 `.anim` 7개에 이벤트만 추가한다.
`animTestScene`은 계속 `fireEvents=false`이므로 버튼 테스트에서 전투/VFX가 실행되지 않는다.
생성 메뉴를 다시 실행해도 기존 Controller/Mask/Material, 이미 있는 이벤트 시점은 덮어쓰지 않는다.
이벤트 시점을 수동 변경할 때는 `KaijuBossAnimationDriver`의 대응 시간 상수도 함께 변경해야
경고/동작 속도 계산이 일치한다.

### 중단·사망·일시정지

- 진행 중인 공격은 전투 종료/사망 시 취소하고 경고·빔 및 지속 피해 상태를 정리한다.
- 사망 시 상체 가중치 0, Death 1회 재생 후 마지막 포즈 유지. 사망 후 새 공격 요청은 거절한다.
- 시네마틱 일시정지 시 Animator/회전을 멈추고 기존 패턴 컨트롤러의 취소 정책을 따른다.
- 이미 발사된 탄환의 기존 수명은 유지하지만, 사망 이후 새 탄환/후속 장식 탄환 생성은 막는다.
- 현재 Unity/Metal에서 실패하는 Toon 셰이더는 수정하지 않는다. 전투용 Lit Material 3개만 추가/연결한다.
  따라서 기존 Toon 룩의 재현은 이번 단계의 완료 조건이 아니며 별도 아트 작업이다.

### 검증 방법

메뉴 `Tools > TitanDestroyer > Kaiju Combat`:

1. `Create combat assets and bind BattleArena`: BattleArena를 단독으로 열어 적용한다.
   미저장 변경이 있으면 중단한다. 기존 환경 컴포넌트가 테스트 씬 카메라/조명을 참조하지 않도록 씬을 격리한다.
2. `Verify combat assets`: 2개 레이어/12개 독립 클립/상체 마스크/이벤트 수신기/Death 종료 정책 검사.
3. `Verify runtime poses and events (Play Mode)`: 실제 씬 보스의 격리 복제본에서
   조준 5각도, 이벤트 중복, 하체 마스크, 빔 양방향, 꼬리, 좌우 턴, 유지 사격,
   취소/일시정지/사망 포즈를 검사하고 복제본을 삭제한다. 실제 보스 체력/상태는 바꾸지 않는다.

BattleArena에서 실제 패턴 순환 및 피해/VFX 검증은 위 포즈 검사와 별도로 수행한다.

### D단계 검증 기록

- Unity Play Mode 격리 복제본 검사 통과: 조준 각도 -45/-22.5/0/+22.5/+45도,
  혼합 중 발사 이벤트 1회, 상체 Mask의 하체 비간섭, 전신 빔 2종, 꼬리 타격/복귀,
  +60/-75도 점프 턴의 준비·공중·착지 구간, 추적 빔 포즈 유지,
  취소/일시정지/재개, 사망 이후 발사 거절 및 마지막 포즈 유지.
- 최종 Controller에서 `Animator.SetTrigger("Attack1")` / `SetTrigger("Attack2")` 직접 호출도
  이벤트 1회 발생 후 대기로 복귀하는 것을 회귀 검사했다.
- 전투 이벤트 추가 후 `animTestScene`의 12/12 버튼·포즈·반복/종료·재생 제어 검사를 다시 통과했다.
  `fireEvents=false`, 전투 드라이버 0개로 단독 테스트와 전투 동작이 분리된 것을 확인했다.
- 최종 스크립트 컴파일 및 전투 자산 재생성 검사를 통과했다. 반복 실행 후에도 기존 태양 광원
  참조가 유지되고 BattleArena가 저장된 상태(`dirty=false`, Play 종료)임을 확인했다.
- BattleArena에서 실제 패턴 순환을 관찰했다. `BossDebrisFragmentRuntime`,
  `BossDebrisSalvoRuntime`, `BossAcceleratingSweepBeam`, `BossTrackingResidualBeam` 생성을 확인했다.
  좌→우/우→좌 전신 빔 상태도 모두 확인했다. 추적 빔 활성 체력 조건 검사를 위해
  Play Mode에서만 보스 체력을 50%로 낮췄으며, 테스트 종료 시 되돌렸다.
- 실제 `BossAcceleratingSweepBeam` 발사 중 보스에게 치명 피해를 주었다.
  이후 `IsDead=true`, 상태 `Death`, 빔 오브젝트 0개, `IsBattleActive=false`,
  `BossAttackController.enabled=false` 및 사망 마지막 포즈 유지를 확인했다.
- 전투 카메라 캡처에서 Kaiju의 텍스처/정립 자세와 빔을 확인했다.
  기존 헬기의 Toon 셰이더 오류로 헬기는 분홍색으로 표시된다. 이는 남은 기존 문제이며
  헬기 머티리얼이나 프로젝트 전체 셰이더 패키지는 이번 작업에서 수정하지 않았다.
- 카메라/도시/헬기 등 BattleArena의 다른 직렬화 오브젝트는 바꾸지 않았다.
  씬 변경은 기존 Kaiju 인스턴스의 자세/Animator/머티리얼과 신규 드라이버/입 소켓에 한정된다.
  생성 도구는 기존 환경 컴포넌트의 OnValidate가 태양 광원 참조를 바꾸는 부작용도 막아,
  저장되어 있던 RenderSettings.sun을 유지한다.
- 공격 발사 시점과 상태 연결 검증을 수행한 것이며, 각 패턴의 피해량/난도 재밸런싱이나
  좌측 점프 전용 아트, 사망 카메라 연출, 꼬리 지면 접촉 위치의 최종 아트 승인을 뜻하지 않는다.
