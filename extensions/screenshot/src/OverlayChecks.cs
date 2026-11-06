using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CapturaScreenshotTools;
class OverlayChecks {
 static void Need(bool b,string message){if(!b)throw new Exception(message);}
 static byte[] Pixels(BitmapSource bitmap){var data=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0).CopyPixels(data,bitmap.PixelWidth*4,0);return data;}
 [STAThread] static int Main(){try{
  string root=AppDomain.CurrentDomain.BaseDirectory;string job=Path.Combine(root,"overlay-check-job");Directory.CreateDirectory(job);
  var original=Program.DemoImage();string input=Path.Combine(job,"input.png");Editor.WritePng(original,input);
  OcrDocument document=Services.RecognizeDocumentAsync(input,job,CancellationToken.None).GetAwaiter().GetResult();
  Need(document.Lines.Count==4,"OCR layout did not return four text regions");
  string translation=Services.TranslateLocalAsync(document.Text,"en",job,CancellationToken.None).GetAwaiter().GetResult();
  string[] lines=translation.Replace("\r\n","\n").Split('\n');Need(lines.Length==document.Lines.Count,"Translated lines lost spatial correspondence");
  var regions=new List<TranslationRegion>();for(int i=0;i<lines.Length;i++)regions.Add(new TranslationRegion{Bounds=document.Lines[i].Bounds,Text=lines[i]});
  BitmapSource rendered=TranslationOverlay.Render(original,regions);Need(rendered.PixelWidth==original.PixelWidth && rendered.PixelHeight==original.PixelHeight,"Overlay changed image dimensions");
  byte[] before=Pixels(original),after=Pixels(rendered);int changed=0;
  var painted=new List<Rect>();foreach(var item in regions){var rect=item.Bounds;rect.Inflate(Math.Max(2,Math.Min(6,rect.Height*.10)),Math.Max(2,Math.Min(6,rect.Height*.10)));painted.Add(rect);}
  for(int y=0;y<original.PixelHeight;y++)for(int x=0;x<original.PixelWidth;x++){
   int at=(y*original.PixelWidth+x)*4;bool different=before[at]!=after[at] || before[at+1]!=after[at+1] || before[at+2]!=after[at+2];
   if(different){changed++;Need(painted.Any(r=>r.Contains(new Point(x+.5,y+.5))),"Pixels outside OCR areas were changed");}
  }
  Need(changed>1000,"Translation image did not replace the English glyphs");
  string output=Path.Combine(root,"overlay-actual-ocr-result.png");Editor.WritePng(rendered,output);
  string reread=Services.RecognizeAsync(output,job,CancellationToken.None).GetAwaiter().GetResult();
  Need(!reread.ToLowerInvariant().Contains("satellite") && !reread.ToLowerInvariant().Contains("water quality"),"Original English remained visible under translation");
  Need(reread.Any(c=>c>='\u4e00' && c<='\u9fff'),"Chinese overlay was not readable");
  byte[] darkPixels=new byte[400*100*4];for(int i=0;i<darkPixels.Length;i+=4){darkPixels[i]=45;darkPixels[i+1]=30;darkPixels[i+2]=20;darkPixels[i+3]=255;}
  var dark=BitmapSource.Create(400,100,96,96,PixelFormats.Bgra32,null,darkPixels,400*4);
  byte[] darkResult=Pixels(TranslationOverlay.Render(dark,new[]{new TranslationRegion{Bounds=new Rect(20,20,250,40),Text="深色背景中文覆盖"}}));
  Need(darkResult[0]==45 && darkResult[1]==30 && darkResult[2]==20,"Dark image background changed outside text area");
  int light=0;for(int i=0;i<darkResult.Length;i+=4)if(darkResult[i]>220 && darkResult[i+1]>220 && darkResult[i+2]>220)light++;Need(light>100,"Dark background did not use readable light text");
  Rect scaled=TranslationOverlay.ScaleBounds(new Rect(10,20,100,30),2);Need(scaled==new Rect(20,40,200,60),"Resized OCR coordinates mapped incorrectly");
  var editor=new Editor(original,root);editor.PopulatePreview();Need(editor.ShowingTranslation,"Translation was not the default displayed image");byte[] first=Pixels(editor.Composite());
  editor.ToggleTranslation();Need(!editor.ShowingTranslation,"Original view could not be restored");Need(!Pixels(editor.Composite()).SequenceEqual(first),"Original view still showed translation");
  editor.ToggleTranslation();Need(Pixels(editor.Composite()).SequenceEqual(first),"Switching back lost translation or annotations");editor.Close();
  bool overflow=false;try{TranslationOverlay.Render(original,new[]{new TranslationRegion{Bounds=new Rect(1,1,3,3),Text=new string('中',1000)}});}catch(InvalidOperationException){overflow=true;}
  Need(overflow,"An overlong translation was silently clipped");
  Services.WriteJson(Path.Combine(root,"overlay-test-result.json"),new{ok=true,regions=document.Lines.Count,translation=translation,reread=reread,changed_pixels=changed});
  Console.WriteLine("PASS: OCR-coordinate translation, original-text removal, Chinese rendering, unchanged outside pixels, full-resolution export, scale mapping, original/translated toggle with annotation preservation, overlong-text rejection.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
