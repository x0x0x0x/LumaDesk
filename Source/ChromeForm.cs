using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace LumaDesk {
public class ChromeForm:Form {
 public const int CaptionHeight=42;bool closeHover;
 [DllImport("user32.dll")]static extern bool ReleaseCapture();
 [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wp,IntPtr lp);
 public ChromeForm(){FormBorderStyle=FormBorderStyle.None;BackColor=EditorArt.Bg;ForeColor=EditorArt.Text;Font=Theme.F(10);DoubleBuffered=true;StartPosition=FormStartPosition.CenterScreen;ShowInTaskbar=false;KeyPreview=true;}
 public ChromeForm(string title,int width,int height):this(){Text=title;SetContentSize(width,height);}
 public void SetContentSize(int width,int height){ClientSize=new Size(width,height+CaptionHeight);}
 protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.ClassStyle|=0x00020000;cp.Style|=0x02000000;return cp;}}
 protected override void OnControlAdded(ControlEventArgs e){e.Control.Top+=CaptionHeight;base.OnControlAdded(e);}
 public static void PaintChrome(Graphics g,int width,int height,bool hover=false){g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;using(var b=new SolidBrush(AppTheme.Chrome))g.FillRectangle(b,0,0,width,CaptionHeight);EditorArt.T(g,"LumaDesk",11,EditorArt.Muted,20,15,true);if(hover)EditorArt.Box(g,new Rectangle(width-43,7,30,28),AppTheme.Pick(Color.FromArgb(89,49,58),Color.FromArgb(255,225,229)),6);using(var p=new Pen(hover?AppTheme.Error:EditorArt.Muted,1.5f)){g.DrawLine(p,width-32,16,width-23,25);g.DrawLine(p,width-23,16,width-32,25);}using(var pen=new Pen(AppTheme.Edge)){g.DrawLine(pen,0,CaptionHeight-1,width,CaptionHeight-1);g.DrawRectangle(pen,0,0,width-1,height-1);}}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);PaintChrome(e.Graphics,Width,Height,closeHover);}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);bool over=e.Y<CaptionHeight&&e.X>Width-48;if(over!=closeHover){closeHover=over;Invalidate(new Rectangle(Width-48,0,48,CaptionHeight));}Cursor=over?Cursors.Hand:e.Y<CaptionHeight?Cursors.SizeAll:Cursors.Default;}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);if(closeHover){closeHover=false;Invalidate(new Rectangle(Width-48,0,48,CaptionHeight));}}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&e.Y<CaptionHeight){if(e.X>Width-48){Close();return;}ReleaseCapture();SendMessage(Handle,0x00A1,new IntPtr(2),IntPtr.Zero);}}
 protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Escape){Close();e.Handled=true;}base.OnKeyDown(e);}
}
}
