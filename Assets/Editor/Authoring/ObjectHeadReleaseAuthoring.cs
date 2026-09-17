using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-time migration. Existing authoring assets are never overwritten by a player build.</summary>
public static class ObjectHeadReleaseAuthoring
{
    [Serializable] public class Layout { public float width; public float height; public Node[] nodes; }
    [Serializable] public class Node
    {
        public string name, parent, type, key, text, color, sprite, bind, labelBind;
        public float x, y, w, h;
        public int size = 24;
        public bool inactive, preserveAspect;
        public int alignment = 4;
    }
    private static ObjectHeadContent content;
    private static ObjectHeadLocalizationTable localization;
    private static readonly string[] MapIds = { "wind_meadow", "twin_citadels", "shattered_reef" };

    [MenuItem("Object Head/0916/Install Authored Scenes (once)")]
    public static void Install()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        content = AssetDatabase.LoadAssetAtPath<ObjectHeadContent>("Assets/Resources/ObjectHeadContent.asset");
        if (content != null) throw new InvalidOperationException("0916 content already exists. Edit the scenes, prefabs and recipes directly; migration will not overwrite them.");
        localization = ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
        content = ScriptableObject.CreateInstance<ObjectHeadContent>();
        content.uiFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansCJKkr-Regular.otf");
        if (content.uiFont == null) throw new InvalidOperationException("Bundled Korean font is required.");
        AssetDatabase.CreateAsset(content, "Assets/Resources/ObjectHeadContent.asset");
        content.teamRules = new[] { new ObjectHeadTeamRule { players = 2, charactersPerPlayer = 3 }, new ObjectHeadTeamRule { players = 4, charactersPerPlayer = 2 } };
        content.modes = new[] {
            new ObjectHeadModeDefinition { mode=ObjectHeadMatchMode.Duel,nameKey="mode_duel",players=2,alliances=new[]{1,2},spawnSeats=new[]{0,1} },
            new ObjectHeadModeDefinition { mode=ObjectHeadMatchMode.FreeForAll,nameKey="mode_ffa",players=4,alliances=new[]{1,2,3,4},spawnSeats=new[]{0,1,2,3} },
            new ObjectHeadModeDefinition { mode=ObjectHeadMatchMode.Teams,nameKey="mode_teams",players=4,alliances=new[]{1,2,1,2},spawnSeats=new[]{0,3,1,2} }
        };
        Directory.CreateDirectory("Assets/GameData/Maps");
        AssetDatabase.Refresh();
        content.characters = new[]
        {
            Character(ObjectHeadCharacterKind.Bulb,"bulb","head_bulb_broken_off",new Color32(252,204,70,255)),
            Character(ObjectHeadCharacterKind.Seed,"seed","head_seed",new Color32(116,211,145,255)),
            Character(ObjectHeadCharacterKind.Bomb,"bomb","head_bomb_green",new Color32(241,117,88,255))
        };
        content.allianceColors = new Color[] {new Color32(227,104,90,255), new Color32(89,161,242,255), new Color32(223,185,90,255), new Color32(130,189,93,255)};
        ImportSprite("Assets/Art/Title/Archipelago.png",100,false);
        var recipes = new List<ObjectHeadMapRecipe>();
        foreach (string id in MapIds)
        {
            var recipe = ScriptableObject.CreateInstance<ObjectHeadMapRecipe>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/Editor/Authoring/"+id+".json"),recipe);
            AssetDatabase.CreateAsset(recipe,"Assets/GameData/Maps/"+id+".asset");
            ObjectHeadMapRecipeEditor.Bake(recipe);
            recipes.Add(recipe);
        }
        content.maps = recipes.Select(r => new ObjectHeadMapDefinition { id=r.mapId,nameKey=r.nameKey,descriptionKey=r.descriptionKey,sceneName=r.sceneName,preview=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Maps/"+r.mapId+".png") }).ToArray();
        EditorUtility.SetDirty(content);
        BuildTitle();
        foreach (var recipe in recipes) BuildArena(recipe);
        ConfigureRouting();
        ArchiveUnusedScenes();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/ObjectHeadTitle.unity");
        Validate();
        Debug.Log("[0916] Authoring migration completed. All layouts are serialized; builds only validate them.");
    }

    private static ObjectHeadCharacterDefinition Character(ObjectHeadCharacterKind kind,string key,string portrait,Color accent)
    {
        Directory.CreateDirectory("Assets/Prefabs/Characters");
        var go = new GameObject(key);
        var sr=go.AddComponent<SpriteRenderer>(); sr.enabled=false;
        var body=go.AddComponent<Rigidbody2D>(); body.gravityScale=0;body.freezeRotation=true;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;body.interpolation=RigidbodyInterpolation2D.Interpolate;
        var collider=go.AddComponent<CapsuleCollider2D>();collider.size=new Vector2(.72f,1.15f);collider.offset=new Vector2(0,.02f);
        var visual=go.AddComponent<CharacterVisual>();
        go.AddComponent<TurnCharacterController>();go.AddComponent<CharacterCombat>();go.AddComponent<AimController>();go.AddComponent<PowerChargeController>();go.AddComponent<DemoSkillSelector>();go.AddComponent<SkillFireController>();go.AddComponent<ObjectHeadTeamMember>();go.AddComponent<CommonHeadUseController>();
        var bodyObject = new GameObject("BodyRenderer",typeof(SpriteRenderer));bodyObject.transform.SetParent(go.transform,false);bodyObject.transform.localPosition=new Vector3(0,-.24f,0);
        var headObject = new GameObject("HeadRenderer",typeof(SpriteRenderer));headObject.transform.SetParent(go.transform,false);headObject.transform.localPosition=new Vector3(0,.34f,0);
        var bodyRenderer=bodyObject.GetComponent<SpriteRenderer>();bodyRenderer.sprite=Resources.Load<Sprite>("Sprites/Body/body_idle");bodyRenderer.sortingOrder=10;
        var headRenderer=headObject.GetComponent<SpriteRenderer>();headRenderer.sprite=Resources.Load<Sprite>("Sprites/Heads/"+portrait);headRenderer.sortingOrder=11;
        var serialized=new SerializedObject(visual);
        serialized.FindProperty("characterKind").enumValueIndex=(int)kind;
        serialized.FindProperty("bodyRenderer").objectReferenceValue=bodyRenderer;
        serialized.FindProperty("headRenderer").objectReferenceValue=headRenderer;
        foreach(var name in new[]{"Idle","Throw","Hit"}) serialized.FindProperty("body"+name).objectReferenceValue=Resources.Load<Sprite>("Sprites/Body/body_"+name.ToLowerInvariant());
        string[][] heads={new[]{"head_bulb_broken_off","head_bulb_on","head_bulb_off"},new[]{"head_seed","head_vine_ball","head_thorn_vine_ball"},new[]{"head_bomb_green","head_bomb_red","head_bomb_yellow"}};
        var arr=serialized.FindProperty("authoredSkillHeads");arr.arraySize=3;for(int i=0;i<3;i++)arr.GetArrayElementAtIndex(i).objectReferenceValue=Resources.Load<Sprite>("Sprites/Heads/"+heads[(int)kind][i]);
        serialized.FindProperty("preserveAuthoredTransforms").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,"Assets/Prefabs/Characters/"+key+".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return new ObjectHeadCharacterDefinition{kind=kind,nameKey=key+"_name",descriptionKey=key+"_description",portrait=headRenderer != null ? headRenderer.sprite : Resources.Load<Sprite>("Sprites/Heads/"+portrait),prefab=prefab,accent=accent};
    }

    private static void BuildTitle()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Camera camera = CameraObject(); camera.backgroundColor=new Color32(10,24,43,255);
        var root=CanvasRoot("ObjectHeadTitleRoot",out var canvas);
        var controller=canvas.AddComponent<ObjectHeadTitleScreen>();controller.localization=localization;controller.content=content;controller.uiFont=content.uiFont;
        BuildLayout("TitleLayout",canvas.transform,controller);
        Directory.CreateDirectory("Assets/Prefabs/UI");
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/UI/ObjectHeadTitle.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        PrefabUtility.InstantiatePrefab(prefab,scene);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/ObjectHeadTitle.unity");
    }

