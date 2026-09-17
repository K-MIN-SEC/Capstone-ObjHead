# 0917 아트 방향 및 생성 원문

## 사용자 최종 지시
기존 리볼버의 검은 선과 색칠 방식을 기준으로 한다. 청소기는 흡입구만, 호리병은 살짝 기울이고 광택 제거, TV는 정면이며 디테일 축소. 6종 모두 명암 제거. 웜즈 지원병은 헬기에 탑승한 MG/런처 사격 모션, 프로펠러 회전 표현.

내장 이미지 생성(imagegen) 사용. 반려된 광택 일러스트/과도하게 단순한 낙서 시안은 적용하지 않았다. 원본 생성 결과는 Codex generated_images에 보존한다. 아래 새 파일은 기존 리볼버/전구/씨앗/폭탄을 덮어쓰지 않는다.

확대 미리보기에서 보이는 지원병 주변 색 번짐은 알파 0의 투명 픽셀임을 픽셀 검사와 실게임 렌더로 확인했다. 후속 정리 시안 exec-d0f05eda-04bd-4d01-9962-c0c2f6d008b4.png는 적용하지 않았다. 최종 적용 파일은 아래 목록 기준이다.

## 에디터에서 조정하는 곳

- 캐릭터 머리: `Assets/Prefabs/Characters/magnet.prefab`(기존 ID를 유지한 청소기), `gourd.prefab`의 CharacterVisual. 머리 3종 Sprite와 공통 크기를 사용한다.
- 헬기·MG/런처 3프레임·탑승 위치·문 마스크·총구·프로펠러 폭/두께/속도/원근/잔상: `Assets/Resources/ObjectHeadPresentation.asset`.
- 공개/재충전/체공/TV 준비 연출: `Assets/Resources/ObjectHeadActionCues.asset`와 연결된 `Assets/Prefabs/Effects` 프리팹. 실행 중 화면 좌표를 강제 배치하지 않는다.
- 호리병 사용 횟수 표시: 각 맵 씬의 `HeadInventory/EntryTemplate/Count`. 템플릿을 복제하므로 카드별 배치를 코드에서 다시 고정하지 않는다.
- 이름·설명·기본 밸런스: `Assets/GameData/ObjectHeadData.xlsx`를 편집 후 Object Head의 시트 가져오기 메뉴 사용.

- Assets/Art/Characters/NozzleHeads0917.png ← exec-4f408c4a-dba1-45dc-b666-31ff65e69d54.png
- Assets/Art/Characters/TiltedGourdHeads0917.png ← exec-cfe61390-57d4-41df-ac8b-39dc5fd991b5.png
- Assets/Art/Presentation/FlatTV0917.png ← exec-fd6a168d-1510-4790-aff4-d32e49e824c2.png
- Assets/Art/Presentation/FlatRainbowCat0917.png ← exec-3aff5c60-ae97-4692-b475-7022919c90c9.png
- Assets/Art/Presentation/SupportFiringSheet0917.png ← exec-2410ef64-fec0-4cef-b114-17f897424282.png
- Assets/Art/Presentation/FlatHelicopter0917.png ← exec-ae0bb260-4698-4bb9-bc6e-97b34c7e3414.png

## 최종 프롬프트

### NozzleHeads0917

Use case: stylized-concept. Asset: production transparent 2D game sprite. Match the attached revolver sprite's hand-drawn black outlines and simple clean silhouettes. Use solid FLAT color fills only. User explicitly requires ZERO SHADING: no shadow patches, highlights, gloss, gradients, lighting, texture, glows, 3D depth or volume rendering. Black ink slightly organic but not childish or crudely distorted. No white sticker outline. True transparent alpha outside shapes; no background. Do not copy any gun from the reference except where a soldier weapon is requested. No labels, no lettering. Wide sprite sheet 3 equal columns, 1 row, isolated equal-size sprites with transparent gutters. Draw ONLY vacuum SUCTION NOZZLE HEADS, not a vacuum appliance. NO body, NO wheels, NO hose, NO handle, NO power button. Each consists of a short teal socket connected to a wide flattened cream trapezoid nozzle with a thick black suction slit, like a vacuum cleaner floor attachment. Side/front readable like the reference gun mouth. First nozzle points right, second same with slightly wider dark opening and a small blue rim, third nozzle faces downward. Keep compact shape suitable as a character head. All same scale.

### TiltedGourdHeads0917

Use case: stylized-concept. Asset: production transparent 2D game sprite. Match the attached revolver sprite's hand-drawn black outlines and simple clean silhouettes. Use solid FLAT color fills only. User explicitly requires ZERO SHADING: no shadow patches, highlights, gloss, gradients, lighting, texture, glows, 3D depth or volume rendering. Black ink slightly organic but not childish or crudely distorted. No white sticker outline. True transparent alpha outside shapes; no background. Do not copy any gun from the reference except where a soldier weapon is requested. No labels, no lettering. Wide 3-column 1-row sprite sheet. Three tan double-bulb gourd heads, each tilted 12 degrees toward the right. Smooth simple two-bulb silhouette, small dark brown cork, red cord with tiny knot around neck. NO SHINE or shadows: one solid ochre/tan fill. First cork closed with red cord, second open corkless mouth plus small flat pale green vapor curl, third closed with blue cord. Same size and silhouette, enough clear gutters. No jewels, no question marks, no ornament.

