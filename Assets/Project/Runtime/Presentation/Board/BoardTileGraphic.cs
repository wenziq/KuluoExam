using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Board
{
    public enum BoardSymbol
    {
        Floor, Wall, Goal, Player, Box, BoxOnGoal, Selection, DeadCell
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BoardTileGraphic : MaskableGraphic
    {
        public BoardSymbol Symbol;
        public void SetSymbol(BoardSymbol symbol)
        {
            if (Symbol == symbol)
                return;
            Symbol = symbol;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            void Quad(float x, float y, float w, float h, Color c)
            {
                int start = vh.currentVertCount;
                vh.AddVert(new Vector3(r.x + x * r.width, r.y + y * r.height), c, Vector2.zero);
                vh.AddVert(new Vector3(r.x + (x + w) * r.width, r.y + y * r.height), c, Vector2.zero);
                vh.AddVert(new Vector3(r.x + (x + w) * r.width, r.y + (y + h) * r.height), c, Vector2.zero);
                vh.AddVert(new Vector3(r.x + x * r.width, r.y + (y + h) * r.height), c, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
            void Line(float x1,float y1,float x2,float y2,float thickness,Color c)
            {
                var a=new Vector2(r.x+x1*r.width,r.y+y1*r.height);
                var b=new Vector2(r.x+x2*r.width,r.y+y2*r.height);
                var n=new Vector2(-(b-a).y,(b-a).x).normalized*(Mathf.Min(r.width,r.height)*thickness*.5f);
                int start=vh.currentVertCount;
                vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);
                vh.AddVert(b-n,c,Vector2.zero);vh.AddVert(a-n,c,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
            void Circle(float cx, float cy, float radius, Color c)
            {
                int start = vh.currentVertCount;
                vh.AddVert(new Vector3(r.x + cx * r.width, r.y + cy * r.height), c, Vector2.zero);
                for (int i = 0; i <= 32; i++)
                {
                    float a = i * Mathf.PI / 16;
                    vh.AddVert(new Vector3(r.x + (cx + Mathf.Cos(a) * radius) * r.width, r.y + (cy + Mathf.Sin(a) * radius) * r.height), c, Vector2.zero);
                    if (i > 0)
                        vh.AddTriangle(start, start + i, start + i + 1);
                }
            }
            Color H(string s) => Style.UiTheme.Hex(s);
            switch (Symbol)
            {
                case BoardSymbol.Floor:
                    Quad(.012f, .012f, .976f, .976f, H("c9d1bc"));
                    Quad(.012f, .012f, .976f, .025f, H("bac5ac"));
                    break;
                case BoardSymbol.Wall:
                    Quad(.018f, .018f, .964f, .964f, H("313d27"));
                    Quad(.045f, .08f, .91f, .86f, H("64744e"));
                    Quad(.10f, .1f, .80f, .76f, H("46533a"));
                    Quad(.13f, .13f, .74f, .02f, H("526044"));
                    break;
                case BoardSymbol.Goal:
                    Circle(.5f, .5f, .18f, H("899b68"));
                    Circle(.5f, .5f, .1f, H("c9d1bc"));
                    break;
                case BoardSymbol.Player:
                    Circle(.52f, .46f, .32f, new Color(0, 0, 0, .16f));
                    Circle(.5f, .53f, .31f, H("3d4d2b"));
                    Circle(.5f, .55f, .255f, H("637e42"));
                    Circle(.5f, .51f, .24f, H("29351e"));
                    Quad(.395f, .5f, .045f, .105f, H("dae6c7"));
                    Quad(.565f, .5f, .045f, .105f, H("dae6c7"));
                    Circle(.5f, .85f, .035f, H("a8c87a"));
                    break;
                case BoardSymbol.Box:
                case BoardSymbol.BoxOnGoal:
                    Quad(.10f, .09f, .82f, .79f, H("6c5134"));
                    Quad(.10f, .16f, .80f, .76f, H("cea570"));
                    Quad(.17f, .22f, .66f, .62f, H("a88151"));
                    Quad(.20f, .25f, .60f, .56f, H("bb945e"));
                    Line(.22f,.27f,.78f,.79f,.055f,H("9d7748"));
                    Line(.22f,.79f,.78f,.27f,.055f,H("9d7748"));
                    if (Symbol == BoardSymbol.BoxOnGoal)
                    {
                        Circle(.77f, .79f, .14f, H("c3e997"));
                        Line(.70f,.79f,.75f,.74f,.032f,H("334525"));
                        Line(.75f,.74f,.84f,.84f,.032f,H("334525"));
                    }
                    break;
                case BoardSymbol.Selection:
                    Quad(0, 0, 1, .04f, color);
                    Quad(0, .96f, 1, .04f, color);
                    Quad(0, 0, .04f, 1, color);
                    Quad(.96f, 0, .04f, 1, color);
                    break;
                case BoardSymbol.DeadCell:
                    Quad(.055f, .81f, .20f, .05f, color);
                    Quad(.13f, .735f, .05f, .20f, color);
                    break;
            }
        }
    }
}
