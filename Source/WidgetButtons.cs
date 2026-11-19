using System;
using System.Drawing;
using System.Drawing.Drawing2D;
namespace LumaDesk {
public static class WidgetButtons {
 public static readonly Rectangle WeatherRefreshBounds=new Rectangle(307,14,36,36);
 static readonly Rectangle EmptyAgendaBounds=Measure(CalendarText(false)),AgendaBounds=Measure(CalendarText(true));
 public static string CalendarText(bool hasEvents){return hasEvents?"Все напоминания  ›":"+  Добавить напоминание";}
 public static Rectangle CalendarBounds(bool hasEvents){return hasEvents?AgendaBounds:EmptyAgendaBounds;}
 static Rectangle Measure(string text){using(var path=new GraphicsPath())using(var family=new FontFamily(Scene.FontName))using(var format=new StringFormat(StringFormat.GenericTypographic)){path.AddString(text,family,(int)FontStyle.Bold,12,new PointF(21,491),format);var ink=path.GetBounds();const int padding=7;return Rectangle.FromLTRB((int)Math.Floor(ink.Left)-padding,(int)Math.Floor(ink.Top)-padding,(int)Math.Ceiling(ink.Right)+padding,(int)Math.Ceiling(ink.Bottom)+padding);}}
 public static void CalendarFooter(Graphics g,bool hasEvents){DesktopArt.T(g,CalendarText(hasEvents),12,DesktopArt.Accent,21,491,true);}
 public static void Refresh(Graphics g){float cx=WeatherRefreshBounds.X+WeatherRefreshBounds.Width/2f,cy=WeatherRefreshBounds.Y+WeatherRefreshBounds.Height/2f,r=7.5f;using(var p=new Pen(DesktopArt.Accent,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){g.DrawArc(p,cx-r,cy-r,2*r,2*r,40,285);double a=-35*Math.PI/180;var tip=new PointF(cx+r*(float)Math.Cos(a),cy+r*(float)Math.Sin(a));float tx=-(float)Math.Sin(a),ty=(float)Math.Cos(a);g.DrawLines(p,new[]{new PointF(tip.X-tx*4-ty*2.7f,tip.Y-ty*4+tx*2.7f),tip,new PointF(tip.X-tx*4+ty*2.7f,tip.Y-ty*4-tx*2.7f)});}}
}
}
