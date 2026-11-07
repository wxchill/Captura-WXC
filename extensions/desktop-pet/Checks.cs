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
 static readonly int surface=(int)OrbitArc.SurfaceSize,sceneSize=(int)OrbitArc.SurfaceSize+24;
 static void RenderCompact(UIElement root,string path){var image=Render(root,null,sceneSize,sceneSize);Save(new CroppedBitmap(image,new Int32Rect((int)OrbitArc.Origin-56,(int)OrbitArc.Origin-56,136,136)),path);}
 static void SavePalette(BitmapSource image,PetWindow pet,string name){
  Rect bounds=new Rect(Canvas.GetLeft(pet.Mascot),Canvas.GetTop(pet.Mascot),112,112);
  foreach(var button in ((Canvas)pet.PreviewRoot).Children.OfType<PixelActionButton>())if(button.Visibility==Visibility.Visible)bounds.Union(new Rect(Canvas.GetLeft(button),Canvas.GetTop(button),104,42));
  Save(new CroppedBitmap(image,new Int32Rect((int)Math.Floor(bounds.Left),(int)Math.Floor(bounds.Top),(int)Math.Ceiling(bounds.Width)+25,(int)Math.Ceiling(bounds.Height)+25)),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name));
 }
 static void CheckPlacement(){
  foreach(var area in new[]{new Rect(0,0,1920,1040),new Rect(-1920,-1080,1920,1040),new Rect(0,0,1280,640),new Rect(0,0,640,480),new Rect(0,0,480,320)}){
   for(double x=area.Left-100;x<=area.Right+100;x+=37)for(double y=area.Top-100;y<=area.Bottom+100;y+=31){
    var layout=PetLayout.Calculate(new Point(x,y),area);var avatar=new Rect(layout.Anchor.X-56,layout.Anchor.Y-56,112,112);Need(area.Contains(avatar),"Avatar did not stay in work area");
    for(int i=0;i<4;i++){
     Rect b=layout.ButtonBounds(i);Rect tolerant=area;tolerant.Inflate(.01,.01);Need(tolerant.Contains(b),"Button escaped work area: "+area+" "+x+","+y+" angle="+layout.Rotation+" button="+i+" "+b);
     Need(!b.IntersectsWith(avatar),"Button covers mascot: "+area+" "+x+","+y+" angle="+layout.Rotation+" button="+i);
     for(int j=0;j<i;j++)Need(!b.IntersectsWith(layout.ButtonBounds(j)),"Palette buttons overlap");
    }
   }
  }
  var work=new Rect(0,0,1920,1040);
  var left=PetLayout.Calculate(new Point(-100,520),work);var right=PetLayout.Calculate(new Point(2200,520),work);var top=PetLayout.Calculate(new Point(960,-100),work);var bottom=PetLayout.Calculate(new Point(960,2000),work);var center=PetLayout.Calculate(new Point(960,520),work);
  Need(left.Anchor.X==56 && right.Anchor.X==1864 && top.Anchor.Y==56 && bottom.Anchor.Y==984,"Avatar did not reach all four edges");
  Need(left.Rotation==90 && right.Rotation==-90 && top.Rotation==180 && bottom.Rotation==0 && center.Rotation==0,"Side rotation or default reset incorrect");
  var corners=new[]{PetLayout.Calculate(new Point(0,0),work),PetLayout.Calculate(new Point(1920,0),work),PetLayout.Calculate(new Point(0,1040),work),PetLayout.Calculate(new Point(1920,1040),work)};
  for(int k=0;k<4;k++){
   var c=corners[k];Need(c.Corner==1,"Corner did not choose a quarter circle");
   Vector a=c.ButtonCenter(0)-new Point(OrbitArc.Origin,OrbitArc.Origin)-c.Offset,b=c.ButtonCenter(3)-new Point(OrbitArc.Origin,OrbitArc.Origin)-c.Offset;
   Need(Math.Abs(Vector.AngleBetween(a,b)-90)<.001,"Corner arc does not span 90 degrees");
   foreach(int i in new[]{0,1,2,3}){Vector v=c.ButtonCenter(i)-new Point(OrbitArc.Origin,OrbitArc.Origin)-c.Offset;Need((k%2==0?v.X>=-.01:v.X<=.01) && (k<2?v.Y>=-.01:v.Y<=.01),"Corner points outside inward quadrant");}
  }
  var midway=PetLayout.Interpolate(center,right,.5);Need(midway.Rotation==-45,"Side transition is not rotating");var wrap=PetLayout.Interpolate(top,corners[1],.5);Need(wrap.Rotation==202.5,"Corner rotation took the long way around");
  foreach(var from in corners.Concat(new[]{left,right,top,bottom})){
   var finish=PetLayout.Interpolate(from,center,1);Need(finish.Rotation%360==0 && finish.Corner==0,"Pulling away did not restore top arc");
   for(double t=0;t<=1;t+=.05){var frame=PetLayout.Interpolate(from,center,t);for(int i=0;i<4;i++){Point p=frame.ButtonCenter(i);Need(p.X>=52 && p.X<=surface-52 && p.Y>=21 && p.Y<=surface-21,"Animation exceeded constant transparent surface");}}
  }
  var states=corners.Concat(new[]{left,right,top,bottom,center}).ToArray();
  foreach(var from in states)foreach(var to in states)for(double t=0;t<=1;t+=.025){var frame=PetLayout.Interpolate(from,to,t);for(int i=0;i<4;i++){
   Point p=frame.ButtonCenter(i);Rect b=new Rect(p.X-52,p.Y-21,104,42);Need(b.Left>=0 && b.Right<=surface && b.Top>=0 && b.Bottom<=surface,"Turning animation clipped by its window");
   for(int j=0;j<i;j++){Point q=frame.ButtonCenter(j);Need(!b.IntersectsWith(new Rect(q.X-52,q.Y-21,104,42)),"Buttons overlap while rolling");}
  }}
  Console.WriteLine("PASS: four reachable edges, clockwise/counterclockwise side turns, four inward 90-degree corner fans, short-path interpolation and return to top; all buttons visible and separated on tested work areas.");
 }
 [STAThread]static int Main(){try{
  CheckPlacement();
  var revealState=new PaletteReveal();revealState.SetTarget(true,0);Need(revealState.IsAnimating && revealState.Value==0,"Opening appeared instantly");revealState.Advance(90);double openMid=revealState.Value;Need(openMid>0 && openMid<1,"Missing outward transition");
  revealState.SetTarget(false,90);Need(revealState.Value==openMid,"Reversing a click jumped to an endpoint");revealState.Advance(115);double closeMid=revealState.Value;Need(closeMid>0 && closeMid<openMid,"Missing inward transition");
  revealState.SetTarget(true,115);Need(revealState.Value==closeMid,"Reopening during retraction jumped");revealState.Advance(1000);Need(revealState.Value==1 && !revealState.IsAnimating,"Opening did not settle");
  for(int i=0;i<100;i++){revealState.SetTarget(i%2==0,1010+i*7);Need(revealState.Value>=0 && revealState.Value<=1,"Rapid toggling produced invalid progress");}revealState.Advance(3000);Need(revealState.Value==0 && !revealState.IsAnimating,"Rapid toggles did not finish retracted");
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
  main.RecordingViewModel.RecorderState="Recording";main.RecordingViewModel.TimeSpan=TimeSpan.FromMinutes(12)+TimeSpan.FromSeconds(34);var pet=new PetWindow(adapter,delegate{},false);pet.ApplyPlacement(new Point(960,520),new Rect(0,0,1920,1040));pet.SetExpanded(true);pet.Refresh();
  Need(pet.Mascot.Recording && pet.Mascot.DisplayedTime=="00:12:34","Face did not show active recording time");
  var palette=(Canvas)pet.PreviewRoot;Need(palette.Children.OfType<PixelActionButton>().Count()==4,"Palette does not have exactly four action buttons");Need(!palette.Children.OfType<Border>().Any(),"Separate timer/badge remains");
  double width=pet.Width,height=pet.Height,left=pet.Left,top=pet.Top,mx=Canvas.GetLeft(pet.Mascot),my=Canvas.GetTop(pet.Mascot);
  var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<100;i++)pet.SetExpanded(i%2==0);watch.Stop();
  Need(pet.Width==width && pet.Height==height && pet.Left==left && pet.Top==top && Canvas.GetLeft(pet.Mascot)==mx && Canvas.GetTop(pet.Mascot)==my,"Toggling moved or resized the layered window/mascot");Need(pet.Mascot.Width==112 && pet.Mascot.Height==112,"Mascot was not reduced to 112 pixels");pet.SetExpanded(true);
  Console.WriteLine("100 expand/collapse toggles: "+watch.Elapsed.TotalMilliseconds.ToString("0.0")+" ms; window bounds and avatar position unchanged.");
  // Exercise a partially visible frame without showing any desktop window.
  var liveReveal=(PaletteReveal)typeof(PetWindow).GetField("reveal",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(pet);
  var redraw=typeof(PetWindow).GetMethod("DrawPalette",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
  liveReveal.Snap(false);liveReveal.SetTarget(true,0);liveReveal.Advance(65);redraw.Invoke(pet,null);
  foreach(var button in palette.Children.OfType<PixelActionButton>()){
   Point center=button.RenderTransform.Transform(new Point(52,21));center.Offset(Canvas.GetLeft(button),Canvas.GetTop(button));
   Point expected=new Point(OrbitArc.Origin+(Canvas.GetLeft(button)+52-OrbitArc.Origin)*liveReveal.Value,OrbitArc.Origin+(Canvas.GetTop(button)+21-OrbitArc.Origin)*liveReveal.Value);
   Need((center-expected).Length<.01 && button.Opacity>0 && button.Opacity<1 && !button.IsHitTestVisible,"Button did not emerge from center or became clickable while moving");
  }
  Need(Panel.GetZIndex(pet.Mascot)>Panel.GetZIndex(palette.Children.OfType<PixelActionButton>().First()),"Emerging buttons cover mascot instead of appearing from behind it");
  liveReveal.Snap(true);redraw.Invoke(pet,null);Need(palette.Children.OfType<PixelActionButton>().All(b=>b.IsHitTestVisible && b.Opacity==1),"Settled buttons are not usable");
  Point first=OrbitArc.CenterAt(-155),second=OrbitArc.CenterAt(-115),third=OrbitArc.CenterAt(-65),last=OrbitArc.CenterAt(-25);Need(first.X<second.X && second.X<third.X && third.X<last.X && second.Y<first.Y && third.Y<last.Y,"Actions are not on an upper curved arc");
  Need(pet.EndRecording(),"Pet stop request failed");Need(pet.Mascot.DisplayedTime=="00:00:00" && !pet.Mascot.Recording && !pet.Mascot.Paused,"Stopping did not zero the face and restore idle expression");Need(main.RecordingViewModel.TimeSpan.TotalSeconds==754,"Face reset changed the recording metadata duration");
  main.RecordingViewModel.RunningStopRecordingCount=1;pet.Refresh();Need(pet.Mascot.DisplayedTime=="00:00:00","Saving redisplayed the stale elapsed time");main.RecordingViewModel.RunningStopRecordingCount=0;
  Need(pet.BeginRecording() && pet.Mascot.DisplayedTime=="00:00:00","New recording displayed the previous session time");main.RecordingViewModel.TimeSpan=TimeSpan.FromSeconds(1);pet.Refresh();Need(pet.Mascot.DisplayedTime=="00:00:01","Fresh recording clock did not advance");adapter.PauseResume();pet.Refresh();Need(pet.Mascot.Paused && pet.Mascot.DisplayedTime=="00:00:01","Paused clock was not preserved");pet.EndRecording();
  var originalCommand=main.RecordingViewModel.RecordCommand;main.RecordingViewModel.RecorderState="Recording";main.RecordingViewModel.RecordCommand=new Command(delegate{});pet.Refresh();Need(pet.EndRecording() && pet.Mascot.DisplayedTime=="00:00:00" && !pet.Mascot.Recording,"Async stop did not zero the face immediately");main.RecordingViewModel.RecorderState="NotRecording";pet.Refresh();main.RecordingViewModel.RecordCommand=originalCommand;
  main.RecordingViewModel.RecorderState="Recording";main.RecordingViewModel.TimeSpan=TimeSpan.FromMinutes(12)+TimeSpan.FromSeconds(34);pet.Refresh();
  var root=pet.PreviewRoot;pet.Content=null;var background=new Border{Background=new SolidColorBrush(Color.FromRgb(235,242,247)),Child=root,Padding=new Thickness(12)};
  var scene=Render(background,null,sceneSize,sceneSize);SavePalette(scene,pet,"pet-expanded-preview.png");
  liveReveal.Snap(false);liveReveal.SetTarget(true,0);liveReveal.Advance(30);redraw.Invoke(pet,null);SavePalette(Render(background,null,sceneSize,sceneSize),pet,"pet-pop-out-preview.png");liveReveal.Advance(260);liveReveal.SetTarget(false,300);liveReveal.Advance(435);redraw.Invoke(pet,null);SavePalette(Render(background,null,sceneSize,sceneSize),pet,"pet-retract-preview.png");liveReveal.Snap(true);redraw.Invoke(pet,null);
  var face=new CroppedBitmap(scene,new Int32Rect((int)OrbitArc.Origin-44,(int)OrbitArc.Origin-44,112,112));var pixels=new byte[112*112*4];face.CopyPixels(pixels,112*4,0);int letters=0,numbers=0;for(int y=40;y<61;y++)for(int x=25;x<80;x++){int at=(y*112+x)*4;if(pixels[at]>210 && pixels[at+1]>210 && pixels[at+2]>210){if(y<47)letters++;else numbers++;}}
  Need(letters==0 && numbers>20,"Face still contains a status-text row or is missing the numeric clock");
  pet.SetExpanded(false);var transparent=new RenderTargetBitmap(surface,surface,96,96,PixelFormats.Pbgra32);transparent.Render(root);var alpha=new byte[surface*surface*4];transparent.CopyPixels(alpha,surface*4,0);Need(alpha[3]==0 && alpha[alpha.Length-1]==0,"Hidden palette leaves opaque desktop-blocking pixels");
  RenderCompact(background,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pet-collapsed-preview.png"));main.RecordingViewModel.RecorderState="NotRecording";pet.Refresh();pet.Mascot.EyeX=3;pet.Mascot.EyeY=-2;RenderCompact(background,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pet-idle-preview.png"));
  pet.SetExpanded(true);string[] names={"left","right","top","bottom","top-left","top-right","bottom-left","bottom-right"};Point[] positions={new Point(0,520),new Point(1920,520),new Point(960,0),new Point(960,1040),new Point(0,0),new Point(1920,0),new Point(0,1040),new Point(1920,1040)};
  for(int i=0;i<positions.Length;i++){pet.ApplyPlacement(positions[i],new Rect(0,0,1920,1040));SavePalette(Render(background,null,sceneSize,sceneSize),pet,"pet-"+names[i]+"-preview.png");}
  pet.DisposePet();
  Console.WriteLine("PASS: center pop-out/retraction, continuous reversal, safe hit testing, numeric face, stable window, edge/corner rotation, clock reset, metadata, live settings and screenshot. No desktop input or recording.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
