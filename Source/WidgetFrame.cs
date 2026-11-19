using System;
using System.Linq;
namespace LumaDesk {
// Only values that affect pixels belong here; desktop coordinates and note
// hidden state must not force a new layered-window upload.
public static class WidgetFrame {
 public static string Key(int kind,int height,DateTime now,DateTime month,State state,WeatherData weather,string status,int offset){
  object content;
  if(kind==0){var alarm=state.Items.Where(a=>a.Kind=="alarm"&&a.Enabled).OrderBy(a=>a.Next).FirstOrDefault();content=new {time=now.ToString("yyyy-MM-dd HH:mm"),world=state.WorldCities,next=alarm==null?"":alarm.NextLocal};}
  else if(kind==1)content=new {day=now.Date,month=month.ToString("yyyy-MM"),items=state.Items.Where(a=>a.Kind=="reminder"&&a.Enabled).Select(a=>new {a.NextLocal,a.Days,a.Title}).ToArray()};
  else if(kind==3)content=new {offset,notes=NotesModel.Order(state.Notes).Select(n=>new {n.Id,n.Title,n.Body}).ToArray()};
  else content=new {state.City,weather,status};
  return CloudJson.Write(new {kind,height,state.LightTheme,state.Opacity,content});
 }
}
}
