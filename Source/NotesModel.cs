using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace LumaDesk {
public class Note {
 public string Id=Guid.NewGuid().ToString("N"),Title="Новая заметка",Body="",UpdatedUtc=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);
}
public static class NotesModel {
 public const int VisibleRows=5,RowTop=108,RowPitch=44,RowHeight=40,MaxBody=20000,MaxCount=500;
 static DateTime Updated(Note n){DateTime value;return DateTime.TryParse(n.UpdatedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out value)?value.ToUniversalTime():DateTime.MinValue;}
 public static List<Note> Order(IEnumerable<Note> notes){return (notes??new Note[0]).OrderByDescending(Updated).ThenBy(n=>n.Title,StringComparer.Create(new CultureInfo("ru-RU"),true)).ThenBy(n=>n.Id,StringComparer.Ordinal).ToList();}
 public static bool Same(Note a,Note b){if(a==null||b==null)return a==b;return a.Id==b.Id&&a.Title==b.Title&&a.Body==b.Body;}
 static Note Copy(Note n){return CloudJson.Copy(n);}
 static Note Conflict(Note note){var copy=Copy(note);using(var h=SHA256.Create()){var hash=h.ComputeHash(Encoding.UTF8.GetBytes(note.Id+"|"+note.Title+"|"+note.Body));copy.Id=BitConverter.ToString(hash,0,16).Replace("-","").ToLowerInvariant();}copy.Title=(note.Title.Length>120?note.Title.Substring(0,120):note.Title)+" (копия конфликта)";return copy;}
 static Dictionary<string,Note> Map(IEnumerable<Note> values){return (values??new Note[0]).ToDictionary(n=>n.Id,n=>n);}
 public static List<Note> Merge(IEnumerable<Note> baseline,IEnumerable<Note> local,IEnumerable<Note> remote,out int conflicts){var b=Map(baseline);var l=Map(local);var r=Map(remote);var output=new Dictionary<string,Note>();conflicts=0;foreach(string id in b.Keys.Union(l.Keys).Union(r.Keys).OrderBy(s=>s,StringComparer.Ordinal)){Note bn=null,ln=null,rn=null;b.TryGetValue(id,out bn);l.TryGetValue(id,out ln);r.TryGetValue(id,out rn);Note take=null;if(Same(ln,bn))take=rn;else if(Same(rn,bn))take=ln;else if(Same(ln,rn))take=ln==null?null:Updated(ln)>=Updated(rn)?ln:rn;else{conflicts++;if(ln!=null&&rn!=null){output[rn.Id]=Copy(rn);var clone=Conflict(ln);output[clone.Id]=clone;}else{var clone=Conflict(ln??rn);output[clone.Id]=clone;}continue;}if(take!=null)output[take.Id]=Copy(take);}return output.Values.OrderBy(n=>n.Id,StringComparer.Ordinal).ToList();}
 public static List<Note> Initial(IEnumerable<Note> remote,IEnumerable<Note> local,bool import){if(!import)return remote.Select(Copy).ToList();int conflicts;return Merge(new Note[0],local,remote,out conflicts);}
 public static List<Note> CommitPreview(IEnumerable<Note> current,Note original,Note edited){var all=current.ToList();string id=original!=null?original.Id:edited.Id;var remote=all.Where(n=>n.Id==id).ToList();int conflicts;var merged=Merge(original==null?new Note[0]:new[]{original},edited==null?new Note[0]:new[]{edited},remote,out conflicts);all.RemoveAll(n=>n.Id==id);foreach(var note in merged){all.RemoveAll(n=>n.Id==note.Id);all.Add(note);}return all;}
 public static void CheckQuota(State state,List<Note> notes){if(notes.Count>MaxCount||notes.Sum(n=>(long)n.Body.Length)>200000)throw new Exception("Лимит: 500 заметок и 200 000 знаков текста суммарно.");var doc=CloudMerge.Capture(state);doc.notes=notes;if(Encoding.UTF8.GetByteCount(CloudJson.Write(doc))>900000)throw new Exception("Общий размер данных слишком большой для синхронизации. Сократи текст или убери ненужные события.");}
}
}
