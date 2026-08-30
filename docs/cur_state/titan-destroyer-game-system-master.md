---
title: Titan Destroyer 게임 시스템 중심 문서
document_id: TD-GAME-SYSTEM-SSOT
document_type: game-system-ssot
status: live
version: "1.0.38"
last_verified: "2026-08-30"
last_verification_scope: "배경 지상 장갑차 3종 기본 8대의 간헐적 포구 화염을 보강. 단발 Ambient 종료 프레임의 즉시 소등을 제거하고 Muzzle 교차 Quad를 발사마다 0.075~0.19초 맥동시킨다. 배치 Play Mode에서 화염 ON→자동 OFF, 8대 이동·접지, 보스 2000·플레이어 100/120·락온 5 불변식 통과. 전체 EditMode 115/115. 7/10대·다화면비·10분 soak·모바일은 미검증."
partial_review_baseline: "git a6554a7 + 2026-08-29 Background Ally Army 공중 구현 작업 트리"
implementation_baseline: "git a6554a7 + Background Ally Army 공중 구현 작업 트리 + 2026-08-30 지상 장갑차 기본 8대 프로토타입. 기존 전투·오디오·HUD·락온·횡단 빔 구현 이력 보존"
unity_version: 6000.4.0f1
authoritative_scope:
  - game_flow
  - player_controls
  - player_weapons
  - player_survivability
  - boss_stats
  - boss_attack_patterns
  - combat_balance
  - scene_music
  - global_sound
  - main_menu_presentation
  - combat_background_allies
---

# Titan Destroyer 게임 시스템 중심 문서

> **현재 게임 시스템의 단일 기준 문서(SSOT)**
> 사람과 Codex 모두 이 문서를 먼저 읽고 현재 규칙, 실제 구현, 미완성 사항을 판단한다. 과거 문서와 충돌하면 이 문서의 상태 표기와 최신 검증 결과를 확인한다.

## 0. 문서 사용 규칙

### 0.1 이 문서가 답해야 하는 것

- 플레이어가 무엇을 입력하고 어떤 공격을 하는가?
- 기관총과 락온 미사일의 피해량, 발사 주기, 충전 단계는 얼마인가?
- 체력과 아머는 어떤 순서와 공식으로 피해를 받는가?
- 몬스터(현재 구현에서는 단일 보스)가 어떤 순서와 조건으로 공격하는가?
- 기획된 내용과 실제 구현된 내용이 어디에서 다른가?
- 수치를 바꿀 때 어느 코드·씬·에셋과 문서를 함께 수정해야 하는가?

### 0.2 상태 표기

| 상태 | 의미 |
| --- | --- |
| **구현됨** | 현재 런타임 경로에서 실제 사용된다. |
| **프로토타입/디버그** | 코드나 UI는 있으나 에디터 메뉴·개발 빌드에 의존하거나 완전한 게임 흐름에 연결되지 않았다. |
| **계획** | 의도 또는 향후 작업이며 현재 플레이에는 적용되지 않는다. |
| **레거시/비활성** | 코드·에셋·과거 문서가 남아 있어도 현재 활성 경로에서는 사용하지 않는다. |
| **불일치** | 문서·직렬화 값·런타임 소비 경로가 서로 달라 결정 또는 정리가 필요하다. |

### 0.3 진실성 원칙

1. 이 문서는 **기획 의도와 구현 현황을 함께 기록하는 기준점**이다.
2. 실제 동작을 판단할 때는 런타임 코드, 현재 씬, 프리팹, ScriptableObject를 함께 확인한다.
3. 문서와 구현이 다르면 조용히 어느 한쪽에 맞추지 않는다. 현재 동작은 `구현됨`, 원하는 변경은 `계획`, 차이는 `불일치`로 기록한다.
4. 계산값은 원본 튜닝값과 구분해 `계산값`이라고 표시한다.
5. 아래 숫자는 디버그 패널의 런타임 임시 오버라이드를 적용하지 않은 현재 기본값이다.

## 1. 현재 시스템 한눈에 보기

| 영역 | 현재 기준 | 상태 |
| --- | --- | --- |
| 전투 형태 | 화면 기준 2D 평면에서 헬기를 이동하며 단일 거대 보스를 상대 | 구현됨 |
| 플레이어 이동 | 좌우·상하 기본 7.2, 락온 충전 중 4.32 | 구현됨 |
| 플레이어 공격 | 2초 발사/2초 휴지 자동 기관총 + 정상 시간 충전식 다중 락온 미사일 일제사격. 실제 성공 락 1~5개가 각각 원본 단계 프로필을 사용 | 구현됨 |
| 제거된 공격 | 과거 단발 미사일, 필살기 버튼, 조준점 선택형 확률 크리티컬 | 레거시/비활성 |
| 플레이어 생존 | Hull 100 / Armor 120, 아머 자동 수리, 아머 파손 중 Hull 피해 1.25배 | 구현됨 |
| 보스 체력 | 싱글 2,000 / 멀티 플레이스홀더 2,800 | 구현됨 |
| 활성 보스 패턴 | 파편 산탄, 파편 일제사격, 가속 횡단 빔, 추적 잔류 빔 | 구현됨 |
| 난이도 | Easy / Normal / Expert / Nightmare 선택 상태는 있으나 전투 수치 연동 없음 | 불일치 |
| 약점 개방 | 타깃과 배율은 있으나 실제 전투에서 개방시키는 흐름은 디버그에만 연결 | 프로토타입/디버그 |
| 씬 BGM | MainMenu `BGM_title.wav`, BattleArena `BGM_battle_01.ogg`, 무한 반복, 앱 시작 기본 OFF, 전역 static 상태로 모든 등록 BGM 일괄 ON/OFF | 구현됨 |
| 전역 효과음 | 기관총과 락온 단계·릴리즈·부스트 등 BGM 외 AudioSource, 앱 시작 기본 OFF, 전역 static 상태와 BattleArena `SOUND ON/OFF` 버튼으로 일괄 제어 | 구현됨 |
| 전투 배경 아군 | 500 tris 헬기 4대 + 779~820 tris 지상 장갑차 기본 8대가 보스 주변을 순찰하며 피해 없는 공중·지상 공격을 연출 | 공중 구현됨 / 지상 프로토타입 구현 |

현재 플레이어에게 유효한 공격 선택지는 아래 두 가지뿐이다.

1. **기관총:** 별도 공격 입력 없이 보스의 고정 `AimPoint`를 향해 2초간 자동 사격하고 2초간 쉬는 주기를 반복한다.
2. **락온 미사일:** PC 우클릭과 모바일 `LOCK ON` 모두 입력을 누르면 첫 충전 타깃 마커가 즉시 점멸하고, 단계별 1 / 1.5 / 2 / 2.5 / 3초를 들여 최대 5개 부위를 순차 락온한다. 놓으면 실제 성공 락 수에 맞춰 `5/10/15/20/30발`, 기본 피해 `9/20/35/60/100`을 사용한다. 장착 Sidewinder 2발·헬기 회전 등 풀살보 연출은 실제 5락에서만 실행한다.

락온 충전 중 플레이어에게 실제 피해가 적용되면 충전과 획득한 락은 즉시 취소되며, 해당 입력을 놓아도 미사일은 발사되지 않는다.

## 2. 게임 흐름과 전투 종료

### 2.1 기본 흐름

1. 메인 메뉴에서 스테이지 선택으로 이동한다.
2. 지역과 난이도를 선택하고 `BattleArena`에 진입한다.
3. 플레이어는 이동, 기관총, 락온 미사일로 보스를 공격한다.
4. 보스 Hull이 0이면 승리한다.
5. 플레이어 Hull이 0이면 패배한다.
6. 결과 화면에서 재도전하거나 메인 메뉴로 돌아간다.

### 2.2 모드

| 모드 | 보스 체력 | 비고 |
| --- | ---: | --- |
| Single | 2,000 | 현재 기본 플레이 경로 |
| MultiPlaceholder | 2,800 | 동료 플레이스홀더가 활성화되지만 완성된 멀티플레이는 아님 |

첫 보스 공격은 전투 씬 시작 후 **4초 지연** 뒤 시작한다. 승리 후에는 `R`로 같은 씬을 재시작한다. 패배 중에는 `R` 입력을 받지 않고 결과 오버레이의 Retry/Quit를 사용한다.

### 2.3 스테이지와 난이도

- 선택 가능한 지역 데이터: Tokyo, Seoul, Paris, Hollywood, Beijing
- 선택 가능한 난이도: Easy, Normal, Expert, Nightmare
- **현재 확인 결과:** 선택 상태와 UI는 존재하지만 난이도별 보스 체력, 공격력, 패턴, 플레이어 수치를 바꾸는 전투 연결은 없다.

### 2.4 씬 BGM과 전역 효과음

상태: **구현됨**

| 씬 | 재생 에셋 | AudioSource | 재생 규칙 |
| --- | --- | --- | --- |
| `MainMenu` | `Assets/Audio/Music/MainMenu/BGM_title.wav` | `MainMenuRoot/Systems/MainMenuMusic` | 씬 활성화 시 자동 시작, 2D, 볼륨 0.7, 무한 반복. 전역 OFF이면 즉시 음소거 |
| `BattleArena` | `Assets/Audio/Music/BattleArena/BGM_battle_01.ogg` | `BattleArenaRoot/BattleArenaMusic` | HUD가 동일 소스를 초기화, 2D, 볼륨 0.7, 무한 반복. 전역 ON이면 재생하고 OFF이면 음소거 |

- 두 BGM은 긴 음원용 `Streaming` 로드와 백그라운드 로드를 사용한다.
- 모든 씬에서 `GlobalMusicSettings.MusicEnabled = true/false` 또는 `GlobalMusicSettings.ToggleMusic()`를 호출해 등록된 BGM 전체를 일괄 제어한다. `MusicEnabled`는 public static 프로퍼티이며 기본값은 OFF다.
- 전역 음악 상태는 현재 실행 중인 앱 또는 Play Mode 안에서 씬이 바뀌어도 유지된다. 앱/Play Mode를 새로 시작하면 OFF로 초기화하며, 아직 `PlayerPrefs`에 저장하지는 않는다.
- `GlobalMusicSource`가 각 BGM AudioSource를 전역 레지스트리에 등록한다. 현재 MainMenu는 `MenuPresenter`, BattleArena는 `HUDPresenter`가 기존 씬 AudioSource에 이 컴포넌트를 보장한다.
- OFF 전환은 재생 중인 BGM을 정지시키지 않고 `mute`하여 재생 위치를 유지한다. OFF 상태에서 새로 들어온 씬의 정지된 BGM은 재생을 시작하지 않으며, ON 전환 시 음소거를 해제하고 정지된 등록 BGM도 재생한다.
- BattleArena의 `MUSIC ON/OFF` HUD 버튼도 같은 전역 상태를 변경하며, 다른 코드가 상태를 바꾸면 이벤트를 받아 표시를 동기화한다.
- Music 상태는 BGM 전용이다. 기관총·락온 등 효과음은 아래의 독립된 Sound 상태를 따른다.
- 향후 다른 씬에 BGM을 추가할 때는 해당 AudioSource에 `GlobalMusicSource`를 붙이거나 `GlobalMusicSettings.RegisterSource(...)`로 등록해야 일괄 ON/OFF 대상이 된다.
- 씬 전용 AudioSource이며 `DontDestroyOnLoad`로 유지하지 않는다. 따라서 씬을 떠나면 해당 BGM이 종료되고, 다음 씬에 지정된 BGM이 별도로 시작한다.
- 이전 전투 음원 `battle_arena_bgm.mp3`와 그 메타 파일은 프로젝트에서 제거했다. 현재 전투 씬과 HUD 직렬화 참조는 모두 `BGM_battle_01.ogg`를 사용한다.

#### 전역 Sound

- 모든 씬에서 `GlobalSoundSettings.SoundEnabled = true/false` 또는 `GlobalSoundSettings.ToggleSound()`를 호출해 BGM을 제외한 효과음 출력을 일괄 제어한다. `SoundEnabled`는 public static 프로퍼티이며 기본값은 OFF다.
- Sound 상태도 현재 실행 중인 앱 또는 Play Mode 안에서 씬이 바뀌어도 유지되고, 새 앱/Play Mode 시작 시 OFF로 초기화한다. 아직 `PlayerPrefs`에는 저장하지 않는다.
- OFF는 `AudioListener.volume=0`, ON은 `1`로 적용한다. `AudioListener.pause`는 사용하지 않으므로 OFF 중 발생한 짧은 효과음이 ON 전환 뒤 늦게 몰아서 재생되지 않는다.
- `GlobalMusicSource`에 등록된 BGM은 Listener 볼륨과 일시정지를 무시하고 기존 Music 상태만 따른다. 따라서 Music과 Sound는 서로 독립적으로 ON/OFF할 수 있다.
- 현재 기관총 반복음과 락온 단계·릴리즈·부스트 효과음은 Listener 제어를 따르며, 런타임에 나중에 생성되는 일반 AudioSource도 별도 예외 설정이 없으면 자동으로 Sound 상태를 따른다.
- BattleArena HUD는 기존 `MUSIC ON/OFF` 오른쪽에 `SOUND ON/OFF` 버튼을 표시한다. 두 버튼은 각각의 전역 이벤트를 받아 상태 문구와 색상을 동기화하며, 각 버튼 자체 크기의 배경만 사용한다.
- 두 오디오 버튼 뒤에 있던 740×132 크기의 공용 디버그 패널 배경 Image는 비활성화했다. 디버그 글자는 유지하지만 배경과 글자 모두 UI raycast를 받지 않으므로 위쪽 환경 디버그의 `Undead` 버튼을 가리거나 입력을 차단하지 않는다.
- 향후 효과음을 추가할 때 `ignoreListenerVolume`을 켜지 않는다. 새 BGM만 `GlobalMusicSource`로 등록해 Sound 제어에서 제외한다.

### 2.5 메인 메뉴 배경 레이어

상태: **구현됨**

`MainMenu`의 전체 화면 배경은 `MenuPresenter`가 런타임에 아래 순서로 생성한다. 목록의 뒤쪽 레이어일수록 화면 앞에 그려진다.

```text
MainSky → MainCloud → MainBackground → MainHellicopter → Muffler → MainCharacter
```

- `Muffler`는 `MainCharacter` 바로 뒤에 배치한다. 따라서 머플러 애니메이션이 캐릭터 몸과 머리카락보다 앞을 덮지 않는다.
- 머플러 프레임 애니메이션과 12 FPS 재생 속도는 그대로 유지하며, 이번 변경은 형제 순서만 바꾼다.

### 2.6 전투 배경 아군 공중 부대

상태: **구현됨**

BattleArena의 `BattleArenaRoot/AmbientAllyArmyRoot` 아래에서 순수 시각 연출용 공중 아군 부대를 생성한다.

| 항목 | 현재 기준 |
| --- | --- |
| 기본 구성 | 단독기 1대 + 편대장/좌우 편대원 3대, 총 4대 |
| 모델 | `BackgroundChopper_500.prefab`, 1대당 500 tris |
| 순찰 | Base 카메라 right/up/forward 기준 X 11m / Y 5m 타원, Up +5.5m, 깊이 -2m, 28초 주기 |
| 편대 | 1.5m 후행, 좌우 0.85m, 0.28초 SmoothDamp, 최대 8° 뱅크 |
| 자세 | 이동 경로의 상하 변화는 유지하되 기수는 월드 수평을 기준으로 계산. 최대 피치 7°로 제한 |
| 모델 전방 | VisualRoot Y=-90°로 기수와 런타임 +Z 이동 방향을 일치시킴 |
| 로터 | 단일 메시 위에 Main/Tail 블러 평면을 추가하고 관리자에서 회전 |
| 가짜 공격 | 9~16초 간격, 38% 시도, 한 번에 한 그룹, 2~4발 트레이서. 공격 기동 속도 배율 0.5 |
| 순찰 개틀링 | 기체별 0.55~0.9초 버스트, 0.075~0.11초 화염 간격, 1.25~2.2초 쿨타임 |
| 랜덤 추락 | 20~34초 간격, 동시에 1대, 연기·중력·240~420°/s 자회전, 4~7초 후 재보충 |
| VFX 풀 | LineRenderer 12개 고정 풀, 트레이서 수명 0.18초 |
| 렌더 비용 정책 | shared Mesh/Material, GPU Instancing, 헬기 그림자 Off, Collider/Rigidbody 없음 |

가짜 공격은 `BattleController.TryHitBoss`, 실제 `ProjectileController`, 피해 숫자, 보스 피격 틴트, 최근 공격 타깃 기록을 사용하지 않는다. 런타임 격리 검증에서 공격 전후 보스 HP 2000, 플레이어 Hull/Armor 100/120, 락온 유효 타깃 5개가 유지됐다.

전투가 비활성화되거나 보스가 사망하면 새 가짜 공격을 시작하지 않고 활성 트레이서를 정리하며 순찰만 유지한다. 배경 헬기는 PlayerVisual 오버레이 레이어에 넣지 않고 Base 카메라 월드 연출로 표시한다.

### 2.7 전투 배경 아군 지상 부대

상태: **프로토타입 구현됨**

`BattleArenaRoot/AmbientAllyArmyRoot/GroundArmoredUnits` 아래에서 탱크 3대, 대형 개틀링건 차량 3대, 박격포 차량 2대의 기본 8대 편성을 생성한다. 차량은 StageVisualRoot 로컬 Catmull-Rom 폐회로 3개를 누적 거리로 샘플링하며, 3대 종대와 지원 2대, 독립 차량 3대가 서로 다른 위상·속도로 움직인다.

| 항목 | 현재 기준 |
| --- | --- |
| 모델 | 탱크 800 tris / 개틀링 779 tris / 박격포 820 tris, 기본 8대 합계 6,397 tris |
| 화면 크기 | 1280×720 배치 실행에서 폭 약 17.4~48.3px |
| 접지 | StageVisualRoot 로컬 Y 0.12~0.20, 런타임 Raycast 없음 |
| 조준 | `TurretYawPivot`, `BarrelPitchPivot`, `Muzzle` 사용 |
| 이동 | 폐회로 3개, 종대는 리더 거리에서 1.45/2.95m 후행 |
| 공격 | 탱크·개틀링 이동 사격, 세 차종 감속·정차·조준·사격·회복·재가속, 0.075~0.19초 간헐적 포구 화염 |
| 공용 예산 | 공중·지상 Primary 최대 1개, 지상 Ambient 최대 2개 |
| VFX | 트레이서 16, 박격포 아크 6, 폭발 링 8 고정 풀; Muzzle 교차 Quad 화염은 발사마다 맥동 후 자동 소등 |
| 물리/피해 | Collider·Rigidbody 0, 실제 Projectile·피해 API 호출 0 |

