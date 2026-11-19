using System;
using System.IO;
using System.Media;
using System.Collections.Generic;
namespace LumaDesk {
public static class Melodies {
 public static readonly string[] Ids={"@dawn","@orbit","@bells"};
 public static readonly string[] Names={"Рассвет","Орбита","Колокольчики"};
 public static readonly string[] Descriptions={"Мягкое пробуждение","Электронный ритм","Звонкий перелив"};
 static Dictionary<string,byte[]> cache=new Dictionary<string,byte[]>();
 public static int Index(string id){if(string.IsNullOrEmpty(id))return 0;return Array.IndexOf(Ids,id);}
 public static string Name(string id){int i=Index(id);return i>=0?Names[i]:Path.GetFileName(id);}
 public static byte[] Wave(string id){int kind=Index(id);if(kind<0)throw new ArgumentException("Неизвестная мелодия");id=Ids[kind];byte[] found;if(cache.TryGetValue(id,out found))return found;
 const int rate=22050;double length=kind==0?3.6:kind==1?3.2:4.0;var samples=new double[(int)(rate*length)];
 double[] notes=kind==0?new[]{261.6256,329.6276,391.9954,523.2511,391.9954,329.6276}:kind==1?new[]{440.0,554.3653,659.2551,554.3653,440.0,659.2551,880.0,659.2551}:new[]{783.9909,987.7666,1174.6591,1567.9817,1174.6591,987.7666};
 double step=kind==1?.31:.43;
 for(int n=0;n<notes.Length;n++){double start=.08+n*step,decay=kind==0?1.1:kind==1?.30:1.35;for(int j=(int)(start*rate);j<samples.Length;j++){double t=j/(double)rate-start;if(t<0)continue;double env=(1-Math.Exp(-t*110))*Math.Exp(-t/(decay*.32));double wave=Math.Sin(2*Math.PI*notes[n]*t);if(kind==0)wave+=.22*Math.Sin(2*Math.PI*notes[n]*2*t)+.07*Math.Sin(2*Math.PI*notes[n]*3*t);else if(kind==1)wave+=.2*Math.Sin(2*Math.PI*notes[n]*2*t);else wave+=.33*Math.Sin(2*Math.PI*notes[n]*2.01*t)+.12*Math.Sin(2*Math.PI*notes[n]*3.97*t);samples[j]+=wave*env*.30;}}
 using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){int bytes=samples.Length*2;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+bytes);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(bytes);for(int j=0;j<samples.Length;j++){double fade=Math.Min(1,(samples.Length-1-j)/(rate*.08));w.Write((short)(Math.Max(-.94,Math.Min(.94,samples[j]*fade))*32767));}w.Flush();found=stream.ToArray();}cache[id]=found;return found;}
 public static int Duration(string id){try{using(var stream=Index(id)>=0?new MemoryStream(Wave(id)):(Stream)File.OpenRead(id))using(var r=new BinaryReader(stream)){if(new string(r.ReadChars(4))!="RIFF")return 4000;r.ReadInt32();if(new string(r.ReadChars(4))!="WAVE")return 4000;int rate=0;long data=0;while(stream.Position+8<=stream.Length){string chunk=new string(r.ReadChars(4));uint size=r.ReadUInt32();long end=stream.Position+size;if(end>stream.Length)break;if(chunk=="fmt "&&size>=16){r.ReadInt16();r.ReadInt16();r.ReadInt32();rate=r.ReadInt32();}if(chunk=="data")data=size;stream.Position=end+(size%2);}return rate>0&&data>0?(int)Math.Max(100,Math.Min(600000,data*1000/rate)):4000;}}catch{return 4000;}}
}
public sealed class MelodyPlayer:IDisposable {
 static List<MelodyPlayer> active=new List<MelodyPlayer>();static MelodyPlayer current;SoundPlayer player;Stream stream;
 public void Play(string sound,bool loop){if(current!=null&&current!=this)current.Stop();Stop();if(Melodies.Index(sound)>=0){stream=new MemoryStream(Melodies.Wave(sound));player=new SoundPlayer(stream);}else player=new SoundPlayer(sound);try{player.Load();active.Add(this);current=this;if(loop)player.PlayLooping();else player.Play();}catch{Stop();throw;}}
 public void Stop(){active.Remove(this);if(player!=null){if(current==this){player.Stop();current=null;}player.Dispose();player=null;}if(stream!=null){stream.Dispose();stream=null;}}
 public static void StopAll(){foreach(var p in active.ToArray())p.Stop();}
 public void Dispose(){Stop();}
}
}
