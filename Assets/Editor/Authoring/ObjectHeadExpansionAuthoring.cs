using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Explicit authoring pass only. Never runs when entering Play or on an ordinary build.
public static class ObjectHeadExpansionAuthoring
{
    private static ObjectHeadContent catalog;
    private static Font font;
    private static Sprite panelSprite;
    [MenuItem("Object Head/0916/Apply Expansion (replaces expansion layouts)")]
    public static void Apply()
    {
        catalog=ObjectHeadContent.Load();font=catalog.uiFont;
        panelSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/InkPanel.png");
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
        Characters();CommonAndEffects();Audio();Title();Maps();
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        Debug.Log("[EXPANSION] Six characters, authored skills, motion frames, front end and catalog browser saved.");
    }
    public static void ApplyAndBuild(){Apply();ObjectHeadTitleSceneBuilder.BuildWindowsDemo();}

    public static Sprite[] Slice(string name,int cols,int rows,string folder="Characters",bool largestConnectedShape=false,bool uniformCells=false)
    {
        string path="Assets/Art/"+folder+"/"+name+".png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.isReadable=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;
        importer.spritePixelsPerUnit=100;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int w=texture.width/cols,h=texture.height/rows;
        var metadata=new SpriteMetaData[cols*rows];
        for(int i=0;i<metadata.Length;i++)
        {
            int x0=i%cols*w,y0=(rows-1-i/cols)*h;
            int minX=w,minY=h,maxX=0,maxY=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(texture.GetPixel(x0+x,y0+y).a>.05f)
            {minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
            if(minX>maxX)throw new Exception("Empty sprite cell: "+name+"/"+i);
            metadata[i]=new SpriteMetaData{name=name+"_"+i,rect=new Rect(x0+minX,y0+minY,maxX-minX+1,maxY-minY+1),pivot=new Vector2(.5f,.5f),alignment=(int)SpriteAlignment.Center};
            if(largestConnectedShape)metadata[i].rect=LargestShapeBounds(texture,x0,y0,w,h);
            if(uniformCells)metadata[i].rect=new Rect(x0,y0,w,h);
        }
        if(name=="UtilityHeads")
        {
            metadata[0].rect=new Rect(40,615,385,380);metadata[1].rect=new Rect(490,615,320,520);metadata[2].rect=new Rect(810,615,460,310);
            for(int k=0;k<3;k++){var r=metadata[k].rect;metadata[k].rect=new Rect(r.x*texture.width/1280f,r.y*texture.height/1280f,r.width*texture.width/1280f,r.height*texture.height/1280f);}
        }
        SetSpriteMetadata(importer,metadata);
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        return Enumerable.Range(0,metadata.Length).Select(i=>sprites.Single(s=>s.name==name+"_"+i)).ToArray();
    }
    // Only trims the Sprite Editor rectangle; the original transparent image is untouched.
    private static Rect LargestShapeBounds(Texture2D texture,int x0,int y0,int w,int h)
    {
        var pixels=texture.GetPixels32();var visited=new bool[w*h];var queue=new int[w*h];
        int best=0;Rect bounds=default;
        for(int start=0;start<visited.Length;start++)
        {
            if(visited[start] || pixels[(y0+start/w)*texture.width+x0+start%w].a<13)continue;
            int read=0,write=1,minX=w,minY=h,maxX=0,maxY=0;queue[0]=start;visited[start]=true;
            while(read<write)
            {
                int at=queue[read++],x=at%w,y=at/w;
                minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;if(nx<0||nx>=w||ny<0||ny>=h)continue;
                    int next=ny*w+nx;if(visited[next])continue;visited[next]=true;
                    if(pixels[(y0+ny)*texture.width+x0+nx].a>=13)queue[write++]=next;
                }
            }
            if(write>best){best=write;bounds=new Rect(x0+minX,y0+minY,maxX-minX+1,maxY-minY+1);}
        }
        return bounds;
    }
    private static void SetSpriteMetadata(TextureImporter importer,SpriteMetaData[] metadata)
    {
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects();
        var rects=metadata.Select(m=>new SpriteRect{name=m.name,rect=m.rect,pivot=m.pivot,alignment=SpriteAlignment.Custom,
            spriteID=old.FirstOrDefault(s=>s.name==m.name)?.spriteID ?? GUID.Generate()}).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply();importer.SaveAndReimport();
        AssetDatabase.ImportAsset(importer.assetPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
    }
    private static void CommonAndEffects()
    {
        var utility=Slice("UtilityHeads",3,2);
        var effects=Slice("WorldEffects",3,2,"Presentation");var ice=Slice("CommonVariants",3,1)[1];var potion=Slice("HealingPotion",1,1)[0];
        var library=ObjectHeadPresentation.Load();library.dirtTerrainSprite=effects[3];library.cloudTerrainSprite=utility[2];library.smokeSprite=utility[3];library.airstrikeBombSprite=effects[5];
        var profiles=library.skills.ToList();
        Sprite[] zones={effects[0],effects[1],effects[2]};int[] ids={11,12,23};
        for(int i=0;i<ids.Length;i++){var profile=profiles.FirstOrDefault(p=>p.skillId==ids[i]);if(profile!=null)profile.zoneSprite=zones[i];}
        var launch=profiles.First().launchPrefab;var flight=profiles.First().flightPrefab;
        foreach(var character in catalog.characters.Where(c=>(int)c.kind>=3))for(int slot=0;slot<3;slot++)
        {
            int id=((int)character.kind+1)*10+slot+1;
            profiles.RemoveAll(p=>p.skillId==id);
            var basis=profiles.First(p=>p.skillId==((int)character.kind==4?12:31));
            profiles.Add(new ObjectHeadSkillPresentation{skillId=id,launchPrefab=launch,flightPrefab=flight,impactPrefab=basis.impactPrefab,impactScale=.7f,zoneSprite=id==62?utility[3]:null});
        }
        var bullet=AssetDatabase.LoadAssetAtPath<ObjectHeadSkillDefinition>("Assets/GameData/Skills/revolver_1.asset");bullet.settings.projectileSprite=utility[5];bullet.settings.projectileVisualDiameter=.25f;EditorUtility.SetDirty(bullet);
        var entries=new System.Collections.Generic.List<ObjectHeadCommonDefinition>();
        var classicCommon=Slice("LegacyCommonHeads",3,1);
        var classicTypes=new[]{CommonHeadType.Attack,CommonHeadType.Mobility,CommonHeadType.TerrainCreation};
        for(int i=0;i<classicTypes.Length;i++)entries.Add(new ObjectHeadCommonDefinition{type=classicTypes[i],sprite=classicCommon[i],spawnCount=2});
        var sprites=new[]{potion,ice,utility[0],utility[1]};var names=new[]{"potion","ice","helmet","smoke"};
        for(int i=0;i<4;i++)
        {
            string path="Assets/GameData/Skills/common_"+names[i]+".asset";
            var skill=AssetDatabase.LoadAssetAtPath<ObjectHeadSkillDefinition>(path);
            if(skill==null){skill=ScriptableObject.CreateInstance<ObjectHeadSkillDefinition>();AssetDatabase.CreateAsset(skill,path);}
            skill.balancePrefix="common."+names[i];skill.nameKey="common_"+names[i];
            var s=ObjectHeadSkillSettings.CreateDefault(sprites[i],Color.white,Color.white,0,1.8f,0);s.skillId=104+i;
            if(i==0){s.effectType=SkillEffectType.HealBurst;s.healing=24;}
            if(i==1){s.effectType=SkillEffectType.CreateSlowZone;s.zoneDurationRounds=2;s.zoneLengthWorld=3.5f;s.slowMultiplier=.45f;s.zoneThicknessWorld=.2f;}
            if(i==3){s.effectType=SkillEffectType.SmokeZone;s.zoneDurationRounds=2;s.explosionRadiusWorld=2.2f;}
            skill.settings=s;EditorUtility.SetDirty(skill);
            entries.Add(new ObjectHeadCommonDefinition{type=(CommonHeadType)(4+i),nameKey=skill.nameKey,sprite=sprites[i],skill=i==2?null:skill,use=i==2?ObjectHeadCommonUse.SelfShield:ObjectHeadCommonUse.Projectile,spawnCount=1,shieldAmount=25});
            profiles.RemoveAll(p=>p.skillId==s.skillId);profiles.Add(new ObjectHeadSkillPresentation{skillId=s.skillId,launchPrefab=launch,flightPrefab=flight,zoneSprite=i==1?utility[4]:null});
        }
        catalog.commonHeads=entries.ToArray();library.skills=profiles.ToArray();EditorUtility.SetDirty(library);
    }
    private static void Maps()
    {
        foreach(var map in catalog.maps)
        {
            var recipe=AssetDatabase.LoadAssetAtPath<ObjectHeadMapRecipe>("Assets/GameData/Maps/"+map.id+".asset");
            JsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/Editor/Authoring/"+map.id+".json"),recipe);
            ObjectHeadMapRecipeEditor.Bake(recipe);
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+map.sceneName+".unity");
            var screen=Object.FindAnyObjectByType<ObjectHeadBattleScreen>();
            if(screen.trainingResetButton==null)screen.trainingResetButton=Button(screen.menuPanel.transform,"reset_training",0,-170,350,48);
            EditorSceneManager.SaveScene(scene);
        }
    }
    private static void Audio()
    {
        AssetDatabase.Refresh();const string path="Assets/Resources/ObjectHeadAudioLibrary.asset";
        var audio=AssetDatabase.LoadAssetAtPath<ObjectHeadAudioLibrary>(path);
        if(audio==null){audio=ScriptableObject.CreateInstance<ObjectHeadAudioLibrary>();AssetDatabase.CreateAsset(audio,path);}
        audio.music=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/IslandLoop.wav");
        EditorUtility.SetDirty(audio);
    }
    private static void Characters()
    {
        Directory.CreateDirectory("Assets/GameData/Skills");AssetDatabase.Refresh();
        var old=Slice("ClassicHeads",3,3);var motion=Slice("BodyMotion",4,3);
        var heads=new[]{old.Take(3).ToArray(),old.Skip(3).Take(3).ToArray(),new[]{old[7],old[6],old[8]},Slice("RevolverHeads",3,1),Slice("MagnetHeads",3,1),Slice("KettleHeads",3,1)};
        string[] names={"bulb","seed","bomb","revolver","magnet","kettle"};
        string[] roles={"control","control","damage","support","control","support"};
        int[] hp={85,120,100,95,110,105};
        float[] power={1.15f,.88f,1.1f,1,1,1};float[] resist={.75f,1.25f,.9f,1,1.15f,1};
        var definitions=new ObjectHeadCharacterDefinition[6];
        for(int kind=0;kind<6;kind++)
        {
            string path="Assets/Prefabs/Characters/"+names[kind]+".prefab";
            string source=File.Exists(path)?path:"Assets/Prefabs/Characters/bomb.prefab";
            var root=PrefabUtility.LoadPrefabContents(source);root.name=names[kind];
            var visual=root.GetComponent<CharacterVisual>();var data=new SerializedObject(visual);
            data.FindProperty("characterKind").enumValueIndex=kind;
            data.FindProperty("spriteFacesRightByDefault").boolValue=true;
            data.FindProperty("preserveAuthoredTransforms").boolValue=true;
            SetArray(data,"authoredSkillHeads",heads[kind]);SetArray(data,"walkFrames",motion.Take(4).ToArray());
            string[] fields={"bodyIdle","bodyCharge","bodyThrow","bodyHit","jumpTakeoff","jumpRise","jumpFall","jumpLand"};
            for(int i=0;i<fields.Length;i++)data.FindProperty(fields[i]).objectReferenceValue=motion[i+4];
            var body=(SpriteRenderer)data.FindProperty("bodyRenderer").objectReferenceValue;
            var head=(SpriteRenderer)data.FindProperty("headRenderer").objectReferenceValue;
            body.sprite=motion[4];head.sprite=heads[kind][0];
            body.transform.localScale=Vector3.one*(.62f/motion[4].bounds.size.y);
            head.transform.localScale=Vector3.one*(.78f/Mathf.Max(head.sprite.bounds.size.x,head.sprite.bounds.size.y));
            data.ApplyModifiedPropertiesWithoutUndo();
            var selector=new SerializedObject(root.GetComponent<DemoSkillSelector>());
            selector.FindProperty("characterKind").enumValueIndex=kind;
            if(kind>=3)SetArray(selector,"authoredSkills",Enumerable.Range(0,3).Select(slot=>Skill(kind,slot,names[kind],heads[kind][slot])).ToArray());
            selector.ApplyModifiedPropertiesWithoutUndo();
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
            definitions[kind]=new ObjectHeadCharacterDefinition{kind=(ObjectHeadCharacterKind)kind,nameKey=names[kind]+"_name",descriptionKey=names[kind]+"_description",roleKey="role_"+roles[kind],portrait=heads[kind][0],prefab=prefab,maxHp=hp[kind],throwPower=power[kind],knockbackResistance=resist[kind],accent=Color.white};
        }
        catalog.characters=definitions;
        Slice("HealingPotion",1,1);
    }
    private static ObjectHeadSkillDefinition Skill(int kind,int slot,string character,Sprite head)
    {
        string prefix="skill."+character+"."+(slot+1),path="Assets/GameData/Skills/"+character+"_"+(slot+1)+".asset";
        var asset=AssetDatabase.LoadAssetAtPath<ObjectHeadSkillDefinition>(path);
        if(asset==null){asset=ScriptableObject.CreateInstance<ObjectHeadSkillDefinition>();AssetDatabase.CreateAsset(asset,path);}
        asset.balancePrefix=prefix;asset.nameKey=character+"_skill_"+(slot+1);asset.descriptionKey=asset.nameKey+"_description";
        var s=ObjectHeadSkillSettings.CreateDefault(head,Color.white,Color.white,24,1.5f,6);
        s.skillId=(kind+1)*10+slot+1;asset.cooldown=slot==0?0:slot==1?2:3;
        if(kind==3)
        {
            if(slot==0){s.straightShot=true;s.straightSpeed=28;s.maxDamage=30;s.explosionRadiusWorld=.5f;s.terrainRadiusPx=5;}
            if(slot==1){s.effectType=SkillEffectType.HealBurst;s.maxDamage=0;s.healing=28;s.explosionRadiusWorld=2.1f;s.knockbackForce=0;}
            if(slot==2){s.effectType=SkillEffectType.Airstrike;s.maxDamage=18;s.explosionRadiusWorld=1.35f;s.chainCount=3;s.chainSpacingWorld=1.1f;s.chainDelaySeconds=.28f;s.delaySeconds=.8f;s.terrainRadiusPx=24;}
        }
        if(kind==4)
        {
            s.effectType=SkillEffectType.MagneticPulse;s.pullsTargets=slot!=1;
            s.maxDamage=slot==2?18:12;s.explosionRadiusWorld=slot==2?3.8f:2.5f;s.knockbackForce=slot==2?8:6;
        }
        if(kind==5)
        {
            if(slot==0){s.effectType=SkillEffectType.HealBurst;s.maxDamage=0;s.healing=16;s.explosionRadiusWorld=1.4f;s.knockbackForce=0;asset.cooldown=1;}
            if(slot==1){s.effectType=SkillEffectType.CreateHazardZone;s.maxDamage=8;s.zoneDamagePerTurn=6;s.zoneLengthWorld=4;s.zoneDurationRounds=2;s.slowMultiplier=.75f;s.zoneThicknessWorld=.3f;}
            if(slot==2){s.effectType=SkillEffectType.DelayedExplosion;s.delaySeconds=.7f;s.maxDamage=42;s.explosionRadiusWorld=2.4f;s.terrainRadiusPx=38;}
        }
        asset.settings=s;EditorUtility.SetDirty(asset);return asset;
    }
    private static void SetArray(SerializedObject data,string name,Object[] values)
    {var p=data.FindProperty(name);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}

