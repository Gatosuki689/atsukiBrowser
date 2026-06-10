#define MyAppName "AtsukiBrowser Preview"
#define MyAppVersion "v1.0.4-prev3"
#define MyAppPublisher "Atsuki"
#define MyAppExeName "atsukibrowser.exe"
#define MyAppSourceDir "bin\Release\net8.0-windows10.0.17763.0\win-x64\publish"

[Setup]
; AppId diferente al stable — nunca cambiar
AppId={{B9C4D3E2-F5A6-7890-BCDE-F01234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com
DefaultDirName={autopf}\AtsukiBrowser Preview
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=instalador
OutputBaseFilename=AtsukiBrowser_Preview_Setup
SetupIconFile=icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763

[Registry]
Root: HKCU; Subkey: "Software\AtsukiBrowser"; ValueType: string; ValueName: "PreviewExePath"; ValueData: "{app}\atsukibrowser.exe"; Flags: uninsdeletevalue

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Iconos adicionales:"
Name: "startmenuicon"; Description: "Crear acceso directo en el menú inicio"; GroupDescription: "Iconos adicionales:"

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
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
  LblBadge: TLabel;
  LblDesc: TLabel;
  LblVersion: TLabel;
  LblSep: TLabel;
  LblWarning: TLabel;

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
  WizardForm.Caption := 'Instalador de AtsukiBrowser Preview';

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

  LblBadge := TLabel.Create(WelcomePage);
  LblBadge.Parent   := WelcomePage.Surface;
  LblBadge.Caption  := 'PREVIEW';
  LblBadge.Font.Size  := 9;
  LblBadge.Font.Style := [fsBold];
  LblBadge.Font.Color := $FF9D5A;
  LblBadge.Left     := 4;
  LblBadge.Top      := 44;
  LblBadge.AutoSize := True;

  LblVersion := TLabel.Create(WelcomePage);
  LblVersion.Parent   := WelcomePage.Surface;
  LblVersion.Caption  := 'Versión {#MyAppVersion}';
  LblVersion.Font.Size  := 10;
  LblVersion.Font.Color := $7766AA;
  LblVersion.Left     := 60;
  LblVersion.Top      := 44;
  LblVersion.AutoSize := True;

  LblSep := TLabel.Create(WelcomePage);
  LblSep.Parent     := WelcomePage.Surface;
  LblSep.Caption    := '';
  LblSep.Left       := 0;
  LblSep.Top        := 70;
  LblSep.Width      := WelcomePage.Surface.Width;
  LblSep.Height     := 1;
  LblSep.Color      := $3A2E7C;
  LblSep.AutoSize   := False;

  LblDesc := TLabel.Create(WelcomePage);
  LblDesc.Parent   := WelcomePage.Surface;
  LblDesc.Caption  :=
    'Gracias por probar AtsukiBrowser Preview.' + #13#10 +
    'Esta versión incluye funciones nuevas en desarrollo.';
  LblDesc.Font.Size  := 10;
  LblDesc.Font.Color := $AAAACC;
  LblDesc.Left       := 0;
  LblDesc.Top        := 82;
  LblDesc.AutoSize   := True;
  LblDesc.WordWrap   := True;
  LblDesc.Width      := WelcomePage.Surface.Width;

  LblWarning := TLabel.Create(WelcomePage);
  LblWarning.Parent   := WelcomePage.Surface;
  LblWarning.Caption  :=
    '⚠  Esta es una versión de desarrollo y puede ser inestable,' + #13#10 +
    'se instala de forma independiente a la versión actual.' + #13#10#13#10 +
    'Se recomienda hacer respaldo de los datos antes de utilizar esta versión.';
  LblWarning.Font.Size  := 9;
  LblWarning.Font.Color := $44AAFF;
  LblWarning.Left       := 0;
  LblWarning.Top        := 135;
  LblWarning.AutoSize   := True;
  LblWarning.WordWrap   := True;
  LblWarning.Width      := WelcomePage.Surface.Width;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpWelcome);
end;
