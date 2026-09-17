using UnityEngine;

/// <summary>Camera affects artwork origin only. Landing traces always use deterministic terrain bounds.</summary>
public static class ObjectHeadSkyDrop
{
    public static Vector2 Landing(TerrainManager terrain,Vector2 target)
    {
        if(terrain==null)return target;
        var bounds=terrain.GetTerrainBounds();
        return terrain.TryCheckTerrainHit(new Vector2(target.x,bounds.max.y+1),new Vector2(target.x,bounds.min.y-1),out var hit)?hit.point:target;
    }
    public static Vector2 Origin(Vector2 target,float visualHeight,TerrainManager terrain=null)
    {
        var art=ObjectHeadPresentation.Load();
        float y=target.y+(art!=null?art.airstrikeDropHeight:8);
        if(art!=null && art.dropsFromSkyEdge)
        {
            if(terrain==null)terrain=Object.FindAnyObjectByType<TerrainManager>();
            float top=terrain!=null?terrain.GetTerrainBounds().max.y:y;
            var camera=Camera.main;
            if(camera!=null)
            {
                var controller=camera.GetComponent<ObjectHeadCameraController>();
                if(controller!=null)top=Mathf.Max(top,controller.MaximumVisibleSkyY());
                top=Mathf.Max(top,camera.transform.position.y+camera.orthographicSize);
            }
            y=Mathf.Max(y,top+art.skyDropMargin+visualHeight);
        }
        return new Vector2(target.x,y);
    }
}