    private static Camera CameraObject()
    {
        var go=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));go.tag="MainCamera";
        var c=go.GetComponent<Camera>();c.orthographic=true;c.orthographicSize=16;c.clearFlags=CameraClearFlags.SolidColor;
        go.transform.position=new Vector3(0,6,-10);return c;
    }

    private static GameObject CanvasRoot(string name,out GameObject canvas)
    {
        var root=new GameObject(name);
        canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(root.transform,false);
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var events=new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));events.transform.SetParent(root.transform,false);
        return root;
    }

    private static void BuildLayout(string asset,Transform parent,MonoBehaviour controller)
    {
        var layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Editor/Authoring/"+asset+".json"));
        parent.GetComponent<CanvasScaler>().referenceResolution=new Vector2(layout.width,layout.height);
        var nodes=new Dictionary<string,GameObject>();
        foreach(var n in layout.nodes)
        {
            var go=new GameObject(n.name,typeof(RectTransform));
            go.transform.SetParent(string.IsNullOrEmpty(n.parent)?parent:nodes[n.parent].transform,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(n.x,n.y);rect.sizeDelta=new Vector2(n.w,n.h);
            nodes.Add(n.name,go);
            ColorUtility.TryParseHtmlString(n.color ?? "#FFFFFF",out Color color);
            UnityEngine.Object target=go;
            if(n.type=="text") target=MakeText(go,n,color);
            else if(n.type!="group")
            {
                var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=n.type=="button"||n.type=="input"||n.type=="slider";image.preserveAspect=n.preserveAspect;
                if(!string.IsNullOrEmpty(n.sprite)) image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(n.sprite);
                else if(n.type=="button" || n.type=="input" || (n.type=="image" && n.w>180 && n.h>48 && n.name!="Veil"))
                {
                    image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/InkPanel.png");
                    if(image.sprite!=null)image.type=Image.Type.Sliced;
                }
                target=image;
                if(n.type=="fill") { image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal; }
                if(n.type=="button")
                {
                    var button=go.AddComponent<Button>();button.targetGraphic=image;
                    var colors=button.colors;colors.highlightedColor=new Color(1.1f,1.1f,1.1f,1);colors.pressedColor=new Color(.65f,.8f,.85f,1);colors.disabledColor=new Color(.45f,.5f,.55f,.65f);button.colors=colors;
                    var outline=go.AddComponent<Outline>();outline.effectColor=new Color32(255,220,116,255);outline.effectDistance=new Vector2(3,-3);outline.enabled=false;
                    var label=ChildText(go,n);Bind(controller,n.labelBind,label);target=button;
                }
                if(n.type=="input")
                {
                    var field=go.AddComponent<InputField>();field.targetGraphic=image;field.characterLimit=20;
                    var label=ChildText(go,new Node {size=n.size,text="",alignment=3});field.textComponent=label;
                    var placeholder=ChildText(go,n);placeholder.color=new Color32(153,181,194,255);field.placeholder=placeholder;target=field;
                }
                if(n.type=="slider")
                {
                    var slider=go.AddComponent<Slider>();slider.interactable=false;var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(go.transform,false);
                    var fr=(RectTransform)fill.transform;fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;fill.GetComponent<Image>().color=new Color32(255,193,76,255);slider.fillRect=fr;target=slider;
                }
            }
            Bind(controller,n.bind,target);
            go.SetActive(!n.inactive);
        }
    }

    private static Text ChildText(GameObject parent,Node n)
    {
        var child=new GameObject("Label",typeof(RectTransform));child.transform.SetParent(parent.transform,false);
        var r=(RectTransform)child.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(12,4);r.offsetMax=new Vector2(-12,-4);
        var text=MakeText(child,n,Color.white);
        text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=n.size;
        return text;
    }

    // Explicit authoring operation only. Never called by Start, OnEnable, or a normal build.
    public static void ReplaceVisualLayout(MonoBehaviour controller,string layoutName)
    {
        content=ObjectHeadContent.Load();
        localization=AssetDatabase.LoadAssetAtPath<ObjectHeadLocalizationTable>("Assets/Resources/ObjectHeadLocalization.asset");
        var children=controller.transform.Cast<Transform>().ToArray();
        foreach(var child in children)UnityEngine.Object.DestroyImmediate(child.gameObject);
        BuildLayout(layoutName,controller.transform,controller);
    }
    private static Text MakeText(GameObject go,Node n,Color color)
    {
        var text=go.AddComponent<Text>();text.font=content.uiFont;text.fontSize=n.size;text.color=color;text.alignment=(TextAnchor)n.alignment;text.raycastTarget=false;text.supportRichText=false;
        text.text=string.IsNullOrEmpty(n.key)?n.text??string.Empty:localization.Get(n.key,ObjectHeadLanguage.Korean);
        if(!string.IsNullOrEmpty(n.key))go.AddComponent<ObjectHeadLocalizedLabel>().LocalizationKey=n.key;
        return text;
    }
    private static void Bind(MonoBehaviour controller,string name,UnityEngine.Object value)
    {
        if(string.IsNullOrEmpty(name))return;
        var serialized=new SerializedObject(controller);SerializedProperty property;
        if(name.Contains("["))
        {
            int at=name.IndexOf('['),index=int.Parse(name.Substring(at+1).TrimEnd(']'));
            var array=serialized.FindProperty(name.Substring(0,at));if(array.arraySize<=index)array.arraySize=index+1;property=array.GetArrayElementAtIndex(index);
        }
        else property=serialized.FindProperty(name);
        if(property==null)throw new InvalidOperationException("Missing UI binding "+name);
        var field = controller.GetType().GetField(name.Split('[')[0]);
        if(field != null && field.FieldType == typeof(GameObject) && value is Component component)value=component.gameObject;
        property.objectReferenceValue=value;serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildArena(ObjectHeadMapRecipe recipe)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=CameraObject();camera.backgroundColor=new Color32(71,177,200,255);camera.gameObject.AddComponent<ObjectHeadCameraController>();
        var map=new GameObject("Map - move objects here to design the level");
        var terrainObject=new GameObject("Terrain",typeof(SpriteRenderer),typeof(TerrainManager));terrainObject.transform.SetParent(map.transform,false);terrainObject.transform.position=recipe.origin;
        var terrain=terrainObject.GetComponent<TerrainManager>();var renderer=terrainObject.GetComponent<SpriteRenderer>();renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Maps/"+recipe.mapId+".png");
        var chunk=new GameObject("RuntimeChunks");chunk.transform.SetParent(terrainObject.transform,false);
        var ts=new SerializedObject(terrain);ts.FindProperty("visualSourceTexture").objectReferenceValue=recipe.bakedTerrain;ts.FindProperty("collisionMaskTexture").objectReferenceValue=recipe.bakedTerrain;ts.FindProperty("terrainRenderer").objectReferenceValue=renderer;ts.FindProperty("chunkRoot").objectReferenceValue=chunk.transform;ts.FindProperty("terrainOriginWorld").vector2Value=recipe.origin;ts.FindProperty("pixelsPerUnit").intValue=recipe.pixelsPerUnit;ts.FindProperty("horizontalExpansion").floatValue=1;ts.FindProperty("upperSkyPaddingWorld").floatValue=recipe.skyPadding;ts.FindProperty("collisionCellSizePx").intValue=4;ts.ApplyModifiedPropertiesWithoutUndo();
        var background=new GameObject("Background",typeof(SpriteRenderer));background.transform.SetParent(map.transform,false);var bg=background.GetComponent<SpriteRenderer>();bg.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Title/Archipelago.png");bg.sortingOrder=-30;background.transform.position=new Vector3(0,6,8);background.transform.localScale=new Vector3(4.5f,4.5f,1);
        var water=new GameObject("Water - top edge is the death surface",typeof(BoxCollider2D),typeof(WaterZone));water.transform.SetParent(map.transform,false);water.transform.position=new Vector3(0,recipe.waterY-2,0);var wc=water.GetComponent<BoxCollider2D>();wc.isTrigger=true;wc.size=new Vector2(recipe.width/(float)recipe.pixelsPerUnit+16,4);
        var death=new GameObject("DeathBoundary",typeof(BoxCollider2D),typeof(DeathZone));death.transform.SetParent(map.transform,false);death.transform.position=new Vector3(0,recipe.waterY-10,0);var dc=death.GetComponent<BoxCollider2D>();dc.isTrigger=true;dc.size=new Vector2(200,8);
        var systems=new GameObject("MatchSystems");var turns=systems.AddComponent<TurnManager>();var inventory=systems.AddComponent<PlayerInventoryManager>();var spawner=systems.AddComponent<TerrainRandomSpawner>();var items=systems.AddComponent<CommonHeadItemSpawner>();var bootstrap=systems.AddComponent<ObjectHeadMatchBootstrap>();
        var authoring=map.AddComponent<ObjectHeadMapAuthoring>();var author=new SerializedObject(authoring);author.FindProperty("terrainOriginMarker").objectReferenceValue=terrainObject.transform;
        var waterMarker=new GameObject("WaterSurfaceMarker");waterMarker.transform.SetParent(map.transform,false);waterMarker.transform.position=new Vector3(0,recipe.waterY,0);author.FindProperty("waterSurfaceMarker").objectReferenceValue=waterMarker.transform;author.ApplyModifiedPropertiesWithoutUndo();
        BuildSpawns(map, recipe);
        var bs=new SerializedObject(bootstrap);Set(bs,"sceneTerrain",terrain);Set(bs,"content",content);Set(bs,"sceneTurnManager",turns);Set(bs,"sceneInventory",inventory);Set(bs,"sceneSpawner",spawner);Set(bs,"sceneItemSpawner",items);Set(bs,"mapAuthoring",authoring);bs.FindProperty("cleanupExistingPrototypeScene").boolValue=false;bs.ApplyModifiedPropertiesWithoutUndo();
        var root=CanvasRoot("BattleUI",out var canvas);var screen=canvas.AddComponent<ObjectHeadBattleScreen>();screen.turns=turns;screen.content=content;screen.localization=localization;BuildLayout("BattleLayout",canvas.transform,screen);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/"+recipe.sceneName+".unity");
    }
    private static void BuildSpawns(GameObject map, ObjectHeadMapRecipe recipe)
    {
        var authored = map.AddComponent<ObjectHeadSpawnLayout>();
        authored.layouts = new ObjectHeadModeSpawnLayout[content.modes.Length];
        for (int m = 0; m < content.modes.Length; m++)
        {
            var mode = content.modes[m];
            var group = new GameObject(mode.mode + " - spawn markers"); group.transform.SetParent(map.transform, false);
            var layout = new ObjectHeadModeSpawnLayout { mode=mode.mode, seats=new ObjectHeadSpawnSeat[mode.players] };
            authored.layouts[m] = layout;
            for (int seat = 0; seat < mode.players; seat++)
            {
                float[] xs = mode.players == 2 ? (seat == 0 ? new[]{-19.5f,-18f,-16.5f} : new[]{19.5f,18f,16.5f}) : new[]{-18f+12*seat};
                layout.seats[seat] = new ObjectHeadSpawnSeat { characterSlots=new Transform[xs.Length] };
                for (int slot=0;slot<xs.Length;slot++)
                {
                    var point=new GameObject("Seat"+(seat+1)+" Slot"+(slot+1));
                    point.transform.SetParent(group.transform,false);
                    point.transform.position=new Vector3(xs[slot],recipe.origin.y+280f/recipe.pixelsPerUnit+.62f,0);
                    layout.seats[seat].characterSlots[slot]=point.transform;
                }
            }
        }
    }

    private static void Set(SerializedObject s,string name,UnityEngine.Object value)=>s.FindProperty(name).objectReferenceValue=value;
    private static void ImportSprite(string path,float ppu,bool readable)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var i=(TextureImporter)AssetImporter.GetAtPath(path);i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=ppu;i.isReadable=readable;i.mipmapEnabled=false;i.SaveAndReimport();
    }
    private static void ConfigureRouting()
    {
        var config=AssetDatabase.LoadAssetAtPath<ObjectHeadNetworkConfig>("Assets/Resources/ObjectHeadNetworkConfig.asset");var s=new SerializedObject(config);
        s.FindProperty("fallbackMapId").stringValue=content.maps[0].id;
        var settings=s.FindProperty("defaultRoomSettings");settings.FindPropertyRelative("rulesetVersion").stringValue=ObjectHeadRoomSettings.CurrentRulesetVersion;settings.FindPropertyRelative("fixedMapId").stringValue=content.maps[0].id;
        var pool=settings.FindPropertyRelative("randomMapPool");pool.arraySize=content.maps.Length;
        var bindings=s.FindProperty("mapScenes");bindings.arraySize=content.maps.Length;
        for(int i=0;i<content.maps.Length;i++){pool.GetArrayElementAtIndex(i).stringValue=content.maps[i].id;bindings.GetArrayElementAtIndex(i).FindPropertyRelative("mapId").stringValue=content.maps[i].id;bindings.GetArrayElementAtIndex(i).FindPropertyRelative("sceneName").stringValue=content.maps[i].sceneName;}
        s.ApplyModifiedPropertiesWithoutUndo();
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/ObjectHeadTitle.unity",true)}.Concat(content.maps.Select(m=>new EditorBuildSettingsScene("Assets/Scenes/"+m.sceneName+".unity",true))).ToArray();
    }
    private static void ArchiveUnusedScenes()
    {
        if(!AssetDatabase.IsValidFolder("Assets/Scenes/Archive"))AssetDatabase.CreateFolder("Assets/Scenes","Archive");
        foreach(string name in new[]{"6974","8529","99999","SampleScene","TerrainTestScene"})
        {
            string source="Assets/Scenes/"+name+".unity";
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(source)!=null)
            {
                string error=AssetDatabase.MoveAsset(source,"Assets/Scenes/Archive/"+name+".unity");if(!string.IsNullOrEmpty(error))throw new Exception(error);
            }
        }
    }
    public static void RepairStagingBindingsAndBuild()
    {
        // Repairs references only. Existing designer transforms and graphics are preserved.
        var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/ObjectHeadTitle.prefab");
        RepairBindings(root.GetComponentInChildren<ObjectHeadTitleScreen>(true),"TitleLayout");
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/UI/ObjectHeadTitle.prefab");
        PrefabUtility.UnloadPrefabContents(root);
        foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
        {
            var scene=EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Single);
            foreach(var item in scene.GetRootGameObjects())
            foreach(var screen in item.GetComponentsInChildren<ObjectHeadBattleScreen>(true))
                RepairBindings(screen,"BattleLayout");
            var background=GameObject.Find("Background")?.GetComponent<SpriteRenderer>();
            if(background!=null)
            {
                background.sprite=Resources.Load<Sprite>("Sprites/Map/background_ocean_sky");
                var size=background.sprite.bounds.size;
                float scale=70f/size.x;
                background.transform.localScale=Vector3.one*scale;
                background.transform.position=new Vector3(0,8,8);
                EditorUtility.SetDirty(background);
            }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        ObjectHeadTitleSceneBuilder.BuildWindowsDemo();
    }
    private static void RepairBindings(MonoBehaviour controller,string layoutName)
    {
        var layout=JsonUtility.FromJson<Layout>(File.ReadAllText("Assets/Editor/Authoring/"+layoutName+".json"));
        var children=controller.GetComponentsInChildren<Transform>(true);
        foreach(var button in controller.GetComponentsInChildren<Button>(true))
        {
            var text=button.transform.Find("Label")?.GetComponent<Text>();
            if(text!=null){text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=24;EditorUtility.SetDirty(text);}
        }
        foreach(var node in layout.nodes)
        {
            var transform=children.First(t=>t.name==node.name);
            if(!string.IsNullOrEmpty(node.bind))
            {
                UnityEngine.Object value=transform.gameObject;
                if(node.type=="button")value=transform.GetComponent<Button>();
                else if(node.type=="input")value=transform.GetComponent<InputField>();
                else if(node.type=="text")value=transform.GetComponent<Text>();
                else if(node.type=="slider")value=transform.GetComponent<Slider>();
                else if(node.type!="group")value=transform.GetComponent<Image>();
                Bind(controller,node.bind,value);
            }
            if(!string.IsNullOrEmpty(node.labelBind))Bind(controller,node.labelBind,transform.GetComponentInChildren<Text>(true));
        }
    }

    [MenuItem("Object Head/0916/Validate Release Assets")]
    public static void Validate()
    {
        var catalog=ObjectHeadContent.Load();if(catalog==null||catalog.uiFont==null)throw new Exception("Content/font missing.");
        foreach(var entry in catalog.characters)if(entry.prefab==null||entry.portrait==null)throw new Exception("Character assets missing.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled))
            {
                var scene=EditorSceneManager.OpenScene(entry.path,OpenSceneMode.Single);
                foreach(var root in scene.GetRootGameObjects())
                {
                foreach(var text in root.GetComponentsInChildren<Text>(true))
                    if(text.font==null)throw new Exception("Missing font: "+entry.path+" / "+text.name);
                foreach(var controller in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!(controller is ObjectHeadTitleScreen) && !(controller is ObjectHeadBattleScreen)) continue;
                    foreach(var field in controller.GetType().GetFields())
                    {
                        if(field.Name=="quickMatch3Button")continue;
                        if(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && (UnityEngine.Object)field.GetValue(controller)==null)
                            throw new Exception("Missing serialized UI reference: "+entry.path+" / "+field.Name);
                        if(field.FieldType.IsArray && field.GetValue(controller) is Array array)
                            foreach(var value in array)if(value==null || (value is UnityEngine.Object obj && obj==null))
                                throw new Exception("Missing UI array reference: "+field.Name);
                    }
                }
                }
                Debug.Log("[0916 VALIDATE] "+entry.path);
            }
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
}
