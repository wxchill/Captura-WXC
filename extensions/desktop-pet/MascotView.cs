using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CapturaDesktopPet {
 public sealed class MascotView : FrameworkElement {
  static readonly BitmapSource sprite=LoadSprite();
  public double EyeX,EyeY;public bool Blink,Recording,Paused;
  public string DisplayedTime="00:00:00";
  public MascotView(){Width=130;Height=130;Focusable=false;RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.NearestNeighbor);SnapsToDevicePixels=true;}
  static BitmapSource LoadSprite(){
   using(var stream=typeof(MascotView).Assembly.GetManifestResourceStream("CapturaDesktopPet.device.png")){
    var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
    // Asset viewport is set to the solid camcorder silhouette, retaining alpha.
    var cropped=new CroppedBitmap(bitmap,new Int32Rect(64,130,1140,1048));cropped.Freeze();return cropped;
   }
  }
  public static Vector Gaze(Vector direction,double max){double length=direction.Length;if(length<0.001)return new Vector();double factor=Math.Min(1,length/220);return direction/length*(max*factor);}
  protected override void OnRender(DrawingContext dc){
   double w=ActualWidth>0?ActualWidth:Width,h=ActualHeight>0?ActualHeight:Height;
   dc.DrawImage(sprite,new Rect(0,0,w,h));
   double u=w/64,cy=h*.47;
   Brush white=new SolidColorBrush(Color.FromRgb(235,250,255)),dark=new SolidColorBrush(Color.FromRgb(11,24,43));
   if(Recording || Paused){
    double unit=Math.Max(1,Math.Min(2,Math.Floor(w*.60/27)));PixelFont.Center(dc,DisplayedTime,w*.47,h*.48-2.5*unit,unit,Paused?Brushes.Orange:white);
   }else{foreach(double cx in new[]{w*.34,w*.62}){
    if(Blink){dc.DrawRectangle(white,null,new Rect(cx-4*u,cy+2*u,8*u,u));continue;}
    dc.DrawRectangle(white,null,new Rect(cx-3*u,cy-5*u,6*u,10*u));
    dc.DrawRectangle(white,null,new Rect(cx-4*u,cy-4*u,8*u,8*u));
    double x=Math.Round(EyeX/u)*u,y=Math.Round(EyeY/u)*u;
    dc.DrawRectangle(dark,null,new Rect(cx-1.5*u+x,cy-2*u+y,3*u,4*u));
   }
   double mx=w*.48,my=h*.59;
   dc.DrawRectangle(white,null,new Rect(mx-3*u,my,2*u,u));dc.DrawRectangle(white,null,new Rect(mx-u,my+u,2*u,u));dc.DrawRectangle(white,null,new Rect(mx+u,my,2*u,u));
   }
   if(w>=80)PixelFont.Center(dc,"wxc",w*.47,h*.795,Math.Max(1,Math.Floor(w/64)),dark);
   Brush lamp=Recording?Brushes.Red:Paused?Brushes.Orange:Brushes.Coral;
   dc.DrawRectangle(lamp,null,new Rect(w*.70,h*.23,4*u,4*u));
  }
 }
}