    private static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static void Place(Component c,Transform parent,float x,float y,float w,float h)
    {var r=(RectTransform)c.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
    private static Text Label(Transform parent,string key,float x,float y,float w,float h,int size=24)
    {
        var r=Rect(key,parent,x,y,w,h);var text=r.gameObject.AddComponent<Text>();text.font=font;text.fontSize=size;text.color=new Color32(239,243,232,255);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
        if(!string.IsNullOrEmpty(key)){var loc=r.gameObject.AddComponent<ObjectHeadLocalizedLabel>();loc.LocalizationKey=key;loc.Preview();}return text;
    }
    private static Button Button(Transform parent,string key,float x,float y,float w=420,float h=64)
    {
        var r=Rect(key,parent,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.sprite=panelSprite;image.type=Image.Type.Sliced;image.color=new Color32(43,81,92,255);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;
        Label(r,key,0,0,w-20,h-8);return b;
    }
    private static GameObject Panel(Transform parent,string name)
    {
        var r=Rect(name,parent,-430,-35,610,580);var image=r.gameObject.AddComponent<Image>();image.sprite=panelSprite;image.type=Image.Type.Sliced;image.color=new Color32(16,43,53,245);return r.gameObject;
    }
    private static void Title()
    {
        var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/ObjectHeadTitle.prefab");
        var t=root.GetComponentInChildren<ObjectHeadTitleScreen>(true);
        // Rebuild the previous authored baseline explicitly, not during runtime.
        ObjectHeadReleaseAuthoring.ReplaceVisualLayout(t,"TitleLayout");
        var previous=t.GetComponent<ObjectHeadFrontEnd>();if(previous!=null)Object.DestroyImmediate(previous);
        var oldBrowser=t.GetComponent<ObjectHeadCharacterBrowser>();if(oldBrowser!=null)Object.DestroyImmediate(oldBrowser);
        var oldSettings=t.GetComponent<ObjectHeadSettingsPanel>();if(oldSettings!=null)Object.DestroyImmediate(oldSettings);
        var f=t.gameObject.AddComponent<ObjectHeadFrontEnd>();t.frontEnd=f;f.title=t;
        var main=t.mainMenuPanel.transform;
        f.home=Panel(main,"Home");f.play=Panel(main,"Play");f.quick=Panel(main,"QuickMatch");f.create=Panel(main,"CreateRoom");f.find=Panel(main,"FindRoom");f.settings=Panel(main,"Settings");
        f.playButton=Button(f.home.transform,"menu_play",0,130);f.createButton=Button(f.home.transform,"create_room",0,25);f.settingsButton=Button(f.home.transform,"menu_settings",0,-80);
        f.quickButton=Button(f.play.transform,"quick_match_title",0,160);f.findButton=Button(f.play.transform,"find_room",0,55);f.trainingButton=Button(f.play.transform,"training",0,-50);
        Place(t.localPlayButton,f.play.transform,0,-160,420,54);
        Place(t.quickMatch2Button,f.quick.transform,0,130,440,66);Place(t.quickMatch4Button,f.quick.transform,0,20,440,66);Place(t.quickMatchTeamsButton,f.quick.transform,0,-90,440,66);
        Label(f.quick.transform,"quick_match_title",0,230,540,45,28);
        Label(f.create.transform,"create_room",0,230,540,45,30);Label(f.create.transform,"mode_select",0,135,400,40);
        Place(t.roomSizeMinusButton,f.create.transform,-220,65,45,48);Place(t.roomSizeValueText,f.create.transform,0,65,360,48);Place(t.roomSizePlusButton,f.create.transform,220,65,45,48);Place(t.createRoomButton,f.create.transform,0,-75,480,68);
        Label(f.find.transform,"find_room",0,220,540,45,30);Place(t.roomCodeInput,f.find.transform,0,75,470,60);Place(t.joinRoomButton,f.find.transform,0,-35,470,64);
        Label(f.find.transform,"join_code_hint",0,-140,490,70,20);
        Place(t.nicknameInput,main,420,-270,460,50);Label(main,"nickname_label",420,-220,460,40,20);
        f.backButton=Button(main,"back",-430,-370,260,46);
        var card=main.Find("MenuCard");if(card!=null)Object.DestroyImmediate(card.gameObject);
        var notice=main.Find("Notice");if(notice!=null)Place(notice,main,420,-345,580,50);
        Settings(t,f);Browser(t);
        foreach(var p in new[]{f.play,f.quick,f.create,f.find,f.settings})p.SetActive(false);
        foreach(var loc in t.GetComponentsInChildren<ObjectHeadLocalizedLabel>(true))loc.Preview();
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/UI/ObjectHeadTitle.prefab");PrefabUtility.UnloadPrefabContents(root);
    }
    private static void Browser(ObjectHeadTitleScreen t)
    {
        foreach(var old in t.characterButtons)if(old!=null)Object.DestroyImmediate(old.gameObject);t.characterButtons=Array.Empty<Button>();
        var b=t.gameObject.AddComponent<ObjectHeadCharacterBrowser>();t.characterBrowser=b;b.title=t;
        var lobby=t.lobbyPanel.transform;
        var choose=lobby.Find("ChooseLabel");if(choose!=null)Object.DestroyImmediate(choose.gameObject);
        b.search=Object.Instantiate(t.nicknameInput,lobby);b.search.name="CharacterSearch";b.search.text="";Place(b.search,lobby,55,45,470,42);
        var placeholder=b.search.placeholder.GetComponent<ObjectHeadLocalizedLabel>();if(placeholder==null)placeholder=b.search.placeholder.gameObject.AddComponent<ObjectHeadLocalizedLabel>();placeholder.LocalizationKey="search_character";
        b.roleButton=Button(lobby,"role_all",410,45,210,42);b.roleLabel=b.roleButton.GetComponentInChildren<Text>();
        Object.DestroyImmediate(b.roleLabel.GetComponent<ObjectHeadLocalizedLabel>());
        b.countLabel=Label(lobby,"",590,45,120,40,16);
        var viewport=Rect("CharacterScroll",lobby,220,-75,900,175);viewport.gameObject.AddComponent<RectMask2D>();
        var bg=viewport.gameObject.AddComponent<Image>();bg.color=new Color(0,0,0,.15f);
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=viewport;scroll.scrollSensitivity=110;
        var barObject=DefaultControls.CreateScrollbar(new DefaultControls.Resources());barObject.name="CharacterScrollbar";barObject.transform.SetParent(lobby,false);
        var bar=barObject.GetComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;Place(bar,lobby,682,-75,16,175);scroll.verticalScrollbar=bar;
        b.content=Rect("Content",viewport,0,0,900,175);b.content.anchorMin=new Vector2(0,1);b.content.anchorMax=new Vector2(1,1);b.content.pivot=new Vector2(.5f,1);b.content.sizeDelta=new Vector2(0,175);scroll.content=b.content;
        var grid=b.content.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(207,126);grid.spacing=new Vector2(13,12);grid.padding=new RectOffset(12,12,8,8);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;
        var fitter=b.content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        b.cardTemplate=Button(lobby,"",0,0,207,126);b.cardTemplate.name="CharacterCardTemplate";
        var label=b.cardTemplate.GetComponentInChildren<Text>();label.name="Label";Place(label,b.cardTemplate.transform,0,-42,195,30);label.fontSize=19;
        var portrait=Rect("Portrait",b.cardTemplate.transform,0,18,120,76).gameObject.AddComponent<Image>();portrait.preserveAspect=true;portrait.raycastTarget=false;b.cardTemplate.gameObject.SetActive(false);
        Place(t.characterDescription,lobby,220,-184,910,44);t.characterDescription.fontSize=17;
        Place(t.mapPreview,lobby,-80,-235,170,60);Place(t.previousMapButton,lobby,-195,-235,35,40);Place(t.nextMapButton,lobby,35,-235,35,40);
        Place(t.mapName,lobby,240,-234,350,52);t.mapName.fontSize=16;
        Place(t.toggleMapModeButton,lobby,493,-220,160,32);Place(t.applySettingsButton,lobby,493,-255,160,30);
        t.toggleMapModeButton.GetComponentInChildren<Text>().fontSize=16;t.applySettingsButton.GetComponentInChildren<Text>().fontSize=16;
        Place(t.readyButton,lobby,0,-300,410,44);Place(t.startGameButton,lobby,460,-300,410,44);
    }
    private static Dropdown Dropdown(Transform parent,float y)
    {
        var go=DefaultControls.CreateDropdown(new DefaultControls.Resources());go.transform.SetParent(parent,false);
        var d=go.GetComponent<Dropdown>();Place(d,parent,85,y,340,44);
        foreach(var text in go.GetComponentsInChildren<Text>(true)){text.font=font;text.fontSize=19;text.color=new Color32(18,40,50,255);}
        return d;
    }
    private static void Settings(ObjectHeadTitleScreen t,ObjectHeadFrontEnd f)
    {
        var p=f.settings.transform;var s=t.gameObject.AddComponent<ObjectHeadSettingsPanel>();s.title=t;f.settingsController=s;
        Label(p,"menu_settings",0,240,540,42,30);
        string[] keys={"resolution","window_mode","language","volume_bgm","volume_sfx"};float[] ys={160,98,36,-26,-88};
        for(int i=0;i<keys.Length;i++)Label(p,keys[i],-190,ys[i],150,38,19);
        s.resolution=Dropdown(p,ys[0]);s.windowMode=Dropdown(p,ys[1]);s.language=Dropdown(p,ys[2]);
        s.resolutions=new[]{new ObjectHeadResolution{width=1280,height=720},new ObjectHeadResolution{width=1600,height=900},new ObjectHeadResolution{width=1920,height=1080},new ObjectHeadResolution{width=2560,height=1440},new ObjectHeadResolution{width=3840,height=2160}};
        Slider MakeSlider(float y){var go=DefaultControls.CreateSlider(new DefaultControls.Resources());var slider=go.GetComponent<Slider>();Place(slider,p,85,y,340,30);return slider;}
        s.bgm=MakeSlider(ys[3]);s.sfx=MakeSlider(ys[4]);s.apply=Button(p,"apply_settings",-125,-170,230,48);s.confirm=Button(p,"confirm",125,-170,230,48);s.status=Label(p,"",0,-228,535,48,17);s.confirm.gameObject.SetActive(false);
    }
}