배치 Play Mode 격리 검증에서 기본 8대 생성·이동·접지와 강제 Primary 사격을 확인했다. 사격 전후 보스 HP 2000, 플레이어 Hull/Armor 100/120, 락온 유효 타깃 5가 유지됐다. 7대/10대 프리셋, 각 차종별 강제 공격 육안 승인, 공중·지상 동시 과밀도, 20:9/4:3, 10분 Profiler soak와 모바일 실기기는 아직 미검증이다. 세부 구현 상태는 [지상 장갑차 개발계획서](../background-ground-armored-units-development-plan.md)를 따른다.

## 3. 플레이어 조작과 이동

상태: **구현됨**

| 행동 | PC 입력 | 모바일/HUD | 비고 |
| --- | --- | --- | --- |
| 수평 이동 | `A/D`, 좌/우 방향키 | 현재 별도 입력 없음 | 기본 7.2 / 락온 충전 중 4.32 |
| 수직 이동 | `W/S`, 위/아래 방향키 | 현재 별도 입력 없음 | 기본 7.2 / 락온 충전 중 4.32 |
| 기관총 | 입력 없음 | 입력 없음 | 2초 자동 발사 → 2초 휴지 반복 |
| 락온 충전 | 마우스 오른쪽 버튼 누르기 유지 | `LOCK ON` 버튼 누르기 유지 | 양쪽 모두 단계별 1/1.5/2/2.5/3초·풀차지 10초 |
| 락온 발사 | 마우스 오른쪽 버튼 놓기 | `LOCK ON` 버튼 놓기 | 성공한 락 수 기준 발사 |

- 과거의 우클릭 즉시 5단계 테스트 옵션 `forceFullChargeOnMouseRightForTesting`은 코드는 보존하지만 기본값을 OFF로 복구했다. 따라서 우클릭을 누른 직후 성공 락은 0개이며 정상 시간만큼 유지해야 락이 증가한다.
- 과거의 3·4락을 5락 실행 프로필로 승격하던 테스트 옵션 `promoteThreeOrMoreLocksToFullSalvoForTesting`은 코드만 보존하고 기본값을 OFF로 복구했다. 현재 실행 매핑은 원본 `1→1`, `2→2`, `3→3`, `4→4`, `5→5`다.

이동 속도 공식:

```text
현재 기본 이동 속도 = 이전 기본값 8 × 0.9 = 7.2
락온 충전 중 이동 속도 = 7.2 × 0.6 = 4.32
```

- 여기서 `락온 중`은 우클릭 또는 모바일 버튼을 누르고 락을 획득하는 `Charging` 상태만 뜻한다.
- 입력을 놓아 발사가 승인되거나, 피격·포인터 이탈·일시정지 등으로 충전이 취소되는 즉시 배율은 `1.0`으로 돌아가 기본 속도 7.2를 사용한다.
- `Release`, 미사일 발사 중, `ReuseWait`에서는 추가 감속하지 않는다.
- 기존 씬에 직렬화된 이전 기본값 8은 런타임 초기화에서 정확히 7.2로 이전하며, `PlayerRuntimeState` 기본값도 7.2를 사용한다. 디버그 패널에서 명시적으로 지정한 이동값은 런타임 오버라이드로 우선 적용된다.

이동 구현 기준:

- 헬기 이동 앵커는 카메라 깊이가 고정된 2D 평면에서 움직인다.
- 새 전투 세션에서 `PlayerOrbitController.Configure`가 호출되면 이전 승리·패배 흐름에서 남을 수 있는 입력 잠금을 해제하고 이동 입력을 활성 상태로 초기화한다. 이후 현재 전투의 승리·패배 처리만 다시 입력을 잠글 수 있다.
- `A/D/W/S`와 방향키 이동 처리는 보스 중심·주시 대상 참조의 존재 여부와 분리되어 있다. 보스 참조가 누락되거나 초기화 순서 때문에 잠시 비어 있어도 플레이어 이동은 계속 처리하며, 해당 참조는 거리 계산과 시각 자세의 대체 기준에만 사용한다.
- 초기 화면 위치는 viewport `(0.28, 0.50)`이다.
- 최종 전투 구성이 완료된 최초 1회에 실제 Base 게임 카메라의 `pixelWidth`, `pixelHeight`, `aspect`를 저장한다. 이동 영역은 정규화된 **전체 viewport `(0,0)~(1,1)`**이며 기본 가장자리 패딩은 0px이다.
- 같은 고정 카메라 깊이에서 viewport 네 모서리를 `ViewportToWorldPoint`로 변환하므로 모바일 기기마다 화면비가 달라도 실제 월드 이동 폭·높이가 해당 게임플레이 화면에 맞게 계산된다. 매 프레임 재계산하지 않고 외형/카메라 구성을 명시적으로 새로 고친 경우에만 재초기화한다.
- `PlayerMovementBounds`와 런타임 이동 가이드는 최초 계산된 카메라 viewport 네 모서리에 맞춰 동기화된다.
- 헬기 Renderer 크기로 이동 범위를 줄이지 않는다. 확대된 헬기의 이동 기준점은 화면 끝까지 갈 수 있고 이때 로터나 기체 일부가 화면 밖으로 나가는 것은 현재 허용 규칙이다. 추후 여백이 필요하면 공용 픽셀 패딩을 사용한다.
- 확대된 3D 모델의 화면 투영 사각형 중심을 이동 앵커에 맞춰, 3D AABB의 원근 차이 때문에 외형이 한쪽으로 치우치지 않게 한다. 이 중심 계산에는 기체의 안정된 Mesh Renderer만 사용하며 매 프레임 Bounds가 변하는 Particle/Trail/Line Renderer와 분리·복귀하는 장착 Sidewinder 2발은 제외한다. 불꽃과 Sidewinder 자체는 계속 표시된다.
- 현재 보이는 헬기는 복제 모델이 아니다. `PlayerVisualRoot` 아래 실제 헬기 Renderer를 `PlayerVisual` 레이어로 옮기고 평상시에는 Base 카메라와 자세·투영이 같은 전용 오버레이 카메라 스택에서 그린다. 일제사격 카메라 진동 중에는 오버레이 카메라만 진동 직전 자세와 투영을 유지한다. 이동 앵커와 피격 Collider는 이 시각 레이어에서 분리된다.
- 일제사격 카메라 진동 중 플레이어의 화면 평면 좌표 변환은 진동 전 오버레이 카메라를 사용한다. 이미 이동 viewport와 고정 깊이 안에 있는 좌표는 카메라를 왕복 변환하지 않는다. 따라서 투영 진동이나 동시에 발생한 월드 카메라 위치 진동이 이동 앵커·피격 판정의 월드 좌표로 되먹임되지 않는다.
- 보이는 헬기 모델만 이동 방향으로 최대 12도, 약 0.18초 동안 기울어진다. 이동 앵커와 피격 판정은 기울지 않는다.
- 실제 5락 일제사격은 릴리즈와 첫 미사일 웨이브가 시작되는 순간, 보이는 헬기 모델만 현재 자세에서 **기존 카메라 정면 자세를 게임 카메라 Up 축 기준 정확히 180도 반전한 목표 자세**로 회전하기 시작한다. 1~4락에서는 이 회전을 실행하지 않는다. 회전은 각도별 키프레임이 아니라 `0.3초`의 정규화된 시간에 따라 `Quaternion.Slerp`와 `SmoothStep`으로 매 프레임 보간한다.
- 실제 5락에서는 회전 애니메이션과 30발 전투 미사일 발사, 약한 월드 카메라 진동이 동시에 시작한다. 실제 장착 Sidewinder 2발은 0.3초 회전이 완전히 끝난 뒤에만 후방 불꽃을 켜고, 장착 상태로 1초간 분사한 뒤 같은 프레임에 분리된다. 이 분리 시점을 5락 풀살보의 시각적 발사·무적·카메라 진동 완료로 사용하고, 1초 더 목표 자세를 유지한 뒤 `0.3초` 동안 `Quaternion.Slerp`와 `SmoothStep`으로 매 프레임 보간해 평상시의 카메라 우측 측면 자세로 복귀한다. 이 복귀는 보이는 외형에만 적용되며 이동·입력·피격 판정·발사·무적·마커·재사용 대기에는 영향을 주지 않는다. 30발 전투 미사일의 기존 약 0.6초 발사 흐름은 독립적으로 먼저 끝난다. 이동 앵커·화면 위치·피격 판정과 헬기 시각 루트 자체에는 진동을 적용하지 않는다.
- `forwardSpeed = 10` 값은 남아 있으나 현재 키 입력은 수평/수직 2축만 만든다. 이번 10% 감속 대상은 실제 입력에 쓰이는 두 축이며, 전진값 10과 이를 보조 기준으로 사용하는 추적 빔 속도는 변경하지 않았다.
- 공기압 회전 연출 중에는 기관총 발사가 잠시 차단된다. 현재 활성 패턴 4종에는 공기압 패턴이 없다.

## 4. 플레이어 공격 시스템

### 4.1 일반 기관총

상태: **구현됨**

| 항목 | 현재값 | 구분 |
| --- | ---: | --- |
| 1발 피해량 | 3 | 원본값 |
| 발사 간격 | 0.15초 | 원본값 |
| 자동 발사 구간 | 2초 | 원본값 |
| 자동 휴지 구간 | 2초 | 원본값 |
| 전체 반복 주기 | 4초 | 계산값 |
| 발사 구간 초당 발사 수 | 약 6.667발 | 계산값 |
| 발사 구간 이론 피해량 | 20 DPS | 계산값, 전탄 명중 기준 |
| 4초 주기 이론 평균 피해량 | 10 DPS | 계산값, 50% 발사 비율·전탄 명중 기준 |
| 탄속 | 60 | 원본값 |
| 탄 수명 | 4초 | 원본값 |
| 조준 대상 | 보스 `AimPoint` | 구현 규칙 |
| 크리티컬 | 없음 | 현재 경로 |

피해 공식:

```text
기관총 발사 구간 이론 DPS = 1발 피해량 / 발사 간격
                           = 3 / 0.15
                           = 20

4초 주기 이론 평균 DPS = 발사 구간 DPS × 발사 시간 / 전체 주기
                       = 20 × 2 / (2 + 2)
                       = 10
```

행동 규칙:

- 전투가 활성화되고 플레이어와 보스가 살아 있으면 입력 없이 즉시 2초 발사 구간으로 시작한다.
- 2초 발사 구간에는 0.15초 간격으로 발사하고, 다음 2초에는 완전히 쉬며, 이후 같은 4초 주기를 반복한다.
- 왼쪽 클릭과 `Space`는 더 이상 기관총 입력으로 사용하지 않는다. 마우스가 UI 위에 있는지도 기관총 발사 여부에 영향을 주지 않는다.
- 일시정지 중에는 주기 진행과 발사 음향을 멈춘다. 전투 종료 또는 플레이어/보스 사망 시 발사를 멈추고 다음 전투 활성화 때 새 2초 발사 구간부터 시작한다.
- 기존 공기압 회전 무기 잠금 중에는 실제 탄과 발사 음향을 차단하지만 자동 주기 시간은 계속 흐른다. 현재 활성 보스 패턴에는 이 잠금 경로를 호출하는 패턴이 없다.
- 탄은 과거처럼 클릭한 세부 조준점을 선택하지 않고 보스의 고정 `AimPoint`로 향한다.
- 현재 피해 처리에는 확률 크리티컬이나 조준 부위별 배율이 없다.
- 락온 미사일 일제사격 중에도 이동과 기관총 사격은 계속 가능하다.

### 4.2 락온 미사일 일제사격

상태: **구현됨**, 약점 개방 흐름 일부는 **프로토타입/디버그**

### 입력 상태 흐름

```text
Ready
  └─ PC 우클릭/모바일 LOCK ON 누름 → Charging + 정상 시간 충전
       ├─ 5개 락 완료 + 입력 유지 → Charging 유지
       ├─ 입력 놓음 + 성공 락 1~4개 → 해당 단계 Release/Salvo → ReuseWait
       ├─ 입력 놓음 + 성공 락 5개 → 5락 풀살보 Release/Salvo → ReuseWait
       ├─ 입력 놓음 + 성공 락 0개 → 취소 → Ready
       ├─ 플레이어에게 실제 피해 적용 → 강제 취소 → Ready
       └─ 유효 타깃이 모두 사라진 상태가 1.25초 지속 → 취소 → Ready

ReuseWait 5초 종료 → Ready
```

- 각 단계가 개별적으로 요구하는 시간은 순서대로 `1 / 1.5 / 2 / 2.5 / 3초`다. 누적 락 획득 시점은 `1 / 2.5 / 4.5 / 7 / 10초`이며 풀차지는 10초다. PC 우클릭과 모바일 `LOCK ON` 모두 같은 정상 시간 흐름을 사용한다.
- 입력 직후 자동으로 성공 락을 채우지 않는다. 1초가 되기 전에 입력을 놓으면 성공 락 0개로 취소되고 미사일·일제사격 무적·재사용 대기가 발생하지 않는다.
- 입력을 누르는 즉시 첫 충전 타깃을 확정해 마커를 표시한다. 이 마커는 아직 성공 락·발사 타깃 수에 포함하지 않는다.
- 충전 중인 현재 타깃 마커 하나만 알파 점멸과 스케일 왕복을 반복한다. 단계 완료 시 해당 마커는 즉시 고정되고, 다음 타깃 마커가 나타나 점멸을 이어간다.
- 5개 락이 모두 완료되면 다섯 마커는 모두 고정된다. 입력을 계속 누르는 동안 `Charging` 상태, 충전 중 이동 속도 4.32, 피격 시 전체 취소 규칙을 그대로 유지하며 락온 미사일은 자동 발사하지 않는다.
- 발사 단계는 성공적으로 획득한 고유 락 수로 결정된다. 현재는 실제 락 수와 실행 프로필이 항상 같으며 5락 전용 연출은 실제 성공 락이 5개일 때만 실행한다.
- 한 번의 충전에서 같은 타깃을 중복 락하지 않으며 최대 5개 타깃을 잡는다.
- 정상 발사가 승인되는 순간 5초 재사용 대기시간이 시작된다. 미사일 비행이나 명중 완료 시점과 무관하다.
- 한 번에 하나의 일제사격만 준비/실행할 수 있다.
- 모바일 `LOCK ON` 버튼을 누른 채 포인터가 버튼 밖으로 나가면 현재 충전은 취소된다.
- `Charging` 중 일반 피해 또는 연속 피해가 Hull/Armor 처리 경로에 실제로 적용되면 현재 충전, 성공 락, 월드 마커, 활성 입력 소스를 즉시 초기화한다.
- `Charging` 동안 플레이어 이동은 기본 7.2에 `0.6` 배율을 적용해 4.32가 된다. 충전 종료와 동시에 기본 속도로 복구한다.
- 피격 취소 뒤 같은 우클릭이나 모바일 포인터를 놓는 동작은 발사 요청으로 인정하지 않는다. 미사일, 일제사격 무적, 5초 재사용 대기, `SHOOT ERROR`도 발생하지 않는다.
- 기존 피격 무적, 일제사격 무적, `Undead` 디버그로 차단되어 생존 수치에 반영되지 않은 피해 시도는 새 피격으로 보지 않으며 충전을 취소하지 않는다.
- 이 강제 취소 규칙은 `Charging`에만 적용된다. 이미 발사가 승인된 일제사격과 `ReuseWait`는 이후 피격으로 취소하지 않는다.

### 충전 및 피해 표

락온 미사일의 원본 단계 프로필은 개틀링 피해와 무관한 고정값을 사용한다. 아래 배열과 계산 코드는 삭제하거나 덮어쓰지 않고 그대로 보존한다.

```text
원본 단계 총 기본 피해 = 실행 프로필에 대응하는 고정값 [9, 20, 35, 60, 100]
미사일 1발 기본 피해 = 단계 총 기본 피해 ÷ 발사 수
```

| 원본 실행 프로필 | 최소 충전 시간 | 발사 수 | 총 기본 피해 | 1발 기본 피해 |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 1.00초 | 5 | 9 | 1.8 |
| 2 | 2.50초 | 10 | 20 | 2 |
| 3 | 4.50초 | 15 | 35 | 약 2.333 |
| 4 | 7.00초 | 20 | 60 | 3 |
| 5 | 10.00초 | 30 | 100 | 약 3.333 |

현재 `promoteThreeOrMoreLocksToFullSalvoForTesting = false`이며 실제 실행 매핑은 위 원본 표와 동일한 `1→1, 2→2, 3→3, 4→4, 5→5`다. 과거 승격 코드는 테스트 이력용으로 남아 있지만 기본 실행 경로에서는 비활성이다.

- 3락은 3개의 실제 타깃에 15발·기본 피해 35를, 4락은 4개의 실제 타깃에 20발·기본 피해 60을 분배한다.
- 5락만 5개의 실제 타깃에 30발·기본 피해 100을 분배하고 장착 Sidewinder·헬기 회전 등 풀살보 연출을 실행한다.
- 우클릭 즉시 충전은 꺼져 있으므로 위 누적 충전 시간은 PC와 모바일 모두 실제로 필요하다.

피해는 발사 승인 시점에 스냅샷으로 고정된다.

표의 발사 수는 모두 피해 판정을 가진 **전투 미사일 수**다. 실제 5락에서만 추가로 분리되는 장착 Sidewinder 2발은 30발과 별개이고 피해가 항상 0이므로 5락 총 기본 피해는 계속 100이다.

- 일반 부위 배율: `1.0`
- 개방된 약점 부위 배율: `2.0`
- 약점으로 배정된 **각 미사일**의 피해만 2배가 된다.
- 실제 성공 락 수가 2개 이상이면 실제 타깃별 발사 수 차이가 최대 1발이 되도록 균등 분배한다.
- 개틀링 기본 피해, 개틀링 피해 업그레이드, 개틀링 발사 주기는 락온 미사일 총피해에 영향을 주지 않는다.

### 타깃 선택

타깃 선택은 다음 특성을 우선 그룹으로 사용하고, 같은 그룹 안에서는 가중치 기반으로 선택한다.

1. 개방된 약점
2. 강공격 준비와 연관된 부위
3. 최근 2초 안에 공격받은 부위
4. 화면에 보이는 부위
5. 대형 부위
6. 그 밖의 유효 부위

