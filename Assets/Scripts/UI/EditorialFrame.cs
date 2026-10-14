using UnityEngine;
using UnityEngine.UI;

/// <summary>Scalable cut-paper mesh. Dynamic text and pointer hit areas remain separate.</summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EditorialFrame : MaskableGraphic
{
    public Color ink = new(.055f,.065f,.075f), rim = new(.29f,.36f,.41f), fill = new(.94f,.90f,.81f);
    public Color accent = new(.66f,.27f,.22f);
    public float border = 8, corner = 22;
    public bool showShadow = true, showAccent = true, compact;
    public Vector2 shadowOffset = new(8,-9);
    public void Refresh() => SetVerticesDirty();
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var bounds = rectTransform.rect;
        if (bounds.width <= 0 || bounds.height <= 0) return;
        float edge = Mathf.Min(border, Mathf.Min(bounds.width,bounds.height)*.1f);
        if (showShadow) Polygon(vh, bounds, 0, shadowOffset, ink);
        Polygon(vh,bounds,0,Vector2.zero,ink);
        Polygon(vh,bounds,edge*.5f,Vector2.zero,rim);
        Polygon(vh,bounds,edge,Vector2.zero,ink);
        Polygon(vh,bounds,edge+2,Vector2.zero,fill);
        if (showAccent && bounds.width > 90 && bounds.height > 45)
        {
            int start=vh.currentVertCount;
            float x=bounds.xMax-edge-1,y=bounds.yMax-edge-1;
            Add(vh,new Vector2(x-36,y),accent);Add(vh,new Vector2(x,y),accent);Add(vh,new Vector2(x,y-30),accent);
            vh.AddTriangle(start,start+1,start+2);
        }
    }
    void Polygon(VertexHelper vh, Rect r, float inset, Vector2 offset, Color tint)
    {
        float x1=r.xMin+inset,x2=r.xMax-inset,y1=r.yMin+inset,y2=r.yMax-inset;
        float cut=Mathf.Min(corner,Mathf.Max(0,Mathf.Min(x2-x1,y2-y1)*.22f));
        int start=vh.currentVertCount;
        Add(vh,new Vector2((x1+x2)*.5f,(y1+y2)*.5f)+offset,tint);
        var points=new[]{new Vector2(x1+cut,y2),new Vector2(x2-cut*.6f,y2),new Vector2(x2,y2-cut*.6f),
            new Vector2(x2,y1+cut),new Vector2(x2-cut,y1),new Vector2(x1+cut*.35f,y1),
            new Vector2(x1,y1+cut*.35f),new Vector2(x1,y2-cut)};
        foreach(var p in points) Add(vh,p+offset,tint);
        for(int i=0;i<points.Length;i++) vh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
    }
    void Add(VertexHelper vh,Vector2 point,Color tint)
    { var v=UIVertex.simpleVert;v.position=point;v.color=tint*color;vh.AddVert(v); }
}
