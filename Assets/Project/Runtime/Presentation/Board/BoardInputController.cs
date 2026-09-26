using Sokoban.Core.Data;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Sokoban.Runtime.Presentation.Board
{
    public sealed class BoardInputController:MonoBehaviour
    {
        private WorkshopView view;private PaintStroke stroke;private bool captured;private Vector2 initialPan;private float initialZoom;
        public BoardOverlayView Overlay { get; private set; }
        public void Initialize(WorkshopView view)
        {
            this.view=view;Overlay=new BoardOverlayView(view.Board);
            view.Board.CanInteract=CanInteract;
            view.Board.CellPressed+=Press;view.Board.CellDragged+=Drag;view.Board.PointerReleased+=Release;view.Board.PanStarted+=Capture;
        }
        private bool CanInteract()
        {
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&selected.GetComponentInParent<TMPro.TMP_InputField>()!=null&&Keyboard.current!=null&&Keyboard.current.spaceKey.isPressed)return false;
            return view.Owner.App.Input.Context<=InputContext.Page||captured&&view.Owner.App.Input.Context==InputContext.MenuOrGesture&&!view.Owner.App.Input.HasCaptureOtherThan(this);
        }
        private void Capture()
        { initialPan=view.Board.Pan;initialZoom=view.Board.Zoom;captured=true;view.Owner.App.Input.Capture(this,InputContext.MenuOrGesture);view.Owner.App.Input.SetEscapeHandler(this,Cancel); }
        private void Press(Vector2Int cell,PointerEventData e)
        {
            if(e.button!=PointerEventData.InputButton.Left||view.Owner.Document.SelectedLevelId==null)return;
            view.CommitFields();
            if(view.State.IsSelectionTool){view.SelectCell(cell.x,cell.y);return;}
            stroke=view.Owner.Document.BeginStroke(view.State.Tool);Capture();stroke.AddPoint(cell.x,cell.y);view.RefreshBoard();
        }
        private void Drag(Vector2Int cell,PointerEventData e)
        { if(stroke==null)return;stroke.AddPoint(cell.x,cell.y);view.RefreshBoard(); }
        private void Release(PointerEventData e){if(e.button==PointerEventData.InputButton.Left||e.button==PointerEventData.InputButton.Middle)Commit();}
        public void Commit()
        { var active=stroke;stroke=null;active?.Commit();view.Board.CancelPan();ReleaseCapture();view.RememberViewport();if(active!=null)view.Owner.Changed(); }
        private void ReleaseCapture()
        { if(!captured)return;captured=false;view.Owner.App.Input.RemoveEscapeHandler(this);view.Owner.App.Input.Release(this); }
        public void Cancel()
        { var active=stroke;stroke=null;active?.Cancel();if(view.Board.IsPanning)view.Board.SetView(initialZoom,initialPan);view.Board.CancelPan();ReleaseCapture();view.RefreshBoard(); }
        private void OnApplicationFocus(bool focus){if(!focus)Commit();}
        private void OnDisable(){if(view!=null){Commit();Overlay.ClearPreview();}}
        private void Update()
        {
            if(view==null||view.Owner.Document?.SelectedLevelId==null||Mouse.current==null)return;
            if(!CanInteract()){Overlay.ClearPreview();return;}
            if(view.Board.TryCell(Mouse.current.position.ReadValue(),null,out var cell))
            {
                var preview=view.Owner.Document.PreviewPaint(view.State.Tool,cell.x,cell.y);
                Overlay.Preview(cell.x,cell.y,view.State.IsSelectionTool||preview.CanApply);
                view.SetHover("("+cell.x+", "+cell.y+")  "+(view.State.IsSelectionTool?"选择对象":preview.Reason));
            }
            else {Overlay.ClearPreview();view.SetHover("");}
            if(stroke!=null&&!Mouse.current.leftButton.isPressed)Commit();
        }
    }
}
