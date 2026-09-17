using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ObjectHeadEscapeAuthoring
{
    public static void Apply()
    {
        const string path="Assets/Art/Presentation/CommonEraser.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.isReadable=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var pixels=texture.GetPixels32();int minX=texture.width,minY=texture.height,maxX=0,maxY=0;
        for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>127){minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
        var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var existing=provider.GetSpriteRects().FirstOrDefault(r=>r.name=="CommonEraser");
        var rect=existing??new SpriteRect{name="CommonEraser",spriteID=GUID.Generate(),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center};rect.rect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1);
        provider.SetSpriteRects(new[]{rect});provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(rect.name,rect.spriteID)});provider.Apply();importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single(s=>s.name=="CommonEraser");
        var content=ObjectHeadContent.Load();var entries=content.commonHeads.ToList();var eraser=entries.FirstOrDefault(d=>d.type==CommonHeadType.Eraser);
        if(eraser==null){eraser=new ObjectHeadCommonDefinition{type=CommonHeadType.Eraser};entries.Add(eraser);}
        eraser.nameKey="common_eraser";eraser.sprite=sprite;eraser.use=ObjectHeadCommonUse.EraseTerrain;eraser.spawnCount=2;eraser.worldVisualSize=.78f;content.commonHeads=entries.ToArray();EditorUtility.SetDirty(content);
        var micro=ObjectHeadMicroFeedback.Load();if(micro.Find(ObjectHeadMicroCue.EraserCrumb)==null)
        {
            micro.profiles=micro.profiles.Concat(new[]{new ObjectHeadMicroProfile{cue=ObjectHeadMicroCue.EraserCrumb,sprite=micro.Find(ObjectHeadMicroCue.Debris).sprite,
                tint=new Color(1,.72f,.8f,1),count=3,size=new Vector2(.04f,.11f),lifetime=new Vector2(.25f,.5f),speed=new Vector2(.3f,1.2f),spread=130,gravity=2,spin=210,endScale=.15f}}).ToArray();EditorUtility.SetDirty(micro);
        }
        EditorUtility.SetDirty(ObjectHeadPresentation.Load());EditorUtility.SetDirty(ObjectHeadAITuning.Load());AssetDatabase.SaveAssets();
    }
}
