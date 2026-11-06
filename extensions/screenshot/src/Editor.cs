using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Drawing=System.Drawing;

namespace CapturaScreenshotTools {
 public sealed class Editor : Window {
  BitmapSource original,translatedImage;readonly InkCanvas ink=new InkCanvas();
  OcrDocument document;bool showingTranslation;Button viewOriginal;
  readonly TextBox source=new TextBox(),translated=new TextBox();
  readonly ComboBox mode=new ComboBox(),language=new ComboBox(),size=new ComboBox();
  readonly TextBlock status=new TextBlock(),privacy=new TextBlock();
  readonly List<Button> jobButtons=new List<Button>();
  readonly List<StrokeCollection> history=new List<StrokeCollection>();
  readonly CancellationTokenSource cancellation=new CancellationTokenSource();
  readonly string job;readonly string defaultFolder;
  TabControl tabs;ScaleTransform zoom=new ScaleTransform(1,1);bool restoring,busy;
  public bool Reselect {get;private set;}
  public UIElement PreviewRoot {get{return (UIElement)Content;}}
  public Editor(BitmapSource bitmap,string folder){
   original=bitmap;defaultFolder=folder;Width=1220;Height=820;MinWidth=960;MinHeight=640;
   Title="Captura 截图 · 编辑与文字工具";WindowStartupLocation=WindowStartupLocation.CenterScreen;
   FontFamily=new FontFamily("Microsoft YaHei");FontSize=13;Background=Brushes.White;
   job=Path.Combine(Path.GetTempPath(),"CapturaScreenshotTools-jobs",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);
   Build();
   Closed+=delegate{cancellation.Cancel();try{Directory.Delete(job,true);}catch{}};
   PreviewKeyDown+=PreventButtonKeys;PreviewKeyUp+=PreventButtonKeys;
  }
  void PreventButtonKeys(object sender,KeyEventArgs e){
   DependencyObject target=e.OriginalSource as DependencyObject;
   while(target!=null){if(target is TextBox)return;target=target is Visual?VisualTreeHelper.GetParent(target):LogicalTreeHelper.GetParent(target);}
   e.Handled=true;
  }
  Button MakeButton(string text,Action action,bool worker){
   var button=new Button{Content=text,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,0),Focusable=false,MinHeight=34};
   button.Background=Brushes.White;button.BorderBrush=new SolidColorBrush(Color.FromRgb(202,213,224));button.BorderThickness=new Thickness(1);button.Foreground=new SolidColorBrush(Color.FromRgb(27,49,73));
   if(text=="翻译成中文" || text=="保存图片"){button.Background=new SolidColorBrush(Color.FromRgb(24,137,206));button.Foreground=Brushes.White;button.BorderBrush=button.Background;}
   button.Click+=delegate{try{action();}catch(Exception e){status.Text=e.Message;}};
   if(worker)jobButtons.Add(button);return button;
  }
  void Build(){
   var root=new Grid{Background=Brushes.White};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var heading=new StackPanel{Margin=new Thickness(20,16,20,14)};
   heading.Children.Add(new TextBlock{Text="截图编辑",FontSize=22,FontWeight=FontWeights.Bold,Foreground=new SolidColorBrush(Color.FromRgb(27,49,73))});
   heading.Children.Add(new TextBlock{Text="每次手动框选 · 文字识别在本机完成",Margin=new Thickness(0,4,0,12),Foreground=Brushes.DimGray});
   var tools=new WrapPanel();
   tools.Children.Add(MakeButton("重新框选",delegate{if(busy)return;Reselect=true;Close();},true));
   tools.Children.Add(MakeButton("保存图片",Save,false));tools.Children.Add(MakeButton("复制图片",CopyImage,false));
   viewOriginal=MakeButton("查看原图",ToggleTranslation,false);viewOriginal.IsEnabled=false;tools.Children.Add(viewOriginal);
   tools.Children.Add(MakeButton("画笔",delegate{ink.EditingMode=InkCanvasEditingMode.Ink;ink.DefaultDrawingAttributes.IsHighlighter=false;double width=size.SelectedItem==null?4:Convert.ToDouble(size.SelectedItem);ink.DefaultDrawingAttributes.Width=width;ink.DefaultDrawingAttributes.Height=width;},false));
   tools.Children.Add(MakeButton("荧光笔",delegate{ink.EditingMode=InkCanvasEditingMode.Ink;ink.DefaultDrawingAttributes.IsHighlighter=true;ink.DefaultDrawingAttributes.Color=Colors.Yellow;ink.DefaultDrawingAttributes.Width=18;ink.DefaultDrawingAttributes.Height=18;},false));
   tools.Children.Add(MakeButton("橡皮",delegate{ink.EditingMode=InkCanvasEditingMode.EraseByStroke;},false));
   tools.Children.Add(MakeButton("撤销",Undo,false));
   foreach(Color color in new[]{Colors.Red,Colors.DodgerBlue,Colors.Yellow,Colors.Black,Colors.White}){
    Color selected=color;var swatch=new Button{Width=25,Height=25,Margin=new Thickness(1,5,3,0),Background=new SolidColorBrush(color),BorderBrush=Brushes.Gray,Focusable=false,ToolTip="画笔颜色"};
    swatch.Click+=delegate{ink.DefaultDrawingAttributes.Color=selected;};tools.Children.Add(swatch);
   }
   size.Width=58;size.Margin=new Thickness(6,3,4,0);size.Focusable=false;
   foreach(int value in new[]{2,4,8,12})size.Items.Add(value);size.SelectedItem=4;
   size.SelectionChanged+=delegate{double value=Convert.ToDouble(size.SelectedItem);ink.DefaultDrawingAttributes.Width=value;ink.DefaultDrawingAttributes.Height=value;};tools.Children.Add(size);
   tools.Children.Add(MakeButton("−",delegate{SetZoom(zoom.ScaleX/1.25);},false));tools.Children.Add(MakeButton("＋",delegate{SetZoom(zoom.ScaleX*1.25);},false));
   heading.Children.Add(tools);root.Children.Add(heading);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(360)});Grid.SetRow(body,1);root.Children.Add(body);
   var scroll=new ScrollViewer{Background=new SolidColorBrush(Color.FromRgb(37,44,54)),HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(20),Content=ink};
   ink.Width=original.PixelWidth;ink.Height=original.PixelHeight;ink.Background=new ImageBrush(original){Stretch=Stretch.Fill};ink.LayoutTransform=zoom;ink.Focusable=false;ink.HorizontalAlignment=HorizontalAlignment.Left;ink.VerticalAlignment=VerticalAlignment.Top;
   ink.DefaultDrawingAttributes=new DrawingAttributes{Color=Colors.Red,Width=4,Height=4,FitToCurve=true};
   history.Add(new StrokeCollection());ink.StrokeCollected+=delegate{Snapshot();};ink.StrokeErased+=delegate{Dispatcher.BeginInvoke(new Action(Snapshot));};body.Children.Add(scroll);
   var side=new Grid{Margin=new Thickness(16,0,16,0)};Grid.SetColumn(side,1);body.Children.Add(side);
   side.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});side.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});side.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});side.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});side.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var label=new TextBlock{Text="文字与翻译",FontSize=17,FontWeight=FontWeights.Bold,Margin=new Thickness(0,3,0,10)};side.Children.Add(label);
   var settings=new StackPanel();Grid.SetRow(settings,1);side.Children.Add(settings);
   mode.Items.Add("本地模型（离线）");mode.Items.Add("联网翻译（MyMemory）");mode.SelectedIndex=0;mode.Margin=new Thickness(0,0,0,8);mode.Focusable=false;
   mode.SelectionChanged+=delegate{SetPrivacy();};settings.Children.Add(mode);
   var langs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8)};
   langs.Children.Add(new TextBlock{Text="原文语言",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
   foreach(string[] item in new[]{new[]{"英语","en"},new[]{"日语","ja"},new[]{"韩语","ko"},new[]{"德语","de"},new[]{"法语","fr"},new[]{"西班牙语","es"},new[]{"俄语","ru"}})language.Items.Add(new ComboBoxItem{Content=item[0],Tag=item[1]});
   language.SelectedIndex=0;language.Width=100;language.Focusable=false;langs.Children.Add(language);langs.Children.Add(new TextBlock{Text=" → 简体中文",VerticalAlignment=VerticalAlignment.Center});settings.Children.Add(langs);
   privacy.TextWrapping=TextWrapping.Wrap;privacy.FontSize=11;privacy.Foreground=Brushes.DimGray;privacy.Margin=new Thickness(0,0,0,12);settings.Children.Add(privacy);SetPrivacy();
   var actions=new WrapPanel{Margin=new Thickness(0,0,0,10)};actions.Children.Add(MakeButton("提取文字",delegate{StartJob(false);},true));actions.Children.Add(MakeButton("翻译成中文",delegate{StartJob(true);},true));actions.Children.Add(MakeButton("更新图片译文",ApplyTranslatedText,true));Grid.SetRow(actions,2);side.Children.Add(actions);
   tabs=new TabControl();Grid.SetRow(tabs,3);side.Children.Add(tabs);
   foreach(TextBox box in new[]{source,translated}){box.AcceptsReturn=true;box.TextWrapping=TextWrapping.Wrap;box.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;box.Padding=new Thickness(10);box.BorderThickness=new Thickness(0);box.FontSize=14;}
   tabs.Items.Add(new TabItem{Header="识别文字",Content=source});tabs.Items.Add(new TabItem{Header="中文翻译",Content=translated});
   var copy=MakeButton("复制当前文字",delegate{string value=tabs.SelectedIndex==1?translated.Text:source.Text;if(!String.IsNullOrEmpty(value)){Clipboard.SetText(value);status.Text="文字已复制";}},false);copy.Margin=new Thickness(0,10,0,10);Grid.SetRow(copy,4);side.Children.Add(copy);
   var footer=new Border{Background=new SolidColorBrush(Color.FromRgb(242,245,248)),Padding=new Thickness(20,10,20,10)};Grid.SetRow(footer,2);status.Text=original.PixelWidth+" × "+original.PixelHeight+" 像素 · 翻译后自动覆盖图片原文，可切回原图查看";status.TextWrapping=TextWrapping.Wrap;footer.Child=status;root.Children.Add(footer);Content=root;
   SetZoom(Math.Min(1.0,720.0/original.PixelWidth));
  }
  void SetPrivacy(){privacy.Text=mode.SelectedIndex==0?"本地处理，不发送截图或文字。当前离线翻译模型：英文 → 简体中文。":"联网时仅将原文文字发送至 MyMemory，不上传截图；匿名服务有每日额度限制。";}
  void SetZoom(double value){zoom.ScaleX=zoom.ScaleY=Math.Max(0.1,Math.Min(4,value));}
  void Snapshot(){if(restoring)return;history.Add(ink.Strokes.Clone());if(history.Count>50)history.RemoveAt(0);}
  void Undo(){if(history.Count<2)return;history.RemoveAt(history.Count-1);restoring=true;ink.Strokes=history[history.Count-1].Clone();restoring=false;}
  public bool ShowingTranslation {get{return showingTranslation;}}
  public void ToggleTranslation(){
   if(translatedImage==null)return;showingTranslation=!showingTranslation;RefreshImage();
   status.Text=showingTranslation?"当前显示译图；保存和复制图片包含中文覆盖":"当前显示原图；点击“查看译图”可恢复中文覆盖";
  }
  void RefreshImage(){ink.Background=new ImageBrush(showingTranslation?translatedImage:original){Stretch=Stretch.Fill};viewOriginal.Content=showingTranslation?"查看原图":"查看译图";}
  public void ApplyTranslatedText(){
   if(document==null || document.Lines.Count==0)throw new InvalidOperationException("请先提取文字，获取图片中文字的位置。");
   string[] lines=translated.Text.Replace("\r\n","\n").TrimEnd('\n').Split('\n');
   if(lines.Length!=document.Lines.Count)throw new InvalidOperationException("译文每行需要对应一处原文。请保持文字行数，修改后再更新图片译文。");
   var regions=new List<TranslationRegion>();
   for(int i=0;i<lines.Length;i++){
    if(String.IsNullOrWhiteSpace(lines[i]))throw new InvalidOperationException("有一行译文为空，请补齐后再更新图片译文。");
    if(lines[i]!=document.Lines[i].Text)regions.Add(new TranslationRegion{Bounds=document.Lines[i].Bounds,Text=lines[i]});
   }
   translatedImage=TranslationOverlay.Render(original,regions);showingTranslation=true;viewOriginal.IsEnabled=true;RefreshImage();
   status.Text="中文已覆盖原文位置；保存和复制图片均包含译文。可修改右侧译文后点击“更新图片译文”。";
  }
  public BitmapSource Composite(){
   var visual=new DrawingVisual();using(DrawingContext dc=visual.RenderOpen()){
    dc.DrawImage(showingTranslation?translatedImage:original,new Rect(0,0,original.PixelWidth,original.PixelHeight));ink.Strokes.Draw(dc);
   }
   var bitmap=new RenderTargetBitmap(original.PixelWidth,original.PixelHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  }
  public static void WritePng(BitmapSource bitmap,string path){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);}
  void Save(){var dialog=new SaveFileDialog{Title="保存截图",Filter="PNG 图片|*.png",DefaultExt=".png",FileName="截图_"+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")+".png",AddExtension=true,OverwritePrompt=true};if(Directory.Exists(defaultFolder))dialog.InitialDirectory=defaultFolder;if(dialog.ShowDialog(this)==true){WritePng(Composite(),dialog.FileName);status.Text="图片已保存："+dialog.FileName;}}
  void CopyImage(){Clipboard.SetImage(Composite());status.Text="图片已复制，可直接粘贴到聊天或文档中";}
  async void StartJob(bool translate){
   if(busy)return;busy=true;foreach(Button b in jobButtons)b.IsEnabled=false;source.IsReadOnly=true;translated.IsReadOnly=true;mode.IsEnabled=false;language.IsEnabled=false;
   try{
    if(!translate || document==null || String.IsNullOrWhiteSpace(source.Text)){
     status.Text="正在本地识别文字…";
     string path=Path.Combine(job,"ocr-input.png");BitmapSource input=original;
     double factor=Math.Min(1.0,2800.0/Math.Max(original.PixelWidth,original.PixelHeight));
     if(factor<1)input=new TransformedBitmap(original,new ScaleTransform(factor,factor));
     WritePng(input,path);document=await Services.RecognizeDocumentAsync(path,job,cancellation.Token);
     foreach(OcrLine line in document.Lines)line.Bounds=TranslationOverlay.ScaleBounds(line.Bounds,1/factor);
     source.Text=document.Text;tabs.SelectedIndex=0;
    }
    if(translate){
     string text=source.Text;if(String.IsNullOrWhiteSpace(text))throw new InvalidOperationException("未识别到文字，请重新框选更清晰的文字区域。");
     string code=Convert.ToString(((ComboBoxItem)language.SelectedItem).Tag);
     status.Text=mode.SelectedIndex==0?"正在用本地模型翻译…":"正在联网翻译（MyMemory，仅发送文字）…";
     translated.Text=mode.SelectedIndex==0?await Services.TranslateLocalAsync(text,code,job,cancellation.Token):await Services.TranslateOnlineAsync(text,code,cancellation.Token);tabs.SelectedIndex=1;
     ApplyTranslatedText();
    }
    if(!translate)status.Text="文字提取完成，可以修改、复制或继续翻译";
   }catch(OperationCanceledException){status.Text="已取消";}catch(Exception e){status.Text=e.Message;}
   finally{busy=false;foreach(Button b in jobButtons)b.IsEnabled=true;source.IsReadOnly=false;translated.IsReadOnly=false;mode.IsEnabled=true;language.IsEnabled=true;}
  }
  public void PopulatePreview(){
   document=new OcrDocument{Text="Lake Water Quality\nSatellite images are used to retrieve\nlake water quality.\nThe model combines data from multiple sensors."};
   document.Lines.Add(new OcrLine{Text="Lake Water Quality",Bounds=new Rect(60,50,450,48)});
   document.Lines.Add(new OcrLine{Text="Satellite images are used to retrieve",Bounds=new Rect(60,150,760,36)});
   document.Lines.Add(new OcrLine{Text="lake water quality.",Bounds=new Rect(60,195,370,36)});
   document.Lines.Add(new OcrLine{Text="The model combines data from multiple sensors.",Bounds=new Rect(60,285,820,36)});
   source.Text=document.Text;translated.Text="湖泊水质\n卫星影像用于反演\n湖泊水质。\n该模型融合了来自多个传感器的数据。";tabs.SelectedIndex=1;ApplyTranslatedText();
   var points=new StylusPointCollection{new StylusPoint(80,240),new StylusPoint(450,240),new StylusPoint(680,235)};ink.Strokes.Add(new Stroke(points,new DrawingAttributes{Color=Colors.Red,Width=4,Height=4}));Snapshot();
  }
 }
 public static class Program {
  public static BitmapSource DemoImage(){
   using(var image=new Drawing.Bitmap(960,560)){
    using(Drawing.Graphics g=Drawing.Graphics.FromImage(image)){
     g.Clear(Drawing.Color.White);g.TextRenderingHint=Drawing.Text.TextRenderingHint.AntiAliasGridFit;
     using(var title=new Drawing.Font("Arial",26,Drawing.FontStyle.Bold))g.DrawString("Lake Water Quality",title,Drawing.Brushes.DarkSlateGray,60,50);
     using(var text=new Drawing.Font("Arial",18)){g.DrawString("Satellite images are used to retrieve",text,Drawing.Brushes.Black,60,150);g.DrawString("lake water quality.",text,Drawing.Brushes.Black,60,195);g.DrawString("The model combines data from multiple sensors.",text,Drawing.Brushes.Black,60,285);}
    }
    return ToBitmapSource(image);
   }
  }
  static BitmapSource ToBitmapSource(Drawing.Bitmap image){using(var ms=new MemoryStream()){image.Save(ms,Drawing.Imaging.ImageFormat.Png);ms.Position=0;var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=ms;bitmap.EndInit();bitmap.Freeze();return bitmap;}}
  [STAThread] public static int Main(string[] args){
   bool preview=args.Length>=2 && args[0]=="--render-preview";
   try{
    if(preview){var window=new Editor(DemoImage(),"");window.PopulatePreview();UIElement visual=window.PreviewRoot;visual.Measure(new Size(1220,790));visual.Arrange(new Rect(0,0,1220,790));visual.UpdateLayout();var output=new RenderTargetBitmap(1220,790,96,96,PixelFormats.Pbgra32);output.Render(visual);Editor.WritePng(output,args[1]);Editor.WritePng(DemoImage(),Path.Combine(Path.GetDirectoryName(args[1]),"ocr-test-input.png"));window.Close();return 0;}
    string ready=null,providedFolder=null;for(int i=0;i<args.Length-1;i++){if(args[i]=="--handoff")ready=args[i+1];if(args[i]=="--out-folder")providedFolder=args[i+1];}
    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
    string folder=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    try{string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Captura","Captura.json");var data=Services.ReadJson(path);if(data.ContainsKey("OutPath"))folder=Convert.ToString(data["OutPath"]);}catch{}
    if(!String.IsNullOrWhiteSpace(providedFolder))folder=providedFolder;
    bool again;
    do{
     Thread.Sleep(180);Drawing.Bitmap selected=null;
     try{selected=RegionSelector.Select();}finally{if(ready!=null){try{File.WriteAllText(ready,"ready");}catch{}ready=null;}}
     if(selected==null)break;
     BitmapSource bitmap;using(selected)bitmap=ToBitmapSource(selected);
     var editor=new Editor(bitmap,folder);editor.ShowDialog();again=editor.Reselect;
    }while(again);
    app.Shutdown();return 0;
   }catch(Exception e){if(!preview)MessageBox.Show(e.Message,"截图工具",MessageBoxButton.OK,MessageBoxImage.Information);else Console.Error.WriteLine(e);return 1;}
  }
 }
}
