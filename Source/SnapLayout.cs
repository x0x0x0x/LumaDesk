using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
namespace LumaDesk {
public static class SnapLayout {
 public const int Gap=12,Threshold=16,Margin=12;
 static bool Near(int a,int b){return Math.Abs((long)a-b)<=Threshold;}
 static bool Overlap(int a,int b,int c,int d){return a<=d+Threshold&&b>=c-Threshold;}
 static void Pick(int value,int target,ref int best,ref int distance){int delta=(int)Math.Min(int.MaxValue,Math.Abs((long)value-target));if(delta<=Threshold&&delta<distance){best=target;distance=delta;}}
 public static Point Snap(Rectangle moving,IEnumerable<Rectangle> others,Rectangle work){int x=moving.X,y=moving.Y,dx=Threshold+1,dy=Threshold+1;
 // Screen candidates use the working area, leaving room for the taskbar.
 Pick(moving.X,work.Left+Margin,ref x,ref dx);Pick(moving.X,work.Right-Margin-moving.Width,ref x,ref dx);Pick(moving.Y,work.Top+Margin,ref y,ref dy);Pick(moving.Y,work.Bottom-Margin-moving.Height,ref y,ref dy);
 foreach(var other in others){bool horizontal=Near(moving.Left,other.Right+Gap)||Near(moving.Right,other.Left-Gap);bool vertical=Near(moving.Top,other.Bottom+Gap)||Near(moving.Bottom,other.Top-Gap);
 if(Overlap(moving.Top,moving.Bottom,other.Top,other.Bottom)){Pick(moving.Left,other.Right+Gap,ref x,ref dx);Pick(moving.Left,other.Left-Gap-moving.Width,ref x,ref dx);}
 if(Overlap(moving.Left,moving.Right,other.Left,other.Right)){Pick(moving.Top,other.Bottom+Gap,ref y,ref dy);Pick(moving.Top,other.Top-Gap-moving.Height,ref y,ref dy);}
 if(vertical){Pick(moving.Left,other.Left,ref x,ref dx);Pick(moving.Left,other.Right-moving.Width,ref x,ref dx);}
 if(horizontal){Pick(moving.Top,other.Top,ref y,ref dy);Pick(moving.Top,other.Bottom-moving.Height,ref y,ref dy);}}
 return new Point(x,y);}
 public static Point[] Arrange(Size[] sizes,Rectangle work,Point anchor,bool vertical){var offsets=new List<Point>();int x=0,y=0,lineSize=0,maxRight=0,maxBottom=0;int width=Math.Max(1,work.Width-Margin*2),height=Math.Max(1,work.Height-Margin*2);
 foreach(var size in sizes){if(vertical){if(y>0&&y+size.Height>height){x+=lineSize+Gap;y=0;lineSize=0;}offsets.Add(new Point(x,y));lineSize=Math.Max(lineSize,size.Width);y+=size.Height+Gap;}else{if(x>0&&x+size.Width>width){y+=lineSize+Gap;x=0;lineSize=0;}offsets.Add(new Point(x,y));lineSize=Math.Max(lineSize,size.Height);x+=size.Width+Gap;}var p=offsets[offsets.Count-1];maxRight=Math.Max(maxRight,p.X+size.Width);maxBottom=Math.Max(maxBottom,p.Y+size.Height);}
 int left=Math.Max(work.Left+Margin,Math.Min(anchor.X,work.Right-Margin-maxRight)),top=Math.Max(work.Top+Margin,Math.Min(anchor.Y,work.Bottom-Margin-maxBottom));return offsets.Select(p=>new Point(left+p.X,top+p.Y)).ToArray();}
}
}
