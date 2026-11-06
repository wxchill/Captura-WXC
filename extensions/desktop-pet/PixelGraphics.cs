using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using D=System.Drawing;

namespace CapturaDesktopPet {
 public static class PixelFont {
  sealed class Glyph {public int Width,Height;public bool[,] Pixels;}
  static readonly Dictionary<char,Glyph> cache=new Dictionary<char,Glyph>();
  static readonly Dictionary<char,string> patterns=new Dictionary<char,string>{
   {'0',"111/101/101/101/111"},{'1',"010/110/010/010/111"},{'2',"111/001/111/100/111"},{'3',"111/001/111/001/111"},
   {'4',"101/101/111/001/001"},{'5',"111/100/111/001/111"},{'6',"111/100/111/101/111"},{'7',"111/001/010/010/010"},
   {'8',"111/101/111/101/111"},{'9',"111/101/111/001/111"},{':',"0/1/0/1/0"},
   {'w',"00000/10001/10101/10101/01010"},{'x',"000/000/101/010/101"},{'c',"000/000/011/100/011"}
  };
  static Glyph Get(char c){
   Glyph glyph;if(cache.TryGetValue(c,out glyph))return glyph;
   string pattern;if(patterns.TryGetValue(c,out pattern)){
    string[] rows=pattern.Split('/');glyph=new Glyph{Width=rows[0].Length,Height=rows.Length,Pixels=new bool[rows[0].Length,rows.Length]};
    for(int y=0;y<glyph.Height;y++)for(int x=0;x<glyph.Width;x++)glyph.Pixels[x,y]=rows[y][x]=='1';
   }else{
    // Single-bit CJK glyphs are rendered as square cells, never antialiased text.
    using(var bitmap=new D.Bitmap(24,24)){
     using(var g=D.Graphics.FromImage(bitmap))using(var font=new D.Font("SimSun",14,D.FontStyle.Regular,D.GraphicsUnit.Pixel)){
      g.Clear(D.Color.Black);g.TextRenderingHint=D.Text.TextRenderingHint.SingleBitPerPixelGridFit;g.DrawString(c.ToString(),font,D.Brushes.White,new D.PointF(1,1),D.StringFormat.GenericTypographic);
     }
     int left=24,top=24,right=0,bottom=0;for(int y=0;y<24;y++)for(int x=0;x<24;x++)if(bitmap.GetPixel(x,y).R>128){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);}
     if(left==24){left=0;top=0;right=5;bottom=5;}
     glyph=new Glyph{Width=right-left,Height=bottom-top,Pixels=new bool[right-left,bottom-top]};
     for(int y=0;y<glyph.Height;y++)for(int x=0;x<glyph.Width;x++)glyph.Pixels[x,y]=bitmap.GetPixel(x+left,y+top).R>128;
    }
   }
   cache[c]=glyph;return glyph;
  }
  public static Size Measure(string text,double unit){double width=0,height=0;foreach(char c in text){var g=Get(c);width+=g.Width+1;height=Math.Max(height,g.Height);}return new Size(Math.Max(0,width-1)*unit,height*unit);}
  public static void Draw(DrawingContext dc,string text,Point origin,double unit,Brush brush){
   double x=origin.X;foreach(char c in text){var g=Get(c);for(int y=0;y<g.Height;y++)for(int column=0;column<g.Width;column++)if(g.Pixels[column,y])dc.DrawRectangle(brush,null,new Rect(x+column*unit,origin.Y+y*unit,unit,unit));x+=(g.Width+1)*unit;}
  }
  public static void Center(DrawingContext dc,string text,double centerX,double top,double unit,Brush brush){Size size=Measure(text,unit);Draw(dc,text,new Point(Math.Round(centerX-size.Width/2),Math.Round(top)),unit,brush);}
  public static void WarmUp(){foreach(string text in new[]{"截图","录屏开始","录屏暂停","录屏继续","录屏结束","00:00:00","wxc"})Measure(text,1);}
 }
 public sealed class PixelActionButton : Button {
  string label,kind;
  static readonly Dictionary<Rect,Geometry> shapes=new Dictionary<Rect,Geometry>();
  static readonly Brush outlineBrush=new SolidColorBrush(Color.FromRgb(22,39,65)),fillBrush=new SolidColorBrush(Color.FromRgb(242,255,244)),hoverBrush=new SolidColorBrush(Color.FromRgb(168,239,219)),shadowBrush=new SolidColorBrush(Color.FromArgb(80,22,39,65)),accentBrush=new SolidColorBrush(Color.FromRgb(0,137,136));
  public string Label {get{return label;}set{if(label==value)return;label=value;System.Windows.Automation.AutomationProperties.SetName(this,value);ToolTip=value;InvalidateVisual();}}
  public string Kind {get{return kind;}set{if(kind==value)return;kind=value;InvalidateVisual();}}
  public PixelActionButton(string text,string icon){Label=text;Kind=icon;Width=104;Height=42;Focusable=false;Cursor=Cursors.Hand;Template=new ControlTemplate(typeof(PixelActionButton));RenderOptions.SetEdgeMode(this,EdgeMode.Aliased);IsEnabledChanged+=delegate{InvalidateVisual();};Shape(0,3,104,39);Shape(0,0,104,39);Shape(2,2,100,35);}
  static Geometry Shape(double x,double y,double w,double h){
   Rect key=new Rect(x,y,w,h);Geometry cached;if(shapes.TryGetValue(key,out cached))return cached;
   var g=new StreamGeometry();using(var c=g.Open()){
    c.BeginFigure(new Point(x+8,y),true,true);
    foreach(var p in new[]{new Point(x+w-8,y),new Point(x+w-8,y+2),new Point(x+w-4,y+2),new Point(x+w-4,y+6),new Point(x+w,y+6),new Point(x+w,y+h-6),new Point(x+w-4,y+h-6),new Point(x+w-4,y+h-2),new Point(x+w-8,y+h-2),new Point(x+w-8,y+h),new Point(x+8,y+h),new Point(x+8,y+h-2),new Point(x+4,y+h-2),new Point(x+4,y+h-6),new Point(x,y+h-6),new Point(x,y+6),new Point(x+4,y+6),new Point(x+4,y+2),new Point(x+8,y+2)})c.LineTo(p,true,false);
   }g.Freeze();shapes[key]=g;return g;
  }
  protected override void OnMouseEnter(MouseEventArgs e){base.OnMouseEnter(e);InvalidateVisual();}
  protected override void OnMouseLeave(MouseEventArgs e){base.OnMouseLeave(e);InvalidateVisual();}
  protected override void OnRender(DrawingContext dc){
   double w=ActualWidth,h=ActualHeight-3;Brush outline=outlineBrush;Brush fill=IsMouseOver?hoverBrush:fillBrush;
   if(!IsEnabled)dc.PushOpacity(.45);if(IsPressed)dc.PushTransform(new TranslateTransform(0,2));
   dc.DrawGeometry(shadowBrush,null,Shape(0,3,w,h));dc.DrawGeometry(outline,null,Shape(0,0,w,h));dc.DrawGeometry(fill,null,Shape(2,2,w-4,h-4));
   double x=10,y=13;Brush accent=kind=="stop"?Brushes.IndianRed:accentBrush;
   if(kind=="shot"){dc.DrawRectangle(accent,null,new Rect(x,y,14,11));dc.DrawRectangle(fill,null,new Rect(x+3,y+3,8,5));dc.DrawRectangle(accent,null,new Rect(x+4,y-3,6,3));}
   else if(kind=="pause"){dc.DrawRectangle(accent,null,new Rect(x+2,y,4,13));dc.DrawRectangle(accent,null,new Rect(x+9,y,4,13));}
   else if(kind=="start" || kind=="resume"){for(int row=0;row<7;row++){int length=(row<4?row+1:7-row)*2;dc.DrawRectangle(accent,null,new Rect(x+2,y-1+row*2,length,2));}}
   else dc.DrawRectangle(accent,null,new Rect(x+2,y,12,12));
   Size text=PixelFont.Measure(label,1);PixelFont.Draw(dc,label,new Point(29,Math.Round((h-text.Height)/2)),1,outline);
   if(IsPressed)dc.Pop();if(!IsEnabled)dc.Pop();
  }
 }
 public sealed class OrbitArc : FrameworkElement {
  public const double Origin=416,SurfaceSize=832;
  double rotation,corner,scale=1;Vector offset;
  static readonly Brush arcBrush=new SolidColorBrush(Color.FromArgb(140,46,157,151));
  public void SetLayout(double angle,double cornerAmount,double radiusScale,Vector translation){if(rotation==angle && corner==cornerAmount && scale==radiusScale && offset==translation)return;rotation=angle;corner=cornerAmount;scale=radiusScale;offset=translation;InvalidateVisual();}
  public static Point CenterAt(double angle){return CenterAt(angle,0);}
  public static Point CenterAt(double angle,double rotation){return CenterAt(angle,rotation,0);}
  public static Point CenterAt(double angle,double rotation,double cornerAmount){return CenterAt(angle,rotation,cornerAmount,1);}
  public static Point CenterAt(double angle,double rotation,double cornerAmount,double scale){
   double r=(-90+(angle+90)*(1-cornerAmount+cornerAmount*90/130))*Math.PI/180,t=rotation*Math.PI/180;
   double x=(132+100*cornerAmount)*Math.Cos(r),y=(110+122*cornerAmount)*Math.Sin(r);
   return new Point(Origin+scale*(x*Math.Cos(t)-y*Math.Sin(t)),Origin+scale*(x*Math.Sin(t)+y*Math.Cos(t)));
  }
  protected override void OnRender(DrawingContext dc){for(double angle=-155;angle<=-25;angle+=3){Point p=CenterAt(angle,rotation,corner,scale)+offset;dc.DrawRectangle(arcBrush,null,new Rect(Math.Round(p.X/2)*2-2,Math.Round(p.Y/2)*2-2,4,4));}}
 }
}
