# 세로형 섬 맵 · AI 이동 · 지우개

## 이번 요청의 기준

- 가로 60 → 44 월드 단위, 원본 지형 캔버스 세로 20 → 32. 상단 투사체 공간은 별도 유지.
- 맵을 3개에 고정하지 않고 현재 5개 등록. 기존 3개의 ID는 저장 데이터 호환을 위해 유지하고 표시 이름만 변경.
- 외딴 등대 섬 / 무너진 항구 / 난파선 해안 / 숨은 바닷길 / 층층 협곡.
- 발판·벽·지붕은 그림의 불투명 픽셀이 곧 지형. 파괴 가능하며 별도의 보이지 않는 박스 충돌체 없음.
- 동굴은 외부로 열린 아래 우회로. 천장·겹친 발판이 공격 경로를 가리지만 화면의 캐릭터를 완전히 덮는 전경 가림막은 추가하지 않음.
- 시작 발판은 동일 높이, 좌우 대응 구조. 장시간 대전 밸런스는 별도 검증 필요.
- 맵을 굽는 에디터 작업에서만 512픽셀 미만의 고립된 제작 찌꺼기를 정리. 플레이 중 생긴 잔여 지형이나 생성 지형의 가두기 판정은 자동 삭제하지 않음.
- 바다 뒤의 원경은 흐리고 푸른 색의 별도 SpriteRenderer. 충돌·스폰·지형 파괴 대상이 아님.

## AI

공격 후보 탐색 → 유효한 공격이 없으면 시간 제한 내 가상 발사 위치 탐색 → 실제 이동·점프 → 착지 후 공격 재탐색.
후보 탐색은 캐릭터를 순간이동시키지 않는다. 주변 지형과 실제 캐릭터 점프 속도/중력을 사용해 착지 가능 여부를 검사한다. 전진과 공격 후 후퇴가 같은 점프 판단을 사용한다.
전역 최적 경로 보장은 아니며, 제한 시간 안에 조사한 근거리 후보 중 공격 효용·거리·높이를 비교한다.

## 공용머리 지우개

- 드릴 대신 지우개 오브젝트 채택. 직접 피해와 넉백 없음.
- 자기 주변 반경 2.4 → 최대 차징 4.8. 숫자는 ObjectHeadData.xlsx 수치 시트에서 편집.
- filled-circle TerrainOperation으로 작은 픽셀 조각까지 삭제. 캐릭터 충돌체 크기와 여유 공간도 최소 반경에 포함.
- 기존 지형 생성의 가두기 기능은 유지. 사용 후 바닥이 없어지는 물리 결과는 일반 지형 파괴 규칙을 따름.
- 범위 미리보기 없음. 사용 시 쓱쓱 문지르는 지우개와 분홍색 가루, 약한 먼지 연출. 폭발·피해 숫자·추가 사운드 없음.
- 팀 공유 인벤토리, 월드 스폰, 공용머리 사용 권한과 지형 동기화 경로에 연결. 실제 외부 서버 대전은 이번 검증 범위가 아님.

## 에디터 수정 위치

- Assets/GameData/Maps: 지형 polygon과 structures(그림·위치·높이·최대 너비·좌우 반전). Bake Terrain Texture로 그림/충돌 맵 갱신. 최대 너비는 비율을 보존하며 시작 지점 위를 건물이 덮지 않도록 조절하는 값.
- 각 맵 씬: 스폰 마커, Distant Scenery - non playable의 위치·크기·색·투명도.
- Assets/Resources/ObjectHeadContent.asset: 맵 목록과 지우개 스폰 수.
- Assets/Resources/ObjectHeadAITuning.asset: 탐색 시간·후보 수·이동 시간·점프 검사 간격.
- Assets/Resources/ObjectHeadPresentation.asset: 지우개 연출 시간·크기.
- Assets/Resources/ObjectHeadMicroFeedback.asset: EraserCrumb 가루 모양·색·수명.
- Assets/GameData/ObjectHeadData.xlsx: 맵 이름/설명 및 지우개 번역/반경. 번역 시트 편집은 Spreadsheets 스킬로 기존 행을 보존하며 수행.
- Install Vertical Island Collection은 명시적인 재작성 작업. 일반 빌드는 배치와 지형을 재생성하지 않음.

## 실행 검증 — 2026-09-16

- `Work/vertical-build5.log`: Unity 6000.4.0f1 Windows 빌드 성공. 최종 시각 검토 후 베이크 잔여 조각 제거 기준만 보완해 `Work/vertical-build6.log`로 재빌드. 실행 파일은 `Builds/Latest/ObjectHead.exe`.
- `Work/fix-Island.log`: 메뉴 크기, 좌우 방향키 방향 전환/A·D 이동, 확대 상태와 무관한 하늘 낙하, 5개 맵의 건물 픽셀 충돌·파괴 통과. 실제 렌더링 캡처 5종 확인.
- `Work/fix-Release.log`: 15개 맵/모드 조합, 360개 스폰 시드, 동일 시작 높이, 팀 인벤토리 공유, 승패 판정 통과.
- `Work/fix-Escape.log`: 실제 AI 이동 코드로 장애물 점프 1회, 상승 1.74·전진 5.74 월드 단위. 안전한 착지 지점이 없는 점프 거부, 실제 공용머리 지우개 사용, 차징 반경 증가, 작은 지형 조각 삭제, 전 캐릭터 HP 불변, 연출 소멸, 배경 충돌 없음 통과.
- `Work/fix-AI.log`: 무작위 로스터·3단계 난이도·AI 실제 공격·사람 입력 차단·네트워크 좌석 권한 검사 통과. 이 공격 검사는 적을 가까이 둔 별도 환경이므로 전체 맵 경로 탐색 완성을 의미하지 않음.
- `Work/fix-Tactics.log`: 가상 위치 탐색 중 실제 캐릭터 위치 불변, 탄도·엄폐·아군 피해 비용·적 치유 비용·연출 종료 통과.
- `Work/fix-CombatFix.log`: 보급 머리 낙하/비충돌, 턴 종료 후 치유·공습·포획, 카메라 가장자리 조작 통과.
- `Work/fix-Micro.log`: 지형 파편·착지 먼지·풀 제한·게임 난수 분리·UI 피드백·씬 종료 정리 통과.

