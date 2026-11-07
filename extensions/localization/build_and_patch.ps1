param([Parameter(Mandatory=$true)][string]$RuntimeDirectory,[Parameter(Mandatory=$true)][string]$CecilPath)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$runtime=(Resolve-Path -LiteralPath $RuntimeDirectory).Path
$framework='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs=@('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll')|ForEach-Object{'/r:'+(Join-Path "$framework\WPF" $_)}
$refs+=@("/r:$framework\System.Xaml.dll",'/r:System.Drawing.dll','/r:System.Windows.Forms.dll')
& "$framework\csc.exe" /nologo /target:library /optimize+ "/out:$runtime\lib\Captura.ChineseUi.dll" "/r:$runtime\lib\Newtonsoft.Json.dll" "/resource:$root\translations.json,CapturaChineseUi.translations.json" @refs "$root\ChineseUi.cs"
if($LASTEXITCODE -ne 0){throw 'Chinese UI compilation failed'}
Add-Type -Path $CecilPath
function Expand-ShortBranches($method){
 foreach($i in $method.Body.Instructions){if($i.OpCode.OperandType -eq [Mono.Cecil.Cil.OperandType]::ShortInlineBrTarget){
  $field=[Mono.Cecil.Cil.OpCodes].GetField(($i.OpCode.Code.ToString() -replace '_S$',''))
  if(!$field){throw 'Unknown short branch'};$i.OpCode=$field.GetValue($null)
 }}
}
$helper=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $runtime 'lib\Captura.ChineseUi.dll'))
$main=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $runtime 'captura.exe'))
try{
 $installer=($helper.MainModule.Types|Where-Object FullName -eq 'CapturaChineseUi.Installer').Methods|Where-Object Name -eq 'Install'
 $ctor=($main.MainModule.Types|Where-Object FullName -eq 'Captura.MainWindow').Methods|Where-Object {$_.Name -eq '.ctor' -and $_.Parameters.Count -eq 0}
 if(!@($ctor.Body.Instructions|Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $_.Operand -and $_.Operand.ToString() -match 'CapturaChineseUi.Installer::Install'}).Count){
  $il=$ctor.Body.GetILProcessor()
  foreach($ret in @($ctor.Body.Instructions|Where-Object OpCode -eq ([Mono.Cecil.Cil.OpCodes]::Ret))){
   $il.InsertBefore($ret,$il.Create([Mono.Cecil.Cil.OpCodes]::Ldarg_0));$il.InsertBefore($ret,$il.Create([Mono.Cecil.Cil.OpCodes]::Call,$main.MainModule.ImportReference($installer)))
  }
 }
 Expand-ShortBranches $ctor
 $main.Write((Join-Path $runtime 'captura.exe.localized'))
}finally{$main.Dispose();$helper.Dispose()}
[IO.File]::Move((Join-Path $runtime 'captura.exe.localized'),(Join-Path $runtime 'captura.exe'),$true)
# Native file/message dialogs do not create WPF text elements. Translate only
# these explicitly reviewed message/filter literals, never codec names or IDs.
$native=@(
 'Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.wmp;*.tiff',
 'PNG Image|*.png|JPEG Image|*.jpg;*.jpeg|Bitmap Image|*.bmp|TIFF Image|*.tiff',
 'No Image on Clipboard','Do you want to save your changes before exiting?',
 'Unsaved Changes','Do you want to save your changes?','Select Image',
 'A Recording is in progress. Are you sure you want to exit?','Confirm Exit',
 'Some Recordings have not finished writing to disk. Are you sure you want to exit?',
 'Copy to Clipboard failed','Confirm Deletion','Are you sure you want to Delete: ',
 'Could not find file: ','Could not open file: ','Could not Delete: '
)
$nativePatched=0
foreach($file in @('captura.exe','lib\Captura.Core.dll','lib\Captura.Base.dll')){
 $path=Join-Path $runtime $file;$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
 $local=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $runtime 'lib\Captura.ChineseUi.dll'))
 try{
  $translate=($local.MainModule.Types|Where-Object FullName -eq 'CapturaChineseUi.Translator').Methods|Where-Object Name -eq 'Translate'
  $call=$asm.MainModule.ImportReference($translate)
  foreach($type in $asm.MainModule.GetTypes()){foreach($method in $type.Methods){if(!$method.HasBody){continue};foreach($instruction in @($method.Body.Instructions)){
   if($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr -and $instruction.Operand -cin $native){
    if($instruction.Next -and $instruction.Next.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $instruction.Next.Operand.ToString() -match 'CapturaChineseUi.Translator::Translate'){continue}
    $method.Body.GetILProcessor().InsertAfter($instruction,[Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Call,$call));$nativePatched++
   }
  };if(@($method.Body.Instructions|Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $_.Operand -and $_.Operand.ToString() -match 'CapturaChineseUi.Translator::Translate'}).Count){Expand-ShortBranches $method}}}
  $asm.Write($path+'.localized')
 }finally{$asm.Dispose();$local.Dispose()}
 [IO.File]::Move($path+'.localized',$path,$true)
}
$zhPath=Join-Path $runtime 'languages\zh-CN.json'
$zh=Get-Content -LiteralPath $zhPath -Raw|ConvertFrom-Json -AsHashtable
$data=Get-Content -LiteralPath (Join-Path $root 'translations.json') -Raw|ConvertFrom-Json -AsHashtable
foreach($key in @('No Audio','Screenshot (Region)','ScreenShot (Screen)','ScreenShot (Window)')){$zh[$key]=$data.texts[$key]}
$zh['FFmpegFolder']='FFmpeg 目录';$zh['FFmpegLog']='FFmpeg 日志'
$zh|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $zhPath -Encoding utf8
Write-Output ('PASS: Chinese UI installed into staging; native dialog literals patched: '+$nativePatched+'; display labels preserve model IDs.')
