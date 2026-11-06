using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections;
using System.Windows;

namespace CapturaScreenshotTools {
 public sealed class OcrLine {
  public string Text;
  public Rect Bounds;
 }
 public sealed class OcrDocument {
  public string Text;
  public List<OcrLine> Lines=new List<OcrLine>();
 }
 public static class Services {
  public static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};
  public static string Quote(string text){
   if(text==null)throw new ArgumentNullException("text");
   var result=new StringBuilder("\"");int slashes=0;
   foreach(char ch in text){if(ch=='\\'){slashes++;continue;}if(ch=='\"'){result.Append('\\',slashes*2+1);result.Append('"');}else{result.Append('\\',slashes);result.Append(ch);}slashes=0;}
   result.Append('\\',slashes*2);result.Append('"');return result.ToString();
  }
  public static Dictionary<string,object> ReadJson(string path){return Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));}
  public static void WriteJson(string path,object value){File.WriteAllText(path,Json.Serialize(value),new UTF8Encoding(false));}
  public static string ConfiguredPython(){return Path.Combine(Root,"backend","python","python.exe");}
  public static async Task<string> RecognizeAsync(string image,string job,CancellationToken cancel){
   string result=Path.Combine(job,"ocr-result.json");
   if(File.Exists(Path.Combine(Root,"backend","site","rapidocr_onnxruntime","__init__.py")))
    return await RunWorkerAsync(ConfiguredPython(),Quote(Path.Combine(Root,"backend","ocr_worker.py"))+" "+Quote(image)+" "+Quote(result),result,cancel);
   string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
   string args="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(Path.Combine(Root,"backend","windows_ocr.ps1"))+" -ImagePath "+Quote(image)+" -ResultPath "+Quote(result);
   return await RunWorkerAsync(powershell,args,result,cancel);
  }
  public static async Task<OcrDocument> RecognizeDocumentAsync(string image,string job,CancellationToken cancel){
   string text=await RecognizeAsync(image,job,cancel);
   var document=new OcrDocument{Text=text};var data=ReadJson(Path.Combine(job,"ocr-result.json"));
   if(data.ContainsKey("lines"))foreach(object value in (IEnumerable)data["lines"]){
    var line=(Dictionary<string,object>)value;
    double left=Double.MaxValue,top=Double.MaxValue,right=Double.MinValue,bottom=Double.MinValue;
    foreach(object point in (IEnumerable)line["box"]){
     var coordinates=new List<double>();foreach(object number in (IEnumerable)point)coordinates.Add(Convert.ToDouble(number));
     if(coordinates.Count<2)continue;
     left=Math.Min(left,coordinates[0]);right=Math.Max(right,coordinates[0]);top=Math.Min(top,coordinates[1]);bottom=Math.Max(bottom,coordinates[1]);
    }
    if(right>left && bottom>top)document.Lines.Add(new OcrLine{Text=Convert.ToString(line["text"]),Bounds=new Rect(left,top,right-left,bottom-top)});
   }
   return document;
  }
  public static async Task<string> TranslateLocalAsync(string text,string source,string job,CancellationToken cancel){
   string request=Path.Combine(job,"translate-request.json"),result=Path.Combine(job,"translate-result.json");
   WriteJson(request,new{text=text,source=source});
   return await RunWorkerAsync(ConfiguredPython(),Quote(Path.Combine(Root,"backend","local_translate.py"))+" "+Quote(request)+" "+Quote(result),result,cancel);
  }
  static async Task<string> RunWorkerAsync(string executable,string args,string result,CancellationToken cancel){
   if(!File.Exists(executable))throw new InvalidOperationException("本地运行环境尚未准备好，请先运行准备脚本。");
   if(File.Exists(result))File.Delete(result);
   var info=new ProcessStartInfo(executable,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
   using(var process=Process.Start(info)){
    Task<string> error=process.StandardError.ReadToEndAsync(),output=process.StandardOutput.ReadToEndAsync();
    using(cancel.Register(delegate{try{if(!process.HasExited)process.Kill();}catch{}})){
     bool ended=await Task.Run(()=>process.WaitForExit(120000));
     if(!ended){try{process.Kill();}catch{}throw new TimeoutException("处理超时，请减少文字或缩小截图后重试。");}
     cancel.ThrowIfCancellationRequested();
     string stderr=await error;await output;
     if(!File.Exists(result))throw new InvalidOperationException("本地处理失败："+stderr.Substring(0,Math.Min(300,stderr.Length)));
     var response=ReadJson(result);
     if(!response.ContainsKey("ok") || !(bool)response["ok"])throw new InvalidOperationException(response.ContainsKey("error")?Convert.ToString(response["error"]):"处理失败。");
     return Convert.ToString(response["text"]);
    }
   }
  }
  public static List<string> SplitUtf8(string text,int limit){
   var chunks=new List<string>();var current=new StringBuilder();int count=0;
   for(int i=0;i<text.Length;i++){
    string value=Char.IsHighSurrogate(text[i]) && i+1<text.Length && Char.IsLowSurrogate(text[i+1])?text.Substring(i++,2):text[i].ToString();
    int bytes=Encoding.UTF8.GetByteCount(value);
    if(count+bytes>limit && current.Length>0){chunks.Add(current.ToString());current.Clear();count=0;}
    current.Append(value);count+=bytes;
   }
   if(current.Length>0)chunks.Add(current.ToString());return chunks;
  }
  public static async Task<string> TranslateOnlineAsync(string text,string source,CancellationToken cancel){
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   using(var client=new HttpClient()){
    client.Timeout=TimeSpan.FromSeconds(35);var paragraphs=new List<string>();
    foreach(string paragraph in text.Replace("\r\n","\n").Split('\n')){
     if(String.IsNullOrWhiteSpace(paragraph)){paragraphs.Add("");continue;}
     var translated=new StringBuilder();
     foreach(string chunk in SplitUtf8(paragraph,450)){
      string url="https://api.mymemory.translated.net/get?q="+Uri.EscapeDataString(chunk)+"&langpair="+Uri.EscapeDataString(source+"|zh-CN");
      using(var response=await client.GetAsync(url,cancel)){
       response.EnsureSuccessStatusCode();string body=await response.Content.ReadAsStringAsync();
       var data=Json.Deserialize<Dictionary<string,object>>(body);
       if(!data.ContainsKey("responseStatus") || Convert.ToInt32(data["responseStatus"])!=200)
        throw new InvalidOperationException("联网翻译未完成："+(data.ContainsKey("responseDetails")?Convert.ToString(data["responseDetails"]):"服务暂不可用，可能已达到额度限制。"));
       var result=(Dictionary<string,object>)data["responseData"];
       translated.Append(WebUtility.HtmlDecode(Convert.ToString(result["translatedText"])));
      }
     }
     paragraphs.Add(translated.ToString());
    }
    return String.Join(Environment.NewLine,paragraphs);
   }
  }
 }
}
