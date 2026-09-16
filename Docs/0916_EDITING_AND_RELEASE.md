# 0916 제작/검증 안내

## 이번 규칙 변경
- 2인 개인전: 플레이어당 3캐릭터.
- 4인 개인전: 플레이어당 1캐릭터.
- 4인 2대2: 플레이어당 1캐릭터. P1·P3 / P2·P4가 같은 팀.
- 3인 모드는 타이틀, 매칭, 모드 카탈로그에서 제외.
- 2대2 턴 순서는 양 팀 교대. 같은 팀이 모두 탈락했을 때 팀 패배.
- 기존 폭발/지형/낙하 피해 규칙은 유지(아군 피해 있음). 인벤토리는 기존대로 플레이어 소유.
- 동일 종류 캐릭터 중복 선택 가능. 모드/캐릭터 변경 시 준비 해제.
- 선공과 좌우 배치를 시드로 변경. 4인 개인전은 시작 자리를 섞되 모든 클라이언트가 같은 시드를 사용.

## 직접 편집하는 곳
- 타이틀: Assets/Scenes/ObjectHeadTitle.unity. Canvas 아래 버튼·문구·이미지를 Scene/Inspector에서 편집.
- 타이틀 공통 원본: Assets/Prefabs/UI/ObjectHeadTitle.prefab. 런타임은 위치나 크기를 재생성하지 않음.
- 전투 UI: 각 전투 씬의 BattleUI/Canvas. 레이아웃은 해당 씬에 저장.
- 캐릭터: Assets/Prefabs/Characters. BodyRenderer / HeadRenderer의 위치·크기·스프라이트를 편집.
- 번역·기존 수치: Assets/GameData/ObjectHeadData.xlsx. 기존 가져오기 메뉴로 반영.
- 모드·캐릭터·맵 목록·팀 색: Assets/Resources/ObjectHeadContent.asset.
- 서버 주소: Assets/Resources/ObjectHeadNetworkConfig.asset. 실제 외부 서버 배포와 주소 설정은 별도 필요.
- 맵 원본: Assets/GameData/Maps/*.asset. 폴리곤 좌표(픽셀)와 팔레트 수정 후 Bake Terrain Texture.
- 지형 위치: 각 맵의 Map/Terrain Transform.
- 스폰 위치: Map 아래 각 모드의 spawn markers. 지정한 자리의 높이와 실제 충돌 지형이 맞지 않으면 명시적인 오류로 거부.
- 물: Water 오브젝트의 트리거 상단과 WaterSurfaceMarker를 함께 이동.
- 일회성 생성기 Install Authored Scenes는 기존 콘텐츠가 있으면 중단함. 빌드가 레이아웃을 덮어쓰지 않음.
- Assets/Editor/Authoring/*.json은 최초 제작 기록. 설치 완료 후 레이아웃의 기준은 씬/프리팹.

## 맵 의도
- WindMeadow: 같은 높이의 4개 시작 구역, 완만한 양측 엄폐, 연속된 지상 이동 경로.
- TwinCitadels: 같은 높이의 방어 구역, 파괴 가능한 아치와 내부 공동.
- ShatteredReef: 같은 높이의 4개 섬, 좁은 점프 구간과 낙하 위험.
- 스폰은 낮은 지형까지 무작위 탐색하지 않고 지정 발판만 사용.
- 높이·발판 폭·주변 엄폐 형태를 맞춤. 4인 개인전의 가운데/가장자리 공격 방향 차이까지 없어지는 것은 아님.
- 기하학 검사와 실제 체감 밸런스는 구분. 승률/첫 피격/이동량/초기 집중 공격을 사람이 플레이하며 추가 측정해야 함.

## 씬 정리
기존 6974 / 8529 / 99999 / SampleScene / TerrainTestScene은 Assets/Scenes/Archive로 이동.
GUID를 보존하므로 이전 자료는 복구 가능. 실행 빌드는 타이틀과 새 3개 맵만 포함.

## 출시 전 남은 필수 항목
- 현재 Nakama는 개발용 로컬 서버이며, 외부에서 접속되는 배포 서버가 아님.
- 현재 전투는 클라이언트 호스트 권한 + Nakama 중계. 서버 권한의 치트 방지/입장 거부/상태 검증으로 바꿔야 공개 서비스에 적합.
- 방장 이탈 시 안전 종료 안내. 방장 승계/재접속 복원은 아직 없음.
- 공용 머리 획득·소모, 모든 스킬 효과, 재접속, 지연/손실 환경, 외부망에서의 장시간 대전은 별도 회귀 검증 필요.
- 지형 작업 수신 검사는 전체 픽셀 마스크 일치 검사가 아님. 기존 클라이언트의 예측 지형 변경과 호스트 작업 재적용 중복, 생성 지형의 캐릭터 충돌 제외 영역 차이를 추가로 정리해야 함.
- 기존 외부 에셋의 출처/라이선스 확인, 플랫폼 배포 패키징, 접근성/패드 전체 메뉴 탐색도 출시 게이트.
- 따라서 이 변경본은 검증용 개발 빌드이며 출시 완료라는 의미가 아님.

## 재현 가능한 검증
- 에디터: Object Head > 0916 > Validate Release Assets.
- 로컬 자동 실행: ObjectHead.exe -objectHeadReleaseSmoke -objectHeadCapture [결과폴더]
- 매칭 자동 실행: 각각 다른 프로필로 -objectHeadSmokeProfile [고유이름] -objectHeadSmokeMode Duel 또는 FreeForAll 또는 Teams.
- 실행 결과는 Unity 로그의 RELEASE_PASS / OBJECT_HEAD_SMOKE_PASS를 확인. 실행하지 않은 항목을 통과로 간주하지 않음.
