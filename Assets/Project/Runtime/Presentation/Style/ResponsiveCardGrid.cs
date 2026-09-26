using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Style
{
 public sealed class ResponsiveCardGrid:GridLayoutGroup
 {
  public float cardHeight=304;
  public override void CalculateLayoutInputHorizontal()
  {
   constraint=Constraint.FixedColumnCount;constraintCount=rectTransform.rect.width>=1100?3:rectTransform.rect.width>=650?2:1;
   spacing=new Vector2(18,18);cellSize=new Vector2(Mathf.Max(1,(rectTransform.rect.width-padding.horizontal-spacing.x*(constraintCount-1))/constraintCount),cardHeight);
   base.CalculateLayoutInputHorizontal();
  }
 }
}
