# 섬 지형 · 방향키 · 하늘 낙하 수정

> 아래 지형 3종/폭 60 내용은 1차 작업 기록입니다. 이후 요청에 따라 5종/폭 44·높이 32와 파괴 가능한 건물로 교체했습니다. 현재 맵·AI·지우개 기준은 `0916-Vertical-Islands-AI-Eraser.md`를 확인하세요. UI·입력·하늘 낙하 수정은 유지됩니다.

## 적용 사항

- AI 대전 버튼의 폭/높이, 글자 크기를 훈련장 버튼과 일치시킴. 위치와 동작은 유지. 프리팹 RectTransform으로 직접 편집 가능.
- 방향키 이동 원인: 게임패드 보조 입력이 키보드 방향키도 포함하는 `Horizontal` 축을 읽었음. `ObjectHeadLeftStickX` 조이스틱 전용 축으로 분리. 좌우 방향키는 방향 전환, 위아래는 조준 각도, A/D는 이동 유지.
- 공습 폭탄 표시 높이 0.7 → 1.5, 치유 보급 0.8 → 1.6, 아이언메이든 1.8 → 2.6 월드 단위. 피해/치유 범위와 수치는 변경하지 않음.
- 최대 축소 및 전체 보기에서 보이는 하늘 위쪽 밖에서 낙하. 현재 카메라를 움직여도 판정 위치는 바뀌지 않도록 지면 탐색은 지형 전체 경계만 사용.
- 낙하 연출 0.8초. 공습은 전용 계산 서버에서도 같은 시간을 기다리되 그래픽 오브젝트는 생성하지 않음. 기존 잔여 이동 종료 → 낙하 → 피해 정산 순서를 유지.
- 3개 맵의 폭 60, 4개 스폰 발판 높이는 동일. 부드러운 언덕, 동굴, 아치형 지형, 갈라진 섬으로 구성. 스폰 바깥에서 높낮이/엄폐를 선택하도록 설계.
- 흙은 큰 바위 덩어리 대신 작은 자갈과 흙층, 잔디는 초록 터프 무늬로 교체. 생성되는 스킬 지형의 아트는 이번 맵 텍스처와 별개의 기존 설정을 유지.
- 흙/잔디 반복 좌표를 CPU 베이크에서 명시적으로 정규화. Mirror 반복의 경계와 마지막 열이 길게 늘어나는 현상을 실제 렌더링에서 확인해 수정.

## 에디터에서 바꿀 곳

- `Assets/Prefabs/UI/ObjectHeadTitle.prefab`: AI 대전 버튼 RectTransform / Text.
- `Assets/Resources/ObjectHeadPresentation.asset`: Sky drop artwork, 공습/치유 크기, 하늘 여백, 낙하 시간.
- `Assets/Prefabs/Effects/IronMaiden.prefab`: 크기와 낙하/닫힘 시간.
- `Assets/GameData/Maps/*.asset`: 지형 Polygon, 흙/잔디 텍스처, 반복 크기, 잔디 깊이. 변경 후 Bake Terrain Texture.
- 각 맵 씬의 스폰 마커: 시작 위치. 일반 빌드는 다시 생성하거나 덮어쓰지 않음.

## 이미지 제작 기록

imagegen 스킬과 내장 이미지 생성 도구 사용. 기존 게임 화면은 그림체 참고로만 제공. 원본 생성 결과는 보존하고 프로젝트에 새 파일로 복사.

- `Assets/Art/Presentation/IslandSoilV2.png`
- `Assets/Art/Presentation/IslandGrassV2.png`

### 흙 프롬프트

Use case: stylized-concept. Asset type: seamless opaque tileable soil texture for a 2D side-view destructible island game. Reference image is STYLE ONLY, do NOT copy terrain silhouette or characters. Make a square 1024px full-bleed tile showing warm ochre-brown compact earth in side cutaway. Hand-drawn cartoon, confident dark-brown outlines, broad flat cel colors matching black-outlined cartoon characters. Subtle wavy strata, sparse SMALL rounded pebbles, mostly quiet earth. No giant boulders, no roots, no cracks forming a grid, no grass, no sky, no objects, no text. Soft low-contrast detail so the terrain silhouette and characters remain readable. Exactly seamless left/right and top/bottom, no border, no directional light, no photographic noise, no painterly grunge, no vignette. It will repeat on a large island cross section.

### 잔디 프롬프트

Use case: stylized-concept. Asset type: seamless opaque tileable grass material texture for a 2D side-view island game. Reference image is STYLE ONLY. Square 1024px full-bleed flat cartoon green turf material with small simplified overlapping tufts and broad scalloped leaf-like strokes, dark olive line accents, fresh moss-green and yellow-green flat colors. Designed to be sampled as a thin band following irregular ground surfaces, NOT a landscape or an isolated platform strip. Even detail throughout entire square, seamless both axes. No soil, no stones, no horizon, no sky, no characters, no flowers, no text, no borders, no shadows, no perspective, no photographic noise. Calm readable pattern, black-outlined cartoon game style.

최종 보정: Edit this grass material image only: replace every transparent/black empty background region with a continuous medium moss-green flat turf color (#658C35). Keep the simplified light-green tufts but remove fringed/neon edge noise, keep crisp dark olive outlines and flat fills. The whole square must be fully opaque, including between tufts. Seamless all-over grass texture, no soil, no border, no text. This is a solid green turf material, NOT isolated grass sprites.

## 검증 범위

Island 자동 검사는 버튼 크기/글자, 방향키와 A/D 입력 분리, 최대 축소보다 높은 낙하 시작점, 카메라와 판정의 분리, 아이언메이든 크기를 확인하고 세 맵 실제 렌더링을 저장한다.
기존 Release 검사는 9개 맵/모드 조합과 스폰 시드 216개를 검사한다. 유효한 스폰 검사가 장시간 플레이의 밸런스 공정성을 보장하지는 않는다. 외부 중앙 서버 연결은 이번 작업 범위가 아니다.

## 2026-09-16 최종 확인

- `island-build4.log`: Windows 빌드 성공. 결과는 기존 `Builds/Latest/ObjectHead.exe`에 갱신.
- Island PASS: 세 맵의 방향키 방향 전환/이동 차단과 D 이동, 버튼 크기, 하늘 낙하 시작점, 카메라와 착지 판정 분리, 아이언메이든 크기.
- Release PASS: 9개 맵/모드 조합, 216개 스폰 시드, 타이틀/로비, 승리 조건.
- CombatFix PASS: 공용머리 충돌/낙하, 턴 종료 후 치유/공습/포획, 카메라 가장자리 조작.
- AI PASS: 랜덤 팀 구성, 세 난이도 설정, 실제 스킬 사용, AI 턴 인간 입력 차단. 이동 거리 평가는 이 검사의 통과 조건이 아님.
- Polish PASS: 직전 검증 빌드에서 수동 AI 편성 저장/시작, 아군 AI 선택, 경사 장판 비율/회전 확인.
- 세 맵과 메뉴를 실제 Unity 렌더링으로 캡처해 육안 확인. 텍스처의 가로 늘어짐 수정 후 최종 캡처 재확인.
- 실제 대전의 장시간 밸런스와 외부 중앙 서버 연동은 미검증. 이번 테스트가 출시 전체 검증을 의미하지 않음.