현재 별도 작성 타깃이 없을 때 생성되는 런타임 프로토타입 타깃은 아래 6개다.

| ID | 표시/부위 | 우선도 | 속성 | 현재 기본 상태 |
| --- | --- | ---: | --- | --- |
| `boss.core` | Core / Spine 02 | 90 | 대형 | 선택 가능 |
| `boss.head_weak` | HeadWeakPoint / Head | 120 | 약점 | 닫힘, 선택 불가 |
| `boss.left_upper` | LeftUpper / Clavicle L | 75 | 대형 | 선택 가능 |
| `boss.right_upper` | RightUpper / Clavicle R | 75 | 대형 | 선택 가능 |
| `boss.lower` | LowerBody / Pelvis | 60 | 대형 | 선택 가능 |
| `boss.tail_base` | TailBase / Tail001 | 55 | 대형 | 선택 가능 |

`BossTestState`의 약점 개방과 강공격 준비 상태는 현재 에디터 디버그 메뉴에서만 변경된다. 실제 보스 패턴이나 체력 단계가 약점을 개방하는 게임플레이 흐름은 아직 연결되지 않았다.

- 약점이 닫혀 있어도 일반 타깃 5개가 유효하므로 현재 기본 상태에서 5단계 락은 가능하다.
- 보스에 피해가 들어갈 때 명중점에서 가장 가까운 락온 타깃이 2초간 `최근 공격받음`으로 표시된다. 따라서 기관총 명중 위치가 다음 락온 순서에 영향을 줄 수 있다.

### 일제사격 발사와 비행

| 항목 | 현재값/규칙 |
| --- | --- |
| 동시 발사 단위 | 한 웨이브당 최대 4발 |
| 전체 발사 구간 | 약 0.6초 |
| 5락 프로필 자세 회전 시간 | 0.3초, 시간 기준 `Quaternion.Slerp` + `SmoothStep` 보간. 실제 5락에서만 실행 |
| 5락 프로필 자세 복귀 시간 | Sidewinder 분리 뒤 목표 자세 1초 유지 후 0.3초, 시간 기준 `Quaternion.Slerp` + `SmoothStep` 보간. 정상 완료한 실제 5락에서만 실행 |
| 5락 프로필 장착 Sidewinder 점화 | 헬기 0.3초 회전 완료 후 장착 상태 1초 분사, 좌·우 2발 동시 분리. 실제 5락에서만 실행 |
| 연출용 Sidewinder 비행값 | 분리 속도 5를 0.5초 유지 → 가속도 20으로 순항 35까지 상승 / 회전 속도 초당 180도 |
| 발사대 | 좌/우 발사대 교대 |
| 풀 용량 | 40발 고정 풀, 현재 최대 요청 30발 |
| 전투 미사일 외형 | 검은 원통 몸체 + 진행 방향을 향한 원뿔형 기수. 기존 Sidewinder 복제 외형은 사용하지 않음 |
| 분산 전개 | 0.28초, 전개 거리 5.5 |
| 분산 전개 방향 계수 | 수평 1.0 / 수직 0.65 |
| 아크 비행 | 0.75초 ± 0.18초 |
| 아크 반경 | 수평 10 / 수직 7 |
| 발사 속도 | 18 |
| 순항 속도 | 72 |
| 가속도 | 130 |
| 회전 속도 | 초당 280도 |
| 종말 유도 진입 거리 | 8 |
| 수명 | 6초 |
| 명중 반경 | 1.8 |
| 타깃 소실 | 재탐색하지 않고 마지막 진행 방향으로 비행 후 소멸 |
| 5락 프로필 헬기 자세 | 릴리즈·첫 웨이브와 함께 0.3초 회전 시작 → 회전 완료 후 Sidewinder 1초 분사 → 분리 시점부터 1초 유지 → 0.3초 보간으로 평상시 측면 자세 복귀 |

목표점에는 수평 1.6, 수직 배율 1.25, 깊이 0.2의 분산을 주어 동일 지점에 모든 미사일이 겹치지 않게 한다.

5락 실행 프로필의 자세 반전 신호는 발사 준비와 일제사격 무적 확보가 성공한 뒤, 실제 `StartPreparedSalvo` 호출보다 먼저 발생한다. 이 신호가 현재 표시 회전을 시작값으로 저장하고 0.3초 보간을 시작하며, 곧바로 첫 웨이브도 실행된다. 정상 완료에서는 기존 1초 유지 뒤 별도의 0.3초 시각 복귀 보간을 실행한다. 준비 후 실제 살보 시작이 거부되거나 취소되거나 컴포넌트가 비활성화되면 안전 처리를 위해 진행 중인 회전·복귀 코루틴과 반전 자세를 즉시 해제한다.

### 5락 실행 프로필 장착 Sidewinder 시각 연출

상태: **구현됨**

아래 연출은 실제 성공 락이 5개인 릴리즈에서만 실행한다. 1~4락은 장착 Sidewinder를 점화·분리하지 않고 헬기 풀살보 자세로 회전하지 않는다.

- Viper 좌·우 외곽 파일런 `WeponePylon_L_03`, `WeponePylon_R_03`에 실제로 장착된 Sidewinder 각 1발만 사용한다. 검은 AGM과 로켓 포드는 변경하지 않는다.
- 릴리즈 프레임에는 두 Sidewinder의 불꽃을 아직 켜지 않는다. 헬기 표시 자세의 0.3초 회전 진행률이 1.0이 된 것을 확인한 뒤 두 `FX_Nozzle`에서 밝은 황색·주황색 후방 불꽃과 짧은 궤적을 켠다. 장착 상태 분사를 1초 유지하고 두 발을 같은 프레임에 분리한다.
- 분리된 두 발은 락온 스냅샷의 앞쪽 두 타깃을 향해 동시에 유도 비행한다. 처음 0.5초는 속도 5로 천천히 진행하고, 이후 초당 20씩 가속해 최대 순항 속도 35에 도달한다. 회전 속도는 초당 180도다. 전용 오버레이 렌더러에 외부 시각 루트로 등록하므로 건물이나 월드 지형의 깊이에 가려지지 않는다.
- 이 두 발은 `BattleController.TryHitBoss`나 다른 피해 API를 호출하지 않는 순수 시각 연출이다. 전투 풀 40발, 원본 단계별 전투 미사일 수 `5/10/15/20/30`, 5락 실행 프로필 총 기본 피해 100에 포함되지 않는다.
- 각 Sidewinder는 타깃 반경 1.8 안에 도달하면 즉시 자기 원래 파일런의 로컬 위치·회전·스케일로 복귀한다. 타깃이 사라지면 릴리즈 때 저장한 위치로 계속 날아가며, 명중하지 못해도 분리 후 최대 6초에 강제 복귀한다.
- 새 5락 실행 프로필 발사가 시작될 때 이전 연출용 Sidewinder가 아직 비행 중이면 먼저 원래 장착점으로 복귀시킨 뒤 새 연출을 시작한다.

### 일제사격 중 무적

- 실제 1~4락은 발사가 정상 준비된 직후부터 마지막 전투 미사일 웨이브가 끝날 때까지 약 0.6초간 무적이다.
- 실제 5락은 발사 승인 직후부터 `0.3초 헬기 회전 + 1초 장착 분사` 뒤 실제 Sidewinder 2발이 분리되는 약 1.3초까지 무적이다. 30발 전투 미사일 발사는 약 0.6초에 먼저 끝나지만, 그 완료 이벤트가 Sidewinder 점화·무적·마커 완료를 조기에 끝내지 않는다. 무적은 고정 추정 시간이 아니라 실제 두 발의 분리 이벤트에서 끝나며, 분리 뒤 연출용 Sidewinder가 비행하는 시간에는 무적이 없다.
- 어느 단계도 5초 재사용 대기 전체가 무적인 것은 아니다.
- 일반 투사체 피해와 추적 잔류 빔의 연속 피해를 모두 차단한다.
- 무적 시작 시 연속 피해 틱 누적값도 초기화해 무적 종료 직후 밀린 피해가 한꺼번에 들어오지 않는다.
- 이동, 기관총, 보스 행동, 보스 투사체는 멈추지 않는다. 순간이동, 화면 정지, 시네마틱 연출도 없다.

### 락온 피드백

- HUD 버튼: `LOCK ON`, `HOLD`, `RELEASE n/5`, 남은 재사용 시간, `NO TARGET`
- 월드 락온 마커: 입력 시작 즉시 현재 충전 타깃 1개를 표시하고, 현재 타깃만 0.28초 반주기의 알파 점멸(`0.32~1.0`)과 최대 42% 스케일업/다운을 반복
- 단계 완료 마커: 점멸을 즉시 멈추고 고정하며, 다음 단계 타깃 마커가 새로 점멸. 풀차지 중에는 5개 모두 고정
- 발사 후 마커: 실제 1~4락은 마지막 전투 미사일 웨이브 완료, 실제 5락은 장착 Sidewinder 2발 분리 완료를 기준으로 그 시점부터 약 1초 뒤 제거. 마커 개수는 실제 획득 수를 유지
- 충전 중 피격 취소: 획득한 월드 마커를 즉시 모두 제거
- 충전 게이지와 발사 오류 표시(`SHOOT ERROR`)
- 실제 1~4락은 일제사격 무적과 전투 미사일 발사를 그대로 사용하지만 Base 카메라 투영 진동을 시작하지 않는다. 실제 5락 릴리즈가 성공해 풀살보 이벤트가 발생한 때만 진동을 시작하고, 헬기 회전과 장착 Sidewinder 분사 동안 유지한 뒤 두 대형 Sidewinder가 분리되어 `SalvoInvincibilityEnded`가 발생하는 같은 프레임에 원래 투영을 복구한다. 5락 기본 진폭은 `0.0075`이며 현재 육안 판별용 **프로토타입/디버그** 상수 `TemporaryCameraShakeVisibilityTestMultiplier = 8`을 적용해 유효 진폭은 `0.0600`이다. Full HD 이론상 최대 이동은 가로 57.6px·세로 32.4px이고, 최신 BattleArena 5락 고정 월드 지점 관측 피크는 53.84px였다. 육안 확인이 끝나면 배율 하나를 `1`로 되돌릴 수 있다.
- 진동은 월드 카메라에만 적용한다. `PlayerVisualOverlayRenderer`는 진동 전 카메라 자세와 투영을 모두 유지하고, 플레이어 이동 평면도 이 안정된 카메라를 기준으로 계산한다. Sidewinder 분사 Particle/Trail과 분리 가능한 Sidewinder Mesh는 헬기 중심 보정 Bounds에서 제외하므로 효과의 성장·분리·복귀가 기체를 움직이지 않는다. 헬기·이동 앵커·피격 판정·HUD는 고정되고 보스·배경·월드 전투 미사일만 현재 테스트 배율에 따라 크게 흔들린다.

### 4.3 현재 사용하지 않는 플레이어 공격 규칙

아래 내용은 과거 문서나 일부 직렬화 데이터에 남아 있어도 현재 전투 기준이 아니다.

| 항목 | 현재 상태 |
| --- | --- |
| 별도 단발 미사일 버튼과 2.6초 쿨다운 | 레거시/비활성 |
| 고정 미사일 피해 150 | 현재 락온 피해 공식에서 사용하지 않음 |
| 전투 미사일의 `ViperSidewinderProjectile` 외형 참조 | 직렬화 참조는 남아 있으나 현재 1~5단계 전투 발사에서는 무시하고 검은 원통+원뿔 외형을 생성 |
| 필살기 버튼과 필살기 전용 공격 | 레거시/비활성 |
| 조준점 클릭 선택 | 레거시/비활성 |
| 5% 확률, 20% 추가 피해 등의 확률 크리티컬 | 레거시/비활성 |

`PlayerRuntimeState.MissileDamage`와 미사일 업그레이드 수치가 남아 있지만 현재 `PlayerLockOnController`/`PlayerMissileSalvoLauncher`의 피해 계산에는 연결되지 않는다.

## 5. 플레이어 체력, 아머, 피격 규칙

상태: **구현됨**

현재 `VehiclePlayerStateCatalog`의 폴백과 등록 차량 10종은 모두 같은 생존 수치를 사용한다.

| 항목 | 현재값 |
| --- | ---: |
| 최대 Hull | 100 |
| 최대 Armor | 120 |
| Armor 수리 시작 지연 | 마지막 피격 후 2.5초 |
| Armor 수리 속도 | 초당 8 |
| Armor 파손 회복 기준 | 36 Armor |
| Armor 파손 중 Hull 피해 배율 | 1.25배 |
| 일반 피격 무적 | 1초 |
| 연속 피해 | 일반 피격 무적을 우회하고 자체 틱 사용 |
| 플레이어 패배 조건 | Hull 0 |

### 5.1 피해 처리 순서

```text
피격
 ├─ 일제사격 무적 중 → 피해 0
 ├─ 일반 피해 + 피격 무적 중 → 피해 0
 └─ 유효 피해
      ├─ Armor가 정상 상태 → Armor부터 차감
      │    └─ 이번 피해로 Armor가 깨지면 남은 피해 × 1.25를 Hull에 적용
      ├─ Armor가 파손 상태 → 피해 × 1.25를 Hull에 적용
      └─ 락온 Charging 중이면 충전·락·마커 강제 취소
```

- 모든 유효 피격은 Armor 수리 대기시간을 다시 2.5초로 설정한다.
- `PlayerCombatController`는 위 유효 피해 처리가 끝난 직후 `DamageApplied` 이벤트를 보낸다. `PlayerLockOnController`는 이 이벤트를 받아 현재 상태가 `Charging`일 때만 `PlayerDamaged` 사유로 취소한다.
- 수리는 Armor만 회복하고 Hull은 회복하지 않는다.
- Armor가 수리 중이더라도 36 미만이면 여전히 파손 상태이므로 Hull 피해 1.25배 규칙이 유지된다.
- Armor가 36 이상이 되는 순간 파손 상태가 해제되고 이후 피해는 다시 Armor가 먼저 받는다.
- 에디터/개발 디버그의 `Undead` 플래그가 켜져 있으면 생존 수치가 줄지 않는다. 이는 정상 밸런스 규칙이 아니다.

예시 — Hull 100 / Armor 120에서 150 피해:

```text
Armor가 120 흡수 → Armor 0, 잔여 피해 30
파손 배율 적용 → Hull 피해 30 × 1.25 = 37.5
결과 → Hull 62.5 / Armor 0, Armor 파손 상태
```

### 5.2 업그레이드 상태

`PlayerRuntimeState`에는 아래 레벨당 증가식이 존재한다.

| 업그레이드 | 레벨당 값 | 현재 연결 상태 |
| --- | ---: | --- |
| Hull | +10 | 에디터/개발 빌드 튜닝 적용 경로 |
| Armor | +10 | 에디터/개발 빌드 튜닝 적용 경로 |
| 기관총 피해 | +2 | 개틀링에만 적용되며 락온 미사일 고정 피해표에는 영향 없음 |
| 미사일 피해 | +10 | 현재 락온 미사일 경로에서 미사용 |

확인된 자동 적용 컴포넌트는 에디터 또는 Development Build 조건으로 생성된다. 정식 빌드의 성장/저장 데이터가 전투 스탯으로 적용되는 완성 경로로 간주하지 않는다.

## 6. 보스/몬스터 공통 규칙

현재 전투의 몬스터는 단일 보스형 타이탄이다.

| 항목 | 현재값/규칙 |
| --- | --- |
| 보스 체력 | Single 2,000 / MultiPlaceholder 2,800 |
| 기본 투사체 피해 | 15 |
| 기본 투사체 속도 | 24 |
| 투사체 전역 크기 배율 | 2.5 (`BossAttackController`) |
| 빔/위험 영역 전역 크기 배율 | 2.5 (`BossBulletPatternController`) |
| 기본 공격 간격 | 체력 100%에서 1.8초 → 체력 0%에서 0.9초 |
| 첫 공격 시작 | 씬 시작 후 4초 |
| 패턴 세트 | `KaijuHeavyThreats` |
| 패턴 선택 | 직렬화된 순서대로 라운드로빈, 비활성/조건 불충족 패턴 건너뜀 |
| 승리 조건 | 보스 체력 0 |

보스는 현재 Armor나 피격 무적 없이 유효한 양수 피해를 체력에 바로 받는다. 위 두 `2.5` 크기 배율은 값은 같지만 서로 다른 데이터다. 투사체는 전자를, 빔과 위험 영역은 후자를 사용한다. 아래 패턴 피해는 플레이어 Armor와 피격 무적을 적용하기 전의 원시 피해다.

기본 공격 간격 공식:

```text
기본 공격 간격 = Lerp(0.9, 1.8, 보스 현재 체력 비율)
```

- 체력 100%: 1.8초
- 체력 65%: 약 1.485초
- 체력 50%: 1.35초
- 체력 0%: 0.9초

이 간격은 패턴이 끝난 뒤 적용된다. 따라서 실제 패턴 시작 간격은 `패턴 실행 시간 + 기본 공격 간격 × 패턴 쿨다운 배율`이다.

현재 활성 순서:

```text
보스 체력 65% 초과:
  파편 산탄 → 파편 일제사격 → 가속 횡단 빔 → 반복

보스 체력 65% 이하:
  파편 산탄 → 파편 일제사격 → 가속 횡단 빔 → 추적 잔류 빔 → 반복
```

## 7. 현재 활성 보스 공격 패턴

### 7.1 파편 산탄 — Debris Fragment Scatter

상태: **구현됨**, 모든 체력 구간

| 항목 | 현재값 |
| --- | ---: |
| 발사 수 | 7~10개 |
| 발사 간격 | 0.025초 |
| 1개 피해 | 5.25 (`15 × 0.35`) |
| 속도 | 6 (`24 × 0.25`) |
| 패턴 투사체 스케일 | 0.45 |
| 전역 투사체 스케일 | 2.5 |
| 최종 생성 스케일 | 1.125 (`0.45 × 2.5`) |
| 패턴 후 쿨다운 배율 | 0.9 |

행동:

- 보스가 좌/우 발 중 하나에서 파편을 발생시킨다.
- 0.2초짜리 스톰프 동작 2회와 작은 카메라 흔들림을 사용한다.
- 3~4개는 플레이어 근처의 한 군집을 겨냥하고 나머지는 더 넓게 흩어진다.
- 군집 반경은 기본 플레이어 피격 반경 기준 약 1.4, 전체 산포는 최대 약 8.4다.
- 개별 피해는 작고, 유효 명중 시 Armor 수리 대기를 다시 시작시키는 공간 압박 역할이다. 여러 투사체가 1초 피격 무적 안에 닿으면 추가 피해는 차단된다.

