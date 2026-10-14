using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ObjectHeadNetworkSyncAudit
{
    private static int checks;
    public static void BuildPlaytest()
    {
        ObjectHeadTitlePolishAuthoring.AddConnectionProgress();
        Run();
        ObjectHeadTitleSceneBuilder.BuildWindowsPlaytest();
    }
    public static void Run()
    {
        checks=0;
        var objects = new List<UnityEngine.Object>();
        try
        {
            GameStartData.Clear();
            var source=new Texture2D(260,132,TextureFormat.RGBA32,false); objects.Add(source);
            var pixels=new Color32[source.width*source.height];
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32((byte)(i%203),80,30,(byte)((i/source.width)<90?255:0));
            source.SetPixels32(pixels);source.Apply();
            TerrainManager Create(string name)
            {
                var root=new GameObject(name,typeof(SpriteRenderer));objects.Add(root);
                var chunks=new GameObject("Chunks");chunks.transform.SetParent(root.transform);
                var map=root.AddComponent<TerrainManager>();
                map.Configure(source,null,root.GetComponent<SpriteRenderer>(),chunks.transform,Vector2.zero,32,64,4,1,0);
                objects.Add(map.RuntimeVisualTexture);objects.Add(map.RuntimeCollisionTexture);
                objects.Add(root.GetComponent<SpriteRenderer>().sprite);
                return map;
            }
            var host=Create("AuditHost");var client=Create("AuditGuest");
            Check(host.CaptureNetworkTileHashes().SequenceEqual(client.CaptureNetworkTileHashes()),"identical baseline hashes");
            Check(host.NetworkTileCount==6,"partial edge tile layout");
            var destroy=TerrainEditOperation.Circle(TerrainEditOperationKind.DestroyCircle,new Vector2Int(128,70),35,TerrainType.Base);
            var create=TerrainEditOperation.Circle(TerrainEditOperationKind.CreateCircle,new Vector2Int(128,70),12,TerrainType.Created);
            host.ApplyOperation(destroy);host.ApplyOperation(create);
            var pending=new SortedDictionary<long,TerrainEditOperation>{{2,create}};
            long revision=ObjectHeadTerrainSync.ApplyContiguous(0,pending,op=>client.ApplyOperation(op));
            Check(revision==0 && pending.Count==1,"out-of-order edits wait for gap");
            pending[1]=destroy;
            revision=ObjectHeadTerrainSync.ApplyContiguous(revision,pending,op=>client.ApplyOperation(op));
            Check(revision==2 && pending.Count==0,"missing edit replay drains in order");
            Check(host.CaptureNetworkTileHashes().SequenceEqual(client.CaptureNetworkTileHashes()),"replayed visuals and collision equal host");
            pending[2]=create;
            Check(ObjectHeadTerrainSync.ApplyContiguous(revision,pending,_=>throw new Exception("duplicate applied"))==2,"old edit cannot reapply");
            client.ApplyOperation(destroy);
            Check(!host.CaptureNetworkTileHashes().SequenceEqual(client.CaptureNetworkTileHashes()),"divergence detected");
            for(int i=0;i<host.NetworkTileCount;i++)
            {
                string tile=host.CaptureNetworkTile(i);
                Check(tile.Length<262144 && client.ApplyNetworkTile(i,tile),"bounded tile repair "+i);
            }
            Check(host.CaptureNetworkTileHashes().SequenceEqual(client.CaptureNetworkTileHashes()),"exact tile repair restores mask, type and artwork");
            Check(host.IsSolidWorld(host.PixelToWorld(new Vector2Int(128,70)))==client.IsSolidWorld(client.PixelToWorld(new Vector2Int(128,70))),"collision query equal after repair");
            var before=client.CaptureNetworkTileHashes();
            Check(!client.ApplyNetworkTile(-1,"bad") && !client.ApplyNetworkTile(0,"bad!"),"invalid tile payload rejected");
            Check(before.SequenceEqual(client.CaptureNetworkTileHashes()),"invalid payload leaves map unchanged");
            var recipe=ScriptableObject.CreateInstance<ObjectHeadMapRecipe>();objects.Add(recipe);
            var coverSource=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);objects.Add(coverSource);
            coverSource.SetPixels32(Enumerable.Repeat(new Color32(80,55,30,255),source.width*source.height).ToArray());coverSource.Apply();
            recipe.bakedTunnelForeground=Sprite.Create(coverSource,new Rect(0,0,source.width,source.height),Vector2.zero,32);
            recipe.pixelsPerUnit=32;objects.Add(recipe.bakedTunnelForeground);
            ObjectHeadTunnelForeground Cover(TerrainManager map)
            {
                var go=new GameObject("Cover",typeof(SpriteRenderer));objects.Add(go);
                var cover=go.AddComponent<ObjectHeadTunnelForeground>();cover.Configure(map,recipe);
                typeof(ObjectHeadTunnelForeground).GetMethod("Initialize",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(cover,null);
                return cover;
            }
            var hostCover=Cover(host);var guestCover=Cover(client);
            TerrainEditOperation coverEdit=default;int published=0;
            host.OperationApplied += op=>{coverEdit=op;published++;};
            host.DestroyCircle(host.PixelToWorld(new Vector2Int(128,115)),7);
            Check(published==1 && hostCover.ReadNetworkPixel(128,115).a==0,"cover-only destruction publishes even without solid terrain");
            client.ApplyOperation(coverEdit);
            Check(guestCover.ReadNetworkPixel(128,115).a==0 && host.CaptureNetworkTileHashes().SequenceEqual(client.CaptureNetworkTileHashes()),"guest edit clears the same foreground");
            guestCover.WriteNetworkPixel(128,115,new Color32(1,2,3,255));guestCover.FlushNetworkPixels();client.InvalidateNetworkPixel(128,115);
            Check(!host.CaptureNetworkTileHashes().SequenceEqual(client.CaptureNetworkTileHashes()),"foreground-only divergence is detected");
            for(int i=0;i<host.NetworkTileCount;i++)Check(client.ApplyNetworkTile(i,host.CaptureNetworkTile(i)),"foreground repair tile "+i);
            Check(host.CaptureNetworkTileHashes().SequenceEqual(client.CaptureNetworkTileHashes()),"foreground repair restores exact visible cover");
            Check(ObjectHeadSpawnLayout.TryResolveNearbyMarker(host,new Vector2(4,3.45f),new Vector2(.36f,.58f),.28f,new List<Vector2>(),out var spawn)
                && !host.IsSolidWorld(spawn),"bounded spawn recovery finds clear supported terrain");
            Check(!ObjectHeadSpawnLayout.TryResolveNearbyMarker(host,new Vector2(40,40),new Vector2(.36f,.58f),.28f,new List<Vector2>(),out _),"spawn recovery rejects distant unsupported markers");
            var visualState=new ObjectHeadCombatState{selectedSkill=2,commonSlot=4};
            var visualCopy=JsonUtility.FromJson<ObjectHeadCombatState>(JsonUtility.ToJson(visualState));
            Check(visualCopy.selectedSkill==2 && visualCopy.commonSlot==4,"selected unique and common heads survive wire roundtrip");
            foreach(var map in ObjectHeadContent.Load().maps)
                Check(ObjectHeadNetworkConfig.LoadOrCreateRuntimeDefault().ResolveSceneName(map.id)==map.sceneName,"map scene routing "+map.id);
            bool unknownRejected=false;
            try { ObjectHeadNetworkConfig.LoadOrCreateRuntimeDefault().ResolveSceneName("missing-map"); }
            catch(InvalidOperationException){unknownRejected=true;}
            Check(unknownRejected,"unknown maps cannot silently load harbor");
            var envelope=new ObjectHeadTerrainSyncMessage{kind="manifest",revision=42,matchEpoch=123,hashes=new uint[]{0,uint.MaxValue,2166136261}};
            var copy=JsonUtility.FromJson<ObjectHeadTerrainSyncMessage>(JsonUtility.ToJson(envelope));
            Check(copy.hashes.SequenceEqual(envelope.hashes) && copy.revision==42 && copy.matchEpoch==123,"wire format unsigned hashes and epoch roundtrip");

            var actor=new GameObject("Replica",typeof(Rigidbody2D),typeof(BoxCollider2D));objects.Add(actor);
            var controller=actor.AddComponent<TurnCharacterController>();
            typeof(TurnCharacterController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
            controller.ConfigureAuthoritativeMotion();
            var body=actor.GetComponent<Rigidbody2D>();body.linearVelocity=new Vector2(4,-7);
            typeof(TurnCharacterController).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
            Check(body.bodyType==RigidbodyType2D.Kinematic && body.linearVelocity==new Vector2(4,-7),"replica does not apply a second gravity/movement step");
            controller.ApplyNetworkGrounded(true);Check(controller.IsGrounded,"authoritative grounded state");
            var inventoryRoot=new GameObject("AuditInventory");objects.Add(inventoryRoot);
            var inventory=inventoryRoot.AddComponent<PlayerInventoryManager>();inventory.ConfigurePlayers(2);
            actor.AddComponent<ObjectHeadTeamMember>().Configure(1,1,ObjectHeadCharacterKind.Bulb);
            var use=actor.AddComponent<CommonHeadUseController>();
            typeof(CommonHeadUseController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(use,null);
            inventory.GetInventory(1).TryAdd(CommonHeadType.Eraser,out int slot);
            use.ApplyNetworkSelection(slot);use.ApplyNetworkSelection(slot);
            Check(use.SelectedSlotIndex==slot && use.SelectedType==CommonHeadType.Eraser,"repeated remote common selection does not toggle off");
            use.ApplyNetworkSelection(CommonHeadInventory.SlotCount+2);
            Check(!use.HasSelectedCommonHead,"invalid remote inventory slot cannot equip a head");
            use.ApplyNetworkSelection(slot);use.ApplyNetworkSelection(-1);
            Check(!use.HasSelectedCommonHead,"remote deselection restores unique head");
            var smoother=actor.AddComponent<ObjectHeadNetworkSmoother>();
            typeof(ObjectHeadNetworkSmoother).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(smoother,null);
            smoother.Push(Vector2.zero,Vector2.zero);
            body.constraints=RigidbodyConstraints2D.FreezeAll;
            smoother.Push(new Vector2(.2f,.3f),Vector2.zero);
            Check(Vector2.Distance(body.position,new Vector2(.2f,.3f))<.001f,"captured replica accepts small authoritative corrections");
            Debug.Log($"[NETWORK_SYNC_AUDIT_PASS] {checks} checks. This is a local regression audit, not a two-PC EOS test.");
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);throw;
        }
        finally
        {
            for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)UnityEngine.Object.DestroyImmediate(objects[i]);
        }
    }
    private static void Check(bool pass,string label)
    {if(!pass)throw new Exception("Network audit failed: "+label);checks++;Debug.Log("[NetworkAudit] "+label);}
}