### FlatTV0917

Use case: stylized-concept. Asset: production transparent 2D game sprite. Match the attached revolver sprite's hand-drawn black outlines and simple clean silhouettes. Use solid FLAT color fills only. User explicitly requires ZERO SHADING: no shadow patches, highlights, gloss, gradients, lighting, texture, glows, 3D depth or volume rendering. Black ink slightly organic but not childish or crudely distorted. No white sticker outline. True transparent alpha outside shapes; no background. Do not copy any gun from the reference except where a soldier weapon is requested. No labels, no lettering. One retro television head, STRICT front elevation, perfectly no visible side panel, no perspective. Muted purple rectangular casing with slightly rounded corners. Short V antenna. Pale screen rectangle with 3 flat muted red green blue vertical bars. Exactly one simple round dark knob and two short black speaker slots. No legs, scratches, glass gleam or screen shine. Very low interior detail matching revolver reference. Generous transparent margin.

### FlatRainbowCat0917

Use case: stylized-concept. Asset: production transparent 2D game sprite. Match the attached revolver sprite's hand-drawn black outlines and simple clean silhouettes. Use solid FLAT color fills only. User explicitly requires ZERO SHADING: no shadow patches, highlights, gloss, gradients, lighting, texture, glows, 3D depth or volume rendering. Black ink slightly organic but not childish or crudely distorted. No white sticker outline. True transparent alpha outside shapes; no background. Do not copy any gun from the reference except where a soldier weapon is requested. No labels, no lettering. One recognizable Nyan Cat flying right, gray cat head, simple pink pastry rectangle body, short six-color rainbow extending to left. Four stubby legs and little tail. Black dot eyes, small smile. Flat muted colors, three sprinkle dots. No cheeks glow or shadow, no baked light surrounding rainbow. Keep entire sprite rectangular and compact, long axis horizontal. Controlled line quality like reference, not hypercute glossy mascot.

### SupportFiringSheet0917

Use case: stylized-concept. Asset: production transparent 2D game sprite. Match the attached revolver sprite's hand-drawn black outlines and simple clean silhouettes. Use solid FLAT color fills only. User explicitly requires ZERO SHADING: no shadow patches, highlights, gloss, gradients, lighting, texture, glows, 3D depth or volume rendering. Black ink slightly organic but not childish or crudely distorted. No white sticker outline. True transparent alpha outside shapes; no background. Do not copy any gun from the reference except where a soldier weapon is requested. No labels, no lettering. Animation SPRITE SHEET exactly 3 columns x 2 rows, six isolated frames in equal square cells. Same recognizable pink Worms worm soldier with big white eyes, black pupils and plain olive helmet. Only upper body bust and hands, waist cropped deliberately at same baseline because seated inside helicopter doorway. Faces right, weapon aims 20 degrees DOWN and right. TOP ROW holds compact black machine gun: frame1 ready, frame2 firing with recoil body leans back slightly, frame3 follow-through returns. BOTTOM ROW same character same registration holding simple olive rocket launcher: frame1 ready loaded, frame2 firing and leans back, frame3 spent recoil recovery. Do NOT bake muzzle flash or rocket or smoke into sheet, animation overlays added in engine. Every cell same camera, head position, scale, baseline, identical palette. Heavy simple weapon outline like reference. Opaque fills flat, no shadow. Large transparent gutters.

### FlatHelicopter0917

Use case: stylized-concept. Asset: production transparent 2D game sprite. Match the attached revolver sprite's hand-drawn black outlines and simple clean silhouettes. Use solid FLAT color fills only. User explicitly requires ZERO SHADING: no shadow patches, highlights, gloss, gradients, lighting, texture, glows, 3D depth or volume rendering. Black ink slightly organic but not childish or crudely distorted. No white sticker outline. True transparent alpha outside shapes; no background. Do not copy any gun from the reference except where a soldier weapon is requested. No labels, no lettering. One olive helicopter BODY only, side view facing right, simple military transport cartoon, maintain recognizable rotor mast, small tail, skids, flat pale blue windshield and LARGE rectangular open side doorway at center-right. Door opening must be genuinely TRANSPARENT (an empty hole) for separately animated seated pilot. NOT black fill in doorway. Draw a black door frame around the clear opening. No pilot. Main rotor mast exists but NO LONG MAIN ROTOR BLADES: separate animated rotor will be placed there. Simple short tail rotor is okay. Uniform solid olive, no shadows/highlights. Detail level comparable to reference revolver, no rivets or panel texture. Full silhouette and skids contained within transparent margins. Landscape.
