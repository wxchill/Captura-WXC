param(
 [switch]$SkipInstaller,
 [string]$CecilPath=(Join-Path $PSScriptRoot '..\captura-status\cecil\lib\netstandard2.0\Mono.Cecil.dll'),
 [string]$InnoCompiler=(Join-Path $PSScriptRoot 'tools\InnoSetup\ISCC.exe')
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$payload=Join-Path $root 'payload'
$framework='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs=@('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll')|ForEach-Object{'/r:'+(Join-Path "$framework\WPF" $_)}
$refs+=@("/r:$framework\System.Xaml.dll",'/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Net.Http.dll','/r:System.Web.Extensions.dll')
$csc=Join-Path $framework 'csc.exe'
& $csc /nologo /target:library /optimize+ "/out:$payload\lib\Captura.Distribution.dll" "/r:$payload\lib\Captura.Base.dll" "/r:$payload\lib\Newtonsoft.Json.dll" "$root\src\Bootstrap.cs"
if($LASTEXITCODE -ne 0){throw 'Distribution bootstrap build failed'}
& $csc /nologo /target:library /optimize+ "/out:$payload\lib\Captura.SaveDialogs.dll" @refs "$root\src\Recovered\Captura.SaveDialogs.cs" "$root\src\Recovered\SafeRecordingSave.cs"
if($LASTEXITCODE -ne 0){throw 'Safe save dialog build failed'}
& $csc /nologo /target:library /optimize+ "/out:$payload\lib\Captura.RecordingStatus.dll" @refs "$root\src\Recovered\Captura.RecordingStatus.cs"
if($LASTEXITCODE -ne 0){throw 'Recording status build failed'}
& $csc /nologo /target:library /optimize+ "/out:$payload\lib\Captura.RecordButtons.dll" @refs "$root\src\Recovered\Captura.RecordButtons.cs"
if($LASTEXITCODE -ne 0){throw 'Record button build failed'}
Add-Type -Path $CecilPath
$hostFile=Join-Path $payload 'captura.exe'
$hostAssembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($hostFile)
$bootstrap=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $payload 'lib\Captura.Distribution.dll'))
try {
 $main=($hostAssembly.MainModule.Types|Where-Object FullName -eq 'Captura.App').Methods|Where-Object Name -eq 'Main'
 $il=$main.Body.GetILProcessor()
 foreach($old in @($main.Body.Instructions|Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $_.Operand -and $_.Operand.ToString() -match 'CapturaDistribution.Bootstrap::Initialize'})){
  foreach($instruction in $main.Body.Instructions){if($instruction.Operand -is [Mono.Cecil.Cil.Instruction] -and $instruction.Operand -eq $old){$instruction.Operand=$old.Next}}
  $il.Remove($old)
 }
 $gate=$main.Body.Instructions|Where-Object {$_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and $_.Operand -and $_.Operand.ToString() -match 'SingleInstance::TryEnter'}|Select-Object -First 1
 if(!$gate -or $gate.Next.OpCode.Code -notin @([Mono.Cecil.Cil.Code]::Brtrue,[Mono.Cecil.Cil.Code]::Brtrue_S)){throw 'Single-instance success branch not found'}
 $init=($bootstrap.MainModule.Types|Where-Object FullName -eq 'CapturaDistribution.Bootstrap').Methods|Where-Object Name -eq 'Initialize'
 $call=[Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Call,$hostAssembly.MainModule.ImportReference($init))
 $il.InsertBefore($gate.Next.Operand,$call);$gate.Next.Operand=$call
 $hostAssembly.Write($hostFile+'.new')
}finally{$hostAssembly.Dispose();$bootstrap.Dispose()}
[IO.File]::Move($hostFile+'.new',$hostFile,$true)
$ffFile=Join-Path $payload 'lib\Captura.FFmpeg.dll'
$ff=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($ffFile)
try {
 $methods=($ff.MainModule.Types|Where-Object FullName -eq 'Captura.FFmpegService').Methods|Where-Object {$_.Name -in 'get_FFmpegExists','get_FFmpegExePath'}
 # Import framework types from the target's mscorlib scope, not PowerShell's
 # modern System.Private.CoreLib, so the result remains a .NET Framework DLL.
 $pathType=[Mono.Cecil.TypeReference]::new('System.IO','Path',$ff.MainModule,$ff.MainModule.TypeSystem.Object.Scope)
 $directory=[Mono.Cecil.MethodReference]::new('GetDirectoryName',$ff.MainModule.TypeSystem.String,$pathType)
 $directory.Parameters.Add([Mono.Cecil.ParameterDefinition]::new($ff.MainModule.TypeSystem.String))
 $patched=0
 foreach($method in $methods){foreach($instruction in @($method.Body.Instructions)){
  if($instruction.Operand -and $instruction.Operand.ToString() -match 'Assembly::get_Location' -and $instruction.Next.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Call){
   $method.Body.GetILProcessor().InsertAfter($instruction,[Mono.Cecil.Cil.Instruction]::Create([Mono.Cecil.Cil.OpCodes]::Call,$directory));$patched++
  }
 }}
 if($patched -ne 0 -and $patched -ne 2){throw ('Unexpected encoder lookup count '+$patched)}
 $ff.Write($ffFile+'.new')
}finally{$ff.Dispose()}
[IO.File]::Move($ffFile+'.new',$ffFile,$true)
Copy-Item -LiteralPath (Join-Path $root '安装说明.txt') -Destination (Join-Path $payload '使用说明.txt') -Force
Write-Output 'PASS: staged bootstrap, safe recording save, status failure feedback, encoder fallback path.'
if(!$SkipInstaller){
 & $InnoCompiler (Join-Path $root 'Setup.iss')
 if($LASTEXITCODE -ne 0){throw 'Setup build failed'}
}
