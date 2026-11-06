using System;
using System.IO;

namespace CapturaSaveDialog {
 public static class SafeRecordingSave {
  public static void Move(string source,string destination) {
   source=Path.GetFullPath(source);destination=Path.GetFullPath(destination);
   if(String.Equals(source,destination,StringComparison.OrdinalIgnoreCase))return;
   if(!File.Exists(source))throw new FileNotFoundException("原录像文件不存在。",source);
   Directory.CreateDirectory(Path.GetDirectoryName(destination));
   // Copy onto the destination volume before committing. A failed copy or
   // locked target never removes either the original recording or old target.
   string pending=destination+"."+Guid.NewGuid().ToString("N")+".wxc-pending";
   try {
    File.Copy(source,pending,false);
    using(var file=new FileStream(pending,FileMode.Open,FileAccess.Write,FileShare.None)){file.Flush(true);}
    if(File.Exists(destination))File.Replace(pending,destination,null);
    else File.Move(pending,destination);
   } finally {try{if(File.Exists(pending))File.Delete(pending);}catch{}}
   // A completed destination is usable even if another program locks the
   // original after copying; retain a duplicate rather than report data loss.
   try{File.Delete(source);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }
}
