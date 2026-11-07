using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CapturaChineseUi;

class Model:INotifyPropertyChanged {
 string description;public string Description{get{return description;}set{description=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Description"));}}
 public event PropertyChangedEventHandler PropertyChanged;
}
class Checks {
 static void Need(bool ok,string text){if(!ok)throw new Exception(text);}
 static object Provider(string assembly,string type){return FormatterServices.GetUninitializedObject(Assembly.Load(assembly).GetType(type,true));}
 static string Description(object value){return (string)value.GetType().GetProperty("Description").GetValue(value,null);}
 [STAThread] static int Main(){try{
  Installer.Install(null);
  Translator.SetCulture(new CultureInfo("zh-CN"));
  var loaded=new Button{Content="Drawing Tools",ToolTip="Use internal Gif encoder.\nVariable Frame Rate mode is recommended."};loaded.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));Need((string)loaded.Content=="绘图工具" && ((string)loaded.ToolTip).Contains("可变帧率"),"Automatic loaded-element localization did not run");
  object ff=Provider("Captura.FFmpeg","Captura.Models.FFmpegWriterProvider");
  string ffEnglish=Description(ff),audioEnglish="No Video recorded.\nCan be used for audio-only recording.\nMake sure Audio sources are enabled.";
  var model=new Model{Description=ffEnglish};var text=new TextBlock();text.SetBinding(TextBlock.TextProperty,new Binding("Description"){Source=model,Mode=BindingMode.OneWay});Installer.Attach(text);
  Need(text.Text.Contains("使用 FFmpeg") && !text.Text.Contains("Use FFmpeg"),"FFmpeg tooltip not translated");Need(model.Description==ffEnglish && BindingOperations.IsDataBound(text,TextBlock.TextProperty),"Translation modified model or removed binding");
  model.Description=audioEnglish;text.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();Need(text.Text.Contains("仅录制音频") && model.Description==audioEnglish,"Changing video source lost translation or changed source data");
  var stream=Provider("Captura.FFmpeg","Captura.Models.StreamingWriterProvider");var tab=new TextBlock{Text="Stream",DataContext=stream};Installer.Attach(tab);Need(tab.Text=="推流" && (string)stream.GetType().GetProperty("Name").GetValue(stream,null)=="Stream","Provider identifier changed");
  var editable=new TextBox{Text="Stream"};Installer.Attach(editable);Need(editable.Text=="Stream","Editable value was translated");
  var readOnly=new TextBox{IsReadOnly=true,Text="Copy to Clipboard failed"};Installer.Attach(readOnly);Need(readOnly.Text=="复制到剪贴板失败","Read-only error message was missed");
  foreach(string term in new[]{"FFmpeg","Gif","GIF","SharpAvi","x264","yuv420p","ffmpeg.exe"})Need(Translator.Translate(term)==term,"Technical name was changed: "+term);
  string filter="PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|Bitmap Image|*.bmp|TIFF Image|*.tiff";string[] original=filter.Split('|'),localized=Translator.Translate(filter).Split('|');Need(original.Length==localized.Length,"Filter structure changed");for(int i=1;i<original.Length;i+=2)Need(original[i]==localized[i],"File extension filter changed");
  string path=@"C:\Example\Stream.mp4";Need(Translator.Translate("Could not open file: "+path)=="无法打开文件："+path,"Error message changed file path");
  var root=new StackPanel();root.Children.Add(text);root.Children.Add(tab);
  Translator.SetCulture(new CultureInfo("en"));Installer.LocalizeTree(root);Need(text.Text==audioEnglish && tab.Text=="Stream","English language restoration failed");
  text.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));Translator.SetCulture(new CultureInfo("zh-CN"));Installer.Attach(text);Need(text.Text.Contains("仅录制音频"),"Reloaded tooltip did not translate");
  Installer.LocalizeTree(root);
  var preview=new StackPanel{Margin=new Thickness(18)};preview.Children.Add(new TextBlock{Text="悬停提示 · 汉化预览",FontSize=18,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,12)});
  foreach(var item in new[]{new[]{"仅音频",audioEnglish},new[]{"FFmpeg",ffEnglish},new[]{"GIF","Use internal Gif encoder.\nVariable Frame Rate mode is recommended."},new[]{"SharpAvi","Encode Avi videos using SharpAvi."}}){var card=new StackPanel{Margin=new Thickness(0,0,0,12)};card.Children.Add(new TextBlock{Text=item[0],FontWeight=FontWeights.Bold});var body=new TextBlock{Text=item[1],Margin=new Thickness(0,5,0,0)};Installer.Attach(body);card.Children.Add(body);preview.Children.Add(card);}
  var border=new Border{Background=Brushes.White,BorderBrush=Brushes.SkyBlue,BorderThickness=new Thickness(1),Child=preview,Width=570};border.Measure(new Size(570,1000));border.Arrange(new Rect(new Point(),border.DesiredSize));border.UpdateLayout();var image=new RenderTargetBitmap(570,(int)Math.Ceiling(border.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(border);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Chinese-tooltip-preview.png")))png.Save(file);
  Console.WriteLine("PASS: actual provider tooltips translated; binding updates, model IDs, editable values, technical names, file filters, paths and language switching preserved. No desktop input or recording.");return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