### 7.2 파편 일제사격 — Debris Salvo

상태: **구현됨**, 모든 체력 구간

| 항목 | 현재값 |
| --- | ---: |
| 발사 수 | 체력 손실 1/3 미만: 2발 / 1/3 이상: 3발 / 2/3 이상: 4발 |
| 첫 발 경고 | 0.3초 |
| 발사 간격 | 0.2초 |
| 1발 피해 | 23.25 (`15 × 1.55`) |
| 초기 속도 | 6, 이후 1초 내 도달하도록 가속 |
| 조준 산포 반경 | 약 2.8 |
| 패턴 투사체 스케일 | 2.8 |
| 전역 적용 후 생성 스케일 | 7.0 |
| 접근 중 크기 보정 | 생성 스케일의 0.25배 → 1.19배 |
| 패턴 후 쿨다운 배율 | 1.05 |

행동:

- 첫 발만 0.3초 경고한 뒤 큰 파편을 순차 발사한다.
- 파편은 플레이어 현재 위치 주변의 임의 지점을 겨냥한다.
- 보스 체력이 낮을수록 한 번의 패턴에서 발사 수가 늘어난다.

### 7.3 가속 횡단 빔 — Accelerating Sweep Beam

상태: **구현됨**, 모든 체력 구간

| 항목 | 현재값 |
| --- | ---: |
| 좌/우 방향 | 매 사용 시 무작위 |
| 횡단 각도 | 직렬화값 92도는 화면 방향 계산 실패 시 폴백. 정상 화면 경로의 실제 각도는 가변 |
| 경고 시간 | 0.8초 |
| 느린 구간 | 전체 각도의 첫 20%, 0.3초 |
| 빠른 구간 | 나머지 80%, 0.2초 |
| 총 횡단 시간 | 0.5초 |
| 1회 피해 | 18 (`15 × 1.2`) |
| 기본 폭 × 전역 배율 | `0.78 × 2.5 = 1.95` |
| 빠른 구간 폭 보정 | 최대 1.18배 |
| 길이/위험 반경 × 전역 배율 | `28 × 2.5 = 70` |
| 패턴 후 쿨다운 배율 | 1.2 |

빔 접촉 중 매 프레임 일반 피해를 시도하지만 플레이어의 일반 피격 무적 1초가 적용된다. 현재 빔 횡단은 총 0.5초이므로 한 번의 접촉에서 통상 18 피해가 들어가는 구조다.

현재 코드는 카메라와 플레이어가 유효하면 플레이어의 화면 높이·깊이에서 viewport X `-0.08/1.08` 지점을 구하고, 준비 시작 시 계산한 두 방향 사이를 횡단한다. 92도를 항상 실제 횡단 각도로 해석하지 않는다. 이 화면 경로·진행률·피해 값은 이번 방향 동기화에서도 보존했다.

**구현됨 / 2026-08-29:** `KaijuMouthSocket`의 +Z를 실제 입 출사축에 맞추고, 시작/끝 방향의 Y축 부호 각도로 좌우 클립을 선택한다. 현재 정면 카메라에서는 화면 좌→우에 `BeamRightToL`, 화면 우→좌에 `BeamLeftToR`을 사용한다. Animator 평가 뒤 Neck_01/Neck_02/Head에 35%/35%/30% 보정을 적용하며 보스 루트·다리·hurtbox를 회전시키지 않는다. 보정 후 입 위치와 같은 방향을 `SweepFrame` 하나로 VFX와 일반 피해 판정에 전달한다. 직전 보정은 다음 Animator 평가 전에 복구해 누적 회전을 막는다.

준비 중에는 보정 가중치를 올리고, 활성 중에는 완전히 정렬하며, 정상 종료 후 최대 0.25초에 걸쳐 보정을 해제한다. 새 공격·취소·비활성화·사망은 즉시 해제한다. 시네마틱 일시정지는 기존처럼 진행 중인 패턴을 취소한다. `OnBeamStart`가 발사를 승인하고 누락된 이벤트로 임의 발사하지 않는다. 드라이버가 있는데 필수 소켓/본이 없으면 해당 횡단 공격을 안전하게 취소한다.

