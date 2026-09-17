using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ObjectHeadRainbowSweep
{
    public static Transform Focus {get;private set;}
    public static int LastHitCount {get;private set;}
    public static IEnumerator Play(Vector2 impact,CharacterCombat owner,TerrainManager terrain,ObjectHeadSkillSettings skill)
    {
        if(terrain==null)yield break;
        var art=ObjectHeadPresentation.Load();var bounds=terrain.GetTerrainBounds();
        float start=bounds.min.x-2,end=bounds.max.x+2,duration=Mathf.Max(.2f,art.rainbowSeconds);
        var victims=new HashSet<CharacterCombat>();LastHitCount=0;
        var cues=ObjectHeadActionCues.Load();
        ObjectHeadActionCues.Show(cues?.televisionPrefab,impact,ObjectHeadContent.Load().Common(CommonHeadType.RetroTV).sprite);
        if(cues!=null)yield return new WaitForSeconds(cues.televisionAnticipation);
        GameObject cat=null;AudioSource sound=null;
        if(ObjectHeadNetworkManager.Instance?.IsDedicatedWorker!=true)
        {
            cat=new GameObject("RainbowCat",typeof(SpriteRenderer));var renderer=cat.GetComponent<SpriteRenderer>();renderer.sprite=art.rainbowCat;renderer.sortingOrder=45;
            if(renderer.sprite!=null)cat.transform.localScale=Vector3.one*art.rainbowSize/renderer.sprite.bounds.size.x;
            Focus=cat.transform;sound=ObjectHeadSpecialAudio.Play("rainbow",cat.transform,true);
        }
        float previous=start,nextCut=start,step=Mathf.Max(.1f,Mathf.Min(art.rainbowTerrainStep,skill.terrainRadiusPx/terrain.PixelsPerUnit));
        try
        {
            for(float t=0;;t+=Time.deltaTime)
            {
                float x=Mathf.Lerp(start,end,Mathf.Clamp01(t/duration));
                if(cat!=null){cat.transform.position=new Vector2(x,impact.y);cat.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*24)*3);}
                if(ObjectHeadCommonAuthority.CanWrite)
                {
                    foreach(var hit in Physics2D.OverlapBoxAll(new Vector2((previous+x)*.5f,impact.y),new Vector2(Mathf.Max(.01f,x-previous),skill.explosionRadiusWorld*2),0))
                    {
                        var target=hit.GetComponentInParent<CharacterCombat>();
                        if(!DamageSystem.CanDamage(owner,target)||!victims.Add(target))continue;
                        target.TakeDamage(skill.maxDamage);target.ApplyKnockback(new Vector2(skill.knockbackForce,skill.knockbackForce*.3f));LastHitCount++;
                        ObjectHeadPresentation.Impact(skill.skillId,target.KnockbackCenter,.5f);
                    }
                    int batch=0;while(nextCut<=x && batch++<12){terrain.DestroyCircle(new Vector2(nextCut,impact.y),skill.terrainRadiusPx);nextCut+=step;}
                }
                previous=x;if(t>=duration)break;yield return null;
            }
            // Finish the final tail strip even on slow frames; do not leave frame-rate-dependent holes.
            if(ObjectHeadCommonAuthority.CanWrite)while(nextCut<=end){terrain.DestroyCircle(new Vector2(nextCut,impact.y),skill.terrainRadiusPx);nextCut+=step;yield return null;}
        }
        finally{Focus=null;if(cat!=null){ObjectHeadPresentation.Impact(skill.skillId,cat.transform.position,.5f);Object.Destroy(cat);}if(sound!=null)Object.Destroy(sound.gameObject);}
    }
}
