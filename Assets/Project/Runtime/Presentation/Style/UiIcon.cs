using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Style
{
    public enum IconKind
    {
        Play, PlayFromStart, Save, Undo, Redo, Folder, Analysis, Settings, Eraser, Select
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiIcon : MaskableGraphic
    {
        public IconKind kind;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            void Line(float x1, float y1, float x2, float y2)
            {
                var a = new Vector2(r.x + x1 * r.width, r.y + y1 * r.height);
                var b = new Vector2(r.x + x2 * r.width, r.y + y2 * r.height);
                var n = new Vector2(-(b - a).y, (b - a).x).normalized * .7f;
                int s = vh.currentVertCount;
                vh.AddVert(a + n, color, Vector2.zero);
                vh.AddVert(b + n, color, Vector2.zero);
                vh.AddVert(b - n, color, Vector2.zero);
                vh.AddVert(a - n, color, Vector2.zero);
                vh.AddTriangle(s, s + 1, s + 2);
                vh.AddTriangle(s, s + 2, s + 3);
            }
            switch (kind)
            {
                case IconKind.Eraser:
                    Line(.15f,.4f,.55f,.85f); Line(.55f,.85f,.9f,.55f);
                    Line(.9f,.55f,.5f,.1f); Line(.5f,.1f,.35f,.1f);
                    Line(.35f,.1f,.15f,.4f); Line(.32f,.59f,.68f,.29f);
                    Line(.5f,.1f,.92f,.1f);
                    break;
                case IconKind.Select:
                    Line(.2f,.9f,.2f,.15f); Line(.2f,.9f,.85f,.4f);
                    Line(.85f,.4f,.55f,.4f); Line(.55f,.4f,.7f,.1f);
                    Line(.7f,.1f,.55f,.05f); Line(.55f,.05f,.4f,.35f);
                    Line(.4f,.35f,.2f,.15f);
                    break;
                case IconKind.Play:
                case IconKind.PlayFromStart:
                    Line(.3f, .15f, .3f, .85f);
                    Line(.3f, .85f, .85f, .5f);
                    Line(.85f, .5f, .3f, .15f);
                    if (kind == IconKind.PlayFromStart)
                        Line(.12f, .15f, .12f, .85f);
                    break;
                case IconKind.Folder:
                    Line(.1f, .2f, .9f, .2f);
                    Line(.9f, .2f, .9f, .73f);
                    Line(.9f, .73f, .48f, .73f);
                    Line(.48f, .73f, .38f, .85f);
                    Line(.38f, .85f, .1f, .85f);
                    Line(.1f, .85f, .1f, .2f);
                    break;
                case IconKind.Analysis:
                    Line(.1f, .2f, .9f, .2f);
                    Line(.18f, .35f, .4f, .6f);
                    Line(.4f, .6f, .65f, .5f);
                    Line(.65f, .5f, .9f, .9f);
                    break;
                case IconKind.Undo:
                case IconKind.Redo:
                    float sign = kind == IconKind.Undo ? 1 : -1;
                    float X(float x) => .5f + (x - .5f) * sign;
                    Line(X(.15f), .65f, X(.8f), .65f);
                    Line(X(.8f), .65f, X(.8f), .3f);
                    Line(X(.8f), .3f, X(.55f), .2f);
                    Line(X(.15f), .65f, X(.35f), .85f);
                    Line(X(.15f), .65f, X(.35f), .45f);
                    break;
                case IconKind.Settings:
                    Line(.1f, .25f, .9f, .25f);
                    Line(.1f, .5f, .9f, .5f);
                    Line(.1f, .75f, .9f, .75f);
                    Line(.35f, .15f, .35f, .35f);
                    Line(.65f, .4f, .65f, .6f);
                    Line(.4f, .65f, .4f, .85f);
                    break;
                default:
                    Line(.15f, .15f, .85f, .15f);
                    Line(.85f, .15f, .85f, .85f);
                    Line(.85f, .85f, .15f, .85f);
                    Line(.15f, .85f, .15f, .15f);
                    Line(.3f, .15f, .3f, .45f);
                    Line(.3f, .45f, .7f, .45f);
                    Line(.7f, .45f, .7f, .15f);
                    Line(.3f, .85f, .3f, .65f);
                    Line(.3f, .65f, .7f, .65f);
                    break;
            }
        }
    }
}
