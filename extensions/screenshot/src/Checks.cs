using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Web.Script.Serialization;
using CapturaScreenshotTools;
class Checks {
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 [STAThread] static int Main(string[] args){
  Console.OutputEncoding=new UTF8Encoding(false);
  if(args.Length==2 && args[0]=="--echo"){Console.Write(new JavaScriptSerializer().Serialize(args[1]));return 0;}
  string root=AppDomain.CurrentDomain.BaseDirectory;
  string job=Path.Combine(root,"test-job 中文");Directory.CreateDirectory(job);
  try{
   var r=RegionSelector.Normalize(new System.Drawing.Point(500,400),new System.Drawing.Point(50,40),new System.Drawing.Size(1920,1080));
   Require(r.X==50 && r.Y==40 && r.Width==450 && r.Height==360,"Reverse-drag crop failed");
   r=RegionSelector.Normalize(new System.Drawing.Point(-20,-50),new System.Drawing.Point(2200,1200),new System.Drawing.Size(1920,1080));
   Require(r.X==0 && r.Y==0 && r.Width==1920 && r.Height==1080,"Bounds clamp failed");
   string longText=String.Concat(Enumerable.Repeat("Water 湖泊水质 😀 ",100));
   var chunks=Services.SplitUtf8(longText,450);Require(String.Concat(chunks)==longText,"Translation chunks changed text");
   Require(chunks.All(c=>Encoding.UTF8.GetByteCount(c)<=450),"UTF8 byte limit exceeded");
   Require(chunks.All(c=>!Char.IsHighSurrogate(c[c.Length-1]) && !Char.IsLowSurrogate(c[0])),"Emoji split between requests");
   foreach(string value in new[]{"C:\\folder with spaces\\","中文 路径\\with\"quote\\", ""}){
    var start=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--echo "+Services.Quote(value)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true};
    using(var p=Process.Start(start)){string output=p.StandardOutput.ReadToEnd();p.WaitForExit();Require(new JavaScriptSerializer().Deserialize<string>(output)==value,"Process argument round-trip failed");}
   }
   var editor=new Editor(Program.DemoImage(),root);editor.PopulatePreview();
   var composite=editor.Composite();Require(composite.PixelWidth==960 && composite.PixelHeight==560,"Annotation export changed dimensions");
   byte[] pixel=new byte[4];composite.CopyPixels(new Int32Rect(100,240,1,1),pixel,4,0);
   Require(pixel[2]>200 && pixel[1]<100,"Pen stroke missing from exported PNG");
   Editor.WritePng(composite,Path.Combine(root,"annotation-test.png"));
   typeof(Editor).GetMethod("Undo",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(editor,null);
   editor.Composite().CopyPixels(new Int32Rect(100,240,1,1),pixel,4,0);Require(pixel[1]>240,"Undo did not remove stroke");editor.Close();
   string ocr=Services.RecognizeAsync(Path.Combine(root,"ocr-test-input.png"),job,CancellationToken.None).GetAwaiter().GetResult();
   Require(ocr.ToLowerInvariant().Contains("satellite images") && ocr.ToLowerInvariant().Contains("water quality"),"OCR missed English words or spaces: "+ocr);
   string translated=Services.TranslateLocalAsync("Satellite images are used to retrieve lake water quality.","en",job,CancellationToken.None).GetAwaiter().GetResult();
   Require(translated.Any(c=>c>='\u4e00' && c<='\u9fff'),"Offline model did not produce Chinese");
   Services.WriteJson(Path.Combine(root,"offline-test-result.json"),new{ocr=ocr,translation=translated});
   if(args.Contains("--online")){
    string online=Services.TranslateOnlineAsync("The lake water is clear.","en",CancellationToken.None).GetAwaiter().GetResult();
    Require(online.Any(c=>c>='\u4e00' && c<='\u9fff'),"Online translation did not produce Chinese");
    Services.WriteJson(Path.Combine(root,"online-test-result.json"),new{text=online});
   }
   var cancelled=new CancellationTokenSource();cancelled.Cancel();bool gotCancel=false;
   try{Services.RecognizeAsync(Path.Combine(root,"ocr-test-input.png"),job,cancelled.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){gotCancel=true;}
   Require(gotCancel,"Worker cancellation was not honored");
   Console.WriteLine("PASS: crop coordinates, UTF8 chunking, process argument quoting, annotation export/undo, offline OCR, offline translation, cancellation"+(args.Contains("--online")?", online translation":"")+". No desktop captured.");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
}
