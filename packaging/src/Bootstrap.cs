using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace CapturaDistribution {
 public static class Bootstrap {
  public static void Initialize() {
   string profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"CapturaWXC");
   string docs=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
   InitializeAt(AppDomain.CurrentDomain.BaseDirectory,profile,Path.Combine(docs,"Captura-WXC"));
   Captura.ServiceProvider.SettingsDir=profile;
  }
  public static void InitializeAt(string app,string profile,string output) {
   Directory.CreateDirectory(profile);
   string path=Path.Combine(profile,"Captura.json");
   JObject saved=null;
   if(File.Exists(path)) {
    try{saved=JObject.Parse(File.ReadAllText(path));}
    catch(Newtonsoft.Json.JsonException){
     // Keep the broken file for recovery instead of crashing every launch.
     File.Move(path,path+".corrupt-"+Guid.NewGuid().ToString("N"));
    }
   }
   if(saved!=null) {
    // Preserve existing preferences. Relocation only repairs a missing encoder.
    string old=(string)saved.SelectToken("FFmpeg.FolderPath");
    if(String.IsNullOrWhiteSpace(old) || !File.Exists(Path.Combine(old,"ffmpeg.exe"))) {
     var ff=saved["FFmpeg"] as JObject;if(ff==null){ff=new JObject();saved["FFmpeg"]=ff;}
     ff["FolderPath"]=Path.GetFullPath(app);WriteAtomic(path,saved.ToString());
    }
    return;
   }
   var config=new JObject {
    {"UI",new JObject{{"Language","zh-CN"},{"MinimizeOnStart",false},{"MainWindowLeft",50},{"MainWindowTop",50}}},
    {"Tray",new JObject{{"MinToTrayOnStartup",false},{"MinToTrayOnClose",true}}},
    {"Video",new JObject{{"WriterKind","FFmpeg"},{"Writer","Mp4 (x264 | AAC)"},{"SourceKind","FullScreen"},{"FrameRate",10},{"FpsLimit",true},{"Quality",70}}},
    {"FFmpeg",new JObject{{"FolderPath",Path.GetFullPath(app)},{"Resize",false},{"X264",new JObject{{"Preset","ultrafast"},{"PixelFormat","yuv420p"}}}}},
    {"Audio",new JObject{{"Enabled",false},{"Microphones",new JArray()},{"Speakers",new JArray()}}},
    {"OutPath",output}
   };
   WriteAtomic(path,config.ToString());
   string hotkeys=Path.Combine(profile,"Hotkeys.json");if(!File.Exists(hotkeys))File.WriteAllText(hotkeys,"[]");
  }
  static void WriteAtomic(string path,string text) {
   string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try {File.WriteAllText(temp,text,new System.Text.UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
   finally {if(File.Exists(temp))File.Delete(temp);}
  }
 }
}
