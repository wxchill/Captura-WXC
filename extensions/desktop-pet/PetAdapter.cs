using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CapturaDesktopPet {
 public sealed class RecorderSnapshot {
  public string State="Unavailable";public TimeSpan Elapsed;public int Saving,Countdown;
  public bool CanStart,CanPause,CanStop;
  public string TimeText {get{return ((int)Elapsed.TotalHours).ToString("00")+Elapsed.ToString(@"\:mm\:ss");}}
 }
 public sealed class PetAdapter {
  readonly Func<object> main;
  public PetAdapter(Func<object> currentMain){main=currentMain;}
  public static object Prop(object target,string name){if(target==null)return null;PropertyInfo p=target.GetType().GetProperty(name);return p==null?null:p.GetValue(target,null);}
  object Recorder {get{return Prop(main(),"RecordingViewModel");}}
  static bool Enabled(ICommand command){return command!=null && command.CanExecute(null);}
  public RecorderSnapshot Snapshot(){
   var view=new RecorderSnapshot();object recorder=Recorder;if(recorder==null)return view;
   object state=Prop(recorder,"RecorderState");if(state==null)return view;view.State=state.ToString();
   object span=Prop(recorder,"TimeSpan");if(span is TimeSpan)view.Elapsed=(TimeSpan)span;
   object saving=Prop(recorder,"RunningStopRecordingCount"),countdown=Prop(recorder,"Countdown");
   view.Saving=saving is int?(int)saving:0;view.Countdown=countdown is int?(int)countdown:0;
   bool record=Enabled(Prop(recorder,"RecordCommand") as ICommand),pause=Enabled(Prop(recorder,"PauseCommand") as ICommand);
   view.CanStart=view.State=="NotRecording" && view.Saving==0 && record;
   view.CanStop=(view.State=="Recording" || view.State=="Paused") && record;
   view.CanPause=(view.State=="Recording" || view.State=="Paused") && view.Countdown==0 && pause;
   return view;
  }
  public bool Start(){if(!Snapshot().CanStart)return false;return Execute(Prop(Recorder,"RecordCommand") as ICommand);}
  public bool Stop(){if(!Snapshot().CanStop)return false;return Execute(Prop(Recorder,"RecordCommand") as ICommand);}
  public bool PauseResume(){if(!Snapshot().CanPause)return false;return Execute(Prop(Recorder,"PauseCommand") as ICommand);}
  static bool Execute(ICommand command){if(!Enabled(command))return false;command.Execute(null);return true;}
  public async Task ScreenshotAsync(){
   object shot=Prop(main(),"ScreenShotViewModel");
   if(!Execute(Prop(shot,"ScreenShotCommand") as ICommand))throw new InvalidOperationException("截图命令暂不可用，请返回主页面检查。");
   Type bridge=Type.GetType("CapturaScreenshotBridge.Launcher, Captura.ScreenshotBridge",false);
   PropertyInfo selecting=bridge==null?null:bridge.GetProperty("IsSelecting",BindingFlags.Public|BindingFlags.Static);
   DateTime end=DateTime.UtcNow.AddMinutes(5);
   while(selecting!=null && (bool)selecting.GetValue(null,null) && DateTime.UtcNow<end)await Task.Delay(100);
  }
 }
}
