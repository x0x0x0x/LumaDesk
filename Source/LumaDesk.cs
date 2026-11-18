using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Media;
using Microsoft.Win32;

[assembly:System.Reflection.AssemblyVersion("2.4.0.0")]
[assembly:System.Reflection.AssemblyFileVersion("2.4.0.0")]
[assembly:System.Reflection.AssemblyProduct("LumaDesk")]
namespace LumaDesk {
public class Item {
 public string Id=Guid.NewGuid().ToString(), Title="Будильник", Kind="alarm", Sound="@dawn";
 [ScriptIgnore] public DateTime Next=DateTime.Now.AddHours(1);
 public string NextLocal {get{return Next.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture);}set{Next=DateTime.ParseExact(value,"yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture);}}
 public int Days=0; public bool Enabled=true; public int Snooze=5;
 public override string ToString(){return (Enabled?"● ":"○ ")+Next.ToString("dd.MM HH:mm")+"  "+Title+(Days==0?"":"  ↻");}
}
public class Placement {public int X=-1,Y=-1; public bool Visible=true;}
public class State {
 public List<Item> Items=new List<Item>(); public List<Note> Notes=new List<Note>(); public List<Placement> Windows=new List<Placement>{new Placement(),new Placement(),new Placement(),new Placement()};
 public bool LightTheme=false; public bool Top=false,Locked=false,Seconds=true,Sound=true,Startup=false,Snapping=true; public int Opacity=84, Refresh=30, ThemeRevision=0,WeatherMode=0;
 public string City="Москва"; public double Lat=55.7522,Lon=37.6156;
}
public static class Store {
 public static string Dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LumaDesk");
 public static JavaScriptSerializer Json=new JavaScriptSerializer(); public static State S=new State();
 public static void Load(){Directory.CreateDirectory(Dir);try{S=Json.Deserialize<State>(File.ReadAllText(Path.Combine(Dir,"settings.json")));if(S==null)throw new Exception();}catch{try{S=Json.Deserialize<State>(File.ReadAllText(Path.Combine(Dir,"settings.json.bak")))??new State();}catch{S=new State();}} if(S.Items==null)S.Items=new List<Item>();S.Opacity=Math.Max(40,Math.Min(100,S.Opacity));if(S.Notes==null)S.Notes=new List<Note>();if(S.Windows==null)S.Windows=new List<Placement>();while(S.Windows.Count<4)S.Windows.Add(new Placement());if(S.ThemeRevision<2){S.Opacity=84;S.ThemeRevision=2;Save();}AppTheme.Light=S.LightTheme;}
 public static void SaveStrict(){CloudJson.Atomic(Path.Combine(Dir,"settings.json"),Json.Serialize(S));}
 public static void Save(){try{SaveStrict();}catch(Exception e){MessageBox.Show("Не удалось сохранить настройки: "+e.Message,"LumaDesk");}}

}
public static class Theme {
 public static Color Bg {get{return AppTheme.Pick(Color.FromArgb(28,30,39),Color.FromArgb(247,248,250));}}
 public static Color Card {get{return AppTheme.Pick(Color.FromArgb(41,44,57),Color.FromArgb(237,241,247));}}
 public static Color Text {get{return AppTheme.Text;}} public static Color Muted {get{return AppTheme.Muted;}}
 public static Color Mint {get{return AppTheme.Accent;}} public static Color Purple {get{return AppTheme.Accent;}} public static Color Amber {get{return AppTheme.Accent;}}
 public static Font F(float n,FontStyle s=FontStyle.Regular){return new Font(Scene.FontName,n,s,GraphicsUnit.Point);}
 public static void TextAt(Graphics g,string s,float n,Color c,float x,float y,FontStyle style=FontStyle.Regular){using(var f=F(n,style))using(var b=new SolidBrush(c))g.DrawString(s,f,b,x,y);}
 public static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 public static void Fill(Graphics g,Rectangle r,Color c,int radius){using(var p=Round(r,radius))using(var b=new SolidBrush(c))g.FillPath(b,p);}
 public static SmoothButton Button(string text,int x,int y,int w,EventHandler click){return new SmoothButton(text,new Rectangle(x,y,w,34),click){Primary=true};}
 public static Form Dialog(string title,int w,int h){return new ChromeForm(title,w,h);}
 public static Label Label(string t,int x,int y,int w){return new Label{Text=t,Bounds=new Rectangle(x,y,w,25),ForeColor=Muted};}
 public static TextBox Input(string t,int x,int y,int w){return new TextBox{Text=t,Bounds=new Rectangle(x,y,w,28),BackColor=Card,ForeColor=Text,BorderStyle=BorderStyle.FixedSingle};}
}
public class Host : ApplicationContext {
 public Widget[] W; public NotifyIcon Tray; ToolStripMenuItem[] toggles=new ToolStripMenuItem[4];ToolStripMenuItem snapToggle,lockToggle,darkTheme,lightTheme; bool syncingChecks; System.Windows.Forms.Timer tick; DateTime lastWeather=DateTime.MinValue; bool busy=false; public WeatherData Weather; public string WeatherStatus="Загрузка погоды…"; public DateTime WeatherAt;
 public Host(){W=new Widget[]{new Widget(this,0),new Widget(this,1),new Widget(this,2),new Widget(this,3)};PlaceNewNotes();for(int i=0;i<4;i++)if(Store.S.Windows[i].Visible)W[i].Show();
 var menu=new ContextMenuStrip{ShowCheckMargin=true,ShowImageMargin=false};menu.Items.Add("Показать все виджеты",null,(s,e)=>ShowAll());menu.Items.Add(new ToolStripSeparator());
 for(int i=0;i<4;i++){int k=i;var toggle=new ToolStripMenuItem(new[]{"Часы","Календарь","Погода","Заметки"}[i]){CheckOnClick=true,Checked=Store.S.Windows[k].Visible};toggle.CheckedChanged+=(s,e)=>{if(!syncingChecks)SetWidgetVisibility(k,toggle.Checked);};toggles[k]=toggle;menu.Items.Add(toggle);}
 var arrange=new ToolStripMenuItem("Выравнивание");var column=arrange.DropDownItems.Add("В столбик",null,(s,e)=>ArrangeWidgets(true));var row=arrange.DropDownItems.Add("В ряд",null,(s,e)=>ArrangeWidgets(false));arrange.DropDownItems.Add(new ToolStripSeparator());snapToggle=new ToolStripMenuItem("Прилипание к виджетам и экрану"){CheckOnClick=true,Checked=Store.S.Snapping};snapToggle.CheckedChanged+=(s,e)=>{if(!syncingChecks){Store.S.Snapping=snapToggle.Checked;Store.Save();}};arrange.DropDownItems.Add(snapToggle);menu.Items.Add(new ToolStripSeparator());lockToggle=new ToolStripMenuItem("Запретить перемещение виджетов"){CheckOnClick=true,Checked=Store.S.Locked};lockToggle.CheckedChanged+=(s,e)=>{if(!syncingChecks){Store.S.Locked=lockToggle.Checked;Store.Save();}};menu.Items.Add(lockToggle);menu.Items.Add(arrange);var themes=new ToolStripMenuItem("Тема оформления");darkTheme=new ToolStripMenuItem("Тёмная",null,(s,e)=>SetTheme(false));lightTheme=new ToolStripMenuItem("Светлая · Windows 11",null,(s,e)=>SetTheme(true));themes.DropDownItems.Add(darkTheme);themes.DropDownItems.Add(lightTheme);menu.Items.Add(themes);
 menu.Opening+=(s,e)=>{SyncChecks();column.Enabled=row.Enabled=!Store.S.Locked;};menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Аккаунт и синхронизация",null,(s,e)=>{using(var form=new AccountForm())form.ShowDialog();});var syncNow=menu.Items.Add("Синхронизировать сейчас",null,(s,e)=>CloudSync.Kick());menu.Opening+=(s,e)=>syncNow.Enabled=CloudSync.Connected&&!CloudSync.Busy;menu.Items.Add("Настройки",null,(s,e)=>Settings());menu.Items.Add("Выход",null,(s,e)=>Exit());
 Icon appIcon=SystemIcons.Application;try{appIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath)??appIcon;}catch{}
 Tray=new NotifyIcon{Icon=appIcon,Text="LumaDesk · виджеты",ContextMenuStrip=menu,Visible=true};Tray.DoubleClick+=(s,e)=>{for(int i=0;i<4;i++)if(Store.S.Windows[i].Visible)W[i].Show();};
 try{string p=Path.Combine(Store.Dir,"weather.json");var cache=Store.Json.Deserialize<WeatherCache>(File.ReadAllText(p));if(Math.Abs(cache.Lat-Store.S.Lat)<0.00001&&Math.Abs(cache.Lon-Store.S.Lon)<0.00001){Weather=cache.Data;WeatherAt=cache.At;WeatherStatus="Сохранённый прогноз";}}catch{}
 UpdateAll();tick=new System.Windows.Forms.Timer{Interval=1000};tick.Tick+=(s,e)=>{Check();foreach(var w in W)if(w.Visible)w.Redraw();if((DateTime.Now-lastWeather).TotalMinutes>=Store.S.Refresh)Fetch();};tick.Start(); Fetch();CloudSync.Initialize(weatherChanged=>{SyncChecks();UpdateAll();if(!Store.S.Sound)MelodyPlayer.StopAll();if(weatherChanged){lastWeather=DateTime.MinValue;Fetch();}});
 }
 void PlaceNewNotes(){var p=Store.S.Windows[3];if(p.X!=-1||p.Y!=-1)return;var work=Screen.FromRectangle(W[0].Bounds).WorkingArea;var candidates=new List<Point>{new Point(W[0].Left,W[0].Bottom+SnapLayout.Gap),new Point(W[1].Left,W[1].Bottom+SnapLayout.Gap),new Point(W[2].Left,W[2].Bottom+SnapLayout.Gap),new Point(work.Left+12,work.Top+12)};foreach(var point in candidates){var rect=new Rectangle(point,W[3].Size);if(!work.Contains(rect))continue;bool occupied=false;for(int i=0;i<3;i++)if(Store.S.Windows[i].Visible){var other=W[i].Bounds;other.Inflate(SnapLayout.Gap,SnapLayout.Gap);if(other.IntersectsWith(rect))occupied=true;}if(!occupied){W[3].Location=point;W[3].SavePosition();Store.Save();return;}}}
 public void SetTheme(bool light){Store.S.LightTheme=light;AppTheme.Change(light);Store.Save();SyncChecks();UpdateAll();}
 public void SyncChecks(){syncingChecks=true;try{for(int i=0;i<4;i++)if(toggles[i]!=null)toggles[i].Checked=Store.S.Windows[i].Visible;if(snapToggle!=null)snapToggle.Checked=Store.S.Snapping;if(lockToggle!=null)lockToggle.Checked=Store.S.Locked;if(darkTheme!=null)darkTheme.Checked=!Store.S.LightTheme;if(lightTheme!=null)lightTheme.Checked=Store.S.LightTheme;}finally{syncingChecks=false;}}
 public void SetWidgetVisibility(int k,bool visible){Store.S.Windows[k].Visible=visible;if(visible)W[k].Show();else W[k].Hide();SyncChecks();Store.Save();}
 public void ShowAll(){for(int i=0;i<4;i++){Store.S.Windows[i].Visible=true;W[i].Show();}SyncChecks();Store.Save();}
 public Point SnapWidget(Widget moving,Point wanted){return SnapLayout.Snap(new Rectangle(wanted,moving.Size),W.Where(w=>w!=moving&&w.Visible).Select(w=>w.Bounds),Screen.FromPoint(Cursor.Position).WorkingArea);}
 public void ArrangeWidgets(bool vertical){if(Store.S.Locked)return;var visible=W.Where(w=>w.Visible).ToArray();if(visible.Length==0)return;foreach(var w in visible)w.Fit();var area=Screen.FromRectangle(visible[0].Bounds).WorkingArea;var places=SnapLayout.Arrange(visible.Select(w=>w.Size).ToArray(),area,visible[0].Location,vertical);for(int i=0;i<visible.Length;i++){visible[i].Location=places[i];visible[i].SavePosition();visible[i].Redraw();}Store.Save();}
 public void Exit(){CloudSync.Stop();MelodyPlayer.StopAll();tick.Stop();foreach(var w in W)w.CapturePosition();Store.Save();Tray.Visible=false;Tray.Dispose();foreach(var w in W)w.Dispose();Application.ExitThread();}
 public void UpdateAll(){foreach(var w in W){w.Fit();w.TopMost=Store.S.Top;w.SetOpacityPercent(Store.S.Opacity);w.Redraw();}}
 public static bool MatchesDate(Item a,DateTime date){return a.Days==0?a.Next.Date==date:date>=a.Next.Date&&(a.Days&(1<<(int)date.DayOfWeek))!=0;}
 public static DateTime Advance(Item a,DateTime from){for(int d=0;d<8;d++){var t=from.Date.AddDays(d).Add(a.Next.TimeOfDay);if(t>from&&(a.Days&(1<<(int)t.DayOfWeek))!=0)return t;}return from.AddDays(1);}
 public void Check(){var now=DateTime.Now;bool change=false;foreach(var a in Store.S.Items.ToArray()){if(!a.Enabled||a.Next>now)continue;DateTime due=a.Next; if(a.Days==0)a.Enabled=false;else a.Next=Advance(a,now);change=true;if((now-due).TotalHours<=24)Alert(a,due<now.AddMinutes(-2));}if(change)Store.Save();}
 void Alert(Item a,bool missed){var f=Theme.Dialog(a.Kind=="alarm"?"LumaDesk · Будильник":"LumaDesk · Напоминание",410,218);f.TopMost=true;f.Controls.Add(new Label{Text=(missed?"Пропущено: ":"")+a.Title,Font=Theme.F(19,FontStyle.Bold),ForeColor=Theme.Text,Bounds=new Rectangle(22,22,365,85)});f.Controls.Add(Theme.Label(DateTime.Now.ToString("dd MMMM · HH:mm"),22,110,365));
 var player=new MelodyPlayer();f.Shown+=(s,e)=>{if(Store.S.Sound)try{player.Play(a.Sound,true);}catch{SystemSounds.Exclamation.Play();}};f.FormClosed+=(s,e)=>player.Dispose();
 f.Controls.Add(Theme.Button("Через "+a.Snooze+" мин",22,160,177,(s,e)=>{Store.S.Items.Add(new Item{Title=a.Title,Kind=a.Kind,Next=DateTime.Now.AddMinutes(a.Snooze),Sound=a.Sound,Snooze=a.Snooze});Store.Save();f.Close();}));f.Controls.Add(Theme.Button("Выключить",211,160,177,(s,e)=>f.Close()));f.Show();}
 public void List(string kind,DateTime? date){using(var f=new EventListForm(this,kind,date))f.ShowDialog();UpdateAll();}
 public void Edit(Item original,string kind,DateTime? day){using(var f=new EditorForm(original,kind,day)){if(f.ShowDialog()==DialogResult.OK){if(original!=null)Store.S.Items.Remove(original);Store.S.Items.Add(f.Result);Store.Save();}}UpdateAll();}
 public void EditNote(Note note){var original=note==null?null:CloudJson.Copy(note);using(var form=new NoteEditorForm(note)){var answer=form.ShowDialog();if(answer!=DialogResult.OK&&answer!=DialogResult.Yes)return;var proposed=NotesModel.CommitPreview(Store.S.Notes,original,form.DeleteRequested?null:form.Result);Store.S.Notes=proposed;Store.Save();UpdateAll();CloudSync.Kick();}}
 public void Settings(){using(var f=Theme.Dialog("LumaDesk · Настройки",490,531)){
 var provider=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Theme.Card,ForeColor=Theme.Text,Bounds=new Rectangle(220,342,240,30)};provider.Items.AddRange(new object[]{"Авто: MET → Open-Meteo","MET Norway","Open-Meteo"});provider.SelectedIndex=Math.Max(0,Math.Min(2,Store.S.WeatherMode));var snapping=new CheckBox{Text="Прилипание к виджетам и краям экрана",Checked=Store.S.Snapping,Bounds=new Rectangle(22,210,440,30)};var top=new CheckBox{Text="Поверх других окон",Checked=Store.S.Top,Bounds=new Rectangle(22,20,440,30)};var locked=new CheckBox{Text="Закрепить положение виджетов",Checked=Store.S.Locked,Bounds=new Rectangle(22,58,440,30)};var seconds=new CheckBox{Text="Показывать секунды",Checked=Store.S.Seconds,Bounds=new Rectangle(22,96,440,30)};var sound=new CheckBox{Text="Звук будильников и напоминаний",Checked=Store.S.Sound,Bounds=new Rectangle(22,134,440,30)};
 bool auto=false;using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))auto=key!=null&&key.GetValue("LumaDesk")!=null;
 var startup=new CheckBox{Text="Запускать вместе с Windows",Checked=auto,Bounds=new Rectangle(22,172,440,30)};f.Controls.AddRange(new Control[]{top,locked,seconds,sound,startup});
 f.Controls.Add(Theme.Label("Прозрачность, %",22,220,220));var opacity=new NumericUpDown{BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,Bounds=new Rectangle(300,218,150,30),Minimum=0,Maximum=60,Value=100-Store.S.Opacity};f.Controls.Add(opacity);
 f.Controls.Add(Theme.Label("Обновление погоды, минут",22,263,275));var refresh=new NumericUpDown{BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle,Bounds=new Rectangle(300,261,150,30),Minimum=15,Maximum=180,Value=Math.Max(15,Math.Min(180,Store.S.Refresh))};f.Controls.Add(refresh);
 f.Controls.Add(Theme.Label("LumaDesk 2.4 · Часы и события используют время Windows.\nБудильники работают, пока программа запущена.",22,310,450));f.Controls[f.Controls.Count-1].Height=50;
 f.Controls.Add(Theme.Button("Сохранить",300,385,160,(s,e)=>{try{using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(startup.Checked)key.SetValue("LumaDesk","\""+Application.ExecutablePath+"\"");else key.DeleteValue("LumaDesk",false);}}catch(Exception ex){MessageBox.Show("Автозапуск не изменён: "+ex.Message);return;}bool providerChanged=Store.S.WeatherMode!=provider.SelectedIndex;Store.S.WeatherMode=provider.SelectedIndex;Store.S.Snapping=snapping.Checked;Store.S.Top=top.Checked;Store.S.Locked=locked.Checked;SyncChecks();Store.S.Seconds=seconds.Checked;Store.S.Sound=sound.Checked;if(!Store.S.Sound)MelodyPlayer.StopAll();Store.S.Startup=startup.Checked;Store.S.Opacity=100-(int)opacity.Value;Store.S.Refresh=(int)refresh.Value;Store.Save();UpdateAll();f.Close();if(providerChanged){lastWeather=DateTime.MinValue;Fetch();}}));foreach(Control control in f.Controls)if(control.Top>=ChromeForm.CaptionHeight+210)control.Top+=38;foreach(Control control in f.Controls)if(control.Top>=ChromeForm.CaptionHeight+348)control.Top+=48;f.Controls.Add(snapping);f.Controls.Add(Theme.Label("Источник погоды",22,346,190));f.Controls.Add(provider);f.ShowDialog();}}
 public void CityDialog(){using(var f=Theme.Dialog("Город · Россия",550,390)){
 var q=Theme.Input(Store.S.City,20,20,360);f.Controls.Add(q);var list=new ListBox{Bounds=new Rectangle(20,70,510,175),BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=Theme.F(11)};f.Controls.Add(list);var status=Theme.Label("",20,258,510);f.Controls.Add(status);
 Action search=()=>{list.Items.Clear();foreach(var city in RussianCities.Search(q.Text))list.Items.Add(city);if(list.Items.Count>0)list.SelectedIndex=0;status.Text=list.Items.Count==0?"Город не найден. Попробуй ближайший крупный город.":"Найдено: "+list.Items.Count+" · выбери нужную область";};q.TextChanged+=(s,e)=>search();f.Controls.Add(Theme.Button("Найти",395,15,135,(s,e)=>search()));
 Action choose=()=>{var city=list.SelectedItem as City;if(city==null)return;Store.S.City=city.name;Store.S.Lat=city.latitude;Store.S.Lon=city.longitude;Store.Save();Weather=null;WeatherStatus="Загрузка погоды…";lastWeather=DateTime.MinValue;f.Close();Fetch();UpdateAll();};list.DoubleClick+=(s,e)=>choose();f.Controls.Add(Theme.Button("Выбрать",375,325,155,(s,e)=>choose()));f.Controls.Add(Theme.Label("Встроенный справочник · поиск без сети",20,329,345));search();f.ShowDialog();}}
 public async void Fetch(){if(busy)return;busy=true;if(Weather==null){WeatherStatus="Загрузка погоды…";UpdateAll();}lastWeather=DateTime.Now;double lat=Store.S.Lat,lon=Store.S.Lon;int mode=Store.S.WeatherMode;try{
 var data=await Task.Run(()=>WeatherProviders.Fetch(lat,lon,mode));if(lat==Store.S.Lat&&lon==Store.S.Lon&&mode==Store.S.WeatherMode){Weather=data;WeatherAt=DateTime.Now;WeatherStatus=WeatherAt.ToString("HH:mm");try{File.WriteAllText(Path.Combine(Store.Dir,"weather.json"),Store.Json.Serialize(new WeatherCache{Lat=lat,Lon=lon,Data=data,At=WeatherAt}),Encoding.UTF8);}catch{}}}
 catch{if(lat==Store.S.Lat&&lon==Store.S.Lon&&mode==Store.S.WeatherMode)WeatherStatus=Weather==null?"Нет связи · проверь провайдера в настройках":"Нет связи · данные от "+WeatherAt.ToString("dd.MM HH:mm");}finally{busy=false;UpdateAll();if(lat!=Store.S.Lat||lon!=Store.S.Lon||mode!=Store.S.WeatherMode)Fetch();}}

}
public static class Net {public static string Get(string url){ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;using(var c=new TimedClient()){c.Encoding=Encoding.UTF8;c.Headers["User-Agent"]="LumaDesk/2.4";return c.DownloadString(url);}}}
public class TimedClient:WebClient {protected override WebRequest GetWebRequest(Uri u){var r=base.GetWebRequest(u);r.Timeout=15000;return r;}}
public class City {public string name,admin1,country_code;public double latitude,longitude;public int utc_offset=3;public override string ToString(){return name+(string.IsNullOrEmpty(admin1)?"":" · "+admin1);}}
public class GeoRoot {public City[] results;}
public class Current {public double temperature_2m,apparent_temperature,wind_speed_10m;public int weather_code;public double? relative_humidity_2m;public string time;}
public class Daily {public string[] time;public int[] weather_code;public double[] temperature_2m_max,temperature_2m_min;}
public class WeatherData {public Current current;public Daily daily;public string timezone,Provider;}
public class WeatherCache {public WeatherData Data;[ScriptIgnore] public DateTime At;public string AtLocal {get{return At.ToString("yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture);}set{At=DateTime.ParseExact(value,"yyyy-MM-ddTHH:mm:ss",CultureInfo.InvariantCulture);}}public double Lat,Lon;}
public class Widget:Form {
 Host host;int kind;byte alpha=214;bool rendering;bool dragging,moved,positionReady;System.Windows.Forms.Timer positionSave;Point dragStart,dragOrigin;int noteOffset;DateTime month=DateTime.Today;ToolTip tip=new ToolTip();
 public Widget(Host h,int k){host=h;kind=k;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;DoubleBuffered=false;BackColor=Scene.Paper;ForeColor=Theme.Text;Font=Theme.F(10);AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(360,k==0?232:k==1||k==3?388:300);StartPosition=FormStartPosition.Manual;
 Location=RestorePosition();
 positionSave=new System.Windows.Forms.Timer{Interval=350};positionSave.Tick+=(o,e)=>{positionSave.Stop();Store.Save();};
 LocationChanged+=(o,e)=>{if(!positionReady||IsDisposed)return;SavePosition();positionSave.Stop();positionSave.Start();};
 TopMost=Store.S.Top;alpha=(byte)Math.Round(Math.Max(40,Math.Min(100,Store.S.Opacity))*2.55);
 var menu=new ContextMenuStrip();menu.Items.Add("Настройки",null,(o,e)=>host.Settings());menu.Items.Add("Скрыть виджет",null,(o,e)=>{host.SetWidgetVisibility(kind,false);});menu.Items.Add("Выход из LumaDesk",null,(o,e)=>host.Exit());ContextMenuStrip=menu;
 MouseDown+=(o,e)=>{if(e.Button==MouseButtons.Left&&e.Y<48&&!Store.S.Locked){dragStart=Cursor.Position;dragOrigin=Location;dragging=true;moved=false;Capture=true;}};
 MouseMove+=(o,e)=>{if(dragging){Point pointer=Cursor.Position;int dx=pointer.X-dragStart.X,dy=pointer.Y-dragStart.Y;if(Math.Abs(dx)+Math.Abs(dy)>3)moved=true;if(moved){var wanted=new Point(dragOrigin.X+dx,dragOrigin.Y+dy);Location=Store.S.Snapping&&(ModifierKeys&Keys.Alt)==0?host.SnapWidget(this,wanted):wanted;}}Cursor=e.Y<48?(Store.S.Locked?Cursors.Default:Cursors.SizeAll):Cursors.Hand;};
 MouseUp+=(o,e)=>{if(dragging){dragging=false;Capture=false;if(moved){SavePosition();Store.Save();}}};
 MouseCaptureChanged+=(o,e)=>{if(dragging&&!Capture){dragging=false;if(moved){SavePosition();Store.Save();}}};
 MouseClick+=(o,e)=>{if(e.Button!=MouseButtons.Left||moved){moved=false;return;}if(e.Y<48)return;if(kind==0&&e.Y>=48)host.List("alarm",null);if(kind==1)CalendarClick(e.Location);if(kind==3)NotesClick(e.Location);if(kind==2){if(host.Weather==null&&e.Y>=222&&e.Y<=264){if(e.X<180)host.Fetch();else host.CityDialog();return;}if(e.Y>=Height-44){host.Fetch();return;}if(e.Y>=48&&e.Y<104)host.CityDialog();}};
 MouseWheel+=(o,e)=>{if(kind==3){noteOffset=Math.Max(0,Math.Min(Math.Max(0,Store.S.Notes.Count-NotesModel.VisibleRows),noteOffset-(e.Delta/120)*3));Redraw();}};
 tip.SetToolTip(this,kind==3?"Нажми название — открыть заметку. Колёсико — прокрутить список.":kind==0?"Нажми часы — будильники. Перетаскивай за заголовок.":kind==1?"Нажми дату — напоминание. Стрелки — другой месяц.":"Нажми город, чтобы изменить его. Внизу — обновление погоды.");
 }
 Point RestorePosition(){return WidgetPlacement.Restore(Store.S.Windows[kind],Size,Screen.AllScreens.Select(sc=>sc.WorkingArea).ToArray(),Screen.PrimaryScreen.WorkingArea,kind);}
 public void SavePosition(){Store.S.Windows[kind].X=Left;Store.S.Windows[kind].Y=Top;}
 public void CapturePosition(){if(positionReady&&!IsDisposed)SavePosition();}
 protected override void WndProc(ref Message m){if(m.Msg==0x0011||m.Msg==0x0016){CapturePosition();try{Store.SaveStrict();}catch{}}base.WndProc(ref m);}
 protected override CreateParams CreateParams {get{var cp=base.CreateParams;cp.ExStyle|=0x00080000;return cp;}}
 public void SetOpacityPercent(int value){alpha=(byte)Math.Round(Math.Max(40,Math.Min(100,value))*2.55);}
 public void Fit(){if(kind==2){int desired=host.Weather==null?300:478;if(Height!=desired)Height=desired;}}
 public void Redraw(){if(!positionReady||rendering||IsDisposed||!IsHandleCreated||!Visible)return;rendering=true;try{using(var image=Scene.Render(kind,Height,DateTime.Now,month,Store.S,host.Weather,host.WeatherStatus,noteOffset))NativeSurface.Present(Handle,Bounds,image,alpha);}finally{rendering=false;}}
 protected override void OnShown(EventArgs e){Fit();Location=RestorePosition();positionReady=true;SavePosition();Store.Save();base.OnShown(e);Redraw();}
 protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible)Redraw();}
 protected override void OnPaintBackground(PaintEventArgs e){}
 protected override void OnPaint(PaintEventArgs e){Redraw();}
 void NotesClick(Point p){if(p.X<22||p.X>=338)return;if(p.Y>=346&&p.Y<375){host.EditNote(null);return;}if(p.Y>=NotesModel.RowTop&&p.Y<NotesModel.RowTop+NotesModel.VisibleRows*NotesModel.RowPitch&&(p.Y-NotesModel.RowTop)%NotesModel.RowPitch<NotesModel.RowHeight){var notes=NotesModel.Order(Store.S.Notes);noteOffset=Math.Max(0,Math.Min(noteOffset,Math.Max(0,notes.Count-NotesModel.VisibleRows)));int index=noteOffset+(p.Y-NotesModel.RowTop)/NotesModel.RowPitch;if(index<notes.Count)host.EditNote(notes[index]);}}
 void CalendarClick(Point p){if(p.Y>=55&&p.Y<96&&p.X>=260){month=month.AddMonths(p.X<300?-1:1);Redraw();return;}if(p.Y>=343){host.List("reminder",null);return;}if(p.X>=22&&p.X<337&&p.Y>=130&&p.Y<334){int col=(p.X-22)/45,row=(p.Y-130)/34;var first=new DateTime(month.Year,month.Month,1);host.List("reminder",first.AddDays(row*7+col-((int)first.DayOfWeek+6)%7));}}
 protected override void Dispose(bool disposing){if(disposing){if(positionSave!=null){positionSave.Stop();positionSave.Dispose();}tip.Dispose();}base.Dispose(disposing);}
}
public static class Program {
 public static CultureInfo Ru=new CultureInfo("ru-RU");
 [STAThread]public static void Main(){bool fresh;using(var mutex=new Mutex(true,"Local\\LumaDeskWidgets_1",out fresh)){if(!fresh){MessageBox.Show("LumaDesk уже запущен. Значок находится рядом с часами Windows.");return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Thread.CurrentThread.CurrentCulture=Ru;Thread.CurrentThread.CurrentUICulture=Ru;Store.Load();Application.Run(new Host());}}
}
}
