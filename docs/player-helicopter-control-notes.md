# Player Helicopter Control Notes

Last updated: 2026-06-05

이 문서는 BattleArena 플레이어 헬기 조작을 다시 수정할 때 같은 시행착오를 반복하지 않기 위한 기록이다. 현재 핵심 구현은 `Assets/_Project/Scripts/Gameplay/PlayerOrbitController.cs`에 있다.

## 목표 동작

- 헬기 이동은 항상 카메라 기준 2D 평면 안에서만 처리한다.
- 좌우/상하 이동 중에는 보이는 헬기 모델만 살짝 기울어진다.
- 입력을 놓으면 기울기는 원래 자세로 돌아온다.
- 이동 제한은 해상도와 화면 비율이 바뀌어도 헬기 이미지가 화면 밖으로 나가지 않게 계산한다.
- `PlayerPlaceholder` 같은 이동 앵커 자체를 회전시켜 시각 효과를 만들면 안 된다.

## 현재 구조

`PlayerOrbitController`의 루트 `transform`은 이동 앵커다. 실제 위치 보정, 속도 계산, 충돌 기준은 이 루트가 담당한다. 이 루트는 플레이어가 좌우/상하로 움직일 때 3D 원형 외곽을 타는 것처럼 회전하면 안 된다.

`PlayerVisualRoot`는 보이는 헬기 모델의 루트다. `CrashObserver`는 데미지/충돌용 오브젝트라서 시각 모델 루트 탐색에서 제외된다. 실제 기울기 효과는 `PlayerVisualRoot` 또는 그 내부의 실제 모델에만 적용되어야 한다.

현재는 `useScreenSpaceVisual = true`가 기본이다. 런타임에 헬기 시각 모델을 별도 렌더 카메라가 보는 위치로 복제하고, 그 결과를 `RawImage`로 화면 공간에 표시한다. 원본 월드 모델은 숨기고, 화면에 보이는 것은 screen-space visual이다. 이 구조 때문에 화면 표시 크기와 이동 제한을 월드 좌표 감으로 맞추면 다시 깨지기 쉽다.

## 이동 평면

2D 이동은 `useCameraPlaneMovement = true` 상태에서 카메라 평면 기준으로 처리한다.

- 입력 축은 `movementCamera.transform.right`와 `movementCamera.transform.up`을 사용한다.
- 위치는 `movementCamera.WorldToViewportPoint`와 `ViewportToWorldPoint`를 통해 같은 카메라 깊이에 고정된다.
- `PlayerMoveGuide`는 카메라와 기본 viewport 기준을 제공하지만, screen-space visual이 켜져 있을 때 좌우 제한은 `PlayerMoveGuide`의 X 범위를 그대로 쓰지 않는다.

중요한 점: 좌우가 한쪽은 덜 가고 한쪽은 화면 밖으로 나가는 문제를 `PlayerMoveGuide` 값을 감으로 좌우 다르게 조정해서 해결하지 말 것. 모바일 해상도와 헬기 표시 크기가 바뀌면 다시 깨진다.

## 화면 표시 크기와 제한 영역

주요 값은 `PlayerOrbitController`의 serialize field다.

| 필드 | 현재 기준 | 의미 |
| --- | --- | --- |
| `screenSpaceVisualImageSize` | `780 x 540` | 화면에 배치되는 `RawImage` 크기. 520 x 360 기준 50% 확대값이다. |
| `screenSpaceVisualTextureSize` | `512` | 헬기 모델을 렌더링하는 RenderTexture 해상도. 화면상 크기와 같은 개념이 아니다. |
| `screenSpaceVisualFramePadding` | `1.15` | 렌더 카메라가 모델을 프레이밍할 때 여백. 너무 작으면 잘릴 수 있다. |
| `screenSpaceVisualEdgePadding` | `12 x 12` | 실제 보이는 헬기 외곽이 화면 끝에서 떨어질 최소 픽셀 여백. 좌우를 더 붙이고 싶으면 X를 줄인다. |
| `screenSpaceVisualScaleMultiplier` | `0.65` | 복제된 시각 모델의 기본 스케일. 보통 화면 표시 크기 조정은 `screenSpaceVisualImageSize`부터 본다. |

