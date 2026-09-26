using UnityEngine;
using UnityEngine.UI;
using Sokoban.Runtime.Presentation.Style;
namespace Sokoban.Runtime.Presentation.Board
{
    public sealed class BoardOverlayView
    {
        readonly BoardView board;Image preview,selection;
        public BoardOverlayView(BoardView board){this.board=board;}
        public void Preview(int x,int y,bool valid)
        { Place(ref preview,"BrushPreview",x,y,valid?new Color(.76f,.91f,.59f,.35f):new Color(.93f,.45f,.4f,.4f)); }
        public void Select(int x,int y){Place(ref selection,"SelectedCell",x,y,new Color(.95f,.8f,.35f,.3f));}
        public void ClearPreview(){if(preview!=null)preview.gameObject.SetActive(false);}
        public void ClearSelection(){if(selection!=null)selection.gameObject.SetActive(false);}
        void Place(ref Image image,string name,int x,int y,Color color)
        {
            if(x<0||y<0||x>=board.Width||y>=board.Height)return;
            if(image==null)image=UiFactory.Panel(name,board.overlayLayer,color);
            image.gameObject.SetActive(true);image.color=color;
            var r=image.rectTransform;r.anchorMin=new Vector2((float)x/board.Width,(float)y/board.Height);r.anchorMax=new Vector2((float)(x+1)/board.Width,(float)(y+1)/board.Height);r.offsetMin=new Vector2(2,2);r.offsetMax=new Vector2(-2,-2);
        }
    }
}