외부 서버 연결이나 장시간 대전 밸런스는 검증하지 않음. 새 이미지·수치·연출이 추가됐다는 이유만으로 출시 검증 완료로 간주하지 않는다.

## 이미지 저장 및 제작 기록

내장 image_gen 도구와 imagegen 스킬을 사용. Assets/Art/Presentation/ 아래 프로젝트 사본으로 저장. 원본 생성 이미지는 보존.

- IslandLighthouse.png
- IslandBoathouse.png
- IslandShipwreck.png
- CommonEraser.png (Sprite Editor 메타데이터로 투명 여백 제외)
- DistantIslands.png

## 최종 생성 프롬프트

### lighthousePrompt

Use case: stylized-concept. Asset type: transparent sprite of a destructible island building for a 2D side-view artillery game. Style reference is the supplied game screenshot ONLY: bold slightly irregular dark outlines, simple flat cel colors, playful readable cartoon, not painterly and not realistic. Subject: abandoned coastal lighthouse, squat cream stone tower with faded red horizontal band, broad stone foundation, stepped exterior masonry ledges at several heights that characters can stand on, damaged upper lantern room and broad roof platform. Pure flat side elevation, no perspective. Whole structure visible centered with transparent padding, genuine transparent background. Large chunky shapes, sparse cracks, slate blue metal and warm cream stone. No scenery, no soil, no ocean, no people, no cast shadow, no text. At game scale this is 5 units wide and 9 units tall. Make the base wide and flat for embedding into ground. No prison elements.

### ruinPrompt

Use case: stylized-concept. Asset type: transparent destructible building sprite for a 2D side-view island artillery game. Reference screenshot is STYLE ONLY. Bold slightly irregular dark outlines, simple flat cel colors matching the cartoon game, sparse details, no painterly rendering. Subject: broken abandoned island boathouse / coastal storehouse, stone ground floor and weathered teal wooden upper floor, wide stepped broken rooftops and ledges which can serve as playable platforms, one large arched open doorway. Flat side elevation, whole structure visible, about 8 units wide and 6 units tall. Asymmetric damaged roof but stable broad flat base. Genuine transparent background, including the open doorway. Warm grey masonry, faded teal boards, rusty terracotta roof. No ground, landscape, sea, people, text, prison details or cast shadow.

### wreckPrompt

Use case: stylized-concept. Asset type: transparent destructible shipwreck terrain sprite for a 2D side-view island artillery game. Screenshot is STYLE ONLY: bold hand-drawn dark outlines and simple broad flat cel colors. Subject: a broken small wooden coastal fishing ship with faded ochre hull, teal cabin, a snapped thick mast, chunky broken deck platforms at two heights, an open gap in its hull. Pure flat side elevation, no perspective. Whole ship visible, wide horizontal silhouette, base wide enough to embed partially into island ground. Readable large shapes at game scale 12 units wide 7 units tall. Genuine transparent background including broken openings. No sea, ground, landscape, people, tiny ropes, flags, text, cast shadows, realistic texture or painterly effects.

### eraserPrompt

Use case: stylized-concept. Asset type: transparent common-head inventory and world pickup sprite in a 2D cartoon object-head artillery game. Screenshot is STYLE REFERENCE ONLY. Subject: a chunky worn pink-and-cream rectangular eraser, slightly tilted, with a simple dark teal paper sleeve around its middle, a couple rounded worn corners. Clearly an ERASER, not a potion or weapon. Bold irregular near-black outlines, simple flat cel fills, very few details, matches small hand-drawn game heads in reference. No face, eyes, body, limbs, text, letters, logo, floor, cast shadow, particles, glow or background. One object centered, isolated, genuine transparent background, ample transparent padding, strong readable silhouette.

### sceneryPrompt

Use case: stylized-concept. Asset type: distant background island skyline layer for a 2D cartoon island artillery game. Reference screenshot is STYLE ONLY. A wide panoramic cluster of three small faraway rocky tropical islands, soft palm silhouettes, a tiny abandoned lighthouse on one distant rock and a small half-submerged ship silhouette off another. All objects are scenery in the far background, NOT playable foreground. Muted misty blue-grey and seafoam colors, light soft outlines, low contrast atmospheric haze and slightly blurred distant edges. Flat side-view cartoon cel shapes, not photorealistic. Leave genuine transparent background above and around islands; no colored sky rectangle and no foreground terrain. Flat shared waterline baseline, a few very subtle pale wave marks beneath islands. No characters, UI, text, logos, or dark black outlines. Wide composition filling a 3:1 landscape image, calm decorative depth behind the actual destructible terrain.
