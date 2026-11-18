using System;
using System.IO;
using System.Linq;
using System.Drawing;
using LumaDesk;
class PlacementChecks {
 static int count;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);count++;}
 static void Main(){
  var primary=new Rectangle(0,0,1920,1040);var left=new Rectangle(-1920,-200,1920,1080);var areas=new[]{primary,left};var size=new Size(360,388);
  Check(WidgetPlacement.Restore(new Placement{X=460,Y=92},size,areas,primary,1)==new Point(460,92),"exact saved coordinates restored");
  Check(WidgetPlacement.Restore(new Placement{X=-1700,Y=-120},size,areas,primary,2)==new Point(-1700,-120),"negative secondary monitor coordinates retained");
  Check(WidgetPlacement.Restore(new Placement{X=-1,Y=300},size,areas,primary,0)==new Point(-1,300),"minus one is a valid assigned X coordinate");
  Check(WidgetPlacement.Restore(new Placement{X=1700,Y=900},size,areas,primary,2)==new Point(1700,900),"partially offscreen window stays at chosen position");
  Check(WidgetPlacement.Restore(new Placement{X=-1700,Y=-120},size,new[]{primary},primary,2)==new Point(1536,108),"disconnected monitor recovers window");
  Check(WidgetPlacement.Restore(new Placement{X=200,Y=-380},size,areas,primary,1)==new Point(1536,66),"inaccessible title recovers window");
  Check(WidgetPlacement.Restore(new Placement(),size,areas,primary,0)==new Point(1532,40),"new widget keeps initial default");
  string original=Store.Dir;var old=Store.S;Store.Dir=Path.Combine(Path.GetTempPath(),"lumadesk-position-"+Guid.NewGuid());
  try{
   Store.S=new State{ThemeRevision=2};
   for(int i=0;i<4;i++){Store.S.Windows[i].X=-1600+i*372;Store.S.Windows[i].Y=88+i*12;Store.S.Windows[i].Visible=i!=2;}
   Store.SaveStrict();Store.S=new State();Store.Load();
   Check(Enumerable.Range(0,4).All(i=>Store.S.Windows[i].X==-1600+i*372&&Store.S.Windows[i].Y==88+i*12),"all four positions survive disk save and restart");
   Check(!Store.S.Windows[2].Visible,"hidden widget placement retained");
   var remote=CloudMerge.Capture(new State());CloudMerge.Apply(Store.S,remote);Store.SaveStrict();Store.Load();
   Check(Enumerable.Range(0,4).All(i=>Store.S.Windows[i].X==-1600+i*372&&Store.S.Windows[i].Y==88+i*12),"account synchronization cannot replace local positions");
   File.WriteAllText(Path.Combine(Store.Dir,"settings.json"),"broken");Store.Load();
   Check(Store.S.Windows[0].X==-1600&&Store.S.Windows[3].Y==124,"backup recovers placements after damaged settings");
  }finally{Directory.Delete(Store.Dir,true);Store.Dir=original;Store.S=old;}
  Console.WriteLine(count+" placement checks passed");
 }
}
