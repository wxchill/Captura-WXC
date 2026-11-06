using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CapturaScreenshotBridge {
 public static class Launcher {
  static int selecting;
  public static bool IsSelecting {get{return selecting!=0;}}
  static string CurrentOutputFolder(){
   try{
    Type provider=Type.GetType("Captura.ServiceProvider, Captura.Base",false),settings=Type.GetType("Captura.Settings, Captura.Core",false);
    if(provider==null || settings==null)return null;
    var value=provider.GetMethod("Get").MakeGenericMethod(settings).Invoke(null,null);
    return Convert.ToString(settings.GetProperty("OutPath").GetValue(value,null));
   }catch{return null;}
  }
  static string Quote(string value){var b=new System.Text.StringBuilder("\"");int n=0;foreach(char c in value){if(c=='\\'){n++;continue;}b.Append('\\',c=='"'?n*2+1:n);b.Append(c);n=0;}b.Append('\\',n*2);b.Append('"');return b.ToString();}
  public static Task Capture(){
   var app=Application.Current;
   if(app==null)return Task.FromResult(0);
   if(!app.Dispatcher.CheckAccess()){app.Dispatcher.BeginInvoke(new Action(()=>Capture()));return Task.FromResult(0);}
   if(Interlocked.CompareExchange(ref selecting,1,0)!=0)return Task.FromResult(0);
   Window window=app.MainWindow;bool restore=window!=null && window.IsVisible;
   string job=Path.Combine(Path.GetTempPath(),"CapturaScreenshotTools-handoff",Guid.NewGuid().ToString("N"));
   string ready=Path.Combine(job,"ready");Process process=null;
   try{
    string executable=Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),"ScreenshotTools","Captura.ScreenshotTools.exe");
    if(!File.Exists(executable))throw new FileNotFoundException("截图工具尚未安装完整。",executable);
    Directory.CreateDirectory(job);
    if(restore)window.Hide();
    string folder=CurrentOutputFolder();string arguments="--capture --handoff "+Quote(ready)+(String.IsNullOrWhiteSpace(folder)?"":" --out-folder "+Quote(folder));
    process=Process.Start(new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true});
    var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
    DateTime deadline=DateTime.UtcNow.AddMinutes(5);
    timer.Tick+=delegate{
     bool ended=false;try{ended=process.HasExited;}catch{ended=true;}
     if(!ended && !File.Exists(ready) && DateTime.UtcNow<deadline)return;
     timer.Stop();
     if(restore){bool activation=window.ShowActivated;window.ShowActivated=false;window.Show();window.ShowActivated=activation;}
     Interlocked.Exchange(ref selecting,0);
     try{if(File.Exists(ready))File.Delete(ready);Directory.Delete(job);}catch{}
     process.Dispose();
    };
    timer.Start();
   }catch(Exception e){
    if(restore)window.Show();Interlocked.Exchange(ref selecting,0);
    MessageBox.Show(window,e.Message,"截图工具",MessageBoxButton.OK,MessageBoxImage.Information);
   }
   return Task.FromResult(0);
  }
 }
}
