using System;
namespace LumaDesk {
// Timer ticks only compare timestamps; no serialization, disk or network work
// is done while idle. Failed requests back off instead of polling every minute.
public class SyncSchedule {
 public static readonly TimeSpan PollInterval=TimeSpan.FromMinutes(15),EditDelay=TimeSpan.FromSeconds(8);
 DateTime pollAt,dirtyAt=DateTime.MaxValue,retryAt=DateTime.MinValue;int failures;
 public SyncSchedule(DateTime now){pollAt=now+PollInterval;}
 public void Changed(DateTime now){dirtyAt=now+EditDelay;}
 public bool Due(DateTime now){return now>=retryAt&&(now>=dirtyAt||now>=pollAt);}
 public void Complete(DateTime now,bool success,bool pending){
  if(success){failures=0;retryAt=DateTime.MinValue;pollAt=now+PollInterval;dirtyAt=pending?now+EditDelay:DateTime.MaxValue;}
  else{failures=Math.Min(5,failures+1);retryAt=now+TimeSpan.FromMinutes(Math.Min(60,5*Math.Pow(2,failures-1)));pollAt=retryAt;}
 }
}
}
