using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// An explicit, additive migration. Repeated calls preserve all artist-authored positions.
public static class ObjectHeadRoomBrowserAuthoring
{
    private const string Path="Assets/Prefabs/UI/ObjectHeadTitle.prefab";
    [MenuItem("Object Head/UI/Add Room Browser")]
    public static void Apply()
    {
        ObjectHeadSpreadsheetImporter.ImportAndGetLocalization();
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var title=root.GetComponentInChildren<ObjectHeadTitleScreen>(true);
            var panel=title.frontEnd.find;
            if(panel.GetComponent<ObjectHeadRoomBrowser>()!=null)return;
            var browser=panel.AddComponent<ObjectHeadRoomBrowser>();browser.title=title;
            var parent=panel.transform;
            Place(title.roomCodeInput.transform, -70,-168,340,48);
            Place(title.joinRoomButton.transform,205,-168,120,48);
            var hint=parent.Find("join_code_hint");if(hint!=null)Place(hint,0,-233,530,56);
            browser.refreshButton=Object.Instantiate(title.joinRoomButton,parent);
            browser.refreshButton.name="RefreshRooms";Place(browser.refreshButton.transform,185,218,160,42);
            var label=browser.refreshButton.GetComponentInChildren<ObjectHeadLocalizedLabel>();label.LocalizationKey="rooms_refresh";
            var heading=parent.Find("find_room");if(heading!=null)Place(heading,-110,218,310,46);
            browser.statusLabel=Text(parent,"RoomStatus",0,159,530,46,18);
            browser.statusLabel.text="";
            var viewport=Rect(parent,"RoomScroll",0,12,530,232);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.16f);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.viewport=viewport;
            scroll.scrollSensitivity=70;scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=Rect(viewport,"Rooms",0,0,530,232);
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(0,0);
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.padding=new RectOffset(6,6,6,6);
            layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.content=content;browser.content=content;
            var row=Object.Instantiate(title.joinRoomButton,content);row.name="RoomRowTemplate";
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=66;
            var rowText=row.GetComponentInChildren<Text>();
            Object.DestroyImmediate(rowText.GetComponent<ObjectHeadLocalizedLabel>());
            rowText.fontSize=19;rowText.alignment=TextAnchor.MiddleLeft;rowText.text="";
            var textRect=rowText.rectTransform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;
            textRect.offsetMin=new Vector2(18,6);textRect.offsetMax=new Vector2(-18,-6);
            row.gameObject.SetActive(false);browser.rowTemplate=row;
            foreach(var localized in panel.GetComponentsInChildren<ObjectHeadLocalizedLabel>(true))localized.Preview();
            PrefabUtility.SaveAsPrefabAsset(root,Path);
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    private static void Place(Transform t,float x,float y,float w,float h)
    {var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
    private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);Place(g.transform,x,y,w,h);return (RectTransform)g.transform;}
    private static Text Text(Transform parent,string name,float x,float y,float w,float h,int size)
    {var t=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Text>();t.font=ObjectHeadContent.Load().uiFont;t.fontSize=size;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;}
}
