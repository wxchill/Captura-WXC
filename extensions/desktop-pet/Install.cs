using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace CapturaDesktopPet {
 public static class Installer {
  public const string ResourceKey="Captura.Local.DesktopPet";
  public static void Install(object instance){
   Window owner=instance as Window;if(owner==null || owner.Resources.Contains(ResourceKey))return;
   var controller=new PetController(owner);owner.Resources[ResourceKey]=controller;
   owner.Loaded+=delegate{owner.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(controller.AddEntry));};
   owner.IsVisibleChanged+=delegate{if(owner.IsVisible)controller.HidePet();};
   owner.Closed+=delegate{controller.Dispose();};
   if(owner.IsLoaded)controller.AddEntry();
  }
 }
 public sealed class PetController : IDisposable {
  readonly Window owner;readonly PetAdapter adapter;PetWindow pet;Button entry;
  public PetController(Window window){owner=window;adapter=new PetAdapter(()=>owner.DataContext);}
  static IEnumerable<DependencyObject> Descendants(DependencyObject root){
   int count=VisualTreeHelper.GetChildrenCount(root);for(int i=0;i<count;i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
  }
  static string TextOf(object value){
   if(value==null)return "";if(value is string)return (string)value;
   var text=value as TextBlock;if(text!=null)return text.Text;
   var content=value as ContentControl;if(content!=null)return TextOf(content.Content);
   return value.ToString();
  }
  public void AddEntry(){
   AddEntryTo(owner);
  }
  public void AddEntryTo(DependencyObject searchRoot){
   if(entry!=null)return;ButtonBase minimize=null;
   foreach(var node in Descendants(searchRoot)){
    var button=node as ButtonBase;if(button==null)continue;
    string tip=TextOf(button.ToolTip).Trim();string name=AutomationProperties.GetName(button);
    if(tip=="最小化" || tip.Equals("Minimize",StringComparison.OrdinalIgnoreCase) || name=="最小化"){minimize=button;break;}
   }
   if(minimize==null)return;
   var panel=minimize.Parent as Panel;if(panel==null)return;
   var icon=new MascotView{Width=25,Height=25,IsHitTestVisible=false};
   entry=new Button{Content=icon,Width=32,Height=30,Padding=new Thickness(1),BorderThickness=new Thickness(0),Background=Brushes.Transparent,ToolTip="桌宠快捷控制",Focusable=false,VerticalAlignment=VerticalAlignment.Center};
   AutomationProperties.SetName(entry,"桌宠快捷控制");entry.Click+=delegate{EnterPetMode();};
   var grid=panel as Grid;
   if(grid!=null){int column=Grid.GetColumn(minimize);if(grid.ColumnDefinitions.Count==0){entry=null;return;}grid.ColumnDefinitions.Insert(column,new ColumnDefinition{Width=GridLength.Auto});foreach(UIElement child in grid.Children)if(Grid.GetColumn(child)>=column)Grid.SetColumn(child,Grid.GetColumn(child)+1);Grid.SetColumn(entry,column);Grid.SetRow(entry,Grid.GetRow(minimize));grid.Children.Add(entry);}
   else {int at=panel.Children.IndexOf(minimize);var dock=panel as DockPanel;if(dock!=null && DockPanel.GetDock(minimize)==Dock.Right){DockPanel.SetDock(entry,Dock.Right);at++;}panel.Children.Insert(at,entry);}
  }
  public void EnterPetMode(){
   if(pet==null){pet=new PetWindow(adapter,ReturnToMain,true);pet.ResumeAllowed=()=>!owner.IsVisible;}
   owner.Hide();pet.SetExpanded(false);pet.Show();pet.Refresh();
  }
  public void ReturnToMain(){HidePet();if(owner.WindowState==WindowState.Minimized)owner.WindowState=WindowState.Normal;owner.Show();owner.Activate();}
  public void HidePet(){if(pet!=null)pet.Hide();}
  public void Dispose(){if(pet!=null){pet.DisposePet();pet=null;}}
 }
}
