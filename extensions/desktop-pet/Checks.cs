using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Linq;
using CapturaDesktopPet;
public class Command : ICommand {readonly Action action;public Command(Action a){action=a;}public event EventHandler CanExecuteChanged{add{}remove{}}public bool CanExecute(object p){return true;}public void Execute(object p){action();}}
public class FakeVideo {public int FrameRate=6;public string Source="DISPLAY2";}
public class FakeRecorder {
 public string RecorderState{get;set;}public TimeSpan TimeSpan{get;set;}public int RunningStopRecordingCount{get;set;}public int Countdown{get;set;}
 public ICommand RecordCommand{get;set;}public ICommand PauseCommand{get;set;}
}
public class FakeScreen {public ICommand ScreenShotCommand{get;set;}}
public class FakeMain {public FakeRecorder RecordingViewModel{get;set;}public FakeScreen ScreenShotViewModel{get;set;}public FakeVideo VideoViewModel{get;set;}}
class Checks {
 static void Need(bool b,string m){if(!b)throw new Exception(m);}
 static BitmapSource Render(UIElement root,string path,int width,int height){root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(root);if(path!=null)Save(image,path);return image;}
 static void Save(BitmapSource image,string path){var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))png.Save(file);}
 static void RenderCompact(UIElement root,string path){var image=Render(root,null,384,284);Save(new CroppedBitmap(image,new Int32Rect(124,124,136,136)),path);}
 [STAThread]static int Main(){try{
  var main=new FakeMain{RecordingViewModel=new FakeRecorder{RecorderState="NotRecording"},ScreenShotViewModel=new FakeScreen(),VideoViewModel=new FakeVideo()};
  int starts=0,stops=0,pauses=0,shots=0,fps=0;string selected="";
  main.RecordingViewModel.RecordCommand=new Command(delegate{if(main.RecordingViewModel.RecorderState=="NotRecording"){starts++;fps=main.VideoViewModel.FrameRate;selected=main.VideoViewModel.Source;main.RecordingViewModel.RecorderState="Recording";}else{stops++;main.RecordingViewModel.RecorderState="NotRecording";}});
  main.RecordingViewModel.PauseCommand=new Command(delegate{pauses++;main.RecordingViewModel.RecorderState=main.RecordingViewModel.RecorderState=="Paused"?"Recording":"Paused";});main.ScreenShotViewModel.ScreenShotCommand=new Command(delegate{shots++;});
  var adapter=new PetAdapter(()=>main);Need(adapter.Start() && !adapter.Start() && starts==1,"Double start was not guarded");Need(fps==6 && selected=="DISPLAY2","Main settings not used");
  main.RecordingViewModel.TimeSpan=TimeSpan.FromSeconds(65);Need(adapter.Snapshot().TimeText=="00:01:05","Live elapsed time incorrect");Need(adapter.PauseResume() && main.RecordingViewModel.RecorderState=="Paused","Pause failed");Need(adapter.PauseResume() && pauses==2,"Resume failed");Need(adapter.Stop() && !adapter.Stop() && stops==1,"Stop isolation failed");Need(!adapter.PauseResume(),"Idle pause allowed");
  main.VideoViewModel.FrameRate=24;main.VideoViewModel.Source="DISPLAY1";Need(adapter.Start() && fps==24 && selected=="DISPLAY1","Updated main settings not immediately shared");adapter.Stop();main.RecordingViewModel.RunningStopRecordingCount=1;Need(!adapter.Start(),"Start allowed while saving");main.RecordingViewModel.RunningStopRecordingCount=0;
  adapter.ScreenshotAsync().GetAwaiter().GetResult();Need(shots==1,"Screenshot did not use main command");
  Vector gaze=MascotView.Gaze(new Vector(10000,-10000),6);Need(gaze.X>0 && gaze.Y<0 && gaze.Length<=6.001,"Eye tracking bounds/direction failed");Need(MascotView.Gaze(new Vector(),6).Length==0,"Centered eyes not neutral");
  var owner=new Window();var caption=new StackPanel{Orientation=Orientation.Horizontal};var minimize=new Button{ToolTip="最小化"};caption.Children.Add(minimize);caption.Children.Add(new Button{ToolTip="关闭"});caption.Measure(new Size(400,50));caption.Arrange(new Rect(0,0,400,50));
  var controller=new PetController(owner);controller.AddEntryTo(caption);Need(caption.Children.Count==3 && ((Button)caption.Children[0]).ToolTip.ToString()=="桌宠快捷控制" && caption.Children[1]==minimize,"Pet entry was not inserted left of minimize");controller.AddEntryTo(caption);Need(caption.Children.Count==3,"Duplicate entry inserted");controller.Dispose();owner.Close();
  main.RecordingViewModel.RecorderState="Recording";main.RecordingViewModel.TimeSpan=TimeSpan.FromMinutes(12)+TimeSpan.FromSeconds(34);var pet=new PetWindow(adapter,delegate{},false);pet.SetExpanded(true);pet.Refresh();
  Need(pet.Mascot.Recording && pet.Mascot.DisplayedTime=="00:12:34","Face did not show active recording time");
  var palette=(Canvas)pet.PreviewRoot;Need(palette.Children.OfType<PixelActionButton>().Count()==4,"Palette does not have exactly four action buttons");Need(!palette.Children.OfType<Border>().Any(),"Separate timer/badge remains");
  double width=pet.Width,height=pet.Height,left=pet.Left,top=pet.Top,mx=Canvas.GetLeft(pet.Mascot),my=Canvas.GetTop(pet.Mascot);
  var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<100;i++)pet.SetExpanded(i%2==0);watch.Stop();
  Need(pet.Width==width && pet.Height==height && pet.Left==left && pet.Top==top && Canvas.GetLeft(pet.Mascot)==mx && Canvas.GetTop(pet.Mascot)==my,"Toggling moved or resized the layered window/mascot");Need(pet.Mascot.Width==112 && pet.Mascot.Height==112,"Mascot was not reduced to 112 pixels");pet.SetExpanded(true);
  Console.WriteLine("100 visibility-only toggles: "+watch.Elapsed.TotalMilliseconds.ToString("0.0")+" ms; window bounds and avatar position unchanged.");
  Point first=OrbitArc.CenterAt(-155),second=OrbitArc.CenterAt(-115),third=OrbitArc.CenterAt(-65),last=OrbitArc.CenterAt(-25);Need(first.X<second.X && second.X<third.X && third.X<last.X && second.Y<first.Y && third.Y<last.Y,"Actions are not on an upper curved arc");
  Need(pet.EndRecording(),"Pet stop request failed");Need(pet.Mascot.DisplayedTime=="00:00:00" && !pet.Mascot.Recording && !pet.Mascot.Paused,"Stopping did not zero the face and restore idle expression");Need(main.RecordingViewModel.TimeSpan.TotalSeconds==754,"Face reset changed the recording metadata duration");
  main.RecordingViewModel.RunningStopRecordingCount=1;pet.Refresh();Need(pet.Mascot.DisplayedTime=="00:00:00","Saving redisplayed the stale elapsed time");main.RecordingViewModel.RunningStopRecordingCount=0;
  Need(pet.BeginRecording() && pet.Mascot.DisplayedTime=="00:00:00","New recording displayed the previous session time");main.RecordingViewModel.TimeSpan=TimeSpan.FromSeconds(1);pet.Refresh();Need(pet.Mascot.DisplayedTime=="00:00:01","Fresh recording clock did not advance");adapter.PauseResume();pet.Refresh();Need(pet.Mascot.Paused && pet.Mascot.DisplayedTime=="00:00:01","Paused clock was not preserved");pet.EndRecording();
  var originalCommand=main.RecordingViewModel.RecordCommand;main.RecordingViewModel.RecorderState="Recording";main.RecordingViewModel.RecordCommand=new Command(delegate{});pet.Refresh();Need(pet.EndRecording() && pet.Mascot.DisplayedTime=="00:00:00" && !pet.Mascot.Recording,"Async stop did not zero the face immediately");main.RecordingViewModel.RecorderState="NotRecording";pet.Refresh();main.RecordingViewModel.RecordCommand=originalCommand;
  main.RecordingViewModel.RecorderState="Recording";main.RecordingViewModel.TimeSpan=TimeSpan.FromMinutes(12)+TimeSpan.FromSeconds(34);pet.Refresh();
  var root=pet.PreviewRoot;pet.Content=null;var background=new Border{Background=new SolidColorBrush(Color.FromRgb(235,242,247)),Child=root,Padding=new Thickness(12)};
  var scene=Render(background,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pet-expanded-preview.png"),384,284);
  var face=new CroppedBitmap(scene,new Int32Rect(136,136,112,112));var pixels=new byte[112*112*4];face.CopyPixels(pixels,112*4,0);int letters=0,numbers=0;for(int y=40;y<61;y++)for(int x=25;x<80;x++){int at=(y*112+x)*4;if(pixels[at]>210 && pixels[at+1]>210 && pixels[at+2]>210){if(y<47)letters++;else numbers++;}}
  Need(letters==0 && numbers>20,"Face still contains a status-text row or is missing the numeric clock");
  pet.SetExpanded(false);var transparent=new RenderTargetBitmap(360,260,96,96,PixelFormats.Pbgra32);transparent.Render(root);var alpha=new byte[360*260*4];transparent.CopyPixels(alpha,360*4,0);Need(alpha[3]==0 && alpha[alpha.Length-1]==0,"Hidden palette leaves opaque desktop-blocking pixels");
  RenderCompact(background,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pet-collapsed-preview.png"));main.RecordingViewModel.RecorderState="NotRecording";pet.Refresh();pet.Mascot.EyeX=3;pet.Mascot.EyeY=-2;RenderCompact(background,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pet-idle-preview.png"));pet.DisposePet();
  Console.WriteLine("PASS: numeric-only face, smaller mascot, no resize/move on toggle, transparent hidden palette, four pixel actions, clock reset and restart, metadata preservation, live configuration, screenshot and eye tracking. No desktop input or recording.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
