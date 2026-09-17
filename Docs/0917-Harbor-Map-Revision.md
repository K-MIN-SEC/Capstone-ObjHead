# 0917 항구 맵 재작업

## 범위

- 사용자에게 지적받은 `twin_citadels` / `TwinCitadels` / 표시 이름 `무너진 항구` 하나를 교체한다. 나머지 4개 맵은 유지한다.
- 반복 흙기둥, 얇게 떠 있는 수평 상층, 긴 가로 통로를 제거한다.
- 두꺼운 해안 지형에 중앙 만입부, 돌출 절벽, 짧은 동굴 입구와 넓은 대지에 붙은 건물을 구성한다.
- 배경은 기존 충돌 없는 먼바다 장식을 유지한다. 새 맵 그림은 전경 지형이며 실제 불투명 영역이 충돌/파괴 영역이다.
- 동굴 바닥이 아니라 위쪽 열린 지형에서 시작한다. 플레이어별 서로 다른 높이의 캐릭터 배치를 구성한다. 높이 분포 검사는 기하학적 안전 기준이지 장기 대전 밸런스 보장이 아니다.

## 에디터 수정

- `Assets/GameData/Maps/twin_citadels.asset`: 전체 원화 위치·높이·너비를 `structures`에서 수정. 오른쪽 동굴의 짧은 통로는 `artworkCutouts`로 수정. 수정 후 `Bake Terrain Texture`.
- `Assets/Scenes/TwinCitadels.unity`: 기존 스폰 마커를 직접 이동 가능. `ObjectHeadSpawnLayout`의 `useMarkerLocalSurface`는 마커 바로 아래의 지면을 검사한다. 다른 맵은 기존 최고 표면 탐색 방식을 유지한다.
- `maximumSpawnHeightSpread=7`, `maximumPlayerMeanHeightDifference=1.3`: 이번 맵의 높이 분포 검증값. 기존 맵의 동일 높이 검사값은 .15 유지.
- `Object Head > Maps > Install Hand-drawn Harbor Revision`은 명시적인 재설치 메뉴다. 일반 빌드/플레이는 배치나 원화를 다시 만들지 않는다.
- 현재 검증 실행 파일: `Builds/Latest/ObjectHead.exe`. 커밋/푸시는 수행하지 않는다.

## 검증 결과 (2026-09-17)

- 최종 Windows 빌드 성공 (`Work/harbor-build3.log`).
- HARBOR_PASS: 8명 분산 스폰, 시작 위치 지형 겹침 방지, 양쪽 동굴 입구 실제 캐릭터 이동, 건물 지붕 충돌 및 파괴, 비충돌 원경 확인.
- RELEASE_PASS: 5개 맵 × 3개 모드, 총 360개 시드별 스폰 검사 및 타이틀/로비/승리 회귀 검사.
- AI_PASS: 무작위 구성, 세 난이도 설정, 실제 스킬 실행 회귀 검사. 새 맵 전체의 AI 길찾기 품질을 보장하는 검사는 아니다.
- 실제 Unity 실행 캡처: `Work/fix-captures/harbor-0917-overview.png`, `harbor-0917-play.png`, `harbor-0917-detail.png`.
- 자동 실행 및 화면 캡처로 검증했으며 수동 장기 대전 테스트는 하지 않았다. 높낮이에 따른 공격 유불리와 경기 시간은 후속 플레이 테스트가 필요하다. 서버는 시작하지 않았다.

## 이미지 제작

imagegen 스킬과 내장 image_gen 도구 사용. 기존 게임 화면은 그림체 참고이며 지형 배치는 새로 구성했다. 생성 원본은 보존했다.

- 프로젝트 원화: `Assets/Art/Maps/HarborTerrain0917.png`
- 게임에서 쓰는 베이크 결과: `Assets/Art/Maps/twin_citadels.png`
- 최초 생성 원본: `exec-3fec6781-8713-4c2a-974d-acb1992ae397.png`
- 동굴 입구 수정 원본: `exec-53ca72e6-9bf2-4a44-8763-03dd79df42e0.png`

### 생성 프롬프트

Use case: stylized-concept.
Asset type: actual transparent destructible terrain sprite for a 2D side-view artillery game, NOT a screenshot or scenery backdrop.
Input image: STYLE REFERENCE ONLY for simple flat cartoon outlines/colors. Do NOT copy its bad symmetric pillar geometry, horizontal slit, repeating texture, sky or tiny perched buildings.
Create ONE substantial cohesive island cross-section, wide landscape composition, all foreground fully visible with a small transparent margin. Natural hand-designed asymmetric silhouette: broad rolling grassy headlands, a deep central open ravine/harbor inlet, thick rocky overhangs, a sloping sheltered lower cove, and two differently shaped roomy side-entry caves cut into the solid island mass. Include a few broad near-horizontal walkable grassy ledges at irregular heights connected by short slopes and small steps. Do not use identical columns, repeated hills, a long horizontal tunnel, a detached lower ground strip, floating islands or a rectangular bottom.
A small abandoned teal-and-stone boathouse embedded solidly into a broad upper coastal terrace and a broken short wooden landing embedded in the low cove. These structures belong to the terrain, not perched on thin supports. No extra trees, ropes, hanging isolated props or tiny floating debris. Broad stable stone/soil base descends below waterline but no water is drawn.
Flat orthographic side elevation, NO perspective top surfaces or isometric view. Chunky near-black irregular outlines, simple warm ochre soil with larger exposed slate/warm grey rock strata concentrated near cliffs, olive-green grass lip, sparse nonrepeating cracks and stones; flat 2-3 tone cel shading. Matches playful small orange-prisoner object-head characters, NOT realistic, not glossy, not detailed painting. Strong interesting silhouette occupying most of image, roughly 2:1 width to height. Transparent empty areas outside AND INSIDE every cave and under every arch; cave interiors must be real alpha holes, not black/brown painted back walls. NO sky, water, horizon, background islands, characters, UI, text, numbers, grid, logo or watermark.

### 동굴 입구 수정 프롬프트

Use case: precise-object-edit. Image 1 is the EDIT TARGET: preserve this island's outer silhouette, size, positions, style, palette, boathouse, dock and transparent background. Change ONLY the two small enclosed oval cave holes into usable side-entry caves. For the LEFT cave, open its left wall toward the left-facing coastline with a SHORT wide horizontal mouth at the cave floor level, retaining a thick rock roof and a continuous broad ground floor. For the RIGHT cave, open its left wall toward the central harbor cove with a short wide mouth at cave floor level, retaining a thick rock roof and a continuous floor. Each mouth has enough vertical clearance for a small game character (roughly one third of existing oval height or more), no needle-shaped remnants or thin floating chips. These cave entrances must be genuine transparent alpha like the empty exterior. Keep every other area unchanged. Do not join the caves to each other, do not cut a full-width tunnel, do not add scenery or characters. No painted dark cave back walls.
