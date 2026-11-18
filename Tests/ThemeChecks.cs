using System;using System.Drawing;using System.IO;using LumaDesk;
class ThemeChecks {
 static int n;static void Check(bool ok,string name){if(!ok)throw new Exception(name);n++;Console.WriteLine("PASS "+name);}
 static void Main(){var now=new DateTime(2026,11,18,15,47,0);foreach(bool light in new[]{false,true}){var s=new State{LightTheme=light};for(int k=0;k<4;k++)using(var b=Scene.Render(k,k==0?232:k==2?300:388,now,now,s,null,"Сеть недоступна")){
 Check(b.GetPixel(180,54).R>(light?200:0)&& (light||b.GetPixel(180,54).R<70),(light?"light":"dark")+" background widget "+k);
 bool clear=true;for(int x=302;x<340;x++)for(int y=16;y<45;y++)if(b.GetPixel(x,y).ToArgb()!=b.GetPixel(290,y).ToArgb())clear=false;
 Check(clear,"no ellipsis button in widget "+k+" theme "+light);
 Check(b.GetPixel(0,0).A==0,"transparent rounded corner widget "+k+" theme "+light);
 }
 Check(light?EditorArt.Text.R<60:EditorArt.Text.R>220,"editor text follows theme "+light);
 Check(light?EditorArt.Panel.R>200:EditorArt.Panel.R<60,"editor fields follow theme "+light);
 }
 string dir=Store.Dir;State old=Store.S;Store.Dir=Path.Combine(Path.GetTempPath(),"lumadesk-theme-"+Guid.NewGuid());try{Store.S=new State{ThemeRevision=2,LightTheme=true};Store.SaveStrict();Store.S=new State();Store.Load();Check(Store.S.LightTheme&&AppTheme.Light,"light preference survives disk restart");CloudMerge.Apply(Store.S,CloudMerge.Capture(new State()));Check(Store.S.LightTheme,"cloud synchronization retains local theme");}finally{Directory.Delete(Store.Dir,true);Store.Dir=dir;Store.S=old;AppTheme.Light=old.LightTheme;}Console.WriteLine(n+" theme checks passed");}
}