현재 좌우 이동 제한은 `screenSpaceVisualContentRect`를 이용한다. 렌더 카메라 기준으로 실제 활성 Renderer들의 bounds를 viewport에 투영해서, 투명 여백이 아니라 실제 헬기 외곽 기준으로 좌/우 extents를 구한다.

계산 흐름:

1. `UpdateScreenSpaceVisualContentRect()`가 헬기 renderer bounds의 8개 코너를 렌더 카메라 viewport에 투영한다.
2. `GetEffectiveMovementViewportRect()`가 `screenSpaceVisualImageSize`, 실제 content rect, `screenSpaceVisualEdgePadding`을 이용해 화면 밖으로 나가지 않을 X 범위를 계산한다.
3. Y 범위는 HUD/전투 연출 영역을 보존하기 위해 `PlayerMoveGuide.ViewportRect`와 함께 제한한다.

따라서 좌우 끝 간격이 넓으면 우선 `screenSpaceVisualEdgePadding.x`를 낮춘다. 헬기 표시 크기를 바꾸고 싶으면 `screenSpaceVisualImageSize`를 바꾼다. `PlayerMoveGuide`의 X min/max를 임시로 벌리는 방식은 피한다.

## 기울기 처리

기울기는 `UpdateVisualTilt()`에서 입력을 기준으로 계산한다.

- 상하 입력: pitch 방향으로 기울기
- 좌우 입력: bank 방향으로 기울기
- 현재 기본값: `maxVisualTiltAngle = 12`, `visualTiltDuration = 0.18`
- 입력이 0이 되면 `Vector2.MoveTowards`로 `currentVisualTilt`가 0으로 돌아간다.

screen-space visual이 활성화된 상태에서는 `ApplyScreenSpaceVisualPose()`가 기울기를 화면용 visual root에만 적용한다. 이동 앵커, 카메라, BattleArena 루트 회전으로 기울기를 만들면 안 된다. 그렇게 하면 예전처럼 헬기가 원형 외곽에 붙어 움직이는 느낌이 다시 생긴다.

## 수정 시 우선순위

1. 헬기가 2D 평면에서 벗어나거나 원형 궤도를 타는 것처럼 보이면 기울기 효과보다 이동 평면 고정을 먼저 고친다.
2. 헬기 전체 축이 돌아가는 문제와 이동 중 시각적 기울기는 별개로 본다.
3. 크기 문제는 카메라 거리보다 `screenSpaceVisualImageSize`와 screen-space visual 프레이밍부터 확인한다.
4. 좌우 제한 문제는 `screenSpaceVisualContentRect`와 `screenSpaceVisualEdgePadding` 계산을 확인한다.
5. 원본 모델 계층, `PlayerPlaceholder`, `PlayerVisualRoot`의 base rotation을 감으로 돌려서 해결하지 않는다.

## 검증 체크리스트

- Unity Play Mode에서 좌/우/상/하 이동 후 손을 놓았을 때 헬기 기울기가 원위치로 돌아오는지 확인한다.
- 좌우 끝으로 이동했을 때 실제 보이는 헬기 외곽이 화면 밖으로 나가지 않는지 확인한다.
- 여러 화면 비율에서 좌우 여백이 심하게 비대칭이 아닌지 확인한다.
- 헬기 표시 크기 변경 후 `screenSpaceVisualImageSize`와 이동 제한이 같이 맞는지 확인한다.
- 스크립트 수정 후 `git diff --check`를 통과시키고 Unity compile 상태를 확인한다.

## 작업 주의사항

- `Assets/ZRNAssets/`는 사용자가 추가한 자산이다. 삭제하거나 정리 대상으로 포함하지 않는다.
- 검증용 스크린샷을 남기라는 요청이 있으면 삭제하지 말고 경로를 공유한다.
- 커밋할 때는 의도한 파일만 stage한다. 이 프로젝트에는 스크린샷, 복구 폴더, 사용자 추가 자산 같은 미추적 파일이 같이 있을 수 있다.
