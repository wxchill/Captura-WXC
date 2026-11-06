using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace CapturaDesktopPet {
 public sealed class PetWindow : Window {
  readonly PetAdapter adapter;readonly Action openMain;
  readonly Canvas canvas=new Canvas();readonly MascotView mascot=new MascotView();
  readonly PixelActionButton screenshot,start,pause,stop;readonly OrbitArc orbit=new OrbitArc();readonly TextBlock status=new TextBlock();
  bool stopRequested,awaitNewClock;TimeSpan oldClock;
  readonly DispatcherTimer timer;Point anchor;Point dragCursor,dragAnchor;bool dragging,moved,expanded,allowClose;int ticks;
  public FrameworkElement PreviewRoot {get{return canvas;}}
  public bool Expanded {get{return expanded;}}
  public MascotView Mascot {get{return mascot;}}
  public Func<bool> ResumeAllowed=()=>true;
  public PetWindow(PetAdapter model,Action returnMain,bool animate){
   adapter=model;openMain=returnMain;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=null;Topmost=true;ShowInTaskbar=false;ShowActivated=false;ResizeMode=ResizeMode.NoResize;Focusable=false;
   Title="Captura 桌宠";FontFamily=new FontFamily("Microsoft YaHei");FontSize=12;UseLayoutRounding=true;
   Width=360;Height=260;canvas.Width=360;canvas.Height=260;PixelFont.WarmUp();
   Content=canvas;orbit.Width=360;orbit.Height=260;orbit.IsHitTestVisible=false;canvas.Children.Add(orbit);canvas.Children.Add(mascot);mascot.Width=112;mascot.Height=112;mascot.Cursor=Cursors.Hand;mascot.ToolTip="单击展开 · 按住拖动 · 右键返回主页面";
   screenshot=MakeButton("截图","shot",Capture);start=MakeButton("录屏开始","start",delegate{BeginRecording();});pause=MakeButton("录屏暂停","pause",delegate{Invoke(adapter.PauseResume);});stop=MakeButton("录屏结束","stop",delegate{EndRecording();});
   Canvas.SetLeft(mascot,124);Canvas.SetTop(mascot,124);
   int buttonIndex=0;foreach(var button in new[]{screenshot,start,pause,stop}){Point p=OrbitArc.CenterAt(new[]{-155.0,-115,-65,-25}[buttonIndex++]);At(button,Math.Round(p.X-52),Math.Round(p.Y-21));button.Visibility=Visibility.Hidden;}orbit.Visibility=Visibility.Hidden;
   mascot.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){dragging=true;moved=false;dragCursor=CursorDip();dragAnchor=anchor;mascot.CaptureMouse();e.Handled=true;};
   mascot.MouseMove+=delegate{if(!dragging)return;Vector delta=CursorDip()-dragCursor;if(delta.Length>4)moved=true;if(moved){anchor=dragAnchor+delta;Place();}};
   mascot.MouseLeftButtonUp+=delegate(object sender,MouseButtonEventArgs e){if(!dragging)return;dragging=false;mascot.ReleaseMouseCapture();if(!moved)SetExpanded(!expanded);else SavePosition();e.Handled=true;};
   mascot.LostMouseCapture+=delegate{dragging=false;};
   var menu=new ContextMenu();var mainItem=new MenuItem{Header="返回主页面"};mainItem.Click+=delegate{Hide();openMain();};menu.Items.Add(mainItem);var collapse=new MenuItem{Header="收起按钮"};collapse.Click+=delegate{SetExpanded(false);};menu.Items.Add(collapse);var hide=new MenuItem{Header="隐藏桌宠（保留系统托盘）"};hide.Click+=delegate{Hide();};menu.Items.Add(hide);mascot.ContextMenu=menu;
   timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(40)};timer.Tick+=delegate{Animate();};
   IsVisibleChanged+=delegate{if(IsVisible && animate)timer.Start();else timer.Stop();};
   Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(!allowClose){e.Cancel=true;Hide();}};
   Closed+=delegate{timer.Stop();};PreviewKeyDown+=delegate(object s,KeyEventArgs e){e.Handled=true;};PreviewKeyUp+=delegate(object s,KeyEventArgs e){e.Handled=true;};
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
  void Place(){
   const double cx=180,cy=180;
   // Retain a reachable mascot even when the expanded palette reaches a screen edge.
   Rect work=SystemParameters.WorkArea;var source=PresentationSource.FromVisual(this);
   if(source!=null && source.CompositionTarget!=null){var physical=source.CompositionTarget.TransformToDevice.Transform(anchor);var area=Forms.Screen.FromPoint(new System.Drawing.Point((int)physical.X,(int)physical.Y)).WorkingArea;var a=source.CompositionTarget.TransformFromDevice.Transform(new Point(area.Left,area.Top));var b=source.CompositionTarget.TransformFromDevice.Transform(new Point(area.Right,area.Bottom));work=new Rect(a,b);}
   double minX=work.Left,maxX=work.Right,minY=work.Top,maxY=work.Bottom;
   const double leftSpace=180,rightSpace=180,topSpace=180,bottomSpace=80;
   double x=Math.Max(minX+leftSpace,Math.Min(maxX-rightSpace,anchor.X)),y=Math.Max(minY+topSpace,Math.Min(maxY-bottomSpace,anchor.Y));
   anchor=new Point(x,y);Left=x-cx;Top=y-cy;
  }
  string PositionPath {get{return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Captura","DesktopPet.position.txt");}}
  void SavePosition(){try{System.IO.File.WriteAllLines(PositionPath,new[]{anchor.X.ToString(System.Globalization.CultureInfo.InvariantCulture),anchor.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)});}catch{}}
  void LoadPosition(){try{string[] lines=System.IO.File.ReadAllLines(PositionPath);anchor=new Point(Double.Parse(lines[0],System.Globalization.CultureInfo.InvariantCulture),Double.Parse(lines[1],System.Globalization.CultureInfo.InvariantCulture));}catch{}}
  public void DisposePet(){allowClose=true;timer.Stop();Close();}
 }
}
