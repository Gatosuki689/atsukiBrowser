#define MyAppName "AtsukiBrowser"
#define MyAppVersion "v1.0.2"
#define MyAppPublisher "Atsuki"
#define MyAppExeName "atsukibrowser.exe"
#define MyAppSourceDir "bin\Release\net8.0-windows10.0.17763.0\win-x64\publish"
#define StudioExeName "AtsukiStudio.exe"
#define StudioSourceDir "D:\AtsukiStudio\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{A7B3C2D1-E4F5-6789-ABCD-EF0123456789}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=instalador
OutputBaseFilename=AtsukiBrowser_Setup
SetupIconFile=icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763

[Registry]
Root: HKCU; Subkey: "Software\AtsukiBrowser"; ValueType: string; ValueName: "StableExePath"; ValueData: "{app}\atsukibrowser.exe"; Flags: uninsdeletevalue

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Iconos adicionales:"
Name: "startmenuicon"; Description: "Crear acceso directo en el menú inicio"; GroupDescription: "Iconos adicionales:"
Name: "instalar_studio"; Description: "Instalar AtsukiStudio (editor de extensiones)"; GroupDescription: "Componentes opcionales:"; Flags: unchecked
Name: "studio_desktopicon"; Description: "Crear acceso directo de AtsukiStudio en el escritorio"; GroupDescription: "Componentes opcionales:"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StudioSourceDir}\*"; DestDir: "{app}\AtsukiStudio"; Flags: ignoreversion recursesubdirs createallsubdirs; Tasks: instalar_studio

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\AtsukiStudio"; Filename: "{app}\AtsukiStudio\{#StudioExeName}"; Tasks: instalar_studio
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{commondesktop}\AtsukiStudio"; Filename: "{app}\AtsukiStudio\{#StudioExeName}"; Tasks: studio_desktopicon
Name: "{commonstartmenu}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir {#MyAppName}"; Flags: nowait postinstall skipifsilent

[InstallDelete]
Type: files; Name: "{localappdata}\AtsukiBrowser\Perfiles\default\ultima_version.txt"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\AtsukiBrowser"

[Code]
var
  WelcomePage: TWizardPage;
  LblTitulo: TLabel;
  LblDesc: TLabel;
  LblVersion: TLabel;
  LblSep: TLabel;
  StudioPage: TWizardPage;
  LblStudioTitulo: TLabel;
  LblStudioDesc: TLabel;

procedure ColorearBoton(Btn: TButton; BgColor, FgColor: TColor);
begin
  Btn.Font.Color := FgColor;
end;

procedure InitializeWizard;
begin
  // ── Fondo oscuro global ──
  WizardForm.Color           := $2D2D3E;
  WizardForm.MainPanel.Color := $2D2D3E;
  WizardForm.InnerPage.Color := $38384A;
  WizardForm.PageDescriptionLabel.Font.Color := $AA9988;
  WizardForm.PageNameLabel.Font.Color        := $FF9D5A;
  WizardForm.PageNameLabel.Font.Style        := [fsBold];
  WizardForm.Bevel.Visible  := False;
  WizardForm.Bevel1.Visible := False;
  WizardForm.Caption := 'Instalador de AtsukiBrowser';

  // ── Botones ──
  WizardForm.NextButton.Font.Color   := clWhite;
  WizardForm.BackButton.Font.Color   := $CCAAFF;
  WizardForm.CancelButton.Font.Color := $8888AA;

  // ── Página de bienvenida ──
  WelcomePage := CreateCustomPage(wpWelcome, '', '');

  LblTitulo := TLabel.Create(WelcomePage);
  LblTitulo.Parent   := WelcomePage.Surface;
  LblTitulo.Caption  := 'AtsukiBrowser';
  LblTitulo.Font.Size  := 26;
  LblTitulo.Font.Style := [fsBold];
  LblTitulo.Font.Color := $FF5AED;
  LblTitulo.Left     := 0;
  LblTitulo.Top      := 10;
  LblTitulo.AutoSize := True;

  LblVersion := TLabel.Create(WelcomePage);
  LblVersion.Parent   := WelcomePage.Surface;
  LblVersion.Caption  := 'Versión {#MyAppVersion}';
  LblVersion.Font.Size  := 10;
  LblVersion.Font.Color := $7766AA;
  LblVersion.Left     := 4;
  LblVersion.Top      := 56;
  LblVersion.AutoSize := True;

  // Separador visual
  LblSep := TLabel.Create(WelcomePage);
  LblSep.Parent     := WelcomePage.Surface;
  LblSep.Caption    := '';
  LblSep.Left       := 0;
  LblSep.Top        := 82;
  LblSep.Width      := WelcomePage.Surface.Width;
  LblSep.Height     := 1;
  LblSep.Color      := $3A2E7C;
  LblSep.AutoSize   := False;

  LblDesc := TLabel.Create(WelcomePage);
  LblDesc.Parent   := WelcomePage.Surface;
  LblDesc.Caption  :=
    'Gracias por instalar AtsukiBrowser.' + #13#10 +
    'Un navegador ligero y personalizable.';
  LblDesc.Font.Size  := 10;
  LblDesc.Font.Color := $AAAACC;
  LblDesc.Left       := 0;
  LblDesc.Top        := 95;
  LblDesc.AutoSize   := True;
  LblDesc.WordWrap   := True;
  LblDesc.Width      := WelcomePage.Surface.Width;

  // ── Página de AtsukiStudio ──
  StudioPage := CreateCustomPage(wpSelectTasks, 'AtsukiStudio', 'Editor de extensiones');

  LblStudioTitulo := TLabel.Create(StudioPage);
  LblStudioTitulo.Parent   := StudioPage.Surface;
  LblStudioTitulo.Caption  := 'AtsukiStudio';
  LblStudioTitulo.Font.Size  := 18;
  LblStudioTitulo.Font.Style := [fsBold];
  LblStudioTitulo.Font.Color := $FF9D5A;
  LblStudioTitulo.Left     := 0;
  LblStudioTitulo.Top      := 10;
  LblStudioTitulo.AutoSize := True;

  LblStudioDesc := TLabel.Create(StudioPage);
  LblStudioDesc.Parent   := StudioPage.Surface;
  LblStudioDesc.Caption  :=
    'AtsukiStudio es el editor oficial de extensiones para AtsukiBrowser.' + #13#10#13#10 +
    'Con él puedes crear, editar y exportar extensiones (.atsuki).' + #13#10 +
    'Es opcional — puedes instalarlo ahora o más adelante.';
  LblStudioDesc.Font.Size  := 10;
  LblStudioDesc.Font.Color := $AAAACC;
  LblStudioDesc.Left       := 0;
  LblStudioDesc.Top        := 45;
  LblStudioDesc.AutoSize   := True;
  LblStudioDesc.WordWrap   := True;
  LblStudioDesc.Width      := StudioPage.Surface.Width;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpWelcome);
end;