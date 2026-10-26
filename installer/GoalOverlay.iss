; Script Inno Setup đóng gói ứng dụng GoalOverlay
; Yêu cầu Inno Setup 6 trở lên (chạy lệnh: winget install JRSoftware.InnoSetup)

#define MyAppName "GoalOverlay"
#define MyAppVersion "1.1.0"
#define MyAppPublisher "GoalOverlay"
#define MyAppURL "https://github.com/dangbinhduongwork-ai/Target_X"
#define MyAppExeName "GoalOverlay.exe"

[Setup]
; AppId duy nhất đại diện cho ứng dụng GoalOverlay
AppId={{E8C5D30F-5B6E-4E9B-B12A-18F7E4D1B001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Cài đặt cho người dùng thông thường, không yêu cầu UAC Administrator
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest

; Thư mục xuất ra và tên file cài đặt
OutputDir=output
OutputBaseFilename=GoalOverlay-Setup
SetupIconFile=..\src\GoalOverlay\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Thuật toán nén tối đa
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible

; Tự động đóng GoalOverlay nếu đang chạy trước khi cài đè hoặc gỡ cài đặt
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Khởi động cùng Windows (chạy ngầm trong khay hệ thống)"; GroupDescription: "Tùy chọn khởi động:"; Flags: checkedonce

[Files]
; File thuc thi doc lap duy nhat cua ung dung (Single-file self-contained)
Source: "..\src\GoalOverlay\bin\Release\net8.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Ghi Registry tự chạy khi người dùng tích chọn task 'autostart'
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Gỡ cài đặt sạch sẽ (Clean Uninstall): Xóa Registry Run và tùy chọn xóa dữ liệu %APPDATA%
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDir: String;
  MsgResult: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // 1. Luôn bảo đảm xóa giá trị Registry Run
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#MyAppName}');

    // 2. Hỏi người dùng có muốn xóa sạch thư mục dữ liệu cá nhân trong %APPDATA%\GoalOverlay không
    AppDataDir := ExpandConstant('{userappdata}\{#MyAppName}');
    if DirExists(AppDataDir) then
    begin
      MsgResult := MsgBox(
        'Bạn có muốn xóa toàn bộ dữ liệu mục tiêu và cấu hình cá nhân tại:' + #13#10 +
        AppDataDir + ' không?' + #13#10#13#10 +
        'Chọn Yes để xóa sạch dữ liệu.' + #13#10 +
        'Chọn No để giữ lại mục tiêu cho lần cài đặt sau.',
        mbConfirmation, MB_YESNO
      );
      if MsgResult = IDYES then
      begin
        DelTree(AppDataDir, True, True, True);
      end;
    end;
  end;
end;
