using System.Drawing;
using System.Windows.Forms;
namespace LumaDesk {
public static class AppTheme {
 public static bool Light;
 public static Color Pick(Color dark,Color light){return Light?light:dark;}
 public static Color Bg {get{return Pick(Color.FromArgb(27,29,38),Color.FromArgb(247,248,250));}}
 public static Color Panel {get{return Pick(Color.FromArgb(39,42,55),Color.FromArgb(237,241,247));}}
 public static Color Text {get{return Pick(Color.FromArgb(239,241,248),Color.FromArgb(30,37,48));}}
 public static Color Muted {get{return Pick(Color.FromArgb(148,156,177),Color.FromArgb(91,104,121));}}
 public static Color Edge {get{return Pick(Color.FromArgb(61,65,83),Color.FromArgb(208,216,227));}}
 public static Color Accent {get{return Pick(Color.FromArgb(169,184,244),Color.FromArgb(0,95,184));}}
 public static Color Primary {get{return Pick(Color.FromArgb(79,92,143),Color.FromArgb(0,103,192));}}
 public static Color Selection {get{return Pick(Color.FromArgb(63,73,111),Color.FromArgb(218,235,252));}}
 public static Color Hover {get{return Pick(Color.FromArgb(48,52,68),Color.FromArgb(227,235,245));}}
 public static Color Error {get{return Pick(Color.FromArgb(235,154,160),Color.FromArgb(181,38,50));}}
 public static Color Chrome {get{return Pick(Color.FromArgb(23,25,33),Color.FromArgb(243,246,250));}}
 public static void Change(bool light){
  Color[] before={Bg,Panel,Text,Muted,Edge,Accent,Chrome,Theme.Bg,Theme.Card,Theme.Text,Theme.Muted,Scene.Paper};
  Light=light;
  Color[] after={Bg,Panel,Text,Muted,Edge,Accent,Chrome,Theme.Bg,Theme.Card,Theme.Text,Theme.Muted,Scene.Paper};
  foreach(Form form in Application.OpenForms){Recolor(form,before,after);form.Invalidate(true);}
 }
 static void Recolor(Control control,Color[] before,Color[] after){
  Color bg=control.BackColor,fg=control.ForeColor;
  for(int i=0;i<before.Length;i++){if(bg==before[i]){control.BackColor=after[i];break;}}
  for(int i=0;i<before.Length;i++){if(fg==before[i]){control.ForeColor=after[i];break;}}
  foreach(Control child in control.Controls)Recolor(child,before,after);
 }
}
}
