; Inno Setup Script for RotinaAddonAnydesk Windows Service & Tray Agent
#define MyAppName "RotinaAddonAnydesk"
#define MyAppVersion "1.1.2"
#define MyAppPublisher "Rotina"
#define MyAppURL "https://github.com/RotinaAddonAnydesk"
#define MyAppExeName "RotinaAddonAnydesk.exe"
#define MyAppServiceName "RotinaAddonAnydesk"

[Setup]
AppId={{D37F2190-2B1A-4F8A-9A71-8898F6B12A34}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={commonappdata}\{#MyAppName}
UsePreviousAppDir=yes
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=publish\Installer
OutputBaseFilename=RotinaAddonAnydesk-Setup
SetupIconFile=app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
CloseApplications=yes

[Languages]
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostarttray"; Description: "Iniciar agente da barra de tarefas (Tray) ao iniciar o Windows"; GroupDescription: "Arranque Automático"

[Dirs]
Name: "{app}"; Permissions: users-full; Attribs: hidden

[Files]
Source: "publish\Agent\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace
Source: "publish\Api\*"; DestDir: "{app}\Api"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace
Source: "publish\Web\*"; DestDir: "{app}\Web"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion restartreplace
Source: "agentconfig.json"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName} Tray"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; IconFilename: "{app}\app.ico"
Name: "{group}\Abrir Painel Web"; Filename: "explorer.exe"; Parameters: """http://localhost:7001"""
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName} Tray"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; Tasks: desktopicon; IconFilename: "{app}\app.ico"
Name: "{userstartup}\{#MyAppName} Tray"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; Tasks: autostarttray; IconFilename: "{app}\app.ico"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}Tray"; ValueData: """{app}\{#MyAppExeName}"" --tray"; Tasks: autostarttray; Flags: uninsdeletevalue

[Run]
; Parar e remover serviço antigo silenciosamente antes de criar o novo
Filename: "sc.exe"; Parameters: "stop {#MyAppServiceName}"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "delete {#MyAppServiceName}"; Flags: runhidden waituntilterminated

; Marcar a diretoria de instalação como oculta no Windows
Filename: "attrib.exe"; Parameters: "+h ""{app}"""; Flags: runhidden waituntilterminated

; Criar e iniciar o Windows Service de forma 100% silenciosa com arranque automático (start= auto)
Filename: "sc.exe"; Parameters: "create {#MyAppServiceName} binPath= ""\""{app}\{#MyAppExeName}\"" --service"" start= auto displayName= ""Rotina AnyDesk Monitor Agent"""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "description {#MyAppServiceName} ""Serviço de Monitorização AnyDesk e Servidor do Painel Web"""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "failure {#MyAppServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start {#MyAppServiceName}"; Flags: runhidden waituntilterminated

; Iniciar agente da barra de tarefas (Tray) sem abrir linha de comandos
Filename: "{app}\{#MyAppExeName}"; Parameters: "--tray"; Flags: nowait postinstall skipifsilent; Description: "Iniciar agente da barra de tarefas (Tray)"

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop {#MyAppServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallStopService"
Filename: "sc.exe"; Parameters: "delete {#MyAppServiceName}"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallDeleteService"
Filename: "taskkill.exe"; Parameters: "/F /T /IM {#MyAppExeName}"; Flags: runhidden waituntilterminated; RunOnceId: "UninstallKillApp"

[Code]
procedure StopAndKillExistingApp();
var
  ResultCode: Integer;
begin
  // Parar e eliminar o serviço Windows existente antes de copiar ficheiros
  Exec('sc.exe', 'stop {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc.exe', 'delete {#MyAppServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Encerrar forçadamente qualquer processo do agente/tray ativo
  Exec('taskkill.exe', '/F /T /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /T /IM AnyDeskMonitor.Agent.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /T /IM AnyDeskMonitor.Api.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /T /IM AnyDeskMonitor.Web.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  // Aguardar libertação dos handles de ficheiro pelo sistema operativo
  Sleep(1500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopAndKillExistingApp();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    StopAndKillExistingApp();
  end;
end;

