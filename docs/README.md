# Titan Destroyer 문서 안내

현재 게임 시스템의 단일 기준 문서는 아래 문서다.

- **[Titan Destroyer 게임 시스템 중심 문서](cur_state/titan-destroyer-game-system-master.md)** — 플레이어 공격/방어 수치, 락온 미사일, 보스 공격 패턴, 전투 흐름, 구현 상태, 변경 절차

중심 문서는 `docs/cur_state/`에 있으며 `AGENTS.md`도 같은 경로를 지정한다. 2026-08-29 공유 전 점검에서 경로 안내를 일치시켰다. 파일을 복제하거나 과거 기획서를 대체 기준으로 사용하지 않는다.

그 밖의 문서는 특정 기능의 개발 사양, 과거 기획, 디버그/리팩터링 계획이다. 내용이 중심 문서와 충돌하면 중심 문서의 상태 표기와 최신 구현 검증 결과를 우선 확인한다.

## 개발계획서

- [Background Ally Army](background-ally-army-development-plan.md) — **공중 구현됨 / 지상 계획**, 500 tris 헬기 단독기·3대 편대, 절반 속도 공격 기동, 개틀링 총구 화염, 단일 랜덤 연기 추락을 BattleArena에 연결. 다음 단계는 탱크 개별 분리·종대·포격 연출.
- [Kaiju 가속 횡단 빔·입 방향 동기화](kaiju-sweep-beam-alignment-development-plan.md) — **구현됨**, 기존 횡단 동작을 보존하는 목·머리 보정과 검증 결과. EditMode 105/105, 실제 전투 44조합 통과; 실기기/전체 아트 승인 별도.
- [Kaiju 애니메이션 통합](kaiju-animation-integration-plan.md) — 기존 애니메이션 통합의 사양·구현 이력.
