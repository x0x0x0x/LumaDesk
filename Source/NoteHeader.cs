using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace LumaDesk {
public static class NoteHeader {
 public static readonly PointF PlusOrigin=new PointF(317,17);
 public const float PlusSize=23;
 public static readonly Rectangle PlusBounds=Measure();
 static Rectangle Measure(){using(var path=new GraphicsPath())using(var family=new FontFamily(Scene.FontName))using(var format=new StringFormat(StringFormat.GenericTypographic)){path.AddString("+",family,(int)FontStyle.Bold,PlusSize,PlusOrigin,format);var ink=path.GetBounds();const int padding=8;return Rectangle.FromLTRB((int)Math.Floor(ink.Left)-padding,(int)Math.Floor(ink.Top)-padding,(int)Math.Ceiling(ink.Right)+padding,(int)Math.Ceiling(ink.Bottom)+padding);}}
 public static void DrawPlus(Graphics g){DesktopArt.T(g,"+",PlusSize,DesktopArt.Accent,PlusOrigin.X,PlusOrigin.Y,true);}
}
}
