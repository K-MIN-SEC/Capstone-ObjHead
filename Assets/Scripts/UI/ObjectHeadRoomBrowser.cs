using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Layout and row appearance are authored in the title prefab, never rebuilt at runtime.
public sealed class ObjectHeadRoomBrowser : MonoBehaviour
{
    public ObjectHeadTitleScreen title;
    public Button refreshButton;
    public Text statusLabel;
    public RectTransform content;
    public Button rowTemplate;
    private readonly List<Button> rows = new List<Button>();
    private int generation;
    private bool busy;
    public int VisibleRoomCount => rows.Count;
    private void Awake() => refreshButton.onClick.AddListener(Refresh);
    private void OnEnable() { title.LanguageChanged+=Refresh;Refresh(); }
    private void OnDisable() { title.LanguageChanged-=Refresh;generation++; busy=false; }
    private void ClearRows()
    {
        foreach (var row in rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        rows.Clear();
    }
    private void Status(string key)
    {
        statusLabel.gameObject.SetActive(true);
        statusLabel.text=title.Translate(key);
    }
    public async void Refresh()
    {
        if(busy || !isActiveAndEnabled) return;
        int request=++generation;busy=true;refreshButton.interactable=false;ClearRows();
        Status("rooms_loading");
        try
        {
            var network=ObjectHeadNetworkManager.Instance;
            if(!network.UseDedicatedAuthority && !network.UseEpicOnlineServices){Status("rooms_legacy");return;}
            await title.EnsureConnectedAsync();
            var result=await network.FindPublicRoomsAsync();
            if(this==null || request!=generation || !isActiveAndEnabled)return;
            foreach(var room in (result.rooms??Array.Empty<ObjectHeadNetworkManager.AuthorityRoom>()).OrderBy(r=>r.roomCode))
            {
                if(string.IsNullOrWhiteSpace(room.roomCode))continue;
                var row=Instantiate(rowTemplate,content);row.name="Room_"+room.roomCode;
                var catalog=ObjectHeadContent.Load();
                string mode=catalog.Mode((ObjectHeadMatchMode)room.mode)?.nameKey;
                string map=catalog.maps.FirstOrDefault(m=>m.id==room.mapId)?.nameKey;
                row.GetComponentInChildren<Text>(true).text=string.Format(title.Translate("rooms_row"),room.roomCode,
                    room.players,room.capacity,title.Translate(mode??"mode_select"),title.Translate(map??"map_random"));
                row.onClick.AddListener(()=>Join(room.roomCode));row.gameObject.SetActive(true);rows.Add(row);
            }
            if(rows.Count==0)Status("rooms_empty");
            else statusLabel.gameObject.SetActive(false);
        }
        catch(Exception e)
        {
            if(this!=null && request==generation)
            {
                string key=ObjectHeadRoomAccessPanel.ErrorKey(e.Message);
                Status(title.Translate(key)==key?"rooms_failed":key);
                Debug.LogWarning("[RoomBrowser] "+key);
            }
        }
        finally
        {
            if(this!=null && request==generation){busy=false;refreshButton.interactable=true;}
        }
    }
    private async void Join(string code)
    {
        if(busy)return;
        int request=++generation;busy=true;refreshButton.interactable=false;
        foreach(var row in rows)row.interactable=false;
        Status("rooms_joining");
        try { await title.JoinDiscoveredRoomAsync(code); }
        catch(Exception e)
        {
            if(this!=null && request==generation){Status("rooms_join_failed");Debug.LogWarning("[RoomBrowser] "+e.Message);}
        }
        finally
        {
            if(this!=null && request==generation)
            {busy=false;refreshButton.interactable=true;foreach(var row in rows)row.interactable=true;}
        }
    }
}
