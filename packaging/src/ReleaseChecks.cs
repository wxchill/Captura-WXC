using System;
using System.IO;
using CapturaDistribution;
using CapturaSaveDialog;
using Newtonsoft.Json.Linq;

class ReleaseChecks {
 static void Assert(bool value,string message){if(!value)throw new Exception(message);}
 static void Main(string[] args){
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
  string profile=Path.Combine(root,"profile"),app=AppDomain.CurrentDomain.BaseDirectory;
  Bootstrap.InitializeAt(app,profile,Path.Combine(root,"录像"));
  string config=Path.Combine(profile,"Captura.json");var json=JObject.Parse(File.ReadAllText(config));
  Assert((string)json["UI"]["Language"]=="zh-CN","Default language");
  Assert(!(bool)json["Tray"]["MinToTrayOnStartup"] && (bool)json["Tray"]["MinToTrayOnClose"],"Startup and close-to-tray");
  Assert(File.Exists(Path.Combine((string)json["FFmpeg"]["FolderPath"],"ffmpeg.exe")),"Bundled FFmpeg path");
  Assert(Captura.FFmpegService.FFmpegExists && Captura.FFmpegService.FFmpegExePath==Path.Combine(app,"ffmpeg.exe"),"Real encoder fallback resolves from entry assembly directory");
  Assert((string)json["Video"]["SourceKind"]=="FullScreen","Fullscreen default");
  Assert(File.ReadAllText(Path.Combine(profile,"Hotkeys.json"))=="[]","Mouse-only default");
  string before=File.ReadAllText(config);Bootstrap.InitializeAt(app,profile,"IGNORED");Assert(before==File.ReadAllText(config),"Existing preferences preserved");
  json["FFmpeg"]["FolderPath"]=Path.Combine(root,"missing");json["Video"]["FrameRate"]=7;File.WriteAllText(config,json.ToString());
  Bootstrap.InitializeAt(app,profile,"IGNORED");json=JObject.Parse(File.ReadAllText(config));Assert((int)json["Video"]["FrameRate"]==7 && (string)json["FFmpeg"]["FolderPath"]==Path.GetFullPath(app),"Encoder relocation preserves other preferences");
  File.WriteAllText(config,"{BROKEN");Bootstrap.InitializeAt(app,profile,Path.Combine(root,"录像"));
  Assert(Directory.GetFiles(profile,"Captura.json.corrupt-*").Length==1 && JObject.Parse(File.ReadAllText(config))!=null,"Corrupt preferences retained and recovered");
  string source=Path.Combine(root,"original.mp4"),target=Path.Combine(root,"saved.mp4");
  File.WriteAllText(source,"NEW RECORDING");File.WriteAllText(target,"OLD TARGET");
  using(var locked=File.Open(target,FileMode.Open,FileAccess.Read,FileShare.None)){
   bool failed=false;try{SafeRecordingSave.Move(source,target);}catch(IOException){failed=true;}
   Assert(failed && File.ReadAllText(source)=="NEW RECORDING","Locked target retains original recording");
  }
  Assert(File.ReadAllText(target)=="OLD TARGET","Failed replacement retains old target");
  SafeRecordingSave.Move(source,target);Assert(!File.Exists(source) && File.ReadAllText(target)=="NEW RECORDING","Committed replacement");
  SafeRecordingSave.Move(target,target);Assert(File.ReadAllText(target)=="NEW RECORDING","Same-path no-op");
  if(args.Length>1){string cross=Path.Combine(Path.GetFullPath(args[1]),"录屏跨盘-"+Guid.NewGuid().ToString("N")+".mp4");Directory.CreateDirectory(Path.GetDirectoryName(cross));try{SafeRecordingSave.Move(target,cross);Assert(!File.Exists(target) && File.ReadAllText(cross)=="NEW RECORDING","Cross-volume save");}finally{if(File.Exists(cross))File.Delete(cross);}}
  Assert(Directory.GetFiles(root,"*.wxc-pending",SearchOption.AllDirectories).Length==0,"No abandoned pending files");
  Console.WriteLine("PASS: clean profile, Chinese startup, tray close, bundled FFmpeg, existing preferences, relocation, corrupted-profile recovery, locked-target data preservation, replacement and cross-volume save.");
 }
}
