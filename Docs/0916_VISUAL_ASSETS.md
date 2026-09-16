# 화면 개편 에셋

제작: 내장 이미지 생성 도구. 기존 캐릭터 이미지를 스타일 참조로 사용했다. 캐릭터 원본은 교체하지 않았다. 교도소 배경 및 회화풍 시안은 최종 프로젝트에 포함하지 않았다.

## 사용 파일

- `Assets/Art/Title/ObjectHeadKeyArt.png`: 탈옥 이후 섬에서의 대전 타이틀 삽화.
- `Assets/Art/Presentation/IslandSoil.png`: 섬 지형 내부 재질. Unity의 작성용 폴리곤 마스크와 합쳐 맵 텍스처를 굽는다.
- `Assets/Art/Presentation/FlatEffects.png`: 전구·씨앗·폭탄·연기의 2×2 스프라이트 시트. 원본 알파를 유지하고 Unity에서 분할했다.
- `Assets/Art/UI/InkPanel.png`: Unity 작성 도구로 만든 9-slice 패널 테두리.

## 최종 생성 프롬프트

### title

Use case: illustration-story. A 16:9 game title background MATCHING the attached simple hand-drawn character sprites exactly. Reference images 1-3 show the three heads; reference 4 is their small orange jumpsuit body. Existing game is a doodle-like 2D artillery game, very thick slightly uneven BLACK outlines, SIMPLE FLAT COLORS, tiny squat bodies and oversized object heads, not realistic anatomy. Draw these SAME THREE characters on RIGHT half of a natural grass-topped island above blue ocean; they have already escaped from a prison and now battle on islands. One broken bulb head, one plain brown teardrop seed head, one red spherical fuse bomb head. Orange prison jumpsuits, no belts/backpacks/guns/weapons, no human faces. One poses to THROW its own detachable head with an empty collar, the others brace/playfully face rivals across a gap. Simple rounded rocky islands with a natural arch in distant right background, minimal clouds. LEFT 44% is quiet dark desaturated teal-blue negative space for live menu. Clear composition and expressive silhouettes. NO prison buildings, fences, guard towers. NO gun, no cannon, no extra props. Absolutely NO realistic rendering, painterly textures, brush grain, dramatic lighting, complex shading, gradients on characters, or pixel art. Like a clean handmade cartoon with 2-3 flat tones per shape. No text, letters, logo, UI, or watermark. 2048x1152.

### terrain

Use case: stylized-concept. Seamlessly tileable square texture for a simple hand-drawn 2D island artillery game. Side-view cutaway underground soil: broad warm brown dirt with scattered simple irregular ochre rocks and a few curved crack lines. Thick slightly wobbly dark brown-black ink outlines around rocks, very simple FLAT colors, 2 tones per rock only, clean handmade doodle/cartoon style. Rocks varied sizes, not repeated bricks or straight stripes, soil is a calm solid sienna color with lots of uncluttered space. Texture must read clearly at small game scale beside thick-black-outlined cartoon characters. NO painterly rendering, no brush texture, no noise, no tiny pebbles, no gradients, no shine, no realistic photo. FULL FRAME underground material; no grass, no horizon, no surface, no sky, no text or letters, no objects, no watermark. 1024 square.

### effects

Production transparent VFX sprites for THIS EXACT existing game's drawing style. Attached images are STYLE REFERENCES only: notice their very thick uneven pure-black ink outer contours, very simple flat solid color fills, almost amateur hand-drawn doodle charm. Match that style exactly. Do NOT make polished painterly effects. Create a precise 2 by 2 sprite atlas with four independent sprites, each centered in a square cell with 20% clear padding. Top-left: small angular yellow lightning impact star. Top-right: three simple olive-green leaves swirling. Bottom-left: comic blast, jagged yellow inner star surrounded by a red-orange irregular outline burst. Bottom-right: gray smoke puff with 4 simple rounded lobes. Every sprite has thick slightly wobbly BLACK outlines matching the reference bomb and seed, 2 or 3 FLAT colors only, no gradients, no shading, no textures, no lighting, no glow, no bloom, no highlights. NO characters, no objects copied from references, no words, no labels, no grid lines, no checkerboard painted into the image. Genuinely transparent RGBA background, all space outside four isolated sprites alpha zero. Square 1024x1024 atlas, exact 2x2 equal cells.
