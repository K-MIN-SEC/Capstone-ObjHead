# 0916 확장 작업 기록

## 확정된 리볼버 규칙

- 1번: 리볼버 직선 사격, 차징에 따라 탄속이 달라지지 않음.
- 2번: 초록 플레어 포물선 투사, 범위 안의 자신·아군·적군 모두 치유. 최대 체력 상한, 부활 없음.
- 3번: 빨간 플레어 포물선 투사, 탄착 지점의 공습 범위 안에서 자신·아군·적군 모두 피해.
- 머리 그림은 세 종류 모두 손잡이·방아쇠·방아쇠울 없이 총열과 상부 몸통만 사용.

## 에셋

`Assets/Art/Characters/RevolverHeads.png`: 3칸 가로 아틀라스. 내장 이미지 생성 도구로 원본 스타일을 참조해 생성 후 손잡이·방아쇠 제거 편집. 투명 알파 유지.

최종 편집 프롬프트:

> Keep this transparent three-head sprite atlas exactly, but clean up the undersides. On the LEFT gray revolver, DELETE the small gray triangular trigger-like piece hanging beneath the barrel to the right of the cylinder. On ALL THREE heads, remove the downward pointed grip remnants under the rear frame; make the underside a short smooth closed contour directly under the upper body, with NO trigger, NO trigger guard, NO handle, NO downward nub. Preserve barrels, cylinder, hammers, colors, thick black cartoon outlines, three-cell spacing, right-facing orientation and genuine transparent alpha. No other changes.

## 상태

리볼버·자석·주전자 프리팹과 9개 신규 스킬을 연결해 총 6종/18개 머리를 구성했습니다. 자석과 주전자는 테스트용으로 제안·구현한 캐릭터이며 최종 밸런스 확정은 아닙니다.

## 추가한 플레이 기능

- 자석: 인력탄 / 척력탄 / 넓은 전자석 펄스. 아군도 영향을 받습니다.
- 주전자: 온수 회복 / 피해·감속 증기 장판 / 지연 압력 폭발. 회복과 폭발은 진영을 가리지 않습니다.
- 공용 머리: 빨간 치유 포션, 얼음 감속 장판, 철모 피해 흡수, 연막. 기존 공격·날개·구름과 합쳐 7종입니다.
- 철모는 이동 기술이 아니라 다음 피해를 흡수하는 방어 수단입니다. 연막은 내부 캐릭터의 조준 표시를 가리며 무적이나 피해 감소 효과는 없습니다.
- 흙덩이/구름 이미지를 실제 생성되는 픽셀 지형에 사용합니다. 구름도 충돌·파괴 가능한 지형입니다.
- 섬 3개를 능선·동굴·아치·물 틈이 있는 대칭 지형으로 수정했습니다. 시작 발판의 높이는 동일합니다. 높이가 같다고 모든 전략적 유불리가 검증된 것은 아니므로 플레이테스트가 더 필요합니다.
- 타이틀 홈 → 게임 시작/방 만들기/설정. 게임 시작 → 빠른 매칭/코드로 방 참가/훈련장/한 PC 플레이.
- 훈련장은 팀을 고른 뒤 시작하며 긴 턴 시간, 쿨다운 초기화, 메뉴의 맵 초기화를 제공합니다.
- 캐릭터 편성은 목록 데이터 + 검색 + 역할 필터 + 스크롤입니다. 80개 가상 항목의 검색/필터 검사를 추가했습니다.
- 해상도 5종, 창/테두리 없는 창/전체화면, BGM/SFX, 한국어/영어 설정. 화면 변경을 확인하지 않으면 되돌립니다.
- 걷기·차징·던지기·넉백 피격·점프/낙하/착지 몸체 프레임과 기존 3종 머리를 새로 연결했습니다.
- 기존 공용 공격 폭탄·날개·구름도 `LegacyCommonHeads.png`로 다시 제작하여 필드·인벤토리·장착에 공통 연결했습니다. 전구 점멸도 새 머리 그림을 사용합니다.
- 오디오는 사용자 요청에 따라 임시 Throw/Impact 효과음을 제거했습니다. `Assets/Art/Audio/IslandLoop.wav`만 타이틀 씬에서 재생하고 전투/훈련장에서는 멈춥니다. 타이틀 복귀 시 다시 재생하며 BGM 슬라이더를 즉시 반영합니다. SFX 설정값은 향후 음원용으로 유지하지만 현재 이 두 효과음은 재생하지 않습니다.

## 수정하는 곳

- 문구/밸런스: `Assets/GameData/ObjectHeadData.xlsx` → 기존 Object Head 시트 가져오기 메뉴로 반영. 번역/수치 시트를 늘렸고 계산기 시트는 유지했습니다.
- 캐릭터 목록·역할·프리팹·공용 머리와 보급품 표시 크기: `Assets/Resources/ObjectHeadContent.asset`.
- 신규 스킬: `Assets/GameData/Skills/*.asset`. 기존 효과를 조합한 캐릭터는 전용 실행 코드 대신 스킬 자산을 연결합니다. 새로운 효과 동작 자체는 코드가 필요합니다.
- UI 위치/크기: `Assets/Prefabs/UI/ObjectHeadTitle.prefab`, 각 전투 씬의 BattleUI. 런타임에 고정 좌표로 재생성하지 않습니다.
- 몸체/머리 위치·크기: `Assets/Prefabs/Characters/*.prefab`의 BodyRenderer/HeadRenderer Transform. 에디터 수정값을 보존합니다.
- 맵 실루엣: `Assets/GameData/Maps/*.asset`의 폴리곤 및 Bake. 스폰 마커는 전투 씬에서 편집하고 안전 검사로 확인합니다.
- `Apply Expansion`은 최초 연결용 **명시적 재생성 명령**입니다. 실행하면 확장 UI/프리팹/맵을 다시 작성하므로, 수동 편집 후에는 실행하지 마세요. 일반 빌드/플레이에서는 실행되지 않습니다.

## 검증과 남은 범위

- Windows 빌드와 로컬 확장 검사: 캐릭터 6종, 머리 18개, 회복의 자신/아군/적군 적용, 최대 HP 상한, 철모 흡수, 자석 방향, 구름 충돌·파괴.
- 3개 맵 × 3개 모드, 216개 스폰 시드, 승패 처리 검사 통과.
- Nakama 로컬 두 클라이언트: 매칭·편성·시작·스냅샷·턴·발사·지형 전달 검사 통과. 이것은 인터넷 공개 배포 또는 전체 PvP 품질 보증이 아닙니다.
- 공용 머리 획득/사용/인벤토리 및 철모 상태의 네트워크 권한 처리는 아직 별도 보강이 필요합니다. 현재 공용 머리 검증은 로컬 기준입니다.
- 방 찾기는 현재 코드 참가입니다. 공개 방 목록 조회는 미구현입니다.
- 아이언메이든 자물쇠는 제안 단계이며 이번 구현에 넣지 않았습니다.
- 4인 장시간 실전, 재접속, 호스트 이탈, 외부 서버 배포, 모든 신규 스킬의 다중 클라이언트 결과 일치, 접근성/전 해상도 시각 검수는 남아 있습니다. 출시 완료 상태로 표기하지 않습니다.
