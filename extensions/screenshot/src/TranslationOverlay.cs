using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CapturaScreenshotTools {
 public sealed class TranslationRegion {
  public Rect Bounds;
  public string Text;
 }
 public static class TranslationOverlay {
  sealed class PaintRegion {public Rect Bounds;public Brush Background,Foreground;public FormattedText Text;}
  public static Rect ScaleBounds(Rect rect,double factor){return new Rect(rect.X*factor,rect.Y*factor,rect.Width*factor,rect.Height*factor);}
  public static BitmapSource Render(BitmapSource original,IList<TranslationRegion> regions){
   int width=original.PixelWidth,height=original.PixelHeight;
   var converted=new FormatConvertedBitmap(original,PixelFormats.Bgra32,null,0);
   byte[] pixels=new byte[width*height*4];converted.CopyPixels(pixels,width*4,0);
   var paint=new List<PaintRegion>();
   foreach(TranslationRegion region in regions){
    if(String.IsNullOrWhiteSpace(region.Text))continue;
    Rect rect=region.Bounds;rect.Intersect(new Rect(0,0,width,height));
    if(rect.Width<2 || rect.Height<2)continue;
    Color background=SampleBackground(pixels,width,height,rect);
    double padding=Math.Max(2,Math.Min(6,rect.Height*0.10));rect.Inflate(padding,padding);rect.Intersect(new Rect(0,0,width,height));
    Color foreground=(background.R*0.299+background.G*0.587+background.B*0.114)>150?Color.FromRgb(27,35,44):Colors.White;
    FormattedText formatted=null;double fontSize=Math.Max(5,Math.Min(64,region.Bounds.Height*0.85));
    for(;fontSize>=3;fontSize-=0.5){
     formatted=new FormattedText(region.Text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei"),fontSize,new SolidColorBrush(foreground),1.0);
     formatted.MaxTextWidth=Math.Max(1,rect.Width-4);
     if(formatted.Height<=rect.Height-2 && formatted.Width<=rect.Width-2)break;
    }
    if(fontSize<3)throw new InvalidOperationException("有一行译文过长，无法放进原文字区域。请在右侧缩短该行，再点“更新图片译文”。");
    paint.Add(new PaintRegion{Bounds=rect,Background=new SolidColorBrush(background),Foreground=new SolidColorBrush(foreground),Text=formatted});
   }
   var visual=new DrawingVisual();using(DrawingContext dc=visual.RenderOpen()){
    dc.DrawImage(original,new Rect(0,0,width,height));
    // Erase all original glyphs first, then draw all translations. Adjacent
    // background patches cannot erase Chinese that was just drawn.
    foreach(PaintRegion region in paint)dc.DrawRectangle(region.Background,null,region.Bounds);
    foreach(PaintRegion region in paint){
     dc.PushClip(new RectangleGeometry(region.Bounds));
     dc.DrawText(region.Text,new Point(region.Bounds.Left+2,region.Bounds.Top+Math.Max(1,(region.Bounds.Height-region.Text.Height)/2)));
     dc.Pop();
    }
   }
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  }
  static Color SampleBackground(byte[] pixels,int width,int height,Rect rect){
   var buckets=new Dictionary<int,List<int>>();
   int left=Math.Max(0,(int)Math.Floor(rect.Left)-3),right=Math.Min(width-1,(int)Math.Ceiling(rect.Right)+3);
   int top=Math.Max(0,(int)Math.Floor(rect.Top)-3),bottom=Math.Min(height-1,(int)Math.Ceiling(rect.Bottom)+3);
   for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++){
    if(x>left+1 && x<right-1 && y>top+1 && y<bottom-1)continue;
    int at=(y*width+x)*4;int key=(pixels[at+2]>>4)<<8 | (pixels[at+1]>>4)<<4 | pixels[at]>>4;
    List<int> values;if(!buckets.TryGetValue(key,out values)){values=new List<int>{0,0,0,0};buckets.Add(key,values);}
    values[0]++;values[1]+=pixels[at+2];values[2]+=pixels[at+1];values[3]+=pixels[at];
   }
   List<int> best=null;foreach(List<int> bucket in buckets.Values)if(best==null || bucket[0]>best[0])best=bucket;
   return best==null?Colors.White:Color.FromRgb((byte)(best[1]/best[0]),(byte)(best[2]/best[0]),(byte)(best[3]/best[0]));
  }
 }
}