[개발계획서 §12](../kaiju-sweep-beam-alignment-development-plan.md#12-구현-및-검증-결과--2026-08-29)에 실제 구현·검증 범위를 기록했다. 44개 실제 전투 조합의 최대 입/빔 각도 오차는 0.13706°, 빔 근단과 입 위치의 최대 거리는 0.000013 월드 단위였다. 모바일 실기기 및 모든 자세의 최종 아트 승인은 별도다.

### 7.4 추적 잔류 빔 — Tracking Residual Beam

상태: **구현됨**, 보스 체력 **65% 이하에서만 활성**

| 항목 | 현재값 |
| --- | ---: |
| 충전/준비 시간 | 0.5초 |
| 추적 지속 시간 | 4초 |
| 추적 속도 | 플레이어 기준 최대 이동 속도의 40%, 현재 보통 4 |
| 연속 피해 | 3 |
| 피해 간격 | 0.2초 |
| 이론상 DPS | 15 DPS |
| 4초 최대 원시 피해 | 60 |
| 기본 폭 × 전역 배율 | `0.62 × 2.5 = 1.55` |
| 길이 | 70 |
| 패턴 후 쿨다운 배율 | 1.35 |

행동:

- 충전 후 플레이어를 제한된 속도로 4초간 따라가는 빔을 만든다.
- 일반 1초 피격 무적 대신 0.2초 연속 피해 틱을 사용한다.
- 락온 미사일 일제사격 무적 중에는 피해가 중지되고 누적 틱도 초기화된다.
- 이 패턴에 직렬화된 `damageMultiplier = 0.95`는 현재 연속 피해 경로에서 소비되지 않는다. 실제 피해는 `3 / 0.2초` 상수를 따른다.
- 직렬화된 `telegraphDuration = 0.7`, `trackingTurnRate = 28`, `beamWarmupTrackingSpeedMultiplier = 0.5`도 현재 이 패턴 실행 경로에서 사용되지 않는다. 준비 시간은 `fixedDuration = 0.5`, 활성 추적 속도는 플레이어 속도 배율 `0.4`가 결정한다.

## 8. 비활성/레거시 보스 패턴

| 항목 | 상태 | 설명 |
| --- | --- | --- |
| Pressure Sniper | 레거시/비활성 | 구현 코드/기본값 일부는 남아 있으나 현재 활성 패턴 목록에 없음 |
| Legacy Bullet Hell 세트 | 레거시/비활성 | 직렬화 구조와 코드가 남아 있으나 `KaijuHeavyThreats`가 현재 활성 |
| 공기압 회전/밀치기 경로 | 비활성 | 플레이어 대응 코드는 있으나 현재 4개 활성 패턴에서 호출하지 않음 |

과거 문서에 5개 이상의 패턴이 적혀 있어도 현재 `BattleArena`의 활성 목록은 4개다.

## 9. HUD와 전투 피드백

| 피드백 | 현재 역할 |
| --- | --- |
| Player Hull / Armor | 현재 생존 상태와 Armor 파손/수리 상태 표시 |
| Boss HP | 보스 잔여 체력 표시 |
| 기관총 탄/피해 연출 | 2초 발사/2초 휴지 자동 주기, 발사체와 보스 피해 숫자 표시, 현재 크리티컬 표기 없음 |
| Lock-on Button | 준비, 충전, 놓기, 쿨다운, 타깃 없음 상태 표시 |
| Charge Gauge | 충전 진행과 단계 표시 |
| World Reticle | 현재 충전 타깃은 점멸·스케일 왕복, 완료 타깃은 고정 표시하며 발사 후 유지 상태도 표시 |
| Telegraph | 보스 빔/대형 공격의 사전 경고 |
| Music / Sound Buttons | 좌하단에서 전역 BGM과 BGM 외 효과음을 각각 독립적으로 ON/OFF. 둘 다 새 앱/Play Mode 시작 기본 OFF. 버튼보다 큰 공용 검은 배경과 입력 차단 없음 |
| Result Overlay | 승리 또는 패배와 재시도/이탈 흐름 |

현재 씬 안의 일부 오래된 조작 안내 문자열에는 `Missile button`, `Special button` 문구가 남아 있다. 실제 공격 구조와 맞지 않으므로 UI 정리 대상이다.

## 10. 구현 데이터 소유 위치

수치를 변경할 때 아래 파일을 출발점으로 삼고, 동일한 값이 씬에 직렬화되어 런타임 기본값을 덮어쓰는지 반드시 확인한다.

| 시스템 | 주 소스 | 함께 확인할 소스 |
| --- | --- | --- |
| 전투 모드/승패/초기화 | `Assets/_Project/Scripts/Gameplay/BattleController.cs` | `Assets/_Project/Scripts/Core/GameFlowController.cs`, `Assets/Scenes/BattleArena.unity/BattleArena.unity` |
| 플레이어 이동 | `Assets/_Project/Scripts/Gameplay/PlayerOrbitController.cs` | `Assets/_Project/Scripts/Core/PlayerRuntimeState.cs`, `Assets/_Project/Scripts/Gameplay/BattleController.cs`, `Assets/_Project/Scripts/Gameplay/PlayerLockOnController.cs`, `Assets/_Project/Scripts/Gameplay/PlayerMovementBounds.cs`, `Assets/_Project/Scripts/Gameplay/PlayerMoveGuide.cs`, `Assets/_Project/Scripts/Gameplay/PlayerVisualOverlayRenderer.cs`, 현재 씬 |
| 헬기 시각 자세/5락 실행 프로필 180도 반전 연출 | `Assets/_Project/Scripts/Gameplay/PlayerOrbitController.cs` | `Assets/_Project/Scripts/Gameplay/PlayerLockOnController.cs`, `Assets/_Project/Scripts/Gameplay/MountedSidewinderCosmeticController.cs`, `Assets/_Project/Scripts/Gameplay/PlayerVisualOverlayRenderer.cs` |
| 5락 실행 프로필 실제 장착 Sidewinder 2발 연출 | `Assets/_Project/Scripts/Gameplay/MountedSidewinderCosmeticController.cs` | Viper의 좌·우 외곽 Sidewinder/`FX_Nozzle`, `Assets/_Project/Scripts/Gameplay/PlayerLockOnController.cs`, `Assets/_Project/Scripts/Gameplay/PlayerVisualOverlayRenderer.cs` |
| 5락 풀살보 카메라 진동 | `Assets/_Project/Scripts/Gameplay/LockOnCombatFeedback.cs` | `Assets/_Project/Scripts/Gameplay/PlayerLockOnController.cs`의 `OnFullSalvo`, `PlayerCombatController.cs`의 일제사격 무적 종료 이벤트, `PlayerVisualOverlayRenderer.cs`의 헬기 투영 고정 |
| 기관총 | `Assets/_Project/Scripts/Gameplay/PlayerCombatController.cs` | `Assets/_Project/Scripts/Gameplay/ProjectileController.cs`, `Assets/_Project/Scripts/UI/HUDPresenter.cs`, 현재 씬/투사체 프리팹 |
| 락온 상태/충전 시간/피해 공식/피격 취소 | `Assets/_Project/Scripts/Gameplay/PlayerLockOnController.cs` | `Assets/_Project/Scripts/Gameplay/PlayerCombatController.cs`, `Assets/_Project/Scripts/UI/LockOnButtonInputRelay.cs`, `Assets/_Project/Scripts/MissileStrike/LockOnChargeRules.cs`, `Assets/_Project/Scripts/MissileStrike/LockOnSalvoRules.cs` |
| 락온 HUD/월드 마커 | `Assets/_Project/Scripts/UI/LockOnHudPresenter.cs` | `Assets/_Project/Scripts/Gameplay/PlayerLockOnController.cs`, 락온 마커 Sprite, 현재 씬의 HUD |
| 락온 타깃 | `Assets/_Project/Scripts/Gameplay/BossLockOnTargetProvider.cs`, `Assets/_Project/Scripts/Gameplay/BossLockOnTarget.cs` | `Assets/_Project/Scripts/Gameplay/BossTestState.cs`, 에디터 디버그 메뉴 |
| 미사일 발사/분배 | `Assets/_Project/Scripts/Gameplay/PlayerMissileSalvoLauncher.cs` | `Assets/_Project/Scripts/MissileStrike/MissileStrikeDistribution.cs`, `Assets/_Project/Scripts/Gameplay/SpecialMissilePool.cs` |
| 전투 미사일 비행/명중/검은 원통·원뿔 외형 | `Assets/_Project/Scripts/Gameplay/SpecialHomingMissileController.cs` | `Assets/_Project/Scripts/Gameplay/PlayerMissileSalvoLauncher.cs`, 고정 풀 설정 |
| 플레이어 Hull/Armor | `Assets/Resources/Vehicles/VehiclePlayerStateCatalog.asset` | `Assets/_Project/Scripts/Core/VehiclePlayerStateCatalog.cs`, `Assets/_Project/Scripts/Gameplay/PlayerCombatController.cs` |
| 업그레이드 런타임 상태 | `Assets/_Project/Scripts/Core/PlayerRuntimeState.cs` | `Assets/_Project/Scripts/Debug/BattleDebugTuningApplier.cs` |
| 보스 체력/피격 | `Assets/_Project/Scripts/Gameplay/BossController.cs` | `Assets/_Project/Scripts/Gameplay/BattleController.cs`, 현재 씬의 hurtbox/aim point |
| 보스 공통 공격값 | `Assets/_Project/Scripts/Gameplay/BossAttackController.cs` | 현재 씬 |
| 보스 패턴 | `Assets/_Project/Scripts/Gameplay/BossBulletPatternController.cs` | 현재 씬의 `activePatternSet`, `kaijuHeavyThreatSequence` |
| 횡단 빔 입/방향 정렬 | `Assets/_Project/Scripts/Gameplay/KaijuBossAnimationDriver.cs` | `BossBulletPatternController.SweepFrame`, BattleArena의 `KaijuMouthSocket`, `Assets/Editor/KaijuCombatAnimationVerification.cs`, `Assets/_Project/Tests/EditMode/KaijuSweepBeamAlignmentTests.cs` |
| 난이도/스테이지 선택 | `Assets/_Project/Scripts/Core/StageSelectionState.cs` | 스테이지 선택 씬과 `Assets/_Project/Scripts/Gameplay/BattleController.cs` |
| 씬 BGM/전역 음악 상태 | `Assets/_Project/Scripts/Audio/GlobalMusicSettings.cs`, `Assets/_Project/Scripts/Audio/GlobalMusicSource.cs` | `Assets/Scenes/MainMenu.unity/MainMenu.unity`, `Assets/Scenes/BattleArena.unity/BattleArena.unity`, 두 BGM 에셋, `Assets/_Project/Scripts/UI/MenuPresenter.cs`, `Assets/_Project/Scripts/UI/HUDPresenter.cs` |
| 전역 효과음 상태 | `Assets/_Project/Scripts/Audio/GlobalSoundSettings.cs`, `Assets/_Project/Scripts/Audio/RuntimeAudioOutputGuard.cs` | `Assets/_Project/Scripts/Gameplay/PlayerCombatController.cs`, `Assets/_Project/Scripts/Gameplay/LockOnCombatFeedback.cs`, `Assets/_Project/Scripts/UI/HUDPresenter.cs` |
| 메인 메뉴 배경 레이어 | `Assets/_Project/Scripts/UI/MenuPresenter.cs` | `Assets/Scenes/MainMenu.unity/MainMenu.unity`, 메인 메뉴 배경·캐릭터·머플러 텍스처 |
| 배경 아군 공중·지상 부대 | `Assets/_Project/Scripts/Environment/BackgroundAllyArmyController.cs`, `Assets/_Project/Scripts/Environment/BackgroundGroundArmoredUnitsRuntime.cs` | `BackgroundCosmeticCombatBudget.cs`, `BackgroundGroundRoute.cs`, 3종 Ground Prefab, `BattleArena`의 `AmbientAllyArmyRoot` |

## 11. 불일치 및 결정 필요 항목

이 목록은 단순 메모가 아니라 다음 시스템 작업의 우선 확인 대상이다.

| 우선도 | 항목 | 현재 상황 | 필요한 결정/작업 |
| --- | --- | --- | --- |
| 높음 | 임시 화면 진동 ×8 배율 | 화면 진동은 실제 5락 풀살보에만 적용한다. 육안 판별을 위해 5락 기본 진폭 `0.0075`에 `TemporaryCameraShakeVisibilityTestMultiplier = 8`을 적용하며 유효 진폭은 `0.0600`, Full HD 최신 실측 피크는 53.84px다 | 화면 진동 경로 확인이 끝나면 배율을 `1`로 복원하고 최종 연출 진폭을 플레이 테스트로 결정 |
| 높음 | 약점 개방 게임플레이 | 약점 타깃과 2배 피해는 구현됐지만 개방 조건은 디버그 전용 | 체력 구간, 특정 패턴 후, 부위 파괴 등 실제 개방 규칙 결정 및 연결 |
| 높음 | 미사일 업그레이드 스탯 | `MissileDamage`와 레벨당 +10이 남아 있으나 새 락온 공식은 독립 고정 피해표만 사용 | 제거/마이그레이션 또는 고정 피해표에 적용할 별도 성장 규칙 결정 |
| 높음 | 정식 빌드 성장 스탯 적용 | 확인된 자동 적용은 에디터/개발 빌드 중심 | 저장된 성장 데이터를 정식 전투에 적용하는 소유 경로 확정 |
| 중간 | 난이도 선택 | 4단계 UI/상태는 있으나 전투 튜닝 변화 없음 | 난이도별 체력·피해·패턴·수리 규칙 결정 |
| 중간 | 오래된 HUD 조작 문구 | 씬 직렬화 문자열에 단발 Missile/Special 및 `Space/Left click fire` 안내가 남아 있다. 런타임 생성 HUD는 자동 기관총 안내로 덮어쓴다. | 사용자의 씬 작업과 함께 안전하게 정리할 때 직렬화 문자열도 자동 기관총/LOCK ON 안내로 교체 |
| 중간 | 추적 잔류 빔 잔여 튜닝값 | `damageMultiplier 0.95`, `telegraphDuration 0.7`, `trackingTurnRate 28`, 준비 추적 배율 `0.5`가 현재 실행 경로에 미적용 | 사용하지 않는 필드 제거 또는 실행 공식에 명시적으로 연결 |
| 낮음 | 미사일 비행 프로필 잔여 필드 | `LockOnDelay`, 직진/선회/부스트 값이 스냅샷되지만 현재 팬아웃/아크 비행에서 사용되지 않음 | 삭제하거나 현재 비행 단계에 연결 |
| 낮음 | 구형 전투 미사일 외형 참조 | `PlayerCombatController`에 `ViperSidewinderProjectile`과 텍스처 직렬화 참조가 남아 있지만 현재 풀 발사는 이를 넘기지 않고 검은 기본 외형을 사용 | 장착 Sidewinder 연출 검증이 끝난 뒤 구형 투사체 프리팹/필드의 다른 소비자가 없는지 재확인하고 정리 |
| 낮음 | 비활성 공격 코드 | Pressure Sniper, Legacy Bullet Hell, 공기압 대응 코드 일부 잔존 | 재사용 계획이 없으면 별도 정리 작업으로 제거 |

## 12. 변경 시 업데이트 절차

### 12.1 사람과 Codex 공통 체크리스트

1. 작업 전 이 문서를 읽고 변경 대상의 현재 상태와 데이터 소유 위치를 확인한다.
2. 코드 기본값만 보지 말고 현재 씬, 프리팹, ScriptableObject의 직렬화 값이 덮어쓰는지 확인한다.
3. 동작 또는 수치를 변경한다.
4. 같은 변경에서 이 문서의 다음 항목을 갱신한다.
   - 관련 규칙과 수치 표
   - 계산값과 공식
   - 상태 표기
   - 구현 데이터 소유 위치
   - 불일치 및 결정 필요 항목
   - 상단 `last_verified`, `implementation_baseline`, `version`
   - 아래 변경 이력
5. 가능한 범위에서 EditMode/PlayMode 테스트 또는 실제 전투 재현을 수행한다.
6. 테스트하지 못한 범위는 `검증 메모`에 명시한다.
7. 문서와 구현을 같은 커밋/PR에 포함한다.

### 12.2 Codex 작업 규약

- 게임플레이 질문에 답할 때 과거 기획서를 검색 결과 한 건만 보고 답하지 않는다.
- 제거된 `PlayerSpecialAttackController`, 과거 단발 미사일, 조준점 크리티컬을 현재 기능으로 가정하지 않는다.
- 새 공격이나 수치를 구현한 뒤 이 문서 갱신 없이 작업 완료로 보고하지 않는다.
- 구현하지 않은 기획은 반드시 `계획`, 디버그 경로만 있으면 `프로토타입/디버그`로 기록한다.
- 숫자를 바꿀 때 파생값(DPS, 단계 총 피해, 틱 피해)도 다시 계산한다.
- 현재 씬이 미커밋 상태라면 사용자 변경을 보존하고 충돌 없이 문서 또는 별도 파일을 수정한다.

## 13. 검증 메모

### 2026-08-30 — Background Ground Armored Units 기본 8대 프로토타입

- 최적화 FBX 3종을 `BackgroundTank_800`, `BackgroundGatlingCarrier_800`, `BackgroundMortarCarrier_820` Prefab으로 만들고 공용 instancing Material을 적용했다. FBX 단위는 Unity Importer `globalScale=100`으로 보정했다.
- 기본 8대는 탱크 3, 개틀링 3, 박격포 2다. 3대 종대는 같은 경로의 누적 거리 오프셋을 사용하고 나머지는 서로 다른 경로·위상·속도로 움직인다.
- StageVisualRoot 로컬 Catmull-Rom 폐회로 3개, 감속·정차·조준·사격·회복·재가속 상태, 포탑 Yaw/포신 Pitch, 이동 버스트와 박격포 포물선, 고정 LineRenderer 풀을 구현했다.
- 공중과 지상이 공유하는 `BackgroundCosmeticCombatBudget`은 Primary 1개와 Ambient 2개를 제한한다. 지상 VFX는 실제 투사체·피해 API·Collider·Rigidbody를 사용하지 않는다.
- Unity GUI를 띄우지 않은 Builder와 배치 Play Mode로 씬을 생성·검증했다. 기본 8대의 투영 폭은 17.4~48.3px, StageVisualRoot 로컬 접지 높이는 0.12~0.20이었다.
- 강제 지상 Primary 사격 뒤 보스 HP 2000, 플레이어 Hull/Armor 100/120, 락온 유효 타깃 5가 유지됐다. 집중 EditMode 10/10과 전체 EditMode 115/115가 통과했다.
- 단발 Ambient 종료가 같은 프레임에 포구 화염을 끄던 흐름을 수정했다. 차종별 0.075~0.19초 수명과 크기·회전 펄스를 적용하고, 배치 Play Mode에서 화염 렌더러 ON→자동 OFF와 캡처를 확인했다.
- 미검증: 7대·10대 프리셋, 각 차종별 강제 공격의 독립 시각 승인, 공중·지상 동시 장시간 과밀도, 20:9/4:3, 10분 Profiler soak, 모바일 실기기.

### 2026-08-30 — 배경 아군 방향·속도·개틀링·추락 연출

- FBX 모델 자식의 Y 회전을 `+90° → -90°`로 반전해 보이는 기수와 런타임 루트 +Z를 일치시켰다. Muzzle은 +Z 앞머리 앵커를 유지한다.
- 기수 목표는 이론 경로 접선보다 실제 프레임 이동 벡터를 우선하고, 이동/기수 정렬이 0.25 미만일 때만 회전 응답을 높인다. 최종 실제 최소 내적은 0.911이고 최대 Up 편차는 10.053°였다.
- 공격 Approach/AttackRun/BreakAway/Rejoin 시간에 현재 `attackMotionSpeedScale=0.5`의 역수를 적용해 기존 갑작스러운 기동 속도를 절반으로 줄였다.
- 각 활성 헬기는 0.55~0.9초 개틀링 버스트, 0.075~0.11초 화염 간격, 1.25~2.2초 쿨타임을 독립적으로 사용한다. 총구에는 Collider 없는 additive Cross-Quad 2장을 사용한다.
- 전역 추락 디렉터는 20~34초마다 한 대만 선택한다. 추락 기체는 World smoke, 중력 2.4, 240~420°/s 자회전을 사용하고 화면 이탈 뒤 4~7초 후 궤도에 재보충한다.
- 실제 격리 실행에서 개틀링 화염 167회, 피해 없는 트레이서 3발, 추락 1회·1초 하강 2.367m·누적 회전 368.07°·연기 재생을 확인했다. 보스 HP 2000, 플레이어 100/120, 락온 타깃 5는 유지됐다.
- 집중 EditMode 7/7 통과. 직전 전체 110/110은 유지된다. 신규 전체 MCP 작업은 실패 0으로 95/112까지 진행한 뒤 도구 작업 상태 갱신을 잃어 최종 결과는 미확정이다.

### 2026-08-30 — 배경 아군 헬기 수직 자세·후진 외형 보정

- 원인: 공중 그룹의 카메라 평면 타원 접선 전체를 `LookRotation` 전방으로 사용해 상승·하강 성분이 큰 구간에서 동체 피치가 과도해졌다.
- 이동 궤도와 공격 위치는 바꾸지 않고, 실제 기수 방향은 월드 Up에 수평 투영한 진행 방향으로 계산한다. 수직 이동은 최대 7° 피치로만 표현하고 기존 최대 8° 뱅크를 유지한다.
- 순수 수학에 `EvaluateConstrainedFlightRotation`을 추가하고 수직 이동·급경사 이동에서도 수평 전방과 fallback 전방이 유지되는 테스트 2개를 추가했다. 집중 EditMode 7/7 통과.
- 실제 BattleArena 가짜 공격 전체에서 4대의 자세를 매 프레임 추적했다. 10,910개 샘플의 최대 `unit.up` 편차는 9.889°, 수평 실제 이동과 기수 전방의 최소 내적은 0.608로 모두 양수였다. 꼬리를 아래로 세우는 수직 자세와 뒤로 비행하는 프레임이 검출되지 않았다.
- 공격은 트레이서 3발 후 정상 복귀했고 보스 HP 2000, 플레이어 Hull/Armor 100/120, 락온 유효 타깃 5를 유지했다.
- 직전 전체 EditMode 110/110은 유지된다. 이번 변경 뒤 시작한 전체 MCP 작업은 실패 0 상태로 99/112까지 진행한 뒤 도구 작업 상태 갱신을 잃어 최종 결과가 미확정이므로 새 전체 통과로 기록하지 않는다.

### 2026-08-29 — Background Ally Army 공중 Phase 0~2 구현

- Git 기준점: `a6554a7` + 공중 배경 아군 작업 트리.
- 500 tris FBX와 256/128 텍스처를 프로젝트 전용 아트 경로에 반입하고 URP shared Material, 로터 블러, 트레이서 Material, `BackgroundChopper_500.prefab`을 Builder로 생성했다.
- BattleArena에 `AmbientAllyArmyRoot/AirRoot/CosmeticVfxRoot`를 추가하고 `BattleController.ConfigureBackground()`에서 보스·Base 카메라·StageVisualRoot를 전달한다.
- 실제 1280×720 카메라에서 단독기 1대와 V자 편대 3대가 생성됐으며 로터 포함 투영 크기는 약 59~73×43~50px였다. 순찰 중심은 보스 위쪽 하늘로 이동하고 공격 때 보스 높이로 진입한다.
- 강제 가짜 공격에서 트레이서 3발을 발사한 뒤 공격 슬롯 0, 활성 트레이서 0으로 복귀했다. Collider/Rigidbody는 0개이고 모든 기체가 500 tris Mesh와 instancing Material을 공유했다.
- 공격 전후 보스 HP 2000, 플레이어 Hull/Armor 100/120, 락온 유효 타깃 5개가 유지됐다.
- 신규 순수 수학 집중 EditMode 5/5와 전체 EditMode 110/110을 통과했다. 격리/합성 캡처와 보고서는 로컬 `Logs/BackgroundAllyArmy/`에 남겼다.
- Unity MCP 원격 세션이 연결되지 않아 Unity 6000.4.0f1 배치 모드의 Builder, Test Runner, 비동기 Play Mode 검증으로 대체했다. 모바일 실기기, 10분 Profiler soak, 탱크 단계는 아직 미검증/미구현이다.

### 2026-08-29 — 전체 변경사항 공유 전 문서 경로 점검

- 사용자가 요청한 전체 변경사항·docs Markdown 공유를 준비하면서 `AGENTS.md`와 문서 인덱스를 실제 중심 문서 경로인 `docs/cur_state/titan-destroyer-game-system-master.md`로 통일했다. 중심 문서는 이동하거나 복제하지 않았다.
- `docs` 아래 Markdown 14개와 참조 다이어그램의 추적 정책을 정리했다. 비밀키가 담긴 `.codex/config.toml`은 로컬에 보존하고 Git에서 제외한다. 생성 로그·캐시도 기존 제외 정책을 유지한다.
- 문서 링크·변경 범위·자격증명 패턴을 점검했다. 게임 코드·전투 수치를 추가로 바꾸지 않았으며 아래 구현 검증 결과를 유지한다.

### 2026-08-29 — 가속 횡단 빔·입 방향 동기화 구현

- 기준점: `cbf7180` + 이번 작업 트리, Unity `6000.4.0f1`, Coplay로 연결한 실제 BattleArena.
- `BossBulletPatternController`, `KaijuBossAnimationDriver`, 전투 자산 생성/검증 도구를 수정하고 입 소켓 회전만 씬에 반영했다. FBX/클립/머티리얼/조명/카메라/전투 수치는 보존했다. 드라이버 Update(-100) → Animator → BattleController LateUpdate(200) → 패턴 LateUpdate(300) 순서에서 실제 본을 보정하고 공통 프레임을 소비한다.
- 수정 전 대표 런타임 샘플의 출사축/빔 최대 오차 96.375°. 수정 후 실제 전투 44조합·1396 활성 프레임에서 최대 0.13706°, 빔 근단/입 위치 차이 최대 0.000013 월드 단위, 진행률 오차 최대 0.000066이었다. 모든 조합에서 플레이어 깊이 평면의 화면 양 끝 피복과 정상 종료 후 잔여 0을 확인했다.
- 44조합: 양방향의 30/60/120 게임 프레임 간격 검사 6개, 화면비 16:9/19.5:9/4:3의 중앙·좌우·상하 검사 30개, 16:9 네 모서리 8개. 프레임 간격은 `Time.captureDeltaTime`으로 고정했으며 실기기 성능 벤치마크가 아니다.
- 전체 EditMode 105/105 통과, 최종 집중 검사 19/19 재통과. 신규 실제 리그 30개 포즈 조합에서 루트/발 비간섭·비누적·복구·취소된 공격 번호/잘못된 입력 거부를 확인했다. 기존 5각도 Firing·Mask·Attack1/Attack2·양방향 빔·꼬리·점프 턴·유지 사격·취소/일시정지/사망 포즈 검증도 재통과했다.
- 준비/활성 중 시네마틱 취소, 활성 중 컴포넌트 비활성화, 별도의 실제 활성 빔 중 보스 치명 피해 검사에서 빔/경고/조준 세션이 모두 정리됐다. 마지막 사망 검사는 전투를 종료시키므로 확인 후 Play Mode를 종료했다.
- 원본·보정 후 입 근접 메시 및 실제 Game View 시작/중간/끝을 캡처했다. 실제 접촉에서 Armor 120→102도 확인했다. 기록은 로컬 `Logs/KaijuSweepAlignment/`와 [개발계획서 §12](../kaiju-sweep-beam-alignment-development-plan.md)에 보존했다.
- MCP 콘솔 도구의 기존 reflection 오류와 테스트 작업 상태 갱신 누락은 `Editor.log` 및 실제 TestResults XML로 대체 확인했다. 미검증: 모바일 실기기, 모든 회전 자세의 아트 승인, 비활성 레거시 패턴 재활성화. 다른 시스템의 과거 문서 내용을 일괄 재검증한 것은 아니다.

### 2026-08-29 — 가속 횡단 빔 동기화 개발계획 (부분 정적 검토)

- 기준점: `cbf7180`과 기존 사용자 작업 트리. 새 계획서 작성 및 관련 문서 불일치 기록만 수행했다.
- 확인 소스: `BossAttackController.cs`, `BossBulletPatternController.cs`의 횡단/화면 방향/피해/취소 경로, `KaijuBossAnimationDriver.cs`, `KaijuCombatAnimationBuilder.cs`, `KaijuCombatAnimationVerification.cs`, `BattleArena` 입 소켓·횡단 빔 직렬화값, Kaiju 통합 이력 문서.
- 92도 폴백과 화면 좌우 방향 계산의 차이, 빔 방향과 입 애니메이션의 독립, VFX와 피해 판정의 공통 방향 소비를 정적으로 확인했다. 로컬 회전 identity만으로 입 소켓 축이 잘못됐다고 확정하지 않는다.
- [동기화 개발계획서](../kaiju-sweep-beam-alignment-development-plan.md)에 패턴 보존형 보정안과 재현·검증·중단 정책을 기록했다. 계획은 아직 미구현이다.
- 게임 코드·씬·애니메이션·전투 수치 변경 없음. Unity 컴파일 및 EditMode/PlayMode 재실행, 실제 각도·입 위치 오차 측정은 수행하지 않았다. 상단 검증일은 이 범위에만 해당하며 다른 시스템의 기존 구현 기준점과 검증 이력은 유지했다.

### 2026-08-05 — 5락 발사 후 측면 자세 복귀 보간

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 정상 5락 발사 완료 뒤 기존 목표 자세 유지 1초는 보존하고, 그 다음 보이는 헬기 외형만 현재 자세에서 발사 전 우측 측면 자세까지 0.3초 동안 시간 기준 `Quaternion.Slerp`와 `SmoothStep`으로 보간하도록 변경했다. 피격/발사 거부/비활성화 같은 취소 경로의 즉시 복귀는 유지했다.
- BattleArena 실제 5락 피드백 진단이 `verified=True`였다. 진입 자세 유지 체크 뒤 복귀 상태와 0~1 사이 중간 진행 프레임이 모두 관측됐고, 최종 진행률 1.000, 발사 전 자세와의 각도 오차 0.000도였다.
- 같은 실행에서 30발 요청·30발 완료, 장착 Sidewinder 2발 분사·분리, 무적과 화면 진동 동시 종료, 월드 화면 이동 50.24px, 헬기·플레이어·오버레이 카메라 drift 0, 원래 투영 복구를 함께 확인했다. 따라서 새 복귀 보간은 시각 외형 외의 기존 액션·전투 프로세스를 변경하지 않는다.
- 새 복귀 보간 단위 테스트 1/1과 전체 EditMode 테스트 78/78이 통과했다. 수정 스크립트 진단 오류는 0개였으며 `PlayerOrbitController`의 기존 일반 성능 경고 1개만 남았다. Unity MCP `read_console`의 기존 reflection 초기화 오류 때문에 실행 로그는 `Editor.log`로 대체 확인했다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았고 Play Mode 종료 후 `isDirty=false`였다.

### 2026-08-05 — 화면 진동을 실제 5락 대형 Sidewinder 이벤트로 제한

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 원인은 모든 단계가 공통으로 보내는 `OnLockRelease` 처리에서 카메라 진동을 무조건 시작한 것이었다. 일반 릴리즈 처리에서는 단계·릴리즈·부스트 사운드만 유지하고, 진동 시작은 성공한 5락 풀살보의 `OnFullSalvo` 이벤트로 분리했다. 1~4락의 전투 미사일, 피해, 약 0.6초 일제사격 무적은 변경하지 않았다.
- BattleArena 실제 1~5락 통합 진단이 `verified=True`로 완료됐다. 단계별 진동 상태는 `1=False`, `2=False`, `3=False`, `4=False`, `5=True`로 기대값과 일치했고, 발사 수도 `5/10/15/20/30`, 완료 수도 동일했으며 이동·자동 기관총·보스 공격·40발 풀 불변식·`SHOOT ERROR` 없음이 모두 유지됐다.
- 별도 실제 5락 피드백 진단도 `verified=True`였다. 유효 진폭 `0.0600`, 월드 화면 이동 피크 `53.84px`, 헬기·플레이어·오버레이 카메라 drift 0, 30발 발사, 회전 뒤 장착 Sidewinder 2발 분사·분리, 무적과 진동 동시 종료, 원래 투영 복구를 확인했다.
- 수정 스크립트 진단 오류 0개, 전체 EditMode 테스트 77/77 통과를 확인했다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았고 Play Mode 종료 후 `isDirty=false`였다.

### 2026-08-05 — 원본 1~5락 실행 프로필 복구

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 임시 `promoteThreeOrMoreLocksToFullSalvoForTesting` 기본값을 OFF로 복구했다. 실제 성공 락과 실행 프로필은 다시 `1→1, 2→2, 3→3, 4→4, 5→5`로 일치하며, 과거 승격 코드는 명시적으로 다시 켜지 않는 한 실행되지 않는다.
- BattleArena의 실제 1~5단계 통합 진단이 `verified=True`로 완료됐다. 각 단계 요청·완료 발사 수는 차례로 `5/5`, `10/10`, `15/15`, `20/20`, `30/30`이었고 5개 단계 검사, 이동, 자동 기관총, 보스 공격, 40발 풀 불변식이 모두 통과했으며 `SHOOT ERROR`는 발생하지 않았다.
- 별도 실제 5락 피드백 진단에서 `intentLocks=5`, `profileLocks=5`, 30발 요청·30발 완료, 헬기 회전, 회전 뒤 장착 Sidewinder 분사·2발 분리, 피해 없는 가속 비행, 약 1.3초 무적과 카메라 진동 연동이 `verified=True`였다. 3·4락 풀살보 전용 디버그 메뉴는 제거하고 실제 5락 검증 메뉴만 남겼다.
- 기본 매핑과 명시적 레거시 승격 경로의 회귀 테스트 2/2, 전체 EditMode 테스트 77/77이 통과했다. 수정 스크립트 진단 오류와 Unity 전체 컴파일 오류, 이번 실행의 신규 예외는 0개였다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았고 Play Mode 종료 후 `isDirty=false`를 확인했다.

### 2026-08-05 — 헬기 이동 입력 완전 차단 경로 제거

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 정상 초기화된 BattleArena에서는 수정 전 자동 입력에서도 Transform과 화면상 헬기가 움직여 사용자가 본 정지 상태를 동일하게 재현하지는 못했다. 대신 코드를 추적해 전투 재구성 뒤 이전 `inputEnabled=false`가 남는 경로와 보스 중심·주시 대상이 모두 없으면 `LateUpdate`가 이동 입력 전에 종료되는 두 개의 완전 차단 경로를 확인했다.
- 새 전투 `Configure`에서 이동 입력을 명시적으로 다시 활성화하고, 보스 참조가 없어도 키 입력·평면 제한·시각 동기화·속도 갱신을 수행하도록 수정했다. 승리·패배 후 현재 전투에서 의도적으로 잠근 입력은 그대로 유지한다.
- Input System 테스트 키보드로 보스 참조가 전부 `null`인 상태의 `D` 입력이 `movementInput=(1,0)`까지 전달되는 것과, 입력 잠금 뒤 새 `Configure`가 이를 다시 활성화하는 것을 각각 검증했다. BattleArena 자동 실제 입력에서도 `A`가 초기 x `0.28`에서 `0.195`로 감소했고, 이어서 `D/W/S`가 우측 `1.000`·상단 `1.000`·하단 `0.000`까지 이동시켜 네 방향 모두 좌표 변화가 발생했다.
- 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, 신규 회귀 테스트 2/2 및 전체 EditMode 테스트 77/77 통과를 확인했다. Unity MCP `read_console`의 기존 reflection 초기화 오류 때문에 컴파일과 런타임 로그는 `Editor.log`로 대체 확인했다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았고 Play Mode 종료 후 `isDirty=false`를 확인했다.

### 2026-08-05 — 화면 진동 육안 판별용 임시 ×8 테스트 배율

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 직전 기본 단계별 진폭 `0.0030/0.0040/0.0050/0.0060/0.0075`는 그대로 두고, 제거·변경이 쉬운 단일 상수 `TemporaryCameraShakeVisibilityTestMultiplier = 8`을 추가했다. 현재 유효 진폭은 `0.0240/0.0320/0.0400/0.0480/0.0600`이며 육안 판별이 끝나면 이 상수만 `1`로 되돌릴 수 있다.
- BattleArena 실제 5락 릴리즈에서 `shakeAmplitude=0.0600`, 투영 피크 `0.054308`, 고정 월드 지점 화면 이동 피크 `53.81px`를 확인했다. 30발 요청·30발 완료, 무적과 진동 동시 종료, 원래 투영 복구까지 `verified=True`였다.
- 같은 실행에서 헬기 화면 좌표, 시각 루트, 플레이어 월드 좌표, 오버레이 카메라 위치·회전·투영 drift는 모두 `0.000000`이었다. 따라서 이번 큰 테스트 진동도 헬기·이동 앵커·피격 판정·HUD가 아니라 보스·배경·월드 전투 오브젝트에만 적용된다.
- 수정 스크립트 진단 오류 0개, Unity 컴파일 오류 0개, 신규 단위 테스트 1/1 및 전체 EditMode 테스트 75/75 통과를 확인했다. Unity MCP `read_console`의 기존 reflection 초기화 오류 때문에 컴파일과 런타임 로그는 `Editor.log`로 대체 확인했다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았고 Play Mode 종료 뒤 `isDirty=false`를 확인했다.

### 2026-08-05 — 육안 식별 가능한 월드 화면 진동 보정

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 기존 진동 이벤트와 Base 카메라 투영 변경 자체는 실행되고 있었다. 다만 5락 최대 진폭 `0.0020`은 1920×1080에서 이론상 가로 약 1.92px·세로 약 1.08px, 이전 런타임 관측 피크도 약 1.6~1.9px 수준이라 실제 화면에서는 진동을 알아보기 어려웠다.
- 헬기 전용 오버레이 카메라와 이동·피격 기준 분리는 그대로 유지하고, 단계별 월드 진폭을 `0.0030/0.0040/0.0050/0.0060/0.0075`로 조정했다. 5락 실행 프로필의 이론상 최대 이동은 Full HD 가로 7.2px·세로 4.05px다.
- 진단 성공 조건을 단순 투영 행렬 변화에서 고정 월드 지점의 실제 viewport 픽셀 이동으로 보강했다. BattleArena 실제 5락 릴리즈에서 `shakeAmplitude=0.0075`, 투영 피크 `0.007316`, 월드 화면 이동 피크 `5.87px`를 확인했다. 동시에 헬기 화면 좌표, 시각 루트, 플레이어 월드 좌표, 오버레이 카메라 위치·회전·투영 drift는 모두 `0.000000`이었고 30발 발사, 무적·진동 동시 종료, 원래 투영 복구까지 `verified=True`였다.
- 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, EditMode 테스트 75/75 통과를 확인했다. Unity MCP `read_console`의 기존 reflection 초기화 오류 때문에 `Editor.log`로 컴파일 오류 부재와 런타임 로그를 대체 확인했다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았다.

### 2026-08-05 — 일제사격 화면 진동과 헬기·피격 기준 완전 분리

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 잔여 떨림은 세 경로가 합쳐진 결과였다. Sidewinder 분사 Particle/Trail의 매 프레임 변하는 Bounds가 헬기 중심 보정에 포함됐고, Sidewinder Mesh 2개가 분리될 때 중심 Bounds가 다시 바뀌었으며, 진동 중인 카메라를 이동 평면의 `WorldToViewportPoint`/`ViewportToWorldPoint` 왕복 변환에도 사용해 플레이어 월드 좌표까지 미세 이동했다.
- `PlayerVisualOverlayRenderer`는 Particle/Trail/Line Renderer와 등록된 장착 Sidewinder 계층을 중심 계산에서만 제외한다. 해당 VFX와 Sidewinder는 오버레이에서 계속 보인다. 일제사격 중에는 진동 직전 오버레이 카메라 위치·회전·투영을 함께 고정해, 동시에 발생할 수 있는 보스 파편 스톰프의 기존 카메라 위치 진동도 헬기에는 전달하지 않는다.
- `PlayerOrbitController`는 일제사격 진동 중 안정된 오버레이 카메라를 이동 투영 기준으로 사용한다. 좌표가 viewport 경계와 고정 깊이 안에 있으면 불필요한 카메라 왕복 변환을 생략해 이동 앵커와 피격 Collider가 화면 진동을 따라 움직이지 않게 했다.
- BattleArena에서 실제 성공 락 3·4·5개를 각각 5락 프로필로 릴리즈했다. 세 실행 모두 월드 카메라 투영 진동 피크가 각각 `0.001893/0.001954/0.001695`로 발생한 반면 `helicopterViewportDrift`, `visualRootCorrection`, `visualRootLocalDrift`, `playerWorldDrift`, 오버레이 카메라 위치·회전·투영 drift는 전부 `0.000000`이었고 `helicopterActuallyStable=True`, 전체 `verified=True`였다.
- 분사 중 중심 계산 Renderer 16개, 제외된 동적 VFX Renderer 4개, 제외된 장착 Sidewinder Renderer 2개를 확인했다. 세 실행 모두 30발 요청·30발 완료, 무적과 진동 동시 종료, 원래 투영 복구가 유지됐다.
- 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, EditMode 테스트 74/74 통과를 확인했다. Unity MCP `read_console`의 기존 reflection 초기화 오류 때문에 `Editor.log`로 실행 로그와 컴파일 오류 부재를 대체 확인했다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았다.

### 2026-08-05 — 회전 완료 후 Sidewinder 분사·가속 비행·무적 연동 카메라 진동

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 5락 실행 프로필 릴리즈 직후에는 Sidewinder 분사를 시작하지 않고 `IsWaitingForVisualTurn=true`, 분사 0개, 분리 0발인 것을 확인했다. 헬기 회전 진행률 1.0 이후에만 `IsIgniting=true`, 분사 2개가 되었으며 `LastIgnitionStartedAfterVisualTurn=true`였다.
- 장착 상태 1초 분사 뒤 Sidewinder 2발이 동시에 분리될 때 `MountedSidewindersDetached`가 5락 프레젠테이션을 완료한다. 이 실제 분리 이벤트가 일제사격 무적과 카메라 진동을 함께 종료하며, 30발 전투 미사일이 약 0.6초에 먼저 끝나도 조기 종료하지 않는다.
- Sidewinder 비행은 분리 속도 5를 0.5초 유지한 뒤 가속도 20으로 순항 35까지 증가한다. EditMode 속도 평가에서 경과 `0/0.5/1/2/3초` 속도가 각각 `5/5/15/35/35`였고, 3·4·5락 Play Mode에서 초기 속도 5와 증가한 최고 관측 속도 `13.82/10.09/10.05`를 확인했다.
- 기존 짧고 강한 릴리즈 진동을 진폭 `0.0020`의 약한 지속형 월드 카메라 진동으로 교체했다. 실제 3·4·5락 모두 발사 승인 시 무적과 진동이 동시에 활성화됐고, 회전 완료 후 분사 중에도 둘 다 유지됐으며, 분리 시점에 무적·진동이 함께 종료되고 Base 카메라 투영이 원상복구됐다.
- 헬기 전용 오버레이 카메라는 진동 전 투영을 유지했다. 세 실행 모두 `helicopterStable=True`, `cameraShake=True`, 최종 `projectionRestored=True`, 전체 검증 `verified=True`, 30발 요청·30발 완료였다.
- 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, EditMode 테스트 70/70 통과를 확인했다. Unity MCP `read_console`의 기존 reflection 초기화 오류 때문에 `Editor.log`로 실행 로그와 컴파일 오류 부재를 대체 확인했다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았다.

### 2026-08-05 — 정상 우클릭 충전 복원과 실제 3·4·5락 풀살보 승격

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- `forceFullChargeOnMouseRightForTesting` 기본값을 OFF로 바꿔 PC 우클릭도 모바일과 같은 누적 `1/2.5/4.5/7/10초` 충전으로 복원했다. 우클릭 충전 시작 직후 성공 락이 0개임을 실제 `MouseRight` 입력 소스 진단으로 확인했다.
- 별도 기본 ON 테스트 플래그 `promoteThreeOrMoreLocksToFullSalvoForTesting`을 추가했다. 실제 성공 락과 타깃 스냅샷은 그대로 보존하고 실행 프로필만 `1→1, 2→2, 3→5, 4→5, 5→5`로 해석한다. 플래그를 OFF로 바꾸면 원본 배열을 수정하지 않고 `1→1, 2→2, 3→3, 4→4, 5→5`가 복원되는 EditMode 회귀 테스트도 추가했다.
- `BattleArena`에서 실제 입력 소스를 `MouseRight`로 유지한 채 3락·4락·5락을 각각 만들어 릴리즈했다. 세 경우 모두 실제/프로필 락이 각각 `3/5`, `4/5`, `5/5`였고 30발 요청·30발 완료, 총 기본 피해 100, 5락 릴리즈 피드백, 헬기 180도 회전, 장착 Sidewinder 분사 2개, 풀살보 이벤트가 모두 `verified=True`였다.
- 3락은 실제 마커 3개와 다음 충전 마커 1개, 4락은 실제 마커 4개와 다음 충전 마커 1개, 5락은 완료 마커 5개를 유지했다. 승격이 존재하지 않는 타깃을 추가하지 않으며 30발은 실제 타깃 스냅샷에 분배된다.
- 마지막 실행 뒤 장착 Sidewinder 상태는 탐색 2발, 분리 2발, 복귀 2발, 비행 비활성, `LastBindingFailure=""`였고 풀은 가용 40/예약 0/대여 0으로 복구됐다. 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, EditMode 테스트 69/69를 확인했다.
- Unity MCP `read_console`은 기존 reflection 초기화 오류로 사용할 수 없어 `Editor.log`로 대체 확인했다. 이번 변경의 C# 컴파일 오류와 신규 게임 실행 예외는 없었으며, 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았다.

### 2026-08-05 — 장착 Sidewinder 1초 점화와 가시성 보강

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 재검토 결과 Sidewinder 발사 이벤트나 장착점 탐색이 실패한 것은 아니었다. 변경 전 런타임에서도 좌·우 장착점 2개, 분리 2발, 복귀 2발, 빈 바인딩 오류를 확인했지만, 0.5초 점화와 작은 분사 효과 뒤 빠른 전투 미사일 비행값을 사용해 실제 화면에서는 발사 여부를 알아보기 어려웠다.
- 기본 장착 점화를 1초로 늘리고 좌·우 `FX_Nozzle`의 황색·주황색 파티클 크기·밝기·밀도를 높였다. 분리 뒤 실제 Sidewinder에는 짧은 주황색 궤적을 붙이고, 피해 없는 연출용 비행값을 발사 10/순항 35/가속 45/회전 초당 180도로 분리했다. 30발 전투 미사일의 속도·수량·피해는 변경하지 않았다.
- 1초 점화가 30발 전투 미사일의 약 0.6초 발사 시간보다 길어졌으므로, 전투 발사 완료가 먼저 와도 5단계 완료를 보류한다. Sidewinder 2발이 1초 시점에 실제로 분리될 때 무적·마커 완료·자세 유지 시작을 함께 처리한다.
- `BattleArena` Stage 5 자동 검증의 0.42초 시점에서 `mountedIgniting=True`, 활성 분사 2개, 분리 0발, `mountedIgnitionVisible=True`, 전체 `verified=True`를 확인했다. 이어 실제 비행 중 `IsFlightActive=true`, 분리 2발/복귀 0발을 확인했고, 완료 뒤에는 비행 비활성, 분리 2발/복귀 2발, `LastBindingFailure=""`로 원래 파일런 복귀가 끝났다.
- 수정 스크립트 진단과 Unity 전체 컴파일에서 C# 오류가 없었고 EditMode 테스트 68/68이 통과했다. Unity MCP `read_console`은 기존 reflection 초기화 오류로 사용할 수 없어 `Editor.log`로 대체 확인했으며, 이번 변경의 컴파일 오류나 신규 실행 예외는 없었다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았다.

### 2026-08-05 — PC 우클릭 즉시 5단계 임시 테스트 경로

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- `PlayerLockOnController`에 기본 ON인 `forceFullChargeOnMouseRightForTesting` 직렬화 플래그를 추가했다. `TryBeginCharging(MouseRight)`가 정상 시작 조건과 `OnLockStart`를 통과한 뒤에만 풀차지 시간까지 즉시 진행하며, `MobileHud`와 `Debug` 입력에는 적용되지 않는다.
- 우클릭을 누르는 순간 5개 락이 완료되지만 발사는 기존처럼 우클릭을 놓을 때만 요청한다. 타깃 없음, 피격 취소, 누르는 동안 `Charging`·4.32 이동 속도 유지, 5초 재사용 대기는 변경하지 않았다.
- EditMode 회귀 테스트는 임시 플래그 기본값이 ON이고 `MouseRight`에만 적용되며 `MobileHud`·`Debug`에는 적용되지 않는 것을 확인했다. 전체 EditMode 테스트는 68/68 완료·실패 0개였다.
- `BattleArena` Play Mode의 Stage 5 피드백 자동 검증을 실제 `MouseRight` 입력 소스로 실행해 `began=True`, 즉시 마커 5개·점멸 0개, `released=True`, 요청 30발, 전체 피드백 `verified=True`를 확인했다.
- 반복 5단계 실행 로그에서 새 Sidewinder 불꽃의 `ParticleSystem.duration`을 재생 중 설정하는 기존 Unity 경고를 발견했다. 파티클 생성 직후 정지·초기화하고 설정이 끝난 뒤 명시적으로 재생하도록 수정했으며, 동일 Play Mode 검증에서 경고가 다시 발생하지 않았다. 불꽃 시간과 발사 기능은 변경하지 않았다.
- Unity MCP `read_console`은 기존 reflection 초기화 오류로 사용할 수 없어 컴파일·실행 로그는 `Editor.log`로 대체 확인했다. 이번 변경의 C# 컴파일 오류와 신규 실행 예외는 없었고, 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았다.

### 2026-08-05 — 5단계 실제 장착 Sidewinder 2발과 전투 미사일 외형 분리

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 5단계 피해 경로는 기존 30발, 기본 총피해 100, 40발 고정 풀, 약 0.6초 발사 흐름을 유지했다. 풀에서 생성된 실제 런타임 오브젝트에 `BarrageMissileCylinder`와 `BarrageMissileConeNose`가 생성되고 구형 `MissileSkin`이 생성되지 않는 것을 확인했다.
- 실제 Viper 외곽 파일런의 좌·우 Sidewinder와 `FX_Nozzle`를 각각 1개씩 찾았다. 5단계 Play Mode 진단 후 `ResolvedMountedSidewinderCount=2`, `LastDetachedSidewinderCount=2`, `LastRestoredSidewinderCount=2`, `LastBindingFailure=""`를 확인했다. 연출용 피해 프로퍼티는 0이다.
- 5단계 릴리즈에서 0.5초 점화 구간이 끝나는 시점에 일제사격 무적과 발사 완료 표시 이벤트를 종료한다. 30발 풀의 마지막 웨이브는 기존 0.6초까지 독립적으로 계속된다. 런타임 진단에서 첫 전투 미사일 이전 무적, 30발 전량 발사, 기본 피해 예산 100, 발사 오류 없음이 유지됐다.
- 분리된 Sidewinder는 실제 헬기 Renderer와 같은 전용 오버레이 레이어에 외부 시각 루트로 등록하고, 복귀할 때 원래 파일런에 먼저 재결합한 뒤 외부 등록을 해제한다. 이 때문에 월드 깊이 가림과 복귀 프레임의 레이어 깜빡임을 피한다.
- 새 EditMode 테스트 3개는 기본 점화 0.5초/최대 비행 6초/피해 0, 좌·우 외곽 Sidewinder 2개만 탐색, 고속 이동 구간의 반경 통과 판정을 확인한다. 전체 EditMode 테스트는 67/67 완료·실패 0개였다.
- Unity MCP `read_console`은 기존 reflection 초기화 오류로 사용할 수 없어 컴파일과 실행 예외는 `Editor.log`로 대체 확인했다. 이번 변경의 C# 컴파일 오류와 Play Mode 신규 예외는 없었다. 사용자의 미커밋 `BattleArena` 씬은 저장하지 않았다.

### 2026-08-05 — 5단계 풀차지 0.3초 회전 애니메이션

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 풀차지 릴리즈 순간의 실제 표시 회전과 기존 180도 반전 목표 회전을 저장한 뒤, 고정된 각도 프레임 목록 대신 경과 시간/0.3초를 기준으로 `SmoothStep` 이징한 `Quaternion.Slerp`를 매 프레임 적용한다. 기본 전환 시간은 직렬화 필드 `fullSalvoVisualTurnDuration = 0.3`으로 분리해 추후 쉽게 조정할 수 있다.
- 이 애니메이션은 전용 오버레이 카메라가 그리는 실제 헬기 외형에만 적용하며 첫 미사일 발사를 기다리게 하지 않는다. 따라서 당시 버전의 30발 약 0.6초 발사 흐름, 이동 앵커, 피격 판정은 그대로였다. 5단계 무적 종료는 이후 1.0.19에서 장착 Sidewinder 분리 시점 0.5초로 변경됐다.
- `BattleArena` Play Mode의 5단계 30발 진단에서 릴리즈 직후 `turnStarted=true`, 진행률 `0.008`, 시간 `0.300`을 확인했고, 0.42초 검사에서는 진행률 `1.000`, `turnCompleted=true`와 전체 피드백 검증 성공을 확인했다. 마지막 웨이브와 1초 유지 뒤에는 회전 상태 false, 진행률 0, 살보 ID 0으로 정리됐다.
- 새 EditMode 회귀 테스트는 기본 시간이 정확히 0.3초인지, 시작·25%·50%·종료 시점이 이징된 중간 회전과 정확한 최종 회전을 만드는지 확인한다. 수정 스크립트 진단 오류 0개, 전체 컴파일 오류 0개, EditMode 테스트 64/64 완료·실패 0개였다.

### 2026-08-05 — 5단계 풀차지 자세 180도 반전

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- `PlayerOrbitController`가 사용하던 기존 카메라 정면 표시 회전을 게임 카메라 Up 축 기준 `180도` 회전해 풀차지 반전 자세를 만든다. 씬 직렬화 값은 수정하지 않았고 전용 오버레이 카메라가 그리는 실제 헬기 외형 자세만 바꾸므로 이동 앵커·화면 위치·피격 판정에는 영향이 없다.
- `BattleArena` Play Mode에서 5단계 피드백 진단을 실행해 30발 요청, 락온 단계/릴리즈/부스트/풀살보 피드백 검증 성공과 첫 웨이브 중 `IsFullSalvoFrontViewActive=true`, 살보 ID `1`을 확인했다. 1920×1080 Game View 캡처에서도 카메라를 향하던 이전 풀차지 자세의 반대 방향으로 바뀐 헬기 외형을 확인했다.
- 마지막 발사 웨이브와 1초 유지 시간이 끝난 뒤 상태는 `IsFullSalvoFrontViewActive=false`, 살보 ID `0`으로 정리됐고, Viper 표시 회전은 평상시 측면값 `(8.959, 85.507, 14.921)`로 복귀했다.
- 새 EditMode 회귀 테스트는 임의의 카메라 회전에서도 결과가 이전 카메라 정면 회전의 정확한 180도 반전이고, 헬기 표시 Forward가 카메라 Forward와 일치하는지 확인한다. 수정 스크립트 진단 오류 0개, 전체 컴파일 오류 0개, EditMode 테스트 63/63 완료·실패 0개였다.

### 2026-08-05 — Music/Sound 뒤 디버그 오버랩 제거

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- 원인은 Music/Sound 버튼 자체가 아니라 `GeneratedHUD/DebugPanelRoot`의 740×132 반투명 Image였다. 수정 전 이 Image는 `(16,16)`에서 `raycastTarget=true`로, 위쪽 환경 디버그 패널의 `Undead` 버튼 하단과 겹치며 입력 우선순위를 가졌다.
- 기존 씬을 저장하거나 사용자 씬 변경을 덮어쓰지 않고 `HUDPresenter`가 런타임 바인딩 시 해당 배경 Image를 투명·비활성화하고 `raycastTarget=false`로 강제한다. 디버그 상태 글자는 남기되 글자도 raycast를 받지 않는다.
- BattleArena Play Mode에서 `DebugPanelRoot`가 투명색과 `raycastTarget=false`로 바뀐 것을 확인했다. Music `(28,28)`·Sound `(188,28)` 버튼은 각각 `148×56`, `raycastTarget=true`, `interactable=true`를 유지했고 `Undead` 버튼도 `interactable=true`였다.
- 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, EditMode 테스트 62/62 완료·실패 0개를 확인했다. 진단의 기존 Update 성능 경고 2개는 이번 변경과 무관하다.

### 2026-08-05 — 전역 Sound 기본 OFF와 HUD 버튼

- Git 기준점: `f9ba772` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- `GlobalSoundSettings.SoundEnabled`를 모든 씬에서 접근 가능한 public static 상태로 추가했다. 정적 초기값과 `SubsystemRegistration` 재초기화 값은 모두 OFF다.
- Sound OFF는 Listener 볼륨 0, ON은 1로 적용하고 일시정지는 사용하지 않는다. `GlobalMusicSource`는 Listener 볼륨·일시정지를 모두 무시하므로 Music과 Sound 상태가 독립된다.
- BattleArena Play Mode에서 기존 Music 버튼 `(28,28)`, 크기 `148×56` 오른쪽에 Sound 버튼 `(188,28)`, 같은 크기로 생성되고 기본 `SOUND OFF`를 표시하는 것을 확인했다.
- 같은 Play Mode에서 `AudioListener.pause=false`, `volume=0`을 확인했다. 기관총과 락온 단계·릴리즈·부스트 AudioSource는 `ignoreListenerPause=false`, `ignoreListenerVolume=false`, BattleArena BGM은 두 값이 모두 `true`였다.
- MainMenu를 별도 새 Play Mode로 시작해 Listener 볼륨 0과 `MainMenuMusic`의 `ignoreListenerPause=true`, `ignoreListenerVolume=true`, `mute=true`, `GlobalMusicSource` 등록 상태를 확인했다.
- 새 회귀 테스트 3개가 런타임 초기화 기본 OFF, 전역 볼륨 0/1 전환, Music/효과음 Listener 예외 분리를 확인한다. Unity 전체 컴파일 오류 0개, EditMode 테스트 62/62 완료·실패 0개였다.
- Unity MCP `read_console`의 기존 reflection 초기화 오류는 계속되어 `Editor.log`로 대체 확인했다. 이번 변경과 관련된 실행·컴파일 예외는 없었고, MCP 직렬화 도구의 기존 `TransformHandle` 예외만 확인됐다.

### 2026-08-05 — BGM 기본 OFF

- Git 기준점: `9d63f4f` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- `GlobalMusicSettings`의 정적 초기값과 `SubsystemRegistration` 재초기화 값을 같은 `DefaultMusicEnabled = false` 상수로 통일했다. 따라서 앱 또는 Play Mode를 새로 시작할 때마다 저장값 없이 OFF로 시작한다.
- `MainMenu`를 새 Play Mode로 시작했을 때 `BGM_title.wav` AudioSource가 `mute=true`였고, `playOnAwake` 재생 위치는 기존 규칙대로 계속 진행했다.
- `BattleArena`를 별도 새 Play Mode로 시작했을 때 `BGM_battle_01.ogg` AudioSource가 `mute=true`, `isPlaying=false`였고 전투 HUD 버튼은 `MUSIC OFF`를 표시했다.
- 전역 ON/OFF API와 실행 중 씬 간 상태 유지 방식은 변경하지 않았다. 이번 작업은 새 앱/Play Mode 시작 기본값만 OFF로 바꾼다.
- 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, EditMode 테스트 작업 성공 및 59/59 완료·실패 0개를 확인했다.

### 2026-08-05 — 5단계 풀차지 정면 연출

- Git 기준점: `ac05877` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- `BattleArena` Play Mode의 5단계 풀차지 진단에서 30발 요청과 발사가 정상 완료됐고, 첫 발사 웨이브가 실행되기 전에 `IsFullSalvoFrontViewActive=true`와 살보 ID 연결을 확인했다.
- 1920×1080 Game View 캡처에서 평상시에는 헬기가 화면 우측을 향한 측면 모습이고, 5단계 발사 중에는 기수와 조종석이 게임 카메라를 향한 정면 모습으로 바뀌는 것을 확인했다.
- 실제 `SalvoCompleted`는 마지막 발사 웨이브 뒤 발생하며, 그 이벤트 이후 1초 지연 코루틴이 정면 자세를 해제한다. 해제 후 상태는 `IsFullSalvoFrontViewActive=false`, 살보 ID `0`이었고 보이는 Viper 회전은 발사 전 측면 회전값으로 복귀했다.
- 1~4단계에는 정면 시작 이벤트를 보내지 않는 조건을 코드에서 확인했다. 이번 작업에서는 1~4단계 각각의 화면 캡처는 반복하지 않았다.
- 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, 실행 중 신규 예외 없음, EditMode 테스트 59/59 통과.
- Unity MCP `read_console`의 기존 reflection 초기화 오류는 계속되어 `Editor.log`로 대체 확인했다.

### 2026-08-05 — 전역 음악/메인 메뉴

- Git 기준점: `867a274` 및 기존 사용자 미커밋 작업 트리
- Unity 버전: `6000.4.0f1`
- `MainMenu` Play Mode에서 변경 전 형제 순서가 `MainHellicopter → MainCharacter → Muffler`인 것을 확인한 뒤, 변경 후 `MainHellicopter → Muffler → MainCharacter`로 바뀐 것을 런타임 계층에서 확인했다.
- 1920×1080 Game View 전·후 화면 비교에서 캐릭터 몸과 머리카락이 머플러를 앞에서 가리며, 다른 배경 레이어와 머플러 프레임 애니메이션은 유지되는 것을 확인했다.
- 전역 음악 기본 ON 상태의 `MainMenu` Play Mode에서 `BGM_title.wav`가 `isPlaying=true`, `mute=false`이고 `GlobalMusicSource`에 등록된 것을 확인했다.
- 전역 OFF 전환 후 MainMenu BGM이 `mute=true`인 채 재생 위치를 계속 진행했으며, OFF 상태로 `BattleArena`를 로드했을 때 `BGM_battle_01.ogg`가 `mute=true`, `isPlaying=false`로 유지되어 씬 전환 중 상태가 보존되는 것을 확인했다.
- BattleArena에서 전역 ON 전환 후 전투 BGM이 `mute=false`, `isPlaying=true`로 시작되고 HUD 문구가 `MUSIC ON`으로, 다시 OFF 전환하면 `MUSIC OFF`로 동기화되는 것을 확인했다.
- `MenuPresenter.cs`와 전역 음악 스크립트 컴파일 오류 0개, Unity 전체 컴파일 오류 0개, EditMode 테스트 59/59 통과.
- Unity MCP `read_console`의 기존 reflection 초기화 오류는 계속되어 `Editor.log`로 대체 확인했으며, 이번 변경과 관련된 실행·컴파일 예외는 없었다.

### 2026-08-04

- Git 기준점: `7fd66db` 및 현재 미커밋 `BattleArena` 사용자 작업 트리
- Unity 버전: `6000.4.0f1`
- 확인 범위: MainMenu/BattleArena AudioSource 직렬화 값, 새 BGM 클립 참조, 스트리밍 임포트 설정, 씬별 Play Mode 실제 재생 상태
- `MainMenu` Play Mode에서 `BGM_title.wav`, 볼륨 0.7, 2D, `loop=true`, `isPlaying=true`와 재생 시간 4.97초 진행을 확인했다.
- `BattleArena` Play Mode에서 `BGM_battle_01.ogg`, 볼륨 0.7, 2D, `loop=true`, `isPlaying=true`와 재생 시간 10.30초 진행을 확인했다. HUD 초기화 뒤에도 새 클립 참조가 유지됐다.
- 두 클립 모두 `Streaming`, 백그라운드 로드, 2D 임포트 설정을 사용하며 기존 `battle_arena_bgm.mp3` 참조가 남지 않은 것을 정적 검색으로 확인했다.
- 전체 컴파일에서 C# 오류가 없었고 EditMode 테스트 59/59가 통과했다.
- Unity MCP `read_console`은 프로젝트의 기존 reflection 초기화 오류로 사용할 수 없었다. 대체 확인한 `Editor.log`에는 C# 컴파일 오류가 없었으며, MCP의 `TransformHandle` 직렬화 과정에서 발생한 도구 측 `NullReferenceException` 외에 이번 BGM 변경으로 인한 실행 예외는 확인되지 않았다.
- 사용자 작업인 `BattleArena`의 조명 참조, Viper 스케일 2, 공백 정규화 등 기존 미커밋 씬 변경은 수정 범위와 커밋 대상에서 제외한다.

### 2026-08-01

- Git 기준점: `eef9125` 및 현재 미커밋 `BattleArena` 작업 트리
- Unity 버전: `6000.4.0f1`
- 확인 범위: 플레이어 전투/이동/방어 코드, 락온 규칙과 테스트 코드, 보스 공통 공격/패턴 코드, `BattleArena` 직렬화 값, 차량 상태 카탈로그, 런타임 상태, 락온 개발 사양서
- 검증 방식: 정적 코드/에셋 교차검증, Unity MCP Play Mode 자동 입력, 전체 컴파일, Editor 로그, EditMode 테스트
- Viper 스케일 2와 1920×1080 Game View에서 초기화가 정확히 1회 발생하고 저장값 `aspect=1.778`, 이동 Rect `(0,0)~(1,1)`, 플레이어/외형 중심 `(0.280,0.500)` 일치를 확인했다.
- 자동 이동 결과는 좌 `(0.000,0.490)`, 우 `(1.000,0.487)`, 상 `(0.988,1.000)`, 하 `(0.987,0.000)`으로 전체 viewport 네 변 도달 검증을 통과했다. 확대 외형 일부가 끝 위치에서 화면 밖으로 나가는 것은 현재 의도된 규칙이다.
- 우클릭으로 5개 락을 획득한 뒤 실제 1 피해를 적용한 Play Mode 전용 진단에서 `PlayerDamaged` 취소, `Ready` 복귀, 성공 락/마커 0개, 동일 우클릭 릴리즈 거부, 신규 일제사격 ID·무적 없음이 모두 확인됐다.
- 위 취소 진단 직후 다시 풀차지한 회귀 진단에서 5개 락, 30발 발사 요청, 첫 미사일 이전 무적 시작, `ReuseWait` 진입이 정상 동작했다.
- 이동 속도 Play Mode 진단에서 기본 좌우·상하 7.2, `Charging` 배율 0.6, 유효 속도 4.32, 충전 취소 후 배율 1.0 복구를 확인했다.
- 실제 키 입력을 사용한 전체 viewport 이동 회귀에서 좌 `(0.000,0.480)`, 우 `(1.000,0.477)`, 상 `(0.993,1.000)`, 하 `(0.991,0.000)` 도달을 모두 통과했다.
- 단계별 개별 충전 시간 `1/1.5/2/2.5/3초`와 누적 획득 시점 `1/2.5/4.5/7/10초`를 Play Mode에서 확인했다. 0.99초 조기 릴리즈는 발사 없이 취소됐다.
- 충전 시작 즉시 표시/점멸 마커가 각각 1개였고, 각 단계 완료 직후 표시 마커 수는 `[2,3,4,5,5]`, 점멸 마커 수는 `[1,1,1,1,0]`이었다. 완료 마커는 고정되고 다음 마커만 점멸했다.
- 10초 풀차지 뒤 1초를 더 유지해도 `Charging`, 5개 락, 이동 배율 0.6, 점멸 마커 0개가 유지됐다. 릴리즈 전 피해 적용 시 `PlayerDamaged`로 전부 취소되고 발사되지 않았다.
- 10초 풀차지 릴리즈에서 30발 요청, 첫 미사일 이전 무적, `ReuseWait` 진입이 정상 동작했다. 발사 중 마커 5개 유지와 발사 완료 1초 뒤 제거도 통과했다.
- 수동 기관총 입력이 없는 상태에서 자동 발사 주기를 초기화한 뒤 첫 발사 구간의 탄 생성, 2.25~3.75초 휴지 구간의 발사 수 고정(`12→12`), 4초 이후 다음 발사 구간 재개(`최종 16발`)를 Play Mode에서 확인했다. 현재 검증 환경의 첫 2초 발사 구간에서는 12발이 생성됐다.
- 자동 기관총 적용 뒤 락온 풀차지 5개, 30발 요청, 첫 미사일 이전 무적과 `ReuseWait` 진입 회귀를 다시 통과했다.
- 개틀링 기본 1발 피해 3을 유지한 상태에서 락온 계산 API의 개틀링 인자를 제거하고, Play Mode에서 락온 단계별 총 기본 피해 `9/20/35/60/100`과 1발 피해 `1.8/2/약 2.333/3/약 3.333`을 확인했다.
- 실제 1~5단계 실시간 연속 발사에서 각 단계 피해 예산, `5/10/15/20/30`발 완료, 최대 30발 풀 불변식, 일제사격 무적, 5초 재사용 대기, `SHOOT ERROR` 미발생을 모두 확인했다.
- 수정 스크립트 진단 오류 0개, Unity 전체 컴파일 오류 0개, 신규 실행 예외 없음, EditMode 테스트 59/59 통과.
- 미검증 범위: 실제 19.5:9 모바일 기기 빌드의 터치 이동·Safe Area는 이번 작업에서 실행하지 않았다. 이동 계산 자체는 HUD Safe Area가 아니라 실제 Base 카메라 픽셀 크기와 정규화 viewport를 사용한다.
- 사용자 작업인 `BattleArena`의 Viper 스케일 2와 기타 미커밋 씬·에셋 값은 수정하거나 커밋 대상으로 포함하지 않았다.

## 14. 변경 이력

| 날짜 | 버전 | 변경 내용 | 검증 |
| --- | --- | --- | --- |
| 2026-08-30 | 1.0.38 | 지상 공격대 포구 화염 가시성 보강: 단발 Ambient 즉시 소등 수정, 교차 Quad 확대, 차종별 0.075~0.19초 크기·회전 펄스와 자동 소등 진단 추가 | 전체 EditMode 115/115, 배치 Play Mode 화염 ON→OFF·8대 이동·보스/플레이어/락온 불변식 PASS |
| 2026-08-30 | 1.0.37 | Background Ground Armored Units 기본 8대: 최적화 차량 3종 Prefab, StageVisualRoot 로컬 경로 3개, 3대 종대+지원/독립 이동, 이동·정차 가짜 사격, 고정 VFX 풀, 공중·지상 공용 연출 예산을 BattleArena에 연결 | 집중 10/10, 전체 EditMode 115/115, 배치 Play Mode 8대·17.4~48.3px·접지 Y 0.12~0.20·보스/플레이어/락온 불변식 PASS |
| 2026-08-30 | 1.0.36 | 배경 헬기 모델 전방 반전, 공격 기동 속도 0.5배, 독립 개틀링 총구 화염, 한 대씩 연기·자회전 추락 및 재보충 추가 | 집중 7/7, 이동/기수 최소 내적 0.911, 개틀링 167회, 트레이서 3발, 추락 2.367m·368.07°·연기, 전투 불변식 통과. 신규 전체 작업은 95/112 상태 갱신 손실로 미확정 |
| 2026-08-30 | 1.0.35 | 배경 아군 헬기 이동 경로와 자세를 분리해 월드 수평 전방, 최대 피치 7°·뱅크 8°로 제한. 수직 자세와 후진처럼 보이는 방향 전환 제거 | 집중 EditMode 7/7, 실제 10,910 샘플 최대 Up 편차 9.889°·이동/기수 최소 내적 0.608, 4대·3발·전투 불변식 통과. 신규 전체 작업은 99/112 상태 갱신 손실로 미확정 |
| 2026-08-29 | 1.0.34 | Background Ally Army 공중 Phase 0~2: 500 tris 헬기 단독기 1대+3대 편대 순찰, 로터 블러, 단일 공격 슬롯과 피해 없는 트레이서 풀을 BattleArena에 연결. 탱크 단계는 계획 유지 | 신규 집중 5/5, 전체 EditMode 110/110, BattleArena 4대 생성·3발 공격·복귀, 보스 2000/플레이어 100·120/락온 5 불변식, Collider·Rigidbody 0 |
| 2026-08-29 | 1.0.33 | AGENTS·인덱스의 중심 문서 경로 불일치 해결, docs Markdown 공유 및 로컬 인증 설정 제외 기록 | 경로·링크·Git 범위·자격증명 패턴 점검. 게임 코드 추가 변경 없음 |
| 2026-08-29 | 1.0.32 | 횡단 빔 경로를 보존한 입 소켓 축/실제 목·머리 보정, 클립 방향 선택, 공통 VFX·피해 프레임, 회복/중단 동기화 구현 | EditMode 105/105, 최종 집중 19/19, 실제 전투 44조합/1396 프레임 최대 각도 오차 0.13706°, 중단·사망·포즈 회귀 |
| 2026-08-29 | 1.0.31 | 가속 횡단 빔 92도 폴백/화면 경로 구분, 입 방향 동기화 미연결·문서 경로 불일치 기록, 개발계획 연결 | 관련 코드·씬 정적 교차검증만. 게임 변경·신규 실행 테스트 없음 |
| 2026-08-05 | 1.0.30 | 정상 5락 발사 완료 후 기존 1초 목표 자세 유지는 보존하고, 평상시 우측 측면 자세로 돌아가는 보이는 외형에만 0.3초 시간 기반 `Slerp`·`SmoothStep` 보간 추가. 취소 경로는 즉시 복귀 유지 | BattleArena 실제 5락 복귀 중간 프레임·최종 각도 오차 0도와 기존 30발/Sidewinder/무적/진동/헬기 고정 `verified=True`, EditMode 78/78 |
| 2026-08-05 | 1.0.29 | 공통 락 릴리즈에서 화면 진동을 제거하고 성공한 실제 5락 풀살보 이벤트에서만 시작하도록 분리. 1~4락 발사·피해·무적은 유지 | BattleArena 1~5락 진동 `False/False/False/False/True`, 실제 5락 피드백 `verified=True`, EditMode 77/77 |
| 2026-08-05 | 1.0.28 | 임시 3·4락의 5락 실행 프로필 승격을 기본 OFF로 복구. 원본 `3락=15발/35`, `4락=20발/60`, `5락=30발/100`을 다시 적용하고 헬기 회전·장착 Sidewinder 등 풀살보 연출은 실제 5락에서만 실행 | BattleArena 1~5단계 `5/10/15/20/30발` 통합 진단 및 실제 5락 피드백 `verified=True`, 전체 컴파일, EditMode 77/77 |
| 2026-08-05 | 1.0.27 | 새 전투 구성 시 이전 입력 잠금을 해제하고, 보스 중심·주시 대상 참조가 없어도 헬기 키 이동을 처리하도록 완전 차단 경로 2개 제거 | 대상 없는 실제 Input System 입력 및 재구성 입력 복구 회귀 2/2, BattleArena A/D/W/S 좌표 변화, 전체 컴파일, EditMode 77/77 |
| 2026-08-05 | 1.0.26 | 기본 단계별 진폭은 보존하고 육안 판별용 임시 `×8` 배율을 추가해 5락 유효 진폭을 `0.0600`으로 확대. 테스트 종료 시 단일 상수를 `1`로 복원 가능 | 실제 5락 월드 화면 피크 53.81px, 헬기/시각 루트/플레이어/오버레이 카메라 drift 전부 0, 30발·무적 종료·투영 복구 `verified=True`, 전체 컴파일, EditMode 75/75 |
| 2026-08-05 | 1.0.25 | 실행은 됐지만 Full HD 약 1~2px라 보이지 않던 일제사격 월드 투영 진동을 단계별 `0.0030~0.0075`로 조정하고, 고정 월드 지점의 실제 화면 픽셀 이동을 진단 성공 조건에 추가 | 실제 5락 월드 화면 피크 5.87px, 헬기/시각 루트/플레이어/오버레이 카메라 drift 전부 0, 30발·무적 종료·투영 복구 `verified=True`, 전체 컴파일, EditMode 75/75 |
| 2026-08-05 | 1.0.24 | Sidewinder 분사 VFX와 분리형 장착 미사일을 헬기 중심 Bounds에서 제외하고, 일제사격 동안 오버레이 카메라 자세·투영과 플레이어 이동 투영 기준을 진동 전 상태로 고정. 유효한 이동 좌표의 불필요한 카메라 왕복 변환도 제거해 화면 진동이 이동 앵커·피격 판정으로 전달되는 경로 차단 | 실제 3·4·5락 모두 월드 카메라 진동 피크 발생, 헬기 화면/시각 루트/플레이어 월드/오버레이 카메라 drift 전부 0, 각 30발 완료 `verified=True`, 전체 컴파일, EditMode 74/74 |
| 2026-08-05 | 1.0.23 | 헬기 0.3초 회전 완료 뒤에만 장착 Sidewinder가 1초간 후방 분사하도록 순서를 변경. 분리 후 0.5초간 속도 5를 유지한 뒤 가속도 20으로 순항 35까지 상승. 기체 진동 대신 진폭 0.002의 약한 월드 카메라 진동을 일제사격 무적과 같은 수명으로 적용하고 헬기 전용 투영은 고정 | 실제 3·4·5락에서 즉시 분사 0→회전 완료 후 분사 2→분리 2, 초기 속도 5와 가속, 무적/진동 동시 종료, 헬기 안정·투영 복구, 각 30발 완료 `verified=True`, 전체 컴파일, EditMode 70/70 |
| 2026-08-05 | 1.0.22 | 우클릭 즉시 5락 기본값을 OFF로 바꿔 정상 시간 충전을 복원. 원본 단계 배열은 보존한 채 테스트 플래그가 실제 3·4·5락을 모두 5락의 30발/피해 100/Sidewinder/회전/1초 무적 프로필로 승격하며, 플래그 OFF 시 원본 3/4/5 동작 복원 | `MouseRight` 시작 직후 0락, 실제/프로필 `3/5·4/5·5/5`, 각 30발 요청·완료와 전체 피드백 `verified=True`, Sidewinder 2발 복귀, 전체 컴파일, EditMode 69/69 |
| 2026-08-05 | 1.0.21 | 실제 장착 Sidewinder의 점화를 0.5초에서 1초로 연장하고 분사 파티클·주황색 궤적을 강화. 피해 없는 연출용 비행 속도를 전투 미사일과 분리하고, 30발 발사가 먼저 끝나도 Sidewinder 분리 전에는 5단계 완료·무적·마커를 조기 종료하지 않도록 동기화 | Stage 5 점화 중 분사 2/분리 0, 비행 중 분리 2, 완료 후 복귀 2와 바인딩 오류 없음, `verified=True`, 전체 컴파일, EditMode 68/68 |
| 2026-08-05 | 1.0.20 | 쉬운 반복 테스트를 위해 PC 우클릭 충전 시작 시 5개 락을 즉시 완료하는 임시 플래그를 기본 ON으로 추가. 모바일 정상 시간 충전, 릴리즈 발사, 피격 취소, 이동 감속, 5초 재사용 대기는 유지. 반복 발사 중 Sidewinder 파티클 설정 경고도 제거 | 실제 `MouseRight` 소스 Stage 5 Play Mode에서 즉시 마커 5개·30발·`verified=True`, 전체 컴파일, EditMode 68/68 |
| 2026-08-05 | 1.0.19 | 5단계에서 실제 장착 Sidewinder 2발을 피해·30발 풀과 분리한 0피해 시각 연출로 추가. 0.5초 장착 점화 뒤 동시 분리, 명중/최대 6초 후 개별 복귀. 5단계 무적·자세/마커 완료 기준을 분리 시점으로 변경하고, 1~5단계 전투 미사일 외형을 검은 원통+원뿔로 교체 | 30발·기본 총피해 100·첫 발 이전 무적 회귀, 장착점 2개/분리 2/복귀 2 런타임 상태, 풀 오브젝트 외형 계층, 전체 컴파일, EditMode 67/67 |
| 2026-08-05 | 1.0.18 | 5단계 풀차지 릴리즈 시 헬기 표시 자세를 즉시 전환하지 않고 0.3초 동안 시간 기준 `Slerp`·`SmoothStep`으로 보간. 미사일 발사·무적 타이밍은 기존대로 병행 | Stage 5 Play Mode 시작/완료 진행률, 30발 피드백·복귀 상태, 스크립트 진단, 전체 컴파일, EditMode 64/64 |
| 2026-08-05 | 1.0.17 | 5단계 풀차지 일제사격 헬기 자세를 기존 카메라 정면 자세에서 카메라 Up 축 기준 정확히 180도 반전. 기존 첫 웨이브 이전 적용, 마지막 웨이브 뒤 1초 유지와 측면 복귀 규칙은 보존 | 30발 Stage 5 Play Mode, 1920×1080 캡처, 반전/복귀 상태와 회전, 스크립트 진단, 전체 컴파일, EditMode 63/63 |
| 2026-08-05 | 1.0.16 | Music/Sound 버튼보다 큰 `DebugPanelRoot` 반투명 배경 Image를 비활성화하고 배경·디버그 글자의 raycast를 제거해 환경 디버그 `Undead` 버튼 입력 차단 해소 | BattleArena 런타임 배경 투명·비raycast, 세 버튼 활성 상태, 스크립트 진단, 전체 컴파일, EditMode 62/62 |
| 2026-08-05 | 1.0.15 | 모든 씬에서 접근 가능한 `GlobalSoundSettings.SoundEnabled`를 추가하고 BGM 외 효과음을 기본 OFF로 일괄 제어. BattleArena의 Music 버튼 오른쪽에 독립 `SOUND ON/OFF` 버튼을 추가하고 BGM만 Sound 제어에서 제외 | BattleArena 버튼 위치·기본 문구, Listener 볼륨 0, BGM/효과음 예외 분리, 전체 컴파일, EditMode 62/62 |
| 2026-08-05 | 1.0.14 | 전역 BGM의 앱/Play Mode 시작 기본값을 ON에서 OFF로 변경. 실행 중 ON/OFF 전환과 씬 간 상태 유지는 기존대로 유지 | MainMenu/BattleArena 새 Play Mode 기본 음소거, 전투 HUD `MUSIC OFF`, 스크립트 진단, 전체 컴파일, EditMode 59/59 |
| 2026-08-05 | 1.0.13 | 5단계 풀차지 일제사격 시 첫 미사일 이전에 보이는 헬기만 게임 카메라 정면으로 전환하고, 마지막 발사 웨이브 완료 1초 뒤 평상시 측면 자세로 복귀하도록 구현. 발사 시작 거부·취소 시 즉시 복귀하는 안전 처리 포함 | 30발 풀차지 Play Mode, 1920×1080 전·후 화면, 살보 ID/정면 상태/회전 복귀, 스크립트 진단, 전체 컴파일, EditMode 59/59 |
| 2026-08-05 | 1.0.12 | 모든 씬에서 접근 가능한 `GlobalMusicSettings.MusicEnabled`와 BGM 등록 컴포넌트를 추가하고 MainMenu/BattleArena 음악 및 전투 HUD 버튼을 하나의 전역 ON/OFF 상태로 통합 | MainMenu OFF, OFF 상태 BattleArena 전환, 전투 BGM ON 재생, HUD ON/OFF 동기화, 전체 컴파일, EditMode 59/59 |
| 2026-08-05 | 1.0.11 | MainMenu 런타임 배경에서 머플러를 캐릭터 바로 뒤 레이어로 이동 | Play Mode 형제 순서·1920×1080 화면 비교, 스크립트 진단, 전체 컴파일, EditMode 59/59 |
| 2026-08-04 | 1.0.10 | MainMenu에 `BGM_title.wav` 반복 BGM을 추가하고 BattleArena의 기존 음악을 `BGM_battle_01.ogg`로 교체. 두 클립을 스트리밍·2D로 설정하고 구형 전투 MP3를 제거 | 씬별 Play Mode 실제 재생·시간 진행, 직렬화/GUID 정적 확인, 전체 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.9 | 락온 미사일 피해를 개틀링 기준값에서 분리하고 성공 락 1~5개 총 기본 피해를 `9/20/35/60/100` 고정값으로 변경 | 독립 계산 API, 단계별 1발 피해, Play Mode 5단계 피해표, 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.8 | 개틀링 기본 1발 피해를 25에서 3으로 변경하고 이를 참조하는 락온 1~5단계 총 기본 피해를 `9/12/15/18/30`으로 갱신 | 코드·프리팹·런타임 기본값 3, 락온 피해 표, 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.7 | 기관총의 좌클릭/Space 입력을 제거하고 전투 중 2초 자동 발사/2초 휴지 주기로 변경. 런타임 HUD 조작 안내도 자동 기관총 기준으로 교체 | 입력 없음 상태에서 첫 발사, 휴지 구간 발사 수 `12→12`, 4초 이후 재발사, 락온 30발 회귀, 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.6 | 단계별 충전 시간을 1/1.5/2/2.5/3초로 변경하고, 충전 시작 즉시 현재 타깃 마커를 점멸·스케일 왕복으로 표시한 뒤 완료 마커를 고정하도록 변경 | 누적 1/2.5/4.5/7/10초, 시작 마커 1개, 단계별 표시 `[2,3,4,5,5]`·점멸 `[1,1,1,1,0]`, 풀차지 감속/피격 취소, 30발 발사·마커 유지, 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.5 | 첫 락 획득을 3초로 연장하고 기존 후속 단계 간격을 유지해 누적 충전 시점을 3.0/3.4/3.9/4.45/5.15초로 변경 | 2.99초 조기 릴리즈 차단, 3.0초 1락, 3.4초 2락, 5.15초 30발 풀차지 Play Mode 진단, 스크립트 진단, 전체 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.4 | 실제 좌우·상하 이동 기본값을 8에서 7.2로 10% 감속하고 락온 `Charging` 중 0.6 배율을 적용해 4.32로 감속 | Play Mode 상태별 7.2/4.32/복구 진단, 실제 키 입력 viewport 네 변 도달, 스크립트 진단, 전체 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.3 | 락온 충전 중 실제 플레이어 피해가 적용되면 충전·락·마커·입력을 즉시 취소하고 같은 입력 릴리즈의 발사를 차단 | 우클릭 5락 후 실제 피해 Play Mode 진단, 취소 후 풀차지 30발 회귀, 스크립트 진단, 전체 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.2 | 승리 시 `R` 재시작과 패배 시 Retry/Quit 흐름을 구분하고 구현 기준점을 최신 커밋으로 갱신 | YAML/Markdown 구조, 소스 경로 27개, 피해 공식 정적 검증 |
| 2026-08-01 | 1.0.1 | 실제 Base 카메라 화면비 기반 최초 1회 전체 viewport 이동, 런타임 이동 박스 동기화, Renderer 크기 기반 범위 축소 제거, 확대 외형 화면 중심 보정 규칙 반영 | 1920×1080 Play Mode 네 변 자동 이동, 외형 중심, 컴파일, EditMode 59/59 |
| 2026-08-01 | 1.0.0 | 최초 SSOT 작성. 기관총, 새 락온 미사일, Hull/Armor, 활성 보스 4패턴, 레거시 제거 상태, 유지 절차 정리 | 코드·씬·에셋 정적 교차검증 |
