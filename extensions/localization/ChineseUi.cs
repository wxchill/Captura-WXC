using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace CapturaChineseUi {
 public static class Translator {
  static readonly Dictionary<string,string> texts=new Dictionary<string,string>(StringComparer.Ordinal);
  static readonly Dictionary<string,string> prefixes=new Dictionary<string,string>(StringComparer.Ordinal);
  static CultureInfo culture;
  static Translator(){
   using(var stream=typeof(Translator).Assembly.GetManifestResourceStream("CapturaChineseUi.translations.json"))
   using(var reader=new StreamReader(stream)){
    var data=JObject.Parse(reader.ReadToEnd());foreach(var p in ((JObject)data["texts"]).Properties())texts[Normalize(p.Name)]=(string)p.Value;
    foreach(var p in ((JObject)data["prefixes"]).Properties())prefixes[p.Name]=(string)p.Value;
   }
  }
  static string Normalize(string text){return Regex.Replace(text.Trim(),@"\s+"," ");}
  public static void SetCulture(CultureInfo value){culture=value;}
  public static string Translate(string text){
   if(text==null || (culture??CultureInfo.CurrentUICulture).TwoLetterISOLanguageName!="zh")return text;
   string translated;if(texts.TryGetValue(Normalize(text),out translated))return translated;
   if(text.StartsWith("{}",StringComparison.Ordinal)){string format=Translate(text.Substring(2));return format==text.Substring(2)?text:"{}"+format;}
   foreach(var pair in prefixes)if(text.StartsWith(pair.Key,StringComparison.Ordinal))return pair.Value+text.Substring(pair.Key.Length);
   return text;
  }
 }
 public static class Installer {
  static bool registered;
  static readonly DependencyProperty WatchProperty=DependencyProperty.RegisterAttached("ChineseWatch",typeof(Watch),typeof(Installer),new PropertyMetadata(null));
  public static void Install(object instance){
   if(!registered){
    registered=true;
    EventManager.RegisterClassHandler(typeof(FrameworkElement),FrameworkElement.LoadedEvent,new RoutedEventHandler(Loaded));
    EventManager.RegisterClassHandler(typeof(FrameworkElement),ToolTipService.ToolTipOpeningEvent,new ToolTipEventHandler(Opening));
    try{
     Type manager=Type.GetType("Captura.LanguageManager, Captura.Loc",false);if(manager!=null){object loc=manager.GetProperty("Instance").GetValue(null,null);Translator.SetCulture((CultureInfo)manager.GetProperty("CurrentCulture").GetValue(loc,null));manager.GetEvent("LanguageChanged").AddEventHandler(loc,new Action<CultureInfo>(ChangeCulture));}
    }catch{}
   }
   var owner=instance as Window;if(owner!=null){LocalizeTree(owner);owner.Loaded+=delegate{LocalizeTree(owner);};}
  }
  static void ChangeCulture(CultureInfo value){Translator.SetCulture(value);if(Application.Current==null)return;Application.Current.Dispatcher.BeginInvoke(new Action(delegate{foreach(Window window in Application.Current.Windows)LocalizeTree(window);}));}
  static void Loaded(object sender,RoutedEventArgs e){Attach(sender as FrameworkElement);}
  static void Opening(object sender,ToolTipEventArgs e){var element=sender as FrameworkElement;if(element==null)return;Attach(element);var tip=element.ToolTip as DependencyObject;if(tip!=null)LocalizeTree(tip);}
  public static void LocalizeTree(DependencyObject root){
   if(root==null)return;var element=root as FrameworkElement;if(element!=null)Attach(element);
   var visual=root as Visual;if(visual!=null)for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)LocalizeTree(VisualTreeHelper.GetChild(root,i));
  }
  public static void Attach(FrameworkElement element){
   if(element==null)return;var watch=element.GetValue(WatchProperty) as Watch;
   if(watch!=null){watch.Activate();watch.Refresh();return;}
   var properties=new List<DependencyProperty>();
   if(element is TextBlock)properties.Add(TextBlock.TextProperty);
   var readOnly=element as TextBox;if(readOnly!=null && readOnly.IsReadOnly)properties.Add(TextBox.TextProperty);
   if(element is Label || element is ButtonBase || element is ToolTip)properties.Add(ContentControl.ContentProperty);
   if(element is HeaderedContentControl)properties.Add(HeaderedContentControl.HeaderProperty);
   if(element is HeaderedItemsControl)properties.Add(HeaderedItemsControl.HeaderProperty);
   if(element is ContentControl)properties.Add(ContentControl.ContentStringFormatProperty);
   if(element is Window)properties.Add(Window.TitleProperty);
   if(element.ReadLocalValue(FrameworkElement.ToolTipProperty)!=DependencyProperty.UnsetValue || element.ToolTip!=null)properties.Add(FrameworkElement.ToolTipProperty);
   if(properties.Count==0)return;
   watch=new Watch(element,properties);element.SetValue(WatchProperty,watch);watch.Refresh();
  }
  sealed class Watch {
   readonly FrameworkElement element;readonly List<DependencyProperty> properties;
   readonly Dictionary<DependencyProperty,string> originals=new Dictionary<DependencyProperty,string>(),rendered=new Dictionary<DependencyProperty,string>();bool busy,subscribed;
   public Watch(FrameworkElement target,List<DependencyProperty> values){element=target;properties=values;target.Unloaded+=Unloaded;Activate();}
   public void Activate(){if(subscribed)return;foreach(var p in properties)DependencyPropertyDescriptor.FromProperty(p,element.GetType()).AddValueChanged(element,Changed);subscribed=true;}
   void Unloaded(object sender,RoutedEventArgs e){if(!subscribed)return;foreach(var p in properties)DependencyPropertyDescriptor.FromProperty(p,element.GetType()).RemoveValueChanged(element,Changed);subscribed=false;}
   void Changed(object sender,EventArgs args){Refresh();}
   public void Refresh(){
    if(busy)return;busy=true;
    try{foreach(var p in properties){
     string current=element.GetValue(p) as string;if(current==null)continue;
     string previous,original;
     if(rendered.TryGetValue(p,out previous) && previous==current && originals.TryGetValue(p,out original)){}else original=current;
     // Provider names remain identifiers in the model and saved settings.
     // Only the displayed Stream/Discard tab labels are translated.
     string result=Translator.Translate(original);
     if(original=="Stream" || original=="Discard"){
      string type=element.DataContext==null?"":element.DataContext.GetType().FullName;
      if(type!="Captura.Models.StreamingWriterProvider" && type!="Captura.Models.DiscardWriterProvider" && type!="Captura.Models.DiscardWriterItem")result=original;
     }
     originals[p]=original;rendered[p]=result;
     if(result!=current)element.SetCurrentValue(p,result);
     var text=element as TextBlock;if(text!=null && result!=original && result.Length>38){text.SetCurrentValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);if(Double.IsPositiveInfinity(text.MaxWidth)){text.SetCurrentValue(FrameworkElement.MaxWidthProperty,430.0);text.SetCurrentValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);}}
    }}finally{busy=false;}
   }
  }
 }
}
