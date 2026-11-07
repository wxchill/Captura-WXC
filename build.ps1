param(
 [string]$RuntimeDirectory,
 [switch]$Setup,
 [string]$InnoCompiler,
 [string]$VCRedist
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$package=Join-Path $root 'packaging'
$payload=Join-Path $package 'payload'
if($Setup -and (!$RuntimeDirectory -or !$InnoCompiler -or !$VCRedist)){throw 'Setup requires RuntimeDirectory, InnoCompiler and VCRedist; see docs/BUILD.md.'}
New-Item -ItemType Directory -Path $payload -Force|Out-Null
if($RuntimeDirectory){
 if(!(Test-Path -LiteralPath (Join-Path $RuntimeDirectory 'captura.exe'))){throw 'Runtime directory has no captura.exe'}
 foreach($file in @('captura.exe','captura.exe.config','ffmpeg.exe','ffprobe.exe','LICENSE-Captura.md')){
  $path=Join-Path $RuntimeDirectory $file;if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination $payload -Force}
 }
 New-Item -ItemType Directory -Path "$payload\lib" -Force|Out-Null
 Get-ChildItem -LiteralPath (Join-Path $RuntimeDirectory 'lib') -Filter '*.dll' -File|Copy-Item -Destination "$payload\lib" -Force
 foreach($dir in @('languages','keymaps','licenses','lib\ScreenshotTools')){
  $path=Join-Path $RuntimeDirectory $dir;if(Test-Path -LiteralPath $path){
   $dest=Join-Path $payload (Split-Path $dir -Parent)
   Copy-Item -LiteralPath $path -Destination $dest -Recurse -Force
  }
 }
}else{Expand-Archive -LiteralPath (Join-Path $root 'vendor\legacy-runtime.zip') -DestinationPath $payload -Force}
$framework='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$csc=Join-Path $framework 'csc.exe'
$refs=@('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll')|ForEach-Object{'/r:'+(Join-Path "$framework\WPF" $_)}
$refs+=@("/r:$framework\System.Xaml.dll",'/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Net.Http.dll','/r:System.Web.Extensions.dll')
$pet=Join-Path $root 'extensions\desktop-pet'
& $csc /nologo /target:library /optimize+ "/out:$payload\lib\Captura.DesktopPet.dll" "/resource:$pet\assets\recording-device-final.png,CapturaDesktopPet.device.png" @refs "$pet\PetAdapter.cs" "$pet\PixelGraphics.cs" "$pet\MascotView.cs" "$pet\PetWindow.cs" "$pet\Install.cs"
if($LASTEXITCODE -ne 0){throw 'Desktop pet compilation failed'}
$shot=Join-Path $root 'extensions\screenshot'
New-Item -ItemType Directory -Path "$payload\lib\ScreenshotTools" -Force|Out-Null
& $csc /nologo /target:winexe /optimize+ "/win32manifest:$shot\app.manifest" "/out:$payload\lib\ScreenshotTools\Captura.ScreenshotTools.exe" @refs "$shot\src\Selection.cs" "$shot\src\Services.cs" "$shot\src\TranslationOverlay.cs" "$shot\src\Editor.cs"
if($LASTEXITCODE -ne 0){throw 'Screenshot compilation failed'}
& $csc /nologo /target:library /optimize+ "/out:$payload\lib\Captura.ScreenshotBridge.dll" @refs "$shot\src\Bridge.cs"
if($LASTEXITCODE -ne 0){throw 'Screenshot bridge compilation failed'}
& $csc /nologo /target:library /optimize+ "/out:$payload\lib\Captura.SingleInstance.dll" @refs "$root\extensions\single-instance\SingleInstance.cs"
if($LASTEXITCODE -ne 0){throw 'Single instance compilation failed'}
& "$package\compile_release.ps1" -SkipInstaller -CecilPath "$root\vendor\Mono.Cecil.dll"
& "$root\extensions\localization\build_and_patch.ps1" -RuntimeDirectory $payload -CecilPath "$root\vendor\Mono.Cecil.dll"
if($Setup){
 if(!(Test-Path -LiteralPath "$payload\ffmpeg.exe") -or !(Test-Path -LiteralPath "$payload\lib\ScreenshotTools\backend\python\python.exe")){throw 'Full FFmpeg and screenshot backend are required'}
 Copy-Item -LiteralPath $VCRedist -Destination "$package\VC_redist.x64.exe" -Force
 & $InnoCompiler "/O$root\release" "$package\Setup.iss"
 if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
}
Write-Output ('Built runtime: '+$payload)
