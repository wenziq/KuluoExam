using Sokoban.Core.Data;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Board
{
    public sealed class BoardThumbnailView
    {
        public RectTransform Rect { get; }
        private LevelData previous;private readonly ThumbnailGraphic graphic;
        public BoardThumbnailView(Transform parent)
        { Rect=Presentation.Style.UiFactory.Rect("Thumbnail",parent);graphic=Rect.gameObject.AddComponent<ThumbnailGraphic>();graphic.raycastTarget=false; }
        public void Show(LevelData level)
        {
            if(Same(previous,level))return;
            previous=level.DeepCopy();graphic.SetLevel(previous);
        }
        private static bool Same(LevelData a,LevelData b)
        {
            if(a==null||a.width!=b.width||a.height!=b.height||a.features.Count!=b.features.Count||a.entities.Count!=b.entities.Count)return false;
            for(int i=0;i<a.terrain.Length;i++)if(a.terrain[i]!=b.terrain[i])return false;
            for(int i=0;i<a.features.Count;i++)if(a.features[i].id!=b.features[i].id||a.features[i].x!=b.features[i].x||a.features[i].y!=b.features[i].y)return false;
            for(int i=0;i<a.entities.Count;i++)if(a.entities[i].id!=b.entities[i].id||a.entities[i].type!=b.entities[i].type||a.entities[i].x!=b.entities[i].x||a.entities[i].y!=b.entities[i].y)return false;
            return true;
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ThumbnailGraphic:UnityEngine.UI.MaskableGraphic
    {
        private static readonly Color WallColor=Style.UiTheme.Hex("46533a"),FloorColor=Style.UiTheme.Hex("c9d1bc"),GoalColor=Style.UiTheme.Hex("899b68"),PlayerColor=Style.UiTheme.Hex("29351e"),BoxColor=Style.UiTheme.Hex("cea570");
        private LevelData level;
        public void SetLevel(LevelData snapshot){level=snapshot;SetVerticesDirty();}
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();if(level==null)return;var rect=rectTransform.rect;float cell=Mathf.Min(rect.width/level.width,rect.height/level.height);
            float left=rect.center.x-level.width*cell/2,bottom=rect.center.y-level.height*cell/2;
            void Tile(int x,int y,Color color,float margin=0)
            {
                float x0=left+(x+margin)*cell,y0=bottom+(y+margin)*cell,x1=left+(x+1-margin)*cell,y1=bottom+(y+1-margin)*cell;int n=vh.currentVertCount;
                vh.AddVert(new Vector3(x0,y0),color,Vector2.zero);vh.AddVert(new Vector3(x1,y0),color,Vector2.zero);vh.AddVert(new Vector3(x1,y1),color,Vector2.zero);vh.AddVert(new Vector3(x0,y1),color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            for(int y=0;y<level.height;y++)for(int x=0;x<level.width;x++)Tile(x,y,level.terrain[y*level.width+x]==1?WallColor:FloorColor);
            foreach(var f in level.features)Tile(f.x,f.y,GoalColor,.3f);
            foreach(var e in level.entities)Tile(e.x,e.y,e.type==EntityType.Player?PlayerColor:BoxColor,.12f);
        }
    }
}
