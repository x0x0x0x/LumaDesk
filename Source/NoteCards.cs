using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
namespace LumaDesk {
public class NoteLine {public int Index;public string Text;public bool Task,Done;}
public class NoteCard {public Note Note;public Rectangle Bounds;public List<NoteLine> Lines;public bool Checklist;}
public static class NoteCards {
 public const int Top=60,Bottom=502,Gap=9;
 static readonly Regex TaskPattern=new Regex(@"^(\s*(?:-\s+)?\[)([ xX])(\]\s*)(.*)$");
 public static List<NoteLine> Lines(string body){var result=new List<NoteLine>();var lines=(body??"").Replace("\r\n","\n").Split(new[]{'\n'});for(int i=0;i<lines.Length;i++){var m=TaskPattern.Match(lines[i]);result.Add(new NoteLine{Index=i,Text=m.Success?m.Groups[4].Value:lines[i],Task=m.Success,Done=m.Success&&m.Groups[2].Value!=" "});}return result;}
 public static int Height(Note n){var lines=Lines(n.Body);if(lines.Any(l=>l.Task))return 54+Math.Min(4,lines.Count)*24;string body=(n.Body??"").Trim();int rows=body.Length==0?0:Math.Min(3,body.Split(new[]{'\n'}).Sum(l=>Math.Max(1,(l.Length+41)/42)));return Math.Max(66,54+rows*18);}
 public static int MaxScroll(State s){return Math.Max(0,NotesModel.Order(s.Notes).Sum(n=>Height(n)+Gap)-Gap-(Bottom-Top));}
 public static List<NoteCard> Layout(State s,int scroll){int y=Top-Math.Max(0,Math.Min(MaxScroll(s),scroll));var cards=new List<NoteCard>();foreach(var n in NotesModel.Order(s.Notes)){int h=Height(n);cards.Add(new NoteCard{Note=n,Bounds=new Rectangle(18,y,324,h),Lines=Lines(n.Body).Take(4).ToList(),Checklist=Lines(n.Body).Any(l=>l.Task)});y+=h+Gap;}return cards;}
 public static int ColorIndex(Note n){uint h=2166136261;foreach(char c in n.Id??"")h=unchecked((h^c)*16777619);return (int)(h%4);}
 public static Note Toggle(Note original,int lineIndex){var copy=CloudJson.Copy(original);var lines=(copy.Body??"").Replace("\r\n","\n").Split(new[]{'\n'});if(lineIndex<0||lineIndex>=lines.Length)return copy;var m=TaskPattern.Match(lines[lineIndex]);if(!m.Success)return copy;lines[lineIndex]=m.Groups[1].Value+(m.Groups[2].Value==" "?"x":" ")+m.Groups[3].Value+m.Groups[4].Value;copy.Body=string.Join(original.Body.Contains("\r\n")?"\r\n":"\n",lines);copy.UpdatedUtc=DateTime.UtcNow.ToString("o",System.Globalization.CultureInfo.InvariantCulture);return copy;}
 public static bool TaskAt(NoteCard card,Point point,out int index){index=-1;if(!card.Checklist||point.X<card.Bounds.X+12||point.X>card.Bounds.X+34)return false;int row=(point.Y-card.Bounds.Y-42)/24;if(point.Y<card.Bounds.Y+42||row<0||row>=card.Lines.Count||!card.Lines[row].Task)return false;index=card.Lines[row].Index;return true;}
}
}
