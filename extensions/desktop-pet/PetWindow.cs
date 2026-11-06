using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace CapturaDesktopPet {
 // Pure geometry: dragging clamps only the avatar, while the palette turns
 // inward and adjusts at corners without resizing the transparent window.
 public sealed class PetLayout {
  public static readonly double[] Angles={-155,-115,-65,-25};
  public Point Anchor;public double Rotation,Corner,Scale=1;public Vector Offset;
  public Point ButtonCenter(int index){return OrbitArc.CenterAt(Angles[index],Rotation,Corner,Scale)+Offset;}
  public Rect ButtonBounds(int index){Point p=ButtonCenter(index);return new Rect(Anchor.X+p.X-OrbitArc.Origin-52,Anchor.Y+p.Y-OrbitArc.Origin-21,104,42);}
  static double Clamp(double value,double low,double high){return Math.Max(low,Math.Min(high,value));}
  public static PetLayout Calculate(Point wanted,Rect work){
   var result=new PetLayout{Anchor=new Point(Clamp(wanted.X,work.Left+56,work.Right-56),Clamp(wanted.Y,work.Top+56,work.Bottom-56))};
   double x=result.Anchor.X,y=result.Anchor.Y;
   bool left=x-work.Left<=180,right=work.Right-x<=180,top=y-work.Top<=150,bottom=work.Bottom-y<=150;
   if((left || right) && (top || bottom)){
    result.Corner=1;
    result.Rotation=top?(left?135:-135):(left?45:-45);
   }else result.Rotation=y-work.Top<=130?180:left?90:right?-90:0;
   Rect bounds=Rect.Empty;
   for(int i=0;i<Angles.Length;i++)bounds.Union(result.ButtonBounds(i));
   double dx=Clamp(0,work.Left-bounds.Left,work.Right-bounds.Right),dy=Clamp(0,work.Top-bounds.Top,work.Bottom-bounds.Bottom);
   var avatar=new Rect(x-56,y-56,112,112);
   bool overlap=false;for(int i=0;i<Angles.Length;i++){Rect b=result.ButtonBounds(i);b.Offset(dx,dy);if(b.IntersectsWith(avatar))overlap=true;}
   if(overlap){
    if(result.Rotation==90)dx=Math.Max(dx,avatar.Right+8-bounds.Left);
    else if(result.Rotation==-90)dx=Math.Min(dx,avatar.Left-8-bounds.Right);
    else if(result.Rotation==180)dy=Math.Max(dy,avatar.Bottom+8-bounds.Top);
    else dy=Math.Min(dy,avatar.Top-8-bounds.Bottom);
   }
   result.Offset=new Vector(Clamp(dx,work.Left-bounds.Left,work.Right-bounds.Right),Clamp(dy,work.Top-bounds.Top,work.Bottom-bounds.Bottom));
   return result;
  }
  public static PetLayout Interpolate(PetLayout from,PetLayout to,double progress){
   double amount=Math.Max(0,Math.Min(1,progress));
   double delta=(to.Rotation-from.Rotation)%360;if(delta>180)delta-=360;if(delta< -180)delta+=360;
   var frame=new PetLayout{Anchor=to.Anchor,Rotation=from.Rotation+delta*amount,Corner=from.Corner+(to.Corner-from.Corner)*amount,Offset=from.Offset+(to.Offset-from.Offset)*amount};
   // Horizontal labels remain upright. Widen the radius only when their
   // projected rectangles would overlap while the arc is turning.
   double needed=1;
   for(int i=0;i<Angles.Length;i++)for(int j=0;j<i;j++){
    Vector d=frame.ButtonCenter(i)-frame.ButtonCenter(j);
    needed=Math.Max(needed,Math.Min(106/Math.Max(.001,Math.Abs(d.X)),44/Math.Max(.001,Math.Abs(d.Y))));
   }
   frame.Scale=needed;return frame;
  }
 }
 public sealed class PetWindow : Window {
  readonly PetAdapter adapter;readonly Action openMain;
  readonly Canvas canvas=new Canvas();readonly MascotView mascot=new MascotView();
  readonly PixelActionButton screenshot,start,pause,stop;readonly OrbitArc orbit=new OrbitArc();readonly TextBlock status=new TextBlock();
  bool stopRequested,awaitNewClock;TimeSpan oldClock;
  readonly DispatcherTimer timer,motionTimer;readonly bool animationsEnabled;PetLayout shownLayout,targetLayout,fromLayout;readonly System.Diagnostics.Stopwatch motionWatch=new System.Diagnostics.Stopwatch();
  Point anchor;Point dragCursor,dragAnchor;bool dragging,moved,expanded,allowClose;int ticks;
  public FrameworkElement PreviewRoot {get{return canvas;}}
  public bool Expanded {get{return expanded;}}
  public MascotView Mascot {get{return mascot;}}
  public Func<bool> ResumeAllowed=()=>true;
  public PetWindow(PetAdapter model,Action returnMain,bool animate){
   adapter=model;openMain=returnMain;animationsEnabled=animate;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;Topmost=true;ShowInTaskbar=false;ShowActivated=false;ResizeMode=ResizeMode.NoResize;Focusable=false;
   Title="Captura 桌宠";FontFamily=new FontFamily("Microsoft YaHei");FontSize=12;UseLayoutRounding=true;
   Width=OrbitArc.SurfaceSize;Height=OrbitArc.SurfaceSize;canvas.Width=Width;canvas.Height=Height;PixelFont.WarmUp();
   Content=canvas;orbit.Width=Width;orbit.Height=Height;orbit.IsHitTestVisible=false;canvas.Children.Add(orbit);canvas.Children.Add(mascot);mascot.Width=112;mascot.Height=112;mascot.Cursor=Cursors.Hand;mascot.ToolTip="单击展开 · 按住拖动 · 右键返回主页面";
   screenshot=MakeButton("截图","shot",Capture);start=MakeButton("录屏开始","start",delegate{BeginRecording();});pause=MakeButton("录屏暂停","pause",delegate{Invoke(adapter.PauseResume);});stop=MakeButton("录屏结束","stop",delegate{EndRecording();});
   Canvas.SetLeft(mascot,OrbitArc.Origin-56);Canvas.SetTop(mascot,OrbitArc.Origin-56);
   foreach(var button in new[]{screenshot,start,pause,stop})button.Visibility=Visibility.Hidden;orbit.Visibility=Visibility.Hidden;
   mascot.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){dragging=true;moved=false;dragCursor=CursorDip();dragAnchor=anchor;mascot.CaptureMouse();e.Handled=true;};
   mascot.MouseMove+=delegate{if(!dragging)return;Vector delta=CursorDip()-dragCursor;if(delta.Length>4)moved=true;if(moved){anchor=dragAnchor+delta;Place();}};
   mascot.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){if(!dragging)return;dragging=false;mascot.ReleaseMouseCapture();if(!moved)SetExpanded(!expanded);else SavePosition();e.Handled=true;};
   mascot.LostMouseCapture+=delegate{dragging=false;};
   var menu=new ContextMenu();var mainItem=new MenuItem{Header="返回主页面"};mainItem.Click+=delegate{Hide();openMain();};menu.Items.Add(mainItem);var collapse=new MenuItem{Header="收起按钮"};collapse.Click+=delegate{SetExpanded(false);};menu.Items.Add(collapse);var hide=new MenuItem{Header="隐藏桌宠（保留系统托盘）"};hide.Click+=delegate{Hide();};menu.Items.Add(hide);mascot.ContextMenu=menu;
   timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(40)};timer.Tick+=delegate{Animate();};
   motionTimer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(16)};motionTimer.Tick+=delegate{AdvanceLayout();};
   IsVisibleChanged+=delegate{if(IsVisible){Place();if(animate)timer.Start();}else{timer.Stop();motionTimer.Stop();if(targetLayout!=null)DrawLayout(targetLayout);}};
   Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(!allowClose){e.Cancel=true;Hide();}};
   Closed+=delegate{timer.Stop();motionTimer.Stop();};PreviewKeyDown+=delegate(object s,KeyEventArgs e){e.Handled=true;};PreviewKeyUp+=delegate(object s,KeyEventArgs e){e.Handled=true;};
   anchor=new Point(SystemParameters.WorkArea.Right-190,SystemParameters.WorkArea.Bottom-95);LoadPosition();Place();Refresh();
  }
  PixelActionButton MakeButton(string text,string kind,Action action){
   var b=new PixelActionButton(text,kind);
   b.Click+=delegate{try{action();Refresh();}catch(Exception e){status.Text=e.Message;status.ToolTip=e.ToString();}};canvas.Children.Add(b);return b;
  }
  void Invoke(Func<bool> action){if(!action())status.Text="当前状态不可执行";}
  public bool BeginRecording(){oldClock=adapter.Snapshot().Elapsed;bool started=adapter.Start();if(started){stopRequested=false;awaitNewClock=true;}Refresh();return started;}
  public bool EndRecording(){bool ended=adapter.Stop();if(ended){stopRequested=true;awaitNewClock=false;}Refresh();return ended;}
  async void Capture(){
   bool restore=IsVisible;Hide();
   try{await adapter.ScreenshotAsync();}catch(Exception e){status.Text=e.Message;}
   finally{if(restore && !allowClose && ResumeAllowed()){Show();Refresh();}}
  }
  Point CursorDip(){var p=Forms.Cursor.Position;var source=PresentationSource.FromVisual(this);return source!=null && source.CompositionTarget!=null?source.CompositionTarget.TransformFromDevice.Transform(new Point(p.X,p.Y)):new Point(p.X,p.Y);}
  void Animate(){
   try{if(!mascot.Recording && !mascot.Paused){var p=Forms.Cursor.Position;Point local=mascot.PointFromScreen(new Point(p.X,p.Y));Vector gaze=MascotView.Gaze(local-new Point(54,53),3);bool blink=(DateTime.UtcNow.TimeOfDay.TotalMilliseconds%5100)<120;if(Math.Abs(mascot.EyeX-gaze.X)>.4 || Math.Abs(mascot.EyeY-gaze.Y)>.4 || mascot.Blink!=blink){mascot.EyeX=gaze.X;mascot.EyeY=gaze.Y;mascot.Blink=blink;mascot.InvalidateVisual();}}if(++ticks%6==0)Refresh();}catch{}
  }
  public void Refresh(){
   bool wasRecording=mascot.Recording,wasPaused=mascot.Paused;string oldTime=mascot.DisplayedTime;
   var view=adapter.Snapshot();if(view.State=="NotRecording")stopRequested=false;if(awaitNewClock && (view.Elapsed==TimeSpan.Zero || view.Elapsed!=oldClock))awaitNewClock=false;
   start.IsEnabled=view.CanStart;pause.IsEnabled=view.CanPause && !stopRequested;stop.IsEnabled=view.CanStop && !stopRequested;pause.Label=view.State=="Paused"?"录屏继续":"录屏暂停";pause.Kind=view.State=="Paused"?"resume":"pause";
   mascot.Recording=view.State=="Recording" && view.Countdown==0 && !stopRequested;mascot.Paused=view.State=="Paused" && !stopRequested;
   mascot.DisplayedTime=(mascot.Recording || mascot.Paused) && !awaitNewClock?view.TimeText:"00:00:00";
   status.Foreground=mascot.Recording?Brushes.Firebrick:mascot.Paused?Brushes.DarkOrange:new SolidColorBrush(Color.FromRgb(51,75,95));
   status.Text=view.Countdown>0?"倒计时 "+view.Countdown:mascot.Recording?"录制中 "+mascot.DisplayedTime:mascot.Paused?"已暂停 "+mascot.DisplayedTime:view.Saving>0?"正在保存…":view.State=="Unavailable"?"等待主页面":"待机 · 单击展开，拖动移动";string tip=status.Text+" · 右键返回主页面";if(!Object.Equals(mascot.ToolTip,tip))mascot.ToolTip=tip;
   if(wasRecording!=mascot.Recording || wasPaused!=mascot.Paused || oldTime!=mascot.DisplayedTime)mascot.InvalidateVisual();
  }
  public void SetExpanded(bool value){if(expanded==value)return;expanded=value;
   // Keep the layered window, avatar and anchor unchanged. Hidden controls keep
   // their measured layout, so toggling does not resize/move/recreate the HWND.
   foreach(var item in new UIElement[]{screenshot,start,pause,stop,orbit})item.Visibility=expanded?Visibility.Visible:Visibility.Hidden;
  }
  static void At(UIElement element,double x,double y){Canvas.SetLeft(element,x);Canvas.SetTop(element,y);}
  public void ApplyPlacement(Point desired,Rect work){
   PetLayout layout=PetLayout.Calculate(desired,work);anchor=layout.Anchor;
   Left=anchor.X-OrbitArc.Origin;Top=anchor.Y-OrbitArc.Origin;
   bool changed=targetLayout==null || targetLayout.Rotation!=layout.Rotation || targetLayout.Corner!=layout.Corner || targetLayout.Offset!=layout.Offset;
   targetLayout=layout;
   if(animationsEnabled && IsVisible && shownLayout!=null && changed){fromLayout=shownLayout;motionWatch.Restart();motionTimer.Start();}
   else if(!motionTimer.IsEnabled || !animationsEnabled || !IsVisible)DrawLayout(layout);
  }
  void AdvanceLayout(){
   double t=motionWatch.Elapsed.TotalMilliseconds/220;
   if(t>=1){motionTimer.Stop();DrawLayout(targetLayout);return;}
   DrawLayout(PetLayout.Interpolate(fromLayout,targetLayout,1-Math.Pow(1-t,3)));
  }
  void DrawLayout(PetLayout layout){
   shownLayout=layout;
   int i=0;foreach(var button in new[]{screenshot,start,pause,stop}){Point p=layout.ButtonCenter(i++);double x=Math.Round(p.X-52),y=Math.Round(p.Y-21);if(Canvas.GetLeft(button)!=x)Canvas.SetLeft(button,x);if(Canvas.GetTop(button)!=y)Canvas.SetTop(button,y);}
   orbit.SetLayout(layout.Rotation,layout.Corner,layout.Scale,layout.Offset);
  }
  void Place(){
   Rect work=SystemParameters.WorkArea;var source=PresentationSource.FromVisual(this);
   if(source!=null && source.CompositionTarget!=null){var physical=source.CompositionTarget.TransformToDevice.Transform(anchor);var area=Forms.Screen.FromPoint(new System.Drawing.Point((int)physical.X,(int)physical.Y)).WorkingArea;var a=source.CompositionTarget.TransformFromDevice.Transform(new Point(area.Left,area.Top));var b=source.CompositionTarget.TransformFromDevice.Transform(new Point(area.Right,area.Bottom));work=new Rect(a,b);}
   else {
    double sx=Forms.Screen.PrimaryScreen.Bounds.Width/SystemParameters.PrimaryScreenWidth,sy=Forms.Screen.PrimaryScreen.Bounds.Height/SystemParameters.PrimaryScreenHeight;
    var area=Forms.Screen.FromPoint(new System.Drawing.Point((int)(anchor.X*sx),(int)(anchor.Y*sy))).WorkingArea;
    work=new Rect(area.Left/sx,area.Top/sy,area.Width/sx,area.Height/sy);
   }
   ApplyPlacement(anchor,work);
  }
  string PositionPath {get{
   string directory=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Captura");
   try{Type provider=Type.GetType("Captura.ServiceProvider, Captura.Base",false);if(provider!=null)directory=Convert.ToString(provider.GetProperty("SettingsDir").GetValue(null,null));}catch{}
   System.IO.Directory.CreateDirectory(directory);return System.IO.Path.Combine(directory,"DesktopPet.position.txt");
  }}
  void SavePosition(){try{System.IO.File.WriteAllLines(PositionPath,new[]{anchor.X.ToString(System.Globalization.CultureInfo.InvariantCulture),anchor.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)});}catch{}}
  void LoadPosition(){try{string[] lines=System.IO.File.ReadAllLines(PositionPath);double x=Double.Parse(lines[0],System.Globalization.CultureInfo.InvariantCulture),y=Double.Parse(lines[1],System.Globalization.CultureInfo.InvariantCulture);if(!Double.IsNaN(x) && !Double.IsInfinity(x) && !Double.IsNaN(y) && !Double.IsInfinity(y))anchor=new Point(x,y);}catch{}}
  public void DisposePet(){allowClose=true;timer.Stop();motionTimer.Stop();Close();}
 }
}
