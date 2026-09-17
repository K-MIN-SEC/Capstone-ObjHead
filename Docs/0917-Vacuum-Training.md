# 0917 청소기 및 훈련장 작업

## 변경 범위
- 자석 캐릭터의 카탈로그 항목을 청소기로 교체한다. 저장된 팀 구성을 보존하기 위해 내부 numeric ID 4와 `magnet.prefab` GUID는 유지한다. 이전 자석 기술은 해당 프리팹에서 더 이상 사용하지 않는다.
- 1번: 조준 직선 폭 안의 캐릭터를 밀기. 차징으로 사거리/힘 증가. 직접 피해 없음.
- 2번: 동일 범위 캐릭터를 당기고 가까운 공용머리 최대 3개를 물리적으로 끌어오기. 진영 구분 없음. 지형을 통과하지 않으며 인벤토리에 강제 지급하지 않음. 도중 다른 캐릭터가 획득할 수 있음.
- 3번: 차징 높이에 따라 일정 시간 지면 위로 떠서 수평 이동. 추가 잔여 이동시간. 충돌 유지, 효과 종료 뒤 착지 보호. 물 자체의 사망 판정은 제거하지 않음.
- AI는 1/2번을 포물선이 아닌 직선 범위로 평가한다. 3번을 이용한 AI 경로 탐색은 아직 추가하지 않았다.
- 주전자 재설계 및 추가 CC 캐릭터는 보류.

## 훈련장
- 전체 머리 인벤토리: 카탈로그에 등록된 캐릭터의 각 3개 기술과 공용머리를 동적으로 나열.
- 이름 검색, 전체/캐릭터/공용머리 필터, 스크롤. 항목 템플릿/그리드/패널 위치는 씬에서 편집 가능.
- 캐릭터 종류를 늘리면 목록도 증가한다. 80종을 고정 배열로 작성하지 않았다.
- 로컬 훈련장만 쿨타임 무시, 공용머리 무소모 사용. 방패/날개/지우개도 기존 실제 실행 경로 사용.
- 타이머 정지는 조준 시간만 멈춘다. 잔여 이동, 지연 기술, 피해 결산을 마친 후 같은 캐릭터로 재시도한다. 수동 턴 종료는 다음 캐릭터로 이동한다.
- 일반/온라인 대전에는 무제한 선택 권한을 제공하지 않는다.
- 선택 패널은 유한 횟수 표시 함수를 받을 수 있다. 호리병 3번에 재사용할 기반이며, 호리병 자체 1/2/3 스킬 구현 완료를 의미하지 않는다.

## 편집 위치
- `Assets/GameData/ObjectHeadData.xlsx`: 청소기 한국어/영어, 사거리, 힘, 폭, 흡입 수/속도/시간, 체공 높이/시간, 추가 이동시간, 쿨타임.
- `Assets/GameData/VacuumTuning.asset`: 세부 연출·상승속도·지면 탐색 설정. 시트에 같은 항목이 있으면 시트가 우선.
- `Assets/GameData/Skills/vacuum_1~3.asset`: 효과 종류 및 설정 연결.
- 각 전투 씬의 `HeadInventory`/`TrainingHeads`: UI 배치와 슬롯 템플릿.
- `Object Head/Replace Magnet with Vacuum`는 명시적인 초기 설치 메뉴다. 평소 실행/빌드는 UI 배치를 다시 만들지 않는다.

## 남은 작업
- 호리병 1/2/3 및 유한 인벤토리 연동, TV/냥캣 공용머리.
- 헬기 공격의 전용 소리·로터·MG/런처별 조종사 아트 마무리.
- 중앙 서버 온라인 통합 검증. 이번에는 서버를 켜지 않는다.
- 전체 출시 품질 검증 및 밸런스.

## 아트
- imagegen 스킬, 내장 이미지 생성 도구 사용. 최종 에셋: `Assets/Art/Characters/VacuumHeads0917.png`.
- 프롬프트: “Transparent 2D game sprite sheet. Exactly three separated vacuum-cleaner object heads in one horizontal row of equal square cells. Same squat round teal canister vacuum, short thick ribbed hose and wide nozzle. Left blowing right, middle sucking right, third nozzle downward for hovering. No face, body, legs or hands. Chunky imperfect black ink outlines, two-tone flat cartoon shading, muted teal, cream nozzle, orange detail. Readable at 64px. Transparent background and gutters. No floor, scenery, labels, text or watermark. Wind animated separately in game.”

## 검증
- 2026-09-17 Unity Windows 빌드 성공 (`Work/vacuum-build4.log`). 실행 파일은 `Builds/Latest/ObjectHead.exe`.
- Vacuum PASS: 차징 거리/힘, 밀기/당기기 방향, 직선 밖 제외, 지형 차단, 가까운 3개 머리, 체공/수평 이동/만료/착지 보호.
- Training PASS: 타이틀과 같은 Pending 진입 경로의 버튼 노출, 현재 등록된 29개 선택 항목(캐릭터 18스킬 + 공용머리 11종), 검색, 입력 차단, 다른 캐릭터 기술 및 고유 탄환 그림 유지, 방패의 실제 효과와 반복 사용, 팀 인벤토리 무소모.
- Support PASS: 동굴 천장/관통 통로, 6칸, 수정구 이동/막힌 위치 거부, 헬기 무피해 예고 및 최종 공격/결산, 카메라 대기, 지형 부스러기.
- Release PASS: 18개 맵·모드 조합, 432개 스폰 시드 검사, 타이틀/로비와 승리 판정.
- CombatFix PASS: 공용머리 충돌/낙하, 지연 치유/공습/포획, 카메라 가장자리 조작.
- AI PASS: 랜덤 구성, 난이도 데이터, 실제 기술 사용, 사람 입력 차단. 이번 검사는 AI의 체공 이동·최적 경로를 보증하지 않는다.
- 실제 빌드의 선택창/청소기/동굴/헬기 캡처를 확인했다. 캡처용 카메라 전환이 Overlay UI 순서를 바꾸던 테스트 도구 문제도 수정했다.
- 자동 검사와 캡처는 사람이 직접 플레이한 장기 QA 및 중앙 서버 통합 시험과 동일하지 않다. Unity 종료 로그의 ComputeBuffer 정리 경고는 별도 추적 대상이다.
- 원본 0916으로 반영할 때 변경 전 해시 대조와 파일 백업을 사용하는 `Work/Sync-Expansion.ps1`의 허용 목록 `Work/vacuum-sync-paths.txt`를 적용한다. 커밋/푸시는 하지 않는다.
