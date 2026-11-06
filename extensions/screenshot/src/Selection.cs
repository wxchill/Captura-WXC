using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapturaScreenshotTools {
 public sealed class RegionSelector : Form {
  readonly Bitmap desktop; Point anchor,current; bool selecting;
  public Bitmap SelectedImage {get;private set;}
  public RegionSelector(Bitmap image,Rectangle desktopBounds){
   desktop=image;AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;
   StartPosition=FormStartPosition.Manual;Bounds=desktopBounds;TopMost=true;ShowInTaskbar=false;
   Cursor=Cursors.Cross;DoubleBuffered=true;Text="框选截图 · 右键取消";
  }
  public static Rectangle Normalize(Point a,Point b,Size size){
   int ax=Math.Max(0,Math.Min(size.Width,a.X)),ay=Math.Max(0,Math.Min(size.Height,a.Y));
   int bx=Math.Max(0,Math.Min(size.Width,b.X)),by=Math.Max(0,Math.Min(size.Height,b.Y));
   return Rectangle.FromLTRB(Math.Min(ax,bx),Math.Min(ay,by),Math.Max(ax,bx),Math.Max(ay,by));
  }
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.DrawImageUnscaled(desktop,0,0);
   using(var dim=new SolidBrush(Color.FromArgb(115,0,0,0)))e.Graphics.FillRectangle(dim,ClientRectangle);
   Rectangle rect=Normalize(anchor,current,desktop.Size);
   if(selecting && rect.Width>0 && rect.Height>0){
    e.Graphics.DrawImage(desktop,rect,rect,GraphicsUnit.Pixel);
    using(var pen=new Pen(Color.FromArgb(39,181,255),2))e.Graphics.DrawRectangle(pen,rect);
   }
   string tip=selecting?rect.Width+" × "+rect.Height+"   松开鼠标完成，右键取消":"按住鼠标左键框选截图位置，右键取消";
   int x=selecting?Math.Max(12,Math.Min(ClientSize.Width-450,rect.Left)):28;
   int y=selecting?Math.Max(12,rect.Top-44):28;
   using(var bg=new SolidBrush(Color.FromArgb(230,24,32,43)))e.Graphics.FillRectangle(bg,x,y,440,36);
   using(var f=new Font("Microsoft YaHei",11))e.Graphics.DrawString(tip,f,Brushes.White,x+10,y+8);
  }
  protected override void OnMouseDown(MouseEventArgs e){
   if(e.Button==MouseButtons.Right){DialogResult=DialogResult.Cancel;Close();return;}
   if(e.Button!=MouseButtons.Left)return;
   anchor=e.Location;current=e.Location;selecting=true;Capture=true;Invalidate();
  }
  protected override void OnMouseMove(MouseEventArgs e){if(selecting){current=e.Location;Invalidate();}}
  protected override void OnMouseUp(MouseEventArgs e){
   if(e.Button!=MouseButtons.Left || !selecting)return;
   current=e.Location;Capture=false;Rectangle rect=Normalize(anchor,current,desktop.Size);
   if(rect.Width<4 || rect.Height<4){selecting=false;Invalidate();return;}
   SelectedImage=desktop.Clone(rect,PixelFormat.Format32bppArgb);DialogResult=DialogResult.OK;Close();
  }
  public new static Bitmap Select(){
   Rectangle bounds=SystemInformation.VirtualScreen;
   using(var image=new Bitmap(bounds.Width,bounds.Height,PixelFormat.Format32bppArgb)){
    using(Graphics g=Graphics.FromImage(image))g.CopyFromScreen(bounds.Location,Point.Empty,bounds.Size,CopyPixelOperation.SourceCopy);
    using(var selector=new RegionSelector(image,bounds)){
     selector.ShowDialog();return selector.SelectedImage;
    }
   }
  }
 }
}
