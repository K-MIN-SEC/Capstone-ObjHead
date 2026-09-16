# 0916 화면·연출 수정

## 확정된 세계관

- 오브젝트 헤드를 수용하던 교도소가 공격당했고, 등장인물들은 이미 탈출했다.
- 플레이 장소는 교도소가 아닌 섬이다.
- 보급 머리는 사망한 오브젝트 헤드의 머리이다. 임의로 고어 연출을 추가하지 않는다.
- 캐릭터 원본의 굵은 검은 외곽선과 단순한 색면을 유지한다. 교도소 배경과 회화풍 이펙트 시안은 사용하지 않는다.

## 에디터에서 수정하는 곳

- 타이틀: `Assets/Prefabs/UI/ObjectHeadTitle.prefab`의 Canvas 아래 UI. 위치·크기·색상은 RectTransform과 Image에서 수정한다.
- 전투 HUD: 각 전투 씬의 `BattleUI/Canvas`. 실행 또는 일반 빌드가 UI 배치를 재생성하지 않는다.
- 배경: 각 전투 씬의 `Background` Transform. 넓은 축소 시야를 덮도록 저장되어 있으며 런타임에 위치·배율을 덮어쓰지 않는다.
- 휠: 각 전투 씬 Main Camera의 ObjectHeadCameraController. `Wheel Zoom Fraction` 0.16은 한 칸에 시야 크기를 16% 줄인다. `Min Size` 3, `Max Size` 22. 키보드 확대 속도와 별도다.
- 이펙트: `Assets/Resources/ObjectHeadPresentation.asset`에서 스킬 ID별 발사·비행·충돌 프리팹과 배율을 지정한다. 각 프리팹 Particle System에서 개수·크기·속도·수명을 수정한다.
- 맵: `Assets/GameData/Maps`의 Map Recipe에서 폴리곤·재질·색상을 수정한 뒤 Bake Terrain Texture를 누른다. 스폰 위치는 씬의 모드별 Spawn Markers에서 수정한다. 높이와 여유 공간 검증이 통과해야 한다.
- `ObjectHeadVisualRevision.ApplyAndBuild`는 이번 교체 작업을 위한 명시적 작성 도구다. 일반 빌드에서는 호출하지 않는다. 호출하면 이번 작성용 레이아웃으로 교체되므로 수동 수정한 프로젝트에서 임의 실행하지 않는다.

## 아직 출시 완료를 뜻하지 않는 항목

외부 서버 배포, 지형 최종 픽셀의 네트워크 일치, 재접속, 공용 머리 동기화 및 장시간 대전은 별도의 출시 검증이 필요하다. 동일 높이 스폰은 검증 가능하지만 실전 승률까지 공정함을 보장하지 않는다.
