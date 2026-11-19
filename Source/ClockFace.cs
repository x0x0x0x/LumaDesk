using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace LumaDesk {
public static class ClockFace {
 public static readonly PointF DigitalOrigin=new PointF(151,58);
 public const float DigitalSize=44;
 // Keep the original DrawString position. Only the hit/hover rectangle follows
 // the visible glyphs, with a small, equal margin around them.
 public static Rectangle BoundsFor(string text){
  using(var path=new GraphicsPath())
  using(var family=new FontFamily(Scene.FontName))
  using(var format=new StringFormat(StringFormat.GenericTypographic)){
   path.AddString(text,family,(int)FontStyle.Bold,DigitalSize,DigitalOrigin,format);
   var ink=path.GetBounds();const int padding=8;
   return Rectangle.FromLTRB((int)Math.Floor(ink.Left)-padding,(int)Math.Floor(ink.Top)-padding,(int)Math.Ceiling(ink.Right)+padding,(int)Math.Ceiling(ink.Bottom)+padding);
  }
 }
 public static Rectangle BoundsFor(DateTime now){return BoundsFor(now.ToString("HH:mm"));}
 public static void DrawDigital(Graphics g,string text){DesktopArt.T(g,text,DigitalSize,DesktopArt.Text,DigitalOrigin.X,DigitalOrigin.Y,true);}
}
}
