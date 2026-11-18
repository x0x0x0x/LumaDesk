using System;
using System.Drawing;
using System.Linq;
namespace LumaDesk {
public static class WidgetPlacement {
 public static Point Restore(Placement saved,Size size,Rectangle[] areas,Rectangle primary,int index){
  bool assigned=saved.X!=-1||saved.Y!=-1;
  var point=assigned?new Point(saved.X,saved.Y):new Point(primary.Right-size.Width-28-index*380,primary.Top+40);
  // Keep exact coordinates, including negative monitor coordinates, if the
  // title can still be grabbed. Only recover windows on disconnected screens.
  var title=new Rectangle(point.X,point.Y,size.Width,48);
  if(areas.Any(area=>{var visible=Rectangle.Intersect(area,title);return visible.Width>=48&&visible.Height>=24;}))return point;
  return new Point(primary.Right-size.Width-24,primary.Top+24+index*42);
 }
}
}
