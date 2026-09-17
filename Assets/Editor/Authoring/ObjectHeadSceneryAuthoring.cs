using UnityEditor;
using UnityEngine;

public static class ObjectHeadSceneryAuthoring
{
    public static void AddToScene(int variant)
    {
        const string path="Assets/Art/Presentation/DistantIslands.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        // Static authored props: no collider, no terrain mask and no automatic runtime repositioning.
        var parent=GameObject.Find("Distant Scenery - non playable");if(parent!=null)return;
        parent=new GameObject("Distant Scenery - non playable");
        Layer(parent.transform,sprite,"Far misty islands",new Vector2(variant%2==0?-7:8,1.8f),82,new Color(.75f,.88f,1,.32f),-27,variant%2==1);
        Layer(parent.transform,sprite,"Near distant shoreline",new Vector2(variant%2==0?12:-12,-.5f),58,new Color(.85f,.93f,1,.52f),-26,variant%2==0);
    }
    private static void Layer(Transform parent,Sprite sprite,string name,Vector2 position,float width,Color tint,int order,bool flip)
    {
        var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent,false);go.transform.position=new Vector3(position.x,position.y,5);
        var renderer=go.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.color=tint;renderer.sortingOrder=order;renderer.flipX=flip;
        go.transform.localScale=Vector3.one*(width/sprite.bounds.size.x);
    }
}
