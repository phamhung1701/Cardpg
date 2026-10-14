using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Route identity is exposed only for revealed nodes; shape and fill encode known route state.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MapNodePresentation : MaskableGraphic
{
    public MapNodeType kind;
    public bool unknown,completed,available;
    bool _focused;
    public static void Apply(GameObject obj,PathNode node)
    {
        var surface=obj.GetComponent<EditorialSurface>();if(!surface)surface=obj.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Open;
        var rect=DealbreakUIStyle.Child(obj.transform,"Route Glyph",typeof(MapNodePresentation));
        DealbreakUIStyle.Place(rect,.02f,.02f,.98f,.98f);rect.SetAsFirstSibling();
        if(!rect.GetComponent<CanvasRenderer>())rect.gameObject.AddComponent<CanvasRenderer>();
        var glyph=rect.GetComponent<MapNodePresentation>();glyph.raycastTarget=false;glyph.kind=node.kind;glyph.unknown=obj.GetComponentInChildren<TMPro.TMP_Text>()?.text.Trim()=="?";
        glyph.completed=node.completed;glyph.available=node.accessible&&!node.completed;glyph.SetVerticesDirty();
        var style=obj.GetComponent<EditorialButton>();if(style)style.enabled=false;
        var image=obj.GetComponent<Image>();if(image)image.color=Color.clear;
        var text=obj.GetComponentInChildren<TMPro.TMP_Text>();if(text){DealbreakUIStyle.Place(text.transform,-.4f,-.65f,1.4f,-.05f);text.raycastTarget=true;text.fontSize=19;text.alignment=TMPro.TextAlignmentOptions.Center;text.overflowMode=TMPro.TextOverflowModes.Overflow;}
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(r.width,r.height)*.5f;
        bool focus=EventSystem.current&&EventSystem.current.currentSelectedGameObject==transform.parent.gameObject;
        Color edge=focus?DealbreakUIStyle.Theme.ochre:completed?DealbreakUIStyle.Theme.paper:available?DealbreakUIStyle.Theme.ochre:DealbreakUIStyle.Theme.slate;
        Draw(vh,r.center,radius,edge);Draw(vh,r.center,radius-3,DealbreakUIStyle.Theme.ink);
        Draw(vh,r.center,radius-7,completed?DealbreakUIStyle.Theme.slate:DealbreakUIStyle.Theme.charcoal);
    }
    void Draw(VertexHelper vh,Vector2 center,float radius,Color tint)
    {
        int sides=unknown?24:kind is MapNodeType.Event or MapNodeType.Risk?24:kind==MapNodeType.Headliner?6:4;
        float angle=kind==MapNodeType.Shop&&!unknown?45:0;
        int start=vh.currentVertCount;var v=UIVertex.simpleVert;v.color=tint;v.position=center;vh.AddVert(v);
        for(int i=0;i<sides;i++){float a=(i*360f/sides+angle)*Mathf.Deg2Rad;v.position=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;vh.AddVert(v);}
        for(int i=0;i<sides;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%sides);
    }
    void Update()
    {
        bool focused=EventSystem.current&&EventSystem.current.currentSelectedGameObject==transform.parent.gameObject;
        if(focused==_focused)return;
        _focused=focused;SetVerticesDirty();
    }
}
