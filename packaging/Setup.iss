#define AppVersion "8.0.2026.1107"
#ifndef PayloadDir
 #define PayloadDir "payload"
#endif
#ifndef ReleaseDir
 #define ReleaseDir "..\..\outputs\Captura-WXC发布包\2026.11.06.1"
#endif
[Setup]
AppId={{B52172D4-2D2C-4BFA-B086-E7CFC42C7EC7}
AppName=Captura WXC
AppVersion={#AppVersion}
AppVerName=Captura WXC 2026.11.06.1
AppPublisher=Captura WXC contributors
AppPublisherURL=https://github.com/wxchill/Captura-WXC
AppSupportURL=https://github.com/wxchill/Captura-WXC/issues
DefaultDirName={localappdata}\Programs\Captura-WXC
DefaultGroupName=Captura WXC
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible and x86compatible and not arm64
MinVersion=10.0.19041
DisableProgramGroupPage=yes
DisableDirPage=no
OutputDir={#ReleaseDir}
OutputBaseFilename=Captura-WXC-2026.11.06.1-Setup-x64
Compression=lzma2/normal
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\captura.exe
Uninstallable=not IsExtractOnly
CreateUninstallRegKey=not IsExtractOnly
LicenseFile={#PayloadDir}\LICENSE-Captura.md
InfoBeforeFile=安装说明.txt
SetupIconFile=Captura.ico

[Languages]
Name: chinese; MessagesFile: compiler:Languages\ChineseSimplified.isl
Name: english; MessagesFile: compiler:Default.isl

[Tasks]
Name: desktopicon; Description: 创建桌面快捷方式

[Files]
Source: {#PayloadDir}\*; DestDir: {app}; Flags: ignoreversion recursesubdirs createallsubdirs
Source: VC_redist.x64.exe; Flags: dontcopy

[Icons]
Name: {userprograms}\Captura WXC; Filename: {app}\captura.exe; Check: not IsExtractOnly
Name: {userdesktop}\Captura WXC; Filename: {app}\captura.exe; Tasks: desktopicon; Check: not IsExtractOnly

[Run]
Filename: {app}\captura.exe; Description: 启动 Captura WXC; Flags: nowait postinstall skipifsilent unchecked

[Code]
function IsExtractOnly: Boolean;
begin
 Result := ExpandConstant('{param:EXTRACTONLY|0}') = '1';
end;

function InitializeSetup: Boolean;
var Release: Cardinal;
begin
 Result := RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
 if not Result then
  SuppressibleMsgBox('需要 .NET Framework 4.8 或更新版本。请通过 Windows 更新安装后重试。', mbError, MB_OK, IDOK);
end;

function NeedVC: Boolean;
var Major, Minor: Cardinal;
begin
 Result := not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Major', Major);
 if not Result then begin
  if not RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Minor', Minor) then Result := True
  else Result := (Major < 14) or ((Major = 14) and (Minor < 50));
 end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
 Result := '';
 if IsExtractOnly then exit;
 if NeedVC then begin
  ExtractTemporaryFile('VC_redist.x64.exe');
  if not ShellExec('runas', ExpandConstant('{tmp}\VC_redist.x64.exe'), '/install /passive /norestart', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
   Result := '未能安装截图识别需要的 Microsoft Visual C++ 运行库，请允许安装后重试。'
  else if (ExitCode <> 0) and (ExitCode <> 3010) and (ExitCode <> 1638) then
   Result := 'Microsoft Visual C++ 运行库安装失败，错误码：' + IntToStr(ExitCode)
  else if ExitCode = 3010 then NeedsRestart := True;
 end;
end;
