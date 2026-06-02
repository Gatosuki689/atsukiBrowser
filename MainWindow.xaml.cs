using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Runtime.InteropServices;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Text.Json;
using System.Net.Http;
using System.Management;

namespace atsukibrowser
{
    public partial class MainWindow : Window
    {
        private Color _accentColor = (Color)ColorConverter.ConvertFromString("#7c3aed");
        private static readonly System.Net.Http.HttpClient _httpClient = new();
        private static string? _appVersionCache;
        private bool _recibirPreviews = false;
        private WebView2? _musicaWebView;
        private bool _musicaInicializada = false;
        private bool _musicaPanelAbierto = false;
        private List<(string titulo, string url)> _musicaPlaylist = new();
        private int _musicaIndiceActivo = -1;
        private bool _musicaReproduciendo = false;
        private bool _aplicandoZoom = false;
        private static string AppVersion
        {
            get
            {
                if (_appVersionCache != null) return _appVersionCache;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt");
                _appVersionCache = File.Exists(path) ? File.ReadAllText(path).Trim() : "1.0.0";
                return _appVersionCache;
            }
        }
        private TextBlock? _sbCpuVal, _sbRamVal, _sbDiscoVal, _sbRedVal;
        private System.Windows.Shapes.Rectangle? _sbCpuBar, _sbRamBar, _sbDiscoBar;
        private bool _sbWidgetRendimiento = true;
        private bool _sbWidgetReloj = true;
        private bool _sbWidgetCapturas  = true;
        private bool _sbWidgetBusqueda  = true;
        private GlobalSystemMediaTransportControlsSessionManager? _smtc;
        private List<WebView2> _tabs = new();
        private List<Button> _tabButtons = new();
        private int _activeTab = -1;
        private CoreWebView2Environment? _env;
        private PerfilManager _perfiles = new();
        private HistorialManager _historial = null!;
        private SidebarManager _sidebar = null!;
        private FavoritosManager _favoritos = null!;
        private TemaManager _temas = null!;
        private RendimientoManager _rendimiento = new();
        private DescargasManager _descargas = null!;
        private ExtensionesManager _extensiones = null!;
        private AtajosManager _atajos = null!;
        private readonly string _urlNuevaTab;
        private readonly string _urlHistorial;
        private readonly string _urlFavoritos;
        private readonly string _urlAjustes;
        private readonly string _urlDescargas;
        private readonly string _urlExtensiones;
        private readonly string _urlPerfiles;
        private string _buscadorActivo = "google";
        private bool _perfSuspenderTabs  = true;
        private Dictionary<int, System.Windows.Media.Imaging.BitmapImage> _tabPreviews = new();
        private bool _perfLimpiarCache   = false;
        private bool _perfLimiteTabs     = false;
        private int  _perfLimiteTabsN    = 10;
        private bool _suspenderMediaEnBackground = true;
        private List<MusicaPlaylist> _playlists = new();
        private int _playlistActiva = 0;
        private string _musicaUltimoTitulo = "";
        private string _musicaImagenCache  = "";
        private string _musicaFuenteCache = "";
        private bool _musicaArranqueInicial = true;
        private Window? _musicaToast;
        private System.Timers.Timer? _cacheTimer;
        private string _carpetaPerfil = "";
        private Perfil _perfilActivo = null!;
        private System.Threading.Mutex? _instanciaMutex;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);
        private List<DialEntry> _dials = new();
        private System.Windows.Threading.DispatcherTimer? _previewTimer;
        private int _previewTabIdx = -1;
        private Dictionary<string, double> _zoomPorDominio = new();
        private bool _ignorarTextChanged = false;
        private bool _ignorarGotFocus = false;
        private bool _urlBarClickado = false;
        private static readonly System.Text.Json.JsonSerializerOptions _jsonOpts = new();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private void IniciarDragVentana()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Mouse.Capture(null);
            PostMessage(hwnd, 0x00A1, new IntPtr(2), IntPtr.Zero);
        }

        private void TraerAlFrente()
        {
            Dispatcher.Invoke(() =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                
                if (WindowState == WindowState.Minimized)
                    ShowWindow(hwnd, SW_RESTORE);

                // Truco para saltarse la restricción de Windows
                keybd_event(0, 0, 0, 0);
                
                Show();
                WindowState = WindowState.Normal;
                SetForegroundWindow(hwnd);
                Activate();
                Focus();
            });
        }

        public MainWindow()
        {
            InitializeComponent();
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Loaded += (s, e) =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int color = DWMWA_COLOR_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref color, sizeof(int));
                InicializarControlsMusica();
            };
            StateChanged += (s, e) =>
            {
                MainGrid.Margin = WindowState == WindowState.Maximized
                    ? new Thickness(6)
                    : new Thickness(0);
            };
            string res = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            _urlNuevaTab  = "file:///" + Path.Combine(res, "NuevaTab.html").Replace("\\", "/");
            _urlHistorial = "file:///" + Path.Combine(res, "Historial.html").Replace("\\", "/");
            _urlFavoritos = "file:///" + Path.Combine(res, "Favoritos.html").Replace("\\", "/");
            _urlAjustes   = "file:///" + Path.Combine(res, "Ajustes.html").Replace("\\", "/");
            _urlDescargas = "file:///" + Path.Combine(res, "Descargas.html").Replace("\\", "/");
            _urlExtensiones = "file:///" + Path.Combine(res, "Extensiones.html").Replace("\\", "/");
            _urlPerfiles = "file:///" + Path.Combine(res, "Perfiles.html").Replace("\\", "/");
            // Leer perfil desde argumento
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith("--perfil="))
                {
                    string perfilId = arg.Substring("--perfil=".Length).Trim();
                    _perfiles.CambiarA(perfilId);
                    break;
                }
            }

            InicializarManagers();
            InicializarEntorno();
            SugerenciasPopup.PreviewMouseLeftButtonDown += (s, e) =>
            {
                // Dar tiempo a que WPF seleccione el item antes de leerlo
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (SugerenciasList.SelectedItem is SugerenciaItem sug)
                    {
                        SugerenciasPopup.IsOpen = false;
                        UrlBar.Text = sug.Url;
                        Navegar(sug.Url);
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Keyboard.ClearFocus();
                            if (_activeTab >= 0 && _activeTab < _tabs.Count)
                            {
                                FocusManager.SetFocusedElement(this, _tabs[_activeTab]);
                                _tabs[_activeTab].Focus();
                            }
                            ActualizarUrlDisplay(sug.Url);
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    }
                }), System.Windows.Threading.DispatcherPriority.Send); // Send = máxima prioridad
            };

            SugerenciasList.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && SugerenciasList.SelectedItem is SugerenciaItem sug)
                {
                    SugerenciasPopup.IsOpen = false;
                    UrlBar.Text = sug.Url;
                    Navegar(sug.Url);
                }
            };
            UrlBar.PreviewMouseDown += (s, e) => _urlBarClickado = true;
            this.PreviewGotKeyboardFocus += (s, e) =>
            {
                if (e.NewFocus != UrlBar && 
                    e.NewFocus != SugerenciasList &&
                    !(e.NewFocus is ListBoxItem))
                {
                    SugerenciasPopup.IsOpen = false;
                }
            };
            // Mostrar badge si es versión preview
            if (AppVersion.Contains("-prev") || AppVersion.Contains("-beta") || AppVersion.Contains("-alpha"))
            {
                BadgePreview.Visibility = Visibility.Visible;
                MarcaAgua.Visibility = Visibility.Visible;
            }
            _ = Task.Run(VerificarActualizaciones);
            VerificarPrimeraEjecucion();
            TabScrollViewer.SizeChanged += (s, e) => ActualizarEstiloTabs();
        }

        private async void VerificarActualizaciones()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "AtsukiBrowser");
                string json = await client.GetStringAsync(
                    $"https://gist.githubusercontent.com/Gatosuki689/f24638ebb9ed77db3a58fc2318103b39/raw/version.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

                var doc    = JsonSerializer.Deserialize<JsonElement>(json);
                string ultima = doc.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                string url    = doc.TryGetProperty("url",     out var u) ? u.GetString() ?? "" : "";
                string notas  = doc.TryGetProperty("notas",   out var n) ? n.GetString() ?? "" : "";

                // Revisar canal preview si está activado
                if (_recibirPreviews && doc.TryGetProperty("preview", out var prev))
                {
                    string prevVersion = prev.TryGetProperty("version", out var pv) ? pv.GetString() ?? "" : "";
                    string prevUrl     = prev.TryGetProperty("url",     out var pu) ? pu.GetString() ?? "" : "";
                    string prevNotas   = prev.TryGetProperty("notas",   out var pn) ? pn.GetString() ?? "" : "";

                    // Usar preview si es más nueva que la versión actual
                    if (!string.IsNullOrEmpty(prevVersion) && prevVersion != AppVersion)
                    {
                        ultima = prevVersion;
                        url    = prevUrl;
                        notas  = $"[Preview] {prevNotas}";
                    }
                }

                if (string.IsNullOrEmpty(ultima) || ultima == AppVersion) return;

                Dispatcher.Invoke(() => MostrarNotificacionUpdate(ultima, url, notas));
            }
            catch { }
        }

        private void MostrarNotificacionUpdate(string version, string url, string notas)
        {
            // Popup en esquina inferior derecha
            var popup = new Window
            {
                Width = 300, Height = 90,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            // Posicionar en esquina inferior derecha
            var screen = SystemParameters.WorkArea;
            popup.Left = screen.Right - 310;
            popup.Top  = screen.Bottom - 100;

            var border = new Border
            {
                Background    = new SolidColorBrush(Color.FromRgb(19, 19, 30)),
                BorderBrush   = new SolidColorBrush(Color.FromArgb(180, 124, 58, 237)),
                BorderThickness = new Thickness(1),
                CornerRadius  = new CornerRadius(10),
                Padding       = new Thickness(14, 10, 14, 10),
                Effect        = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color     = Color.FromRgb(124, 58, 237),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity   = 0.4
                }
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text       = "✦ Actualización disponible",
                Foreground = new SolidColorBrush(Color.FromRgb(157, 90, 255)),
                FontSize   = 12,
                FontWeight = FontWeights.SemiBold
            });
            panel.Children.Add(new TextBlock
            {
                Text       = $"v{AppVersion} → v{version}  —  Click para instalar",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 200)),
                FontSize   = 11,
                Margin     = new Thickness(0, 4, 0, 0)
            });

            border.Child = panel;
            popup.Content = border;

            // Click abre el overlay
            border.MouseLeftButtonDown += (s, e) =>
            {
                popup.Close();
                MostrarOverlayUpdate(version, url, notas);
            };

            // Auto-cerrar después de 8 segundos
            var timer = new System.Timers.Timer(8000);
            timer.Elapsed += (s, e) =>
            {
                timer.Stop();
                Dispatcher.Invoke(() => popup.Close());
            };
            timer.Start();

            popup.Show();
        }

        private void MostrarOverlayUpdate(string version, string url, string notas)
        {
            // Overlay semitransparente sobre el navegador
            var overlay = new Window
            {
                Width  = ActualWidth,
                Height = ActualHeight,
                Left   = Left,
                Top    = Top,
                WindowStyle = WindowStyle.None,
                ResizeMode  = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background  = new SolidColorBrush(Color.FromArgb(160, 0, 0, 10)),
                Topmost     = true,
                ShowInTaskbar = false,
                Owner = this
            };

            var card = new Border
            {
                Width  = 420,
                Background    = new SolidColorBrush(Color.FromRgb(13, 13, 26)),
                BorderBrush   = new SolidColorBrush(Color.FromArgb(120, 124, 58, 237)),
                BorderThickness = new Thickness(1),
                CornerRadius  = new CornerRadius(12),
                Padding       = new Thickness(28),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color      = Color.FromRgb(124, 58, 237),
                    BlurRadius = 30,
                    ShadowDepth = 0,
                    Opacity    = 0.5
                }
            };

            var root = new StackPanel();

            root.Children.Add(new TextBlock
            {
                Text       = "✦ Actualización disponible",
                Foreground = new SolidColorBrush(Color.FromRgb(157, 90, 255)),
                FontSize   = 18,
                FontWeight = FontWeights.SemiBold,
                Margin     = new Thickness(0, 0, 0, 6)
            });

            root.Children.Add(new TextBlock
            {
                Text       = $"v{AppVersion}  →  v{version}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 221)),
                FontSize   = 13,
                Margin     = new Thickness(0, 0, 0, 14)
            });

            var notasBorder = new Border
            {
                Background    = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                CornerRadius  = new CornerRadius(6),
                Padding       = new Thickness(12),
                Margin        = new Thickness(0, 0, 0, 20)
            };
            notasBorder.Child = new TextBlock
            {
                Text        = notas,
                Foreground  = new SolidColorBrush(Color.FromRgb(170, 170, 200)),
                FontSize    = 12,
                TextWrapping = TextWrapping.Wrap
            };
            root.Children.Add(notasBorder);

            var botones = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnNo = new Button
            {
                Content  = "Ahora no",
                Width    = 100, Height = 34,
                Margin   = new Thickness(0, 0, 10, 0),
                Background    = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                Foreground    = new SolidColorBrush(Color.FromRgb(150, 150, 170)),
                BorderBrush   = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Cursor   = Cursors.Hand
            };
            btnNo.Click += (s, e) => overlay.Close();

            var btnSi = new Button
            {
                Content  = "Actualizar ahora",
                Width    = 140, Height = 34,
                Background    = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                Foreground    = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor   = Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };
            btnSi.Click += (s, e) =>
            {
                overlay.Close();
                DescargarEInstalar(url, version);
            };

            botones.Children.Add(btnNo);
            botones.Children.Add(btnSi);
            root.Children.Add(botones);

            card.Child = root;
            overlay.Content = card;

            // Click fuera del card cierra
            overlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is Window)
                    overlay.Close();
            };

            overlay.ShowDialog();
        }

        private async void DescargarEInstalar(string url, string version)
        {
            try
            {
                string temp = Path.Combine(Path.GetTempPath(), $"AtsukiSetup_{version}.exe");

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "AtsukiBrowser");

                // Mostrar progreso
                Dispatcher.Invoke(() => UrlBar.Text = $"Descargando actualización v{version}...");

                var bytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(temp, bytes);

                // Ejecutar instalador y cerrar el navegador
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = temp,
                    UseShellExecute = true
                });

                Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show(
                    $"Error al descargar la actualización: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private void VerificarPrimeraEjecucion()
        {
            string path = Path.Combine(_carpetaPerfil, "ultima_version.txt");
            string versionGuardada = File.Exists(path) ? File.ReadAllText(path).Trim() : "";
            if (versionGuardada == AppVersion) return;

            // Primera vez con esta versión — guardar y abrir notas
            File.WriteAllText(path, AppVersion);

            _ = Task.Run(async () =>
            {
                try
                {
                    using var client = new HttpClient();
                    client.DefaultRequestHeaders.Add("User-Agent", "AtsukiBrowser");
                    string json = await client.GetStringAsync(
                        $"https://gist.githubusercontent.com/Gatosuki689/f24638ebb9ed77db3a58fc2318103b39/raw/version.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

                    var doc  = JsonSerializer.Deserialize<JsonElement>(json);
                    string notas = doc.TryGetProperty("notas", out var n) ? n.GetString() ?? "" : "";

                    Dispatcher.Invoke(() => AbrirNotasVersion(AppVersion, notas));
                }
                catch { }
            });
        }

        private async void AbrirNotasVersion(string version, string notas)
        {
            string html = $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
            <meta charset="UTF-8">
            <style>
                * { margin:0; padding:0; box-sizing:border-box; }
                body {
                    font-family: 'Segoe UI', sans-serif;
                    background: #0d0d14;
                    color: rgba(255,255,255,0.85);
                    display: flex;
                    justify-content: center;
                    padding: 60px 20px;
                    min-height: 100vh;
                }
                .card { width: 100%; max-width: 620px; }
                .badge {
                    display: inline-block;
                    background: rgba(124,58,237,0.15);
                    border: 1px solid rgba(124,58,237,0.3);
                    color: #9d5aff;
                    font-size: 11px;
                    font-weight: 600;
                    letter-spacing: 0.08em;
                    text-transform: uppercase;
                    padding: 4px 12px;
                    border-radius: 20px;
                    margin-bottom: 16px;
                }
                h1 { font-size: 32px; font-weight: 700; letter-spacing: -1px; margin-bottom: 6px; }
                .sub { font-size: 13px; color: rgba(255,255,255,0.35); margin-bottom: 32px; }
                .divider { height: 1px; background: rgba(124,58,237,0.2); margin-bottom: 32px; }
                .notas-titulo { font-size: 11px; text-transform: uppercase; letter-spacing: 0.1em; color: #7c3aed; margin-bottom: 16px; }
                .notas { background: #13131f; border: 1px solid rgba(124,58,237,0.2); border-radius: 12px; padding: 20px 24px; font-size: 13px; line-height: 1.8; color: rgba(255,255,255,0.7); white-space: pre-line; }
                .footer { margin-top: 32px; font-size: 12px; color: rgba(255,255,255,0.25); text-align: center; }
            </style>
            </head>
            <body>
                <div class="card">
                    <div class="badge">Novedades</div>
                    <h1>AtsukiBrowser {{version}}</h1>
                    <div class="sub">Gracias por actualizar. Esto es lo nuevo en esta versión.</div>
                    <div class="divider"></div>
                    <div class="notas-titulo">Notas de versión</div>
                    <div class="notas">{{(string.IsNullOrWhiteSpace(notas) ? "Sin notas para esta versión." : notas)}}</div>
                    <div class="footer">AtsukiBrowser · v{{version}}</div>
                </div>
            </body>
            </html>
            """;

            // Esperar a que haya al menos una tab lista
            if (_tabs.Count == 0 || _activeTab < 0)
            {
                await Task.Delay(500);
                if (_tabs.Count == 0 || _activeTab < 0) return;
            }

            AbrirNuevaTab();

            // Verificar que se creó correctamente
            if (_activeTab < 0 || _activeTab >= _tabs.Count) return;

            var webView = _tabs[_activeTab];
            int idx = _activeTab;

            // Esperar a que CoreWebView2 esté listo
            await webView.EnsureCoreWebView2Async(_env);

            // Esperar un tick para que NuevaTab.html termine de iniciar su navegación
            await Task.Delay(300);

            webView.NavigateToString(html);

            if (idx >= 0 && idx < _tabButtons.Count && _tabButtons[idx].Tag is TextBlock label)
                label.Text = $"Novedades v{version}";

            // Mostrar URL amigable en la barra
            if (idx == _activeTab)
            {
                _ignorarGotFocus = true;
                UrlBar.Text = $"atsuki://novedades/v{version}";
                ActualizarUrlDisplay($"atsuki://novedades/v{version}");
                _ignorarGotFocus = false;
            }
        }

        private string[] GetFlagsSegunHardware()
        {
            var flagsBase = new List<string>
            {
                "--process-per-tab",
                "--enable-gpu-rasterization",
                "--enable-zero-copy",
                "--ignore-gpu-blocklist",
                "--enable-accelerated-video-decode",
                "--enable-accelerated-video-encode",
                "--js-flags=--max-old-space-size=256",
                "--renderer-process-limit=6",
                "--disable-dev-shm-usage",
                "--force-color-profile=srgb",
                "--disable-background-timer-throttling",
                "--force-device-scale-factor=1",
                "--disable-renderer-backgrounding",
                "--allow-file-access-from-files",
                "--autoplay-policy=no-user-gesture-required",
                "--disable-background-media-suspend",
                "--audio-output-channels=2",
            };

            try
            {
                // Detectar GPU
                string gpu = "";
                string cpu = "";
                int ramMB = 0;

                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_VideoController"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        gpu = obj["Name"]?.ToString() ?? "";
                        break;
                    }
                }

                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        cpu = obj["Name"]?.ToString() ?? "";
                        break;
                    }
                }

                using (var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT * FROM Win32_ComputerSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        ramMB = (int)((ulong)(obj["TotalPhysicalMemory"] ?? 0UL) / 1024 / 1024);
                        break;
                    }
                }

                bool esIntelLegacy = gpu.Contains("Intel") &&
                    (gpu.Contains("HD Graphics 5") || gpu.Contains("HD Graphics 6") ||
                    gpu.Contains("HD Graphics 4") || gpu.Contains("HD Graphics 3") ||
                    gpu.Contains("HD Graphics 2"));

                bool esNvidia = gpu.Contains("NVIDIA") || gpu.Contains("GeForce");
                bool esAMD    = gpu.Contains("AMD") || gpu.Contains("Radeon");
                bool esIntelModerno = gpu.Contains("Intel") && 
                    (gpu.Contains("Iris") || gpu.Contains("Arc") || gpu.Contains("UHD"));

                bool pocaRAM = ramMB > 0 && ramMB < 6000;

                // ── Intel legacy (HD 2000-6000) ──
                if (esIntelLegacy)
                {
                    flagsBase.AddRange(new[]
                    {
                        "--enable-features=MediaFoundationVideoCapture,MediaFoundationH264Encoding,MediaFoundationClearPlayback,PlatformHEVCDecoderSupport,CanvasOopRasterization,NetworkServiceInProcess,ThrottleDisplayNoneAndVisibilityHiddenCrossOriginIframes",
                        "--disable-features=HeavyAdIntervention,UseChromeOSDirectVideoDecoder,VaapiVideoDecoder,VaapiVideoEncoder,Vulkan,WebRtcHideLocalIpsWithMdns",
                        "--video-threads=2",
                        "--enable-gpu-memory-buffer-video-frames",
                        "--enable-oop-rasterization",
                    });
                }
                // ── Intel moderno (UHD, Iris, Arc) ──
                else if (esIntelModerno)
                {
                    flagsBase.AddRange(new[]
                    {
                        "--enable-features=MediaFoundationVideoCapture,MediaFoundationH264Encoding,MediaFoundationClearPlayback,PlatformHEVCDecoderSupport,CanvasOopRasterization,NetworkServiceInProcess,ThrottleDisplayNoneAndVisibilityHiddenCrossOriginIframes",
                        "--disable-features=HeavyAdIntervention,UseChromeOSDirectVideoDecoder,Vulkan",
                        "--enable-oop-rasterization",
                        "--enable-raw-draw",
                        "--video-threads=4",
                    });
                }
                // ── NVIDIA ──
                else if (esNvidia)
                {
                    flagsBase.AddRange(new[]
                    {
                        "--enable-features=MediaFoundationVideoCapture,MediaFoundationH264Encoding,MediaFoundationClearPlayback,PlatformHEVCDecoderSupport,CanvasOopRasterization,NetworkServiceInProcess,ThrottleDisplayNoneAndVisibilityHiddenCrossOriginIframes",
                        "--disable-features=HeavyAdIntervention,UseChromeOSDirectVideoDecoder,Vulkan",
                        "--enable-oop-rasterization",
                        "--enable-raw-draw",
                        "--video-threads=4",
                    });
                }
                // ── AMD ──
                else if (esAMD)
                {
                    flagsBase.AddRange(new[]
                    {
                        "--enable-features=MediaFoundationVideoCapture,MediaFoundationH264Encoding,MediaFoundationClearPlayback,PlatformHEVCDecoderSupport,CanvasOopRasterization,NetworkServiceInProcess,ThrottleDisplayNoneAndVisibilityHiddenCrossOriginIframes",
                        "--disable-features=HeavyAdIntervention,UseChromeOSDirectVideoDecoder,VaapiVideoDecoder,Vulkan",
                        "--enable-oop-rasterization",
                        "--enable-raw-draw",
                        "--video-threads=4",
                    });
                }
                // ── Fallback genérico ──
                else
                {
                    flagsBase.AddRange(new[]
                    {
                        "--enable-features=MediaFoundationVideoCapture,MediaFoundationH264Encoding,MediaFoundationClearPlayback,PlatformHEVCDecoderSupport,CanvasOopRasterization,NetworkServiceInProcess,ThrottleDisplayNoneAndVisibilityHiddenCrossOriginIframes",
                        "--disable-features=HeavyAdIntervention,UseChromeOSDirectVideoDecoder,Vulkan",
                        "--enable-oop-rasterization",
                        "--video-threads=2",
                    });
                }

                // ── Ajustes por poca RAM ──
                if (pocaRAM)
                {
                    flagsBase.Add("--js-flags=--max-old-space-size=64");
                    flagsBase.Add("--renderer-process-limit=2");
                    flagsBase.Add("--in-process-gpu");
                }
            }
            catch { }
            return flagsBase.ToArray();
        }

        private async void InicializarEntorno()
        {
            string userDataFolder = _carpetaPerfil;

            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = string.Join(" ", GetFlagsSegunHardware())
            };

            _env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);


            AbrirNuevaTab(_urlNuevaTab);
            _rendimiento.DatosActualizados += (datos) =>
            {
                Dispatcher.Invoke(() =>
                {
                    string msg = $"rendimiento:{datos.Cpu},{datos.Ram},{datos.Disco},{datos.Red}";
                    // Solo enviar a la tab activa
                    if (_activeTab >= 0 && _activeTab < _tabs.Count)
                        _tabs[_activeTab].CoreWebView2?.PostWebMessageAsString(msg);
                    ActualizarWidgetsSidebar(datos);
                });
            };
            // Cargar config widgets sidebar
            string sbWidgetsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsukiBrowser", "sb_widgets.json");
            if (File.Exists(sbWidgetsPath))
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                        File.ReadAllText(sbWidgetsPath));
                    if (json.TryGetProperty("rendimiento", out var val))
                        _sbWidgetRendimiento = val.GetBoolean();
                    if (json.TryGetProperty("reloj", out var reloj))
                        _sbWidgetReloj = reloj.GetBoolean();
                    if (json.TryGetProperty("capturas", out var capturas))
                        _sbWidgetCapturas = capturas.GetBoolean();
                    if (json.TryGetProperty("busqueda", out var busqueda))
                        _sbWidgetBusqueda = busqueda.GetBoolean();
                }
                catch { }
            }
            _rendimiento.Iniciar();
            InicializarMusica();
            CargarDials();
            // Cargar buscador
            string buscadorPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsukiBrowser", "buscador.txt");
            if (File.Exists(buscadorPath))
                _buscadorActivo = File.ReadAllText(buscadorPath).Trim();

            // Cargar config rendimiento
            string perfPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsukiBrowser", "perf.json");
            if (File.Exists(perfPath))
                AplicarPerfConfig(File.ReadAllText(perfPath));
            AplicarTemaUI(_temas.TemaActivo);
            // Cargar preferencia de previews
            string previewsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsukiBrowser", "previews.txt");
            if (File.Exists(previewsPath))
                _recibirPreviews = File.ReadAllText(previewsPath).Trim() == "true";
            // Registrar mutex de esta instancia
            string perfilId = _perfiles.Activo.Id;
            string mutexName = $"AtsukiBrowser_perfil_{perfilId}";
            _instanciaMutex = new System.Threading.Mutex(true, mutexName, out _);

            // Watcher para señal de foco
            string señalDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsukiBrowser");
            string señalFile = Path.Combine(señalDir, $"focus_{perfilId}.signal");
            var watcher = new FileSystemWatcher(señalDir, $"focus_{perfilId}.signal")
            {
                EnableRaisingEvents = true
            };
            watcher.Created += (s, e) => { File.Delete(señalFile); TraerAlFrente(); };
            watcher.Changed += (s, e) => { File.Delete(señalFile); TraerAlFrente(); };
            RenderizarSidebar();
            SincronizarExtensionesSidebar();
            IniciarBadgesSidebar();
            _ = InicializarMusicaWebView();
        }
        

        private void InicializarManagers()
        {
            _carpetaPerfil = _perfiles.CarpetaActiva();
            _perfilActivo  = _perfiles.Activo;
            var carpeta = _carpetaPerfil;
            Directory.CreateDirectory(carpeta);
            _historial = new HistorialManager(carpeta);
            _favoritos = new FavoritosManager(carpeta);
            _temas     = new TemaManager(carpeta);
            _sidebar   = new SidebarManager(carpeta);
            _descargas   = new DescargasManager(carpeta);
            _extensiones = new ExtensionesManager(carpeta);
            _atajos = new AtajosManager(carpeta);
        }

        private void CambiarPerfil(string id)
        {
            // Verificar si ya hay una instancia con ese perfil corriendo
            string mutexName = $"AtsukiBrowser_perfil_{id}";
            var mutex = new System.Threading.Mutex(false, mutexName, out bool esNueva);

            if (!esNueva)
            {
                // Ya existe una instancia con ese perfil — enviarle señal para que se enfoque
                // usando un archivo de señal
                string señalPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AtsukiBrowser", $"focus_{id}.signal");
                File.WriteAllText(señalPath, DateTime.Now.Ticks.ToString());
                mutex.Dispose();
                return;
            }

            mutex.Dispose();

            bool eraInvitado = _perfiles.Activo.EsInvitado;
            _perfiles.CambiarA(id);

            string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (!string.IsNullOrEmpty(exe))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"--perfil={id}",
                    UseShellExecute = true
                });
            }

            if (eraInvitado || id == "invitado")
                Dispatcher.Invoke(() => Close());
        }

        // ── Gestión de pestañas ──────────────────────────────

        private async void AbrirNuevaTab(string url = "")
        {
            // Límite de pestañas
            if (_perfLimiteTabs && _tabs.Count >= _perfLimiteTabsN)
            {
                // Cerrar la más antigua que no sea la activa
                int oldest = _activeTab == 0 ? 1 : 0;
                if (oldest < _tabs.Count)
                    CerrarTab(oldest);
            }

            if (string.IsNullOrEmpty(url))
            {
                url = _urlNuevaTab;
            }

            var webView = new WebView2();
            BrowserContainer.Children.Add(webView);

            if (_env != null)
                await webView.EnsureCoreWebView2Async(_env);
                // Optimizaciones por pestaña
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;

                // Interceptar atajos antes de que WebView los consuma
                webView.CoreWebView2.GetDevToolsProtocolEventReceiver("Input.keyEventFired");
                webView.KeyDown += (s, e) =>
                {
                    bool ctrl  = Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl);
                    bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                    bool alt   = Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt);
                    string tecla = e.Key.ToString();

                    if (_atajos.Coincide("busqueda_rapida", ctrl, shift, alt, tecla))
                    {
                        PopupBuscadorSidebar.IsOpen = true;
                        SidebarBuscadorInput.Focus();
                        e.Handled = true;
                    }
                };
                webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        int idx = _tabs.IndexOf(webView);
                        if (idx < 0) return;

                        string titulo = webView.CoreWebView2?.DocumentTitle ?? "Nueva pestaña";
                        if (string.IsNullOrWhiteSpace(titulo)) titulo = "Nueva pestaña";

                        if (_tabButtons.Count > idx && _tabButtons[idx].Tag is TextBlock label)
                            label.Text = titulo;
                    });
                };

            // ── Cargar extensiones Chrome/Edge (no en modo invitado) ──
            if (!_perfiles.Activo.EsInvitado)
            {
                foreach (var ruta in _extensiones.GetExtensionesChrome())
                {
                    try
                    {
                        await webView.CoreWebView2.Profile.AddBrowserExtensionAsync(ruta);
                    }
                    catch { }
                }
            }
            webView.CoreWebView2.ContextMenuRequested += (s, args) =>
            {
                var menuList = args.MenuItems;
                var target = args.ContextMenuTarget;
                var env = webView.CoreWebView2.Environment;

                // Limpiar todo el menú nativo
                menuList.Clear();

                // ── Helper para crear items ──────────────────────
                CoreWebView2ContextMenuItem CrearItem(string label, Action accion)
                {
                    var item = env.CreateContextMenuItem(
                        label, null, CoreWebView2ContextMenuItemKind.Command);
                    item.CustomItemSelected += (s2, e2) => Dispatcher.Invoke(accion);
                    return item;
                }

                CoreWebView2ContextMenuItem CrearSep() =>
                    env.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator);

                // ── Link ─────────────────────────────────────────
                if (!string.IsNullOrEmpty(target.LinkUri))
                {
                    string url = target.LinkUri;
                    menuList.Add(CrearItem("Abrir en una nueva pestaña", () => AbrirNuevaTab(url)));
                    menuList.Add(CrearItem("Abrir en una nueva ventana", () =>
                    {
                        var ventana = new MainWindow();
                        ventana.Show();
                        ventana.InicializarConUrl(url);
                    }));
                    menuList.Add(CrearItem("Copiar dirección del enlace", () =>
                        Clipboard.SetText(url)));
                    menuList.Add(CrearSep());
                }

                // ── Imagen ───────────────────────────────────────
                if (!string.IsNullOrEmpty(target.SourceUri) && 
                    target.Kind == CoreWebView2ContextMenuTargetKind.Image)
                {
                    string imgUrl = target.SourceUri;
                    menuList.Add(CrearItem("Guardar imagen como...", () =>
                    {
                        var dlg = new Microsoft.Win32.SaveFileDialog
                        {
                            FileName = Path.GetFileName(new Uri(imgUrl).LocalPath),
                            Filter = "Imagen|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.svg"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            _ = Task.Run(async () =>
                            {
                                var bytes = await _httpClient.GetByteArrayAsync(imgUrl);
                                File.WriteAllBytes(dlg.FileName, bytes);
                            });
                        }
                    }));
                    menuList.Add(CrearItem("Copiar dirección de la imagen", () =>
                        Clipboard.SetText(imgUrl)));
                    menuList.Add(CrearSep());
                }

                // ── Texto seleccionado ───────────────────────────
                if (target.HasSelection && !string.IsNullOrWhiteSpace(target.SelectionText))
                {
                    string texto = target.SelectionText;
                    menuList.Add(CrearItem("Copiar", () => Clipboard.SetText(texto)));
                    menuList.Add(CrearItem($"Buscar \"{(texto.Length > 20 ? texto[..20] + "…" : texto)}\" en Google", () =>
                        AbrirNuevaTab("https://www.google.com/search?q=" + Uri.EscapeDataString(texto))));
                    menuList.Add(CrearSep());
                }

                // ── Input / campo de texto ───────────────────────
                if (target.IsEditable)
                {
                    menuList.Add(CrearItem("Copiar", () =>
                        webView.CoreWebView2.ExecuteScriptAsync("document.execCommand('copy')")));
                    menuList.Add(CrearItem("Pegar", () =>
                        webView.CoreWebView2.ExecuteScriptAsync("document.execCommand('paste')")));
                    menuList.Add(CrearSep());
                }

                // ── Página general (siempre) ─────────────────────
                string paginaUrl = webView.Source?.ToString() ?? "";
                menuList.Add(CrearItem("Recargar", () => webView.Reload()));
                menuList.Add(CrearItem("Copiar URL de la página", () => Clipboard.SetText(paginaUrl)));
                menuList.Add(CrearSep());
                menuList.Add(CrearItem("Inspeccionar", () =>
                    webView.CoreWebView2.OpenDevToolsWindow()));
            };

            // Cerrar popup de sugerencias cuando el WebView recibe el foco
            webView.GotFocus += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SugerenciasPopup.IsOpen = false;
                }), System.Windows.Threading.DispatcherPriority.Normal);
            };

            // ── Todos los mensajes JS → C# en un solo lugar ──
            webView.CoreWebView2.WebMessageReceived += (s, args) =>
            {
                string msg = args.TryGetWebMessageAsString();

                if (msg == "get:historial")
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(_historial.Entradas);
                    webView.CoreWebView2.PostWebMessageAsString("historial:" + json);
                }
                else if (msg == "limpiar:historial")
                {
                    _historial.Limpiar();
                    webView.CoreWebView2.PostWebMessageAsString("historial:[]");
                }
                else if (msg == "get:favoritos")
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(_favoritos.Entradas);
                    webView.CoreWebView2.PostWebMessageAsString("favoritos:" + json);
                }
                else if (msg.StartsWith("favorito:quitar:"))
                {
                    string favUrl = msg.Substring("favorito:quitar:".Length);
                    _favoritos.Quitar(favUrl);
                    var json = System.Text.Json.JsonSerializer.Serialize(_favoritos.Entradas);
                    webView.CoreWebView2.PostWebMessageAsString("favoritos:" + json);
                }
                else if (msg == "navigate:historial")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlHistorial));
                }
                else if (msg == "navigate:favoritos")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlFavoritos));
                }
                else if (msg == "navigate:ajustes")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlAjustes));
                }
                else if (msg == "navigate:nuevatab")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlNuevaTab));
                }
                else if (msg == "get:dials")
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(_dials);
                    webView.CoreWebView2.PostWebMessageAsString("dials:" + json);
                }
                else if (msg.StartsWith("navigate:"))
                {
                    string navUrl = msg.Substring("navigate:".Length);
                    if (!navUrl.StartsWith("http://") && !navUrl.StartsWith("https://") && !navUrl.StartsWith("file:///"))
                        navUrl = "https://" + navUrl;
                    if (Uri.TryCreate(navUrl, UriKind.Absolute, out var uri))
                        Dispatcher.Invoke(() => webView.Source = uri);
                }
                else if (msg == "favorito:quitar:todos")
                {
                    _favoritos.Limpiar();
                    var json = System.Text.Json.JsonSerializer.Serialize(_favoritos.Entradas);
                    webView.CoreWebView2.PostWebMessageAsString("favoritos:" + json);
                }
                else if (msg == "get:tema")
                {
                    webView.CoreWebView2.PostWebMessageAsString("tema:" + _temas.ToJson());
                }
                else if (msg.StartsWith("tema:predefinido:"))
                {
                    string id = msg.Substring("tema:predefinido:".Length);
                    _temas.AplicarPredefinido(id);
                    AplicarTemaUI(_temas.TemaActivo);
                    PropagaTema();
                }
                else if (msg.StartsWith("tema:custom:"))
                {
                    // FIX 3 — reutiliza _jsonOpts estático
                    string json = msg.Substring("tema:custom:".Length);
                    var t = System.Text.Json.JsonSerializer.Deserialize<Tema>(json, _jsonOpts);
                    if (t != null)
                    {
                        t.Id = "custom";
                        t.EsCustom = true;
                        _temas.AplicarCustom(t.Accent, t.Bg, t.Surface, t.Surface2, t.Font);
                        AplicarTemaUI(_temas.TemaActivo);
                        PropagaTema();
                    }
                }
                else if (msg == "get:extensiones")
                {
                    webView.CoreWebView2.PostWebMessageAsString("extensiones:" + _extensiones.ToJson());
                }
                else if (msg.StartsWith("steam:get:"))
                {
                    var payload = msg.Substring("steam:get:".Length);
                    var sep     = payload.IndexOf(':');
                    if (sep > 0)
                    {
                        string steamId = payload.Substring(0, sep);
                        string apiKey  = payload.Substring(sep + 1);
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/" +
                                        $"?key={apiKey}&steamids={steamId}";
                                var res = await _httpClient.GetStringAsync(url);
                                webView.CoreWebView2.PostWebMessageAsString("steam:data:" + res);
                            }
                            catch (Exception ex)
                            {
                                webView.CoreWebView2.PostWebMessageAsString("steam:error:" + ex.Message);
                            }
                        });
                    }
                }
                else if (msg.StartsWith("extension:toggle:"))
                {
                    var parts = msg.Split(':');
                    // parts[2] = id, parts[3] = true/false
                    if (parts.Length >= 4)
                        _extensiones.SetActiva(parts[2], parts[3] == "true");
                    webView.CoreWebView2.PostWebMessageAsString("extensiones:" + _extensiones.ToJson());
                    Dispatcher.Invoke(SincronizarExtensionesSidebar);
                }
                else if (msg.StartsWith("extension:desinstalar:"))
                {
                    string extId = msg.Substring("extension:desinstalar:".Length);
                    _extensiones.Desinstalar(extId);
                    webView.CoreWebView2.PostWebMessageAsString("extensiones:" + _extensiones.ToJson());
                    Dispatcher.Invoke(SincronizarExtensionesSidebar);
                }
                else if (msg == "extension:instalar:atsuki")
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Instalar extensión",
                            Filter = "Extensión Atsuki (*.atsuki)|*.atsuki|Carpeta (manifest.json)|manifest.json",
                            CheckFileExists = true
                        };
                        if (dialog.ShowDialog() == true)
                        {
                            string archivo = dialog.FileName;
                            if (archivo.EndsWith(".atsuki", StringComparison.OrdinalIgnoreCase))
                                _extensiones.InstalarDesdeAtsuki(archivo);
                            else
                                _extensiones.Instalar(Path.GetDirectoryName(archivo)!);
                        }
                        webView.CoreWebView2.PostWebMessageAsString("extensiones:" + _extensiones.ToJson());
                    });
                    Dispatcher.Invoke(SincronizarExtensionesSidebar);
                }
                else if (msg.StartsWith("extension:exportar:"))
                {
                    string id = msg.Substring("extension:exportar:".Length);
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new Microsoft.Win32.SaveFileDialog
                        {
                            Title = "Exportar extensión",
                            FileName = id,
                            Filter = "Extensión Atsuki (*.atsuki)|*.atsuki",
                            DefaultExt = ".atsuki"
                        };
                        if (dialog.ShowDialog() == true)
                            _extensiones.ExportarAtsuki(id, dialog.FileName);
                    });
                }
                else if (msg == "navigate:extensiones")
                {
                    string extPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                "Resources", "Extensiones.html");
                    Dispatcher.Invoke(() =>
                        webView.Source = new Uri("file:///" + extPath.Replace("\\", "/")));
                }
                else if (msg == "navigate:perfiles")
                {
                    string perfilPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                    "Resources", "Perfiles.html");
                    Dispatcher.Invoke(() =>
                    {
                        if (_activeTab >= 0 && _activeTab < _tabs.Count)
                            _tabs[_activeTab].Source = new Uri("file:///" + perfilPath.Replace("\\", "/"));
                    });
                }
                else if (msg == "perfiles")
                {
                    Dispatcher.Invoke(() => AbrirONavegar("perfiles"));
                }
                else if (msg == "perfil:elegir-imagen")
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Elegir foto de perfil",
                            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.webp",
                            Multiselect = false
                        };
                        if (dialog.ShowDialog() == true)
                        {
                            string ruta = dialog.FileName.Replace("\\", "/");
                            webView.CoreWebView2.PostWebMessageAsString("perfil:imagen:" + ruta);
                        }
                    });
                }
                else if (msg == "navigate:descargas")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlDescargas));
                }
                else if (msg.StartsWith("dials:guardar:"))
                {
                    string json = msg.Substring("dials:guardar:".Length);
                    _dials = System.Text.Json.JsonSerializer.Deserialize<List<DialEntry>>(json, _jsonOpts) ?? _dials;
                    GuardarDials();
                }
                else if (msg == "get:notas")
                {
                    string path = Path.Combine(_carpetaPerfil, "notas.txt");
                    string contenido = File.Exists(path) ? File.ReadAllText(path) : "";
                    webView.CoreWebView2.PostWebMessageAsString("notas:" + contenido);
                }
                else if (msg.StartsWith("notas:guardar:"))
                {
                    string path = Path.Combine(_carpetaPerfil, "notas.txt");
                    File.WriteAllText(path, msg.Substring("notas:guardar:".Length));
                }
                else if (msg == "get:rendimiento")
                {
                    webView.CoreWebView2.PostWebMessageAsString("rendimiento:0,0");
                }
                else if (msg == "get:fondo")
                {
                    string path = Path.Combine(_carpetaPerfil, "fondo.txt");
                    string fondo = File.Exists(path) ? File.ReadAllText(path) : "";
                    webView.CoreWebView2.PostWebMessageAsString("fondo:" + fondo);
                }
                else if (msg.StartsWith("fondo:guardar:"))
                {
                    string fondoPath = msg.Substring("fondo:guardar:".Length);
                    string path = Path.Combine(_carpetaPerfil, "fondo.txt");
                    File.WriteAllText(path, fondoPath);
                    foreach (var tab in _tabs)
                        tab.CoreWebView2?.PostWebMessageAsString("fondo:" + fondoPath);
                }
                else if (msg == "fondo:quitar")
                {
                    string path = Path.Combine(_carpetaPerfil, "fondo.txt");
                    File.WriteAllText(path, "");
                    foreach (var tab in _tabs)
                        tab.CoreWebView2?.PostWebMessageAsString("fondo:");
                }
                else if (msg == "fondo:elegir")
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Elegir imagen de fondo",
                            Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.webp;*.gif",
                            Multiselect = false
                        };
                        if (dialog.ShowDialog() == true)
                        {
                            string imgPath = dialog.FileName.Replace("\\", "/");
                            string path = Path.Combine(_carpetaPerfil, "fondo.txt");
                            File.WriteAllText(path, imgPath);
                            foreach (var tab in _tabs)
                                tab.CoreWebView2?.PostWebMessageAsString("fondo:" + imgPath);
                        }
                    });
                }
                else if (msg.StartsWith("fondo:opacidad:"))
                {
                    string opStr = msg.Substring("fondo:opacidad:".Length).Trim();
                    if (int.TryParse(opStr, out int op))
                    {
                        string path = Path.Combine(_carpetaPerfil, "fondo_opacidad.txt");
                        File.WriteAllText(path, opStr);
                        foreach (var tab in _tabs)
                            tab.CoreWebView2?.PostWebMessageAsString("fondo:opacidad:" + op);
                    }
                }
                else if (msg == "get:fondo:opacidad")
                {
                    string path = Path.Combine(_carpetaPerfil, "fondo_opacidad.txt");
                    string op = File.Exists(path) ? File.ReadAllText(path).Trim() : "35";
                    webView.CoreWebView2.PostWebMessageAsString("fondo:opacidad:" + op);
                }
                else if (msg == "get:sidebar")
                {
                    webView.CoreWebView2.PostWebMessageAsString("sidebar:" + _sidebar.ToJson());
                }
                else if (msg.StartsWith("sidebar:guardar:"))
                {
                    string json = msg.Substring("sidebar:guardar:".Length);
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<SidebarItem>>(json, _jsonOpts);
                    if (items != null)
                    {
                        // Preservar items de extensiones que vengan en el orden del JS
                        _sidebar.Items = items;
                        _sidebar.Guardar();
                        Dispatcher.Invoke(() => RenderizarSidebar());
                    }
                }
                else if (msg == "update:check")
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var client = new HttpClient();
                            client.DefaultRequestHeaders.Add("User-Agent", "AtsukiBrowser");
                            string json = await client.GetStringAsync(
                                $"https://gist.githubusercontent.com/Gatosuki689/f24638ebb9ed77db3a58fc2318103b39/raw/version.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

                            var doc     = JsonSerializer.Deserialize<JsonElement>(json);
                            string ultima = doc.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                            string url    = doc.TryGetProperty("url",     out var u) ? u.GetString() ?? "" : "";
                            string notas  = doc.TryGetProperty("notas",   out var n) ? n.GetString() ?? "" : "";

                            bool hayUpdate = ultima != AppVersion && !string.IsNullOrEmpty(ultima);

                            string respuesta = JsonSerializer.Serialize(new
                            {
                                hayUpdate,
                                version = hayUpdate ? ultima : AppVersion,
                                url,
                                notas,
                                error = (string?)null
                            });

                            Dispatcher.Invoke(() => _tabs[_activeTab].CoreWebView2
                                ?.PostWebMessageAsString("update:" + respuesta));
                        }
                        catch (Exception ex)
                        {
                            string respuesta = JsonSerializer.Serialize(new
                            {
                                hayUpdate = false,
                                version   = AppVersion,
                                url       = "",
                                notas     = "",
                                error     = ex.Message
                            });
                            Dispatcher.Invoke(() => _tabs[_activeTab].CoreWebView2
                                ?.PostWebMessageAsString("update:" + respuesta));
                        }
                    });
                }
                else if (msg.StartsWith("update:instalar:"))
                {
                    string url = msg["update:instalar:".Length..];
                    DescargarEInstalar(url, "");
                }
                else if (msg == "sidebar:reset")
                {
                    _sidebar = new SidebarManager(_carpetaPerfil);
                    _sidebar.Items = new List<SidebarItem>
                    {
                        new() { Id="home",      Emoji="🏠", Nombre="Inicio",    Url="nuevatab",               Visible=true },
                        new() { Id="youtube",   Emoji="▶",  Nombre="YouTube",   Url="https://youtube.com",    Visible=true },
                        new() { Id="twitter",   Emoji="𝕏",  Nombre="Twitter",   Url="https://x.com",          Visible=true },
                        new() { Id="discord",   Emoji="💬", Nombre="Discord",   Url="https://discord.com/app",Visible=true },
                        new() { Id="sep1",      Separador=true,                                                Visible=true },
                        new() { Id="favoritos", Emoji="🔖", Nombre="Favoritos", Url="favoritos",              Visible=true },
                        new() { Id="historial", Emoji="🕐", Nombre="Historial", Url="historial",              Visible=true },
                        new() { Id="extensiones", Emoji="🧩", Nombre="Extensiones", Url="extensiones", Visible=true },
                        new() { Id="descargas", Emoji="⬇", Nombre="Descargas", Url="descargas", Visible=true },
                        new() { Id= "perfiles", Emoji="👤", Nombre="Perfiles", Url="perfiles", Visible=true },
                        new() { Id="ajustes", Emoji="⚙", Nombre="Ajustes", Url="ajustes", Visible=true },
                    };
                    _sidebar.Guardar();
                    Dispatcher.Invoke(() => RenderizarSidebar());
                    webView.CoreWebView2.PostWebMessageAsString("sidebar:" + _sidebar.ToJson());
                }
                else if (msg.StartsWith("widgets:config:"))
                {
                    string json = msg.Substring("widgets:config:".Length);
                    string configPath = Path.Combine(_carpetaPerfil, "widgets.json");
                    File.WriteAllText(configPath, json);

                    Dispatcher.Invoke(() =>
                    {
                        foreach (var tab in _tabs)
                        {
                            if (tab.CoreWebView2 != webView.CoreWebView2)
                                tab.CoreWebView2?.PostWebMessageAsString("widgets:config:" + json);
                        }
                    });
                }
                else if (msg == "get:widgets:config")
                {
                    string configPath = Path.Combine(_carpetaPerfil, "widgets.json");
                    string json = File.Exists(configPath) ? File.ReadAllText(configPath) : "{}";
                    webView.CoreWebView2.PostWebMessageAsString("widgets:config:" + json);
                }
                else if (msg == "get:tareas")
                {
                    string path = Path.Combine(_carpetaPerfil, "tareas.json");
                    string contenido = File.Exists(path) ? File.ReadAllText(path) : "[]";
                    webView.CoreWebView2.PostWebMessageAsString("tareas:" + contenido);
                }
                else if (msg.StartsWith("tareas:guardar:"))
                {
                    string path = Path.Combine(_carpetaPerfil, "tareas.json");
                    File.WriteAllText(path, msg.Substring("tareas:guardar:".Length));
                }
                else if (msg == "musica:play")
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var sessions = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                            sessions.GetCurrentSession()?.TryTogglePlayPauseAsync();
                        }
                        catch { }
                    });
                }
                else if (msg == "musica:anterior")
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var sessions = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                            sessions.GetCurrentSession()?.TrySkipPreviousAsync();
                        }
                        catch { }
                    });
                }
                else if (msg == "musica:siguiente")
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var sessions = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                            sessions.GetCurrentSession()?.TrySkipNextAsync();
                        }
                        catch { }
                    });
                }
                else if (msg == "get:musica")
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var session = _smtc?.GetCurrentSession();
                            if (session == null)
                            {
                                _musicaUltimoTitulo = "";
                                _musicaImagenCache  = "";
                                _musicaFuenteCache  = "";
                                Dispatcher.Invoke(() =>
                                    webView.CoreWebView2?.PostWebMessageAsString(
                                        "musica:{\"titulo\":\"\",\"artista\":\"\",\"imagen\":\"\",\"playing\":false,\"fuente\":\"\",\"progress\":0}"));
                                return;
                            }

                            var info     = await session.TryGetMediaPropertiesAsync();
                            var playback = session.GetPlaybackInfo();
                            bool playing = playback.PlaybackStatus ==
                                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                            string titulo  = info.Title  ?? "";
                            string artista = info.Artist ?? "";

                            // ── Progreso ─────────────────────────────────────
                            double progress = 0;
                            try
                            {
                                var tl = session.GetTimelineProperties();
                                if (tl != null && tl.EndTime.TotalSeconds > 0)
                                    progress = tl.Position.TotalSeconds / tl.EndTime.TotalSeconds * 100;
                            }
                            catch { }

                            // ── Fuente — solo recalcular si cambió la canción ─
                            if (titulo != _musicaUltimoTitulo)
                            {
                                string appId = session.SourceAppUserModelId ?? "";

                                if (appId.Contains("spotify", StringComparison.OrdinalIgnoreCase))
                                    _musicaFuenteCache = "Spotify";
                                else if (appId.Contains("firefox", StringComparison.OrdinalIgnoreCase))
                                    _musicaFuenteCache = "Firefox";
                                else if (appId.Contains("chrome", StringComparison.OrdinalIgnoreCase) &&
                                        !appId.Contains("msedge", StringComparison.OrdinalIgnoreCase))
                                    _musicaFuenteCache = "Chrome";
                                else if (appId.Contains("msedge",  StringComparison.OrdinalIgnoreCase) ||
                                        appId.Contains("webview", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Capturar URLs de tabs en el hilo UI una sola vez
                                    var urls = new List<string>();
                                    Dispatcher.Invoke(() =>
                                    {
                                        foreach (var tab in _tabs)
                                        {
                                            string u = tab.Source?.ToString() ?? "";
                                            if (!string.IsNullOrEmpty(u)) urls.Add(u);
                                        }
                                    });

                                    if      (urls.Any(u => u.Contains("youtube.com")))    _musicaFuenteCache = "YouTube";
                                    else if (urls.Any(u => u.Contains("spotify.com")))    _musicaFuenteCache = "Spotify";
                                    else if (urls.Any(u => u.Contains("soundcloud.com"))) _musicaFuenteCache = "SoundCloud";
                                    else if (urls.Any(u => u.Contains("twitch.tv")))      _musicaFuenteCache = "Twitch";
                                    else if (urls.Any(u => u.Contains("netflix.com")))    _musicaFuenteCache = "Netflix";
                                    else                                                   _musicaFuenteCache = "";
                                }
                                else
                                    _musicaFuenteCache = appId.Length > 20 ? "" : appId;
                            }

                            // ── Imagen — solo si cambió la canción ───────────
                            if (titulo != _musicaUltimoTitulo)
                            {
                                _musicaUltimoTitulo = titulo;
                                _musicaImagenCache  = "";
                                try
                                {
                                    var thumb = info.Thumbnail;
                                    if (thumb != null)
                                    {
                                        var stream = await thumb.OpenReadAsync();
                                        using var reader = new Windows.Storage.Streams.DataReader(stream);
                                        await reader.LoadAsync((uint)stream.Size);
                                        var bytes = new byte[stream.Size];
                                        reader.ReadBytes(bytes);
                                        _musicaImagenCache = "data:image/png;base64," +
                                            Convert.ToBase64String(bytes);
                                    }
                                }
                                catch { }
                            }

                            // ── Enviar ───────────────────────────────────────
                            string imagen  = _musicaImagenCache;
                            string fuente  = _musicaFuenteCache;

                            Dispatcher.Invoke(() =>
                            {
                                var json = System.Text.Json.JsonSerializer.Serialize(
                                    new { titulo, artista, imagen, playing, fuente, progress });
                                webView.CoreWebView2?.PostWebMessageAsString("musica:" + json);
                            });
                        }
                        catch { }
                    });
                }
                else if (msg.StartsWith("sidebar:widget:rendimiento:"))
                {
                    _sbWidgetRendimiento = msg.EndsWith("true");
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "sb_widgets.json");
                    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { rendimiento = _sbWidgetRendimiento }));
                    Dispatcher.Invoke(() => RenderizarSidebar());
                }
                else if (msg.StartsWith("sidebar:widget:reloj:"))
                {
                    _sbWidgetReloj = msg.EndsWith("true");
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "sb_widgets.json");
                    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(
                        new { rendimiento = _sbWidgetRendimiento, reloj = _sbWidgetReloj }));
                    Dispatcher.Invoke(() => RenderizarSidebar());
                }
                else if (msg.StartsWith("sidebar:widget:capturas:"))
                {
                    _sbWidgetCapturas = msg.EndsWith("true");
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "sb_widgets.json");
                    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(
                        new { rendimiento = _sbWidgetRendimiento, reloj = _sbWidgetReloj, 
                            capturas = _sbWidgetCapturas, busqueda = _sbWidgetBusqueda }));
                    Dispatcher.Invoke(() => RenderizarSidebar());
                }
                else if (msg.StartsWith("sidebar:widget:busqueda:"))
                {
                    _sbWidgetBusqueda = msg.EndsWith("true");
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "sb_widgets.json");
                    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(
                        new { rendimiento = _sbWidgetRendimiento, reloj = _sbWidgetReloj,
                            capturas = _sbWidgetCapturas, busqueda = _sbWidgetBusqueda }));
                    Dispatcher.Invoke(() => RenderizarSidebar());
                }
                else if (msg == "get:sidebar:widgets")
                {
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "sb_widgets.json");
                    if (File.Exists(path))
                    {
                        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                            File.ReadAllText(path));
                        if (json.TryGetProperty("rendimiento", out var val))
                            _sbWidgetRendimiento = val.GetBoolean();
                        if (json.TryGetProperty("reloj", out var reloj))
                            _sbWidgetReloj = reloj.GetBoolean();
                        if (json.TryGetProperty("capturas", out var capturas))
                            _sbWidgetCapturas = capturas.GetBoolean();
                        if (json.TryGetProperty("busqueda", out var busqueda))
                            _sbWidgetBusqueda = busqueda.GetBoolean();
                    }
                    webView.CoreWebView2.PostWebMessageAsString("sidebar:widgets:" +
                        System.Text.Json.JsonSerializer.Serialize(new { rendimiento = _sbWidgetRendimiento, reloj = _sbWidgetReloj, capturas = _sbWidgetCapturas, busqueda = _sbWidgetBusqueda }));
                }
                else if (msg.StartsWith("sugerencias:"))
                {
                    string query = msg.Substring("sugerencias:".Length);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var client = new System.Net.Http.HttpClient();
                            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                            var url = $"https://suggestqueries.google.com/complete/search?client=firefox&q={Uri.EscapeDataString(query)}";
                            var res = await client.GetStringAsync(url);
                            Dispatcher.Invoke(() =>
                                webView.CoreWebView2?.PostWebMessageAsString("sugerencias:" + res));
                        }
                        catch { }
                    });
                }
                else if (msg == "get:descargas")
                {
                    var payload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        historial = _descargas.Historial,
                        activas   = _descargas.Activas,
                        carpeta   = _descargas.CarpetaDefault
                    });
                    webView.CoreWebView2.PostWebMessageAsString("descargas:" + payload);
                }
                else if (msg == "descargas:limpiar")
                {
                    _descargas.LimpiarHistorial();
                    var payload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        historial = _descargas.Historial,
                        activas   = _descargas.Activas,
                        carpeta   = _descargas.CarpetaDefault
                    });
                    webView.CoreWebView2.PostWebMessageAsString("descargas:" + payload);
                }
                else if (msg == "descargas:carpeta:elegir")
                {
                    Dispatcher.Invoke(() =>
                    {
                        // FolderBrowserDialog via COM sin WinForms
                        var dialog = new Microsoft.Win32.SaveFileDialog
                        {
                            Title = "Selecciona la carpeta de descargas",
                            FileName = "Selecciona esta carpeta",
                            Filter = "Carpeta|*.none",
                            CheckFileExists = false,
                            CheckPathExists = true
                        };
                        if (dialog.ShowDialog() == true)
                        {
                            string carpeta = Path.GetDirectoryName(dialog.FileName)!;
                            _descargas.SetCarpeta(carpeta);
                            var payload = System.Text.Json.JsonSerializer.Serialize(new
                            {
                                historial = _descargas.Historial,
                                activas   = _descargas.Activas,
                                carpeta   = _descargas.CarpetaDefault
                            });
                            foreach (var tab in _tabs)
                                tab.CoreWebView2?.PostWebMessageAsString("descargas:" + payload);
                        }
                    });
                }
                else if (msg.StartsWith("descargas:abrir:"))
                {
                    string ruta = msg.Substring("descargas:abrir:".Length);
                    if (File.Exists(ruta))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{ruta}\"");
                }
                else if (msg == "navigate:descargas")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlDescargas));
                }
                else if (msg == "get:perf:config")
                {
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "perf.json");
                    string json = File.Exists(path) ? File.ReadAllText(path) : "{}";
                    webView.CoreWebView2.PostWebMessageAsString("perf:config:" + json);
                }
                else if (msg.StartsWith("perf:config:"))
                {
                    string json = msg.Substring("perf:config:".Length);
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "perf.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, json);
                    Dispatcher.Invoke(() => AplicarPerfConfig(json));
                }
                else if (msg == "get:buscador")
                {
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "buscador.txt");
                    string val = File.Exists(path) ? File.ReadAllText(path).Trim() : "google";
                    webView.CoreWebView2.PostWebMessageAsString("buscador:" + val);
                }
                else if (msg.StartsWith("buscador:set:"))
                {
                    string val = msg.Substring("buscador:set:".Length);
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "buscador.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, val);
                    _buscadorActivo = val;
                }
                else if (msg.StartsWith("perf:media_background:"))
                {
                    _suspenderMediaEnBackground = msg.EndsWith("true");
                    // Guardar en perf.json
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "perf.json");
                    string jsonActual = File.Exists(path) ? File.ReadAllText(path) : "{}";
                    AplicarPerfConfig(jsonActual.Replace("}", $",\"suspender_media\":{(_suspenderMediaEnBackground ? "true" : "false")}}}").Replace(",}", "}"));
                }
                else if (msg == "get:perfiles")
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        activo   = _perfilActivo,
                        perfiles = _perfiles.Perfiles
                    }, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });
                    webView.CoreWebView2.PostWebMessageAsString("perfiles:" + json);
                }
                else if (msg.StartsWith("perfil:cambiar:"))
                {
                    string id = msg.Substring("perfil:cambiar:".Length);
                    Dispatcher.Invoke(() => CambiarPerfil(id));
                }
                else if (msg.StartsWith("perfil:crear:"))
                {
                    var json = msg.Substring("perfil:crear:".Length);
                    var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;
                    string nombre = doc.GetProperty("nombre").GetString() ?? "Nuevo perfil";
                    string emoji  = doc.GetProperty("emoji").GetString()  ?? "👤";
                    string icono  = doc.GetProperty("icono").GetString()  ?? "";
                    var nuevo = _perfiles.Crear(nombre, emoji, icono);
                    webView.CoreWebView2.PostWebMessageAsString("perfiles:" + _perfiles.ToJson());
                }
                else if (msg.StartsWith("perfil:eliminar:"))
                {
                    string id = msg.Substring("perfil:eliminar:".Length);
                    _perfiles.Eliminar(id);
                    webView.CoreWebView2.PostWebMessageAsString("perfiles:" + _perfiles.ToJson());
                }
                else if (msg.StartsWith("perfil:editar:"))
                {
                    var json = msg.Substring("perfil:editar:".Length);
                    var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;
                    string id     = doc.GetProperty("id").GetString()     ?? "";
                    string nombre = doc.GetProperty("nombre").GetString() ?? "";
                    string emoji  = doc.GetProperty("emoji").GetString()  ?? "👤";
                    string icono  = doc.GetProperty("icono").GetString()  ?? "";
                    _perfiles.Editar(id, nombre, emoji, icono);
                    webView.CoreWebView2.PostWebMessageAsString("perfiles:" + _perfiles.ToJson());
                }
                else if (msg == "perfil:invitado")
                {
                    Dispatcher.Invoke(() => CambiarPerfil("invitado"));
                }
                else if (msg == "get:atajos")
                {
                    webView.CoreWebView2.PostWebMessageAsString("atajos:" + _atajos.ToJson());
                }
                else if (msg.StartsWith("atajo:establecer:"))
                {
                    var json = msg.Substring("atajo:establecer:".Length);
                    var a = JsonSerializer.Deserialize<Atajo>(json, _jsonOpts);
                    if (a != null)
                        _atajos.Establecer(a.Accion, a.Ctrl, a.Shift, a.Alt, a.Tecla);
                    webView.CoreWebView2.PostWebMessageAsString("atajos:" + _atajos.ToJson());
                }
                else if (msg.StartsWith("atajo:restablecer:"))
                {
                    string accion = msg.Substring("atajo:restablecer:".Length);
                    _atajos.Restablecer(accion);
                    webView.CoreWebView2.PostWebMessageAsString("atajos:" + _atajos.ToJson());
                }
                else if (msg == "atajos:restablecer:todos")
                {
                    _atajos.RestablecerTodos();
                    webView.CoreWebView2.PostWebMessageAsString("atajos:" + _atajos.ToJson());
                }
                else if (msg == "exportar:config")
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new Microsoft.Win32.SaveFileDialog
                        {
                            Title = "Exportar configuración",
                            Filter = "Respaldo de configuración|*.zip",
                            FileName = $"atsuki_config_{_perfilActivo.Nombre}_{DateTime.Now:yyyyMMdd}"
                        };
                        if (dialog.ShowDialog() != true) return;

                        try
                        {
                            var carpeta = _carpetaPerfil;
                            var archivos = new[] { "tema.json", "sidebar.json", "dials.json",
                                                "widgets.json", "atajos.json",
                                                "extensiones_estado.json", "descargas_config.json" };

                            using var zip = System.IO.Compression.ZipFile.Open(
                                dialog.FileName, System.IO.Compression.ZipArchiveMode.Create);

                            foreach (var archivo in archivos)
                            {
                                var ruta = Path.Combine(carpeta, archivo);
                                if (File.Exists(ruta))
                                    zip.CreateEntryFromFile(ruta, archivo);
                            }

                            webView.CoreWebView2.PostWebMessageAsString(
                                "respaldo:✅ Configuración exportada correctamente.");
                        }
                        catch (Exception ex)
                        {
                            webView.CoreWebView2.PostWebMessageAsString(
                                "respaldo:❌ Error al exportar: " + ex.Message);
                        }
                    });
                }
                else if (msg == "exportar:datos")
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new Microsoft.Win32.SaveFileDialog
                        {
                            Title = "Exportar datos",
                            Filter = "Respaldo de datos|*.zip",
                            FileName = $"atsuki_datos_{_perfilActivo.Nombre}_{DateTime.Now:yyyyMMdd}"
                        };
                        if (dialog.ShowDialog() != true) return;

                        try
                        {
                            var carpeta = _carpetaPerfil;
                            var archivos = new[] { "historial.json", "favoritos.json", "descargas.json" };

                            using var zip = System.IO.Compression.ZipFile.Open(
                                dialog.FileName, System.IO.Compression.ZipArchiveMode.Create);

                            foreach (var archivo in archivos)
                            {
                                var ruta = Path.Combine(carpeta, archivo);
                                if (File.Exists(ruta))
                                    zip.CreateEntryFromFile(ruta, archivo);
                            }

                            webView.CoreWebView2.PostWebMessageAsString(
                                "respaldo:✅ Datos exportados correctamente.");
                        }
                        catch (Exception ex)
                        {
                            webView.CoreWebView2.PostWebMessageAsString(
                                "respaldo:❌ Error al exportar: " + ex.Message);
                        }
                    });
                }
                else if (msg == "importar:config")
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Importar configuración",
                            Filter = "Respaldo de configuración|*.zip"
                        };
                        if (dialog.ShowDialog() != true) return;

                        try
                        {
                            var carpeta = _carpetaPerfil;
                            var permitidos = new HashSet<string> { "tema.json", "sidebar.json", "dials.json",
                                                                "widgets.json", "atajos.json",
                                                                "extensiones_estado.json", "descargas_config.json" };

                            using var zip = System.IO.Compression.ZipFile.OpenRead(dialog.FileName);
                            foreach (var entry in zip.Entries)
                            {
                                if (!permitidos.Contains(entry.Name)) continue;
                                entry.ExtractToFile(Path.Combine(carpeta, entry.Name), overwrite: true);
                            }

                            // Recargar managers
                            _temas    = new TemaManager(carpeta);
                            _sidebar  = new SidebarManager(carpeta);
                            _extensiones = new ExtensionesManager(carpeta);

                            webView.CoreWebView2.PostWebMessageAsString(
                                "respaldo:✅ Configuración importada. Recarga la página para ver los cambios.");
                        }
                        catch (Exception ex)
                        {
                            webView.CoreWebView2.PostWebMessageAsString(
                                "respaldo:❌ Error al importar: " + ex.Message);
                        }
                    });
                }
                else if (msg == "importar:datos")
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Importar datos",
                            Filter = "Respaldo de datos|*.zip"
                        };
                        if (dialog.ShowDialog() != true) return;

                        try
                        {
                            var carpeta = _carpetaPerfil;
                            var permitidos = new HashSet<string> { "historial.json", "favoritos.json", "descargas.json" };

                            using var zip = System.IO.Compression.ZipFile.OpenRead(dialog.FileName);
                            foreach (var entry in zip.Entries)
                            {
                                if (!permitidos.Contains(entry.Name)) continue;
                                entry.ExtractToFile(Path.Combine(carpeta, entry.Name), overwrite: true);
                            }

                            // Recargar managers
                            _historial = new HistorialManager(carpeta);
                            _favoritos = new FavoritosManager(carpeta);
                            _descargas = new DescargasManager(carpeta);

                            webView.CoreWebView2.PostWebMessageAsString(
                                "respaldo:✅ Datos importados correctamente.");
                        }
                        catch (Exception ex)
                        {
                            webView.CoreWebView2.PostWebMessageAsString(
                                "respaldo:❌ Error al importar: " + ex.Message);
                        }
                    });
                }
                else if (msg == "get:segundos")
                {
                    string path = Path.Combine(_carpetaPerfil, "segundos.txt");
                    bool val = File.Exists(path) && File.ReadAllText(path).Trim() == "true";
                    webView.CoreWebView2.PostWebMessageAsString("segundos:" + val.ToString().ToLower());
                }
                else if (msg.StartsWith("set:segundos:"))
                {
                    string val = msg.Substring("set:segundos:".Length);
                    File.WriteAllText(Path.Combine(_carpetaPerfil, "segundos.txt"), val);
                }
                else if (msg == "get:ntcfg")
                {
                    string path = Path.Combine(_carpetaPerfil, "ntcfg.json");
                    string json = File.Exists(path) ? File.ReadAllText(path) : "{}";
                    webView.CoreWebView2.PostWebMessageAsString("ntcfg:" + json);
                }
                else if (msg.StartsWith("ntcfg:guardar:"))
                {
                    string path = Path.Combine(_carpetaPerfil, "ntcfg.json");
                    File.WriteAllText(path, msg.Substring("ntcfg:guardar:".Length));
                }
                else if (msg.StartsWith("atsukimusic:player:"))
                {
                    string cmd = msg.Substring("atsukimusic:player:".Length);
                    Dispatcher.Invoke(async () =>
                    {
                        await InicializarMusicaWebView();
                        _musicaWebView?.CoreWebView2?.PostWebMessageAsString(cmd);
                    });
                }
                else if (msg == "atsukimusic:elegir:archivo")
                {
                    Dispatcher.Invoke(async () =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title = "Agregar música",
                            Filter = "Audio|*.mp3;*.flac;*.wav;*.ogg;*.aac;*.m4a",
                            Multiselect = true
                        };
                        if (dialog.ShowDialog() != true) return;

                        await InicializarMusicaWebView();

                        foreach (var archivo in dialog.FileNames)
                        {
                            string nombre = Path.GetFileNameWithoutExtension(archivo);
                            string url    = "file:///" + archivo.Replace("\\", "/");
                            string json   = System.Text.Json.JsonSerializer.Serialize(
                                new { titulo = nombre, url });
                            webView.CoreWebView2?.PostWebMessageAsString("atsukimusic:archivo:" + json);
                        }
                    });
                }
                else if (msg == "previews:activar")
                {
                    _recibirPreviews = true;
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "previews.txt");
                    File.WriteAllText(path, "true");
                }
                else if (msg == "previews:desactivar")
                {
                    _recibirPreviews = false;
                    string path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "AtsukiBrowser", "previews.txt");
                    File.WriteAllText(path, "false");
                }
            };

            // ── Descargas ────────────────────────────────────
            webView.CoreWebView2.DownloadStarting += (s, args) =>
            {
                var op = args.DownloadOperation;
                string nombre = Path.GetFileName(args.ResultFilePath);

                // Redirigir a carpeta configurada
                args.ResultFilePath = Path.Combine(_descargas.CarpetaDefault, nombre);
                args.Handled = true;

                var entrada = _descargas.IniciarDescarga(op.Uri, nombre);
                entrada.Total = (long)(op.TotalBytesToReceive ?? 0UL);

                op.BytesReceivedChanged += (s2, e2) =>
                {
                    entrada.Recibido = (long)op.BytesReceived;
                    entrada.Total    = (long)(op.TotalBytesToReceive ?? (ulong)entrada.Total);
                    NotificarDescargasActivas();
                };

                op.StateChanged += (s2, e2) =>
                {
                    switch (op.State)
                    {
                        case CoreWebView2DownloadState.Completed:
                            _descargas.CompletarDescarga(entrada.Id);
                            break;
                        case CoreWebView2DownloadState.Interrupted:
                            _descargas.CancelarDescarga(entrada.Id);
                            break;
                    }
                    NotificarDescargasActivas();
                    Dispatcher.Invoke(ActualizarBadgeDescargas);
                };

                Dispatcher.Invoke(ActualizarBadgeDescargas);
            };

            webView.CoreWebView2.NewWindowRequested += (s, args) =>
            {
                args.Handled = true;
                string newUrl = args.Uri;
                Dispatcher.Invoke(() => AbrirNuevaTab(newUrl));
            };
            // Detectar cuando WebView2 resetea el zoom y restaurarlo
            webView.ZoomFactorChanged += (s, e) =>
            {
                if (_aplicandoZoom) return;
                int idx = _tabs.IndexOf(webView);
                if (idx != _activeTab) return;

                string url2 = "";
                Dispatcher.Invoke(() => url2 = webView.Source?.ToString() ?? "");
                string dom = GetDominioZoom(url2);

                if (!string.IsNullOrEmpty(dom) && _zoomPorDominio.TryGetValue(dom, out double zoomGuardado))
                {
                    if (Math.Abs(webView.ZoomFactor - zoomGuardado) > 0.01)
                    {
                        _aplicandoZoom = true;
                        webView.ZoomFactor = zoomGuardado;
                        _aplicandoZoom = false;
                    }
                }

                Dispatcher.Invoke(ActualizarZoomLabel);
            };

            webView.Source = new Uri(url);
            webView.NavigationCompleted += async (s, e) =>
            {
                if (!e.IsSuccess) return;
                // ── NUEVO: actualizar título y URL bar ──
                Dispatcher.Invoke(() =>
                {
                    int idx = _tabs.IndexOf(webView);
                    if (idx < 0) return;

                    string url    = webView.Source?.ToString() ?? "";
                    string titulo = webView.CoreWebView2?.DocumentTitle ?? "Nueva pestaña";
                    if (string.IsNullOrWhiteSpace(titulo)) titulo = "Nueva pestaña";

                    // Actualizar URL bar si es la tab activa
                    if (idx == _activeTab)
                    {
                        _ignorarGotFocus = true;
                        UrlBar.Text = url;
                        SugerenciasPopup.IsOpen = false;
                        _ignorarGotFocus = false;
                    }

                    // Actualizar título del botón de tab
                    if (_tabButtons[idx].Tag is TextBlock label)
                        label.Text = titulo;

                    // Guardar en historial
                    _historial.Agregar(url, titulo);

                    // Actualizar estrella de favorito
                    ActualizarEstrellaFavorito();
                });

                webView.CoreWebView2?.PostWebMessageAsString("tema:" + _temas.ToJson());
                webView.CoreWebView2?.PostWebMessageAsString("version:" + AppVersion);
                webView.CoreWebView2?.PostWebMessageAsString("perfil:activo:" + 
                    System.Text.Json.JsonSerializer.Serialize(_perfiles.Activo));
                
                if (_musicaInicializada && _musicaWebView?.CoreWebView2 != null)
                    _musicaWebView.CoreWebView2.PostWebMessageAsString("player:estado");

                // Inyectar scripts de extensiones (no en modo invitado)
                if (!_perfiles.Activo.EsInvitado)
                {
                    foreach (var script in _extensiones.GetScriptsActivos())
                    {
                        try { await webView.CoreWebView2!.ExecuteScriptAsync(script); }
                        catch { }
                    }
                }

                // Inyectar widgets de extensiones en NuevaTab
                string urlActual = webView.Source?.ToString() ?? "";
                if (urlActual.Contains("NuevaTab.html"))
                {
                    var widgets = _extensiones.GetWidgetsActivos()
                    .Where(w => w.ext.Widget?.Destino != "sidebar")
                    .ToList();
                    foreach (var (ext, html) in widgets)
                    {
                        try
                        {
                            // Escapar el HTML para pasarlo como string JS
                            string htmlEscapado = html
                                .Replace("\\", "\\\\")
                                .Replace("`", "\\`")
                                .Replace("$", "\\$");

                            string id      = ext.Widget!.Id;
                            string titulo  = ext.Widget.Titulo;
                            string ancho   = ext.Widget.Ancho;
                            string altoClass = ext.Widget.Alto == "alto" ? " widget-alto" : "";
                            string anchoClass = ancho == "ancho"
                                ? " widget-ancho"
                                : ancho == "estrecho"
                                    ? " widget-estrecho"
                                    : "";

                            string script = $@"
            (function() {{
                const existente = document.getElementById('{id}');
                if (existente) return;

                const wrap = document.getElementById('widgets-wrap');
                if (!wrap) return;

                const widget = document.createElement('div');
                widget.className = 'widget{anchoClass}{altoClass}';
                const body = document.createElement('div');
                body.className = 'widget-body';
                const tmp = document.createElement('div');
                tmp.innerHTML = `{htmlEscapado}`;

                // Separar estilos, scripts y HTML
                tmp.querySelectorAll('style').forEach(s => {{
                    document.head.appendChild(s.cloneNode(true));
                }});
                tmp.querySelectorAll('script').forEach(s => {{
                    const ns = document.createElement('script');
                    ns.textContent = s.textContent;
                    body.appendChild(ns);
                }});
                Array.from(tmp.childNodes).forEach(n => {{
                    if (n.nodeName !== 'STYLE' && n.nodeName !== 'SCRIPT')
                        body.appendChild(n.cloneNode(true));
                }});

                // Ejecutar scripts del widget
                body.querySelectorAll('script').forEach(s => {{
                    try {{ eval(s.textContent); }} catch {{}}
                }});

                widget.appendChild(body);
                wrap.appendChild(widget);

                // Agregar al menú de personalización respetando config guardada
                const cp = document.querySelector('#customize-panel .cp-section:nth-child(2)');
                if (cp) {{
                    const config = window.__widgetsConfig || {{}};
                    const isOn = config.hasOwnProperty('{id}') ? !!config['{id}'] : true;

                    widget.style.display = isOn ? '' : 'none';

                    const row = document.createElement('div');
                    row.className = 'cp-row';
                    row.innerHTML = ""<span>{ext.Icono} {ext.Nombre}</span><button class='cp-toggle' data-widget='{id}'></button>"";
                    const toggle = row.querySelector('.cp-toggle');
                    if (isOn) toggle.classList.add(""on"");

                    toggle.addEventListener('click', () => {{
                        toggle.classList.toggle(""on"");
                        widget.style.display = toggle.classList.contains(""on"") ? """" : ""none"";
                        const cfg = {{}};
                        document.querySelectorAll('.cp-toggle').forEach(b => {{
                            cfg[b.dataset.widget] = b.classList.contains('on');
                        }});
                        window.__widgetsConfig = cfg;
                        window.chrome.webview.postMessage('widgets:config:' + JSON.stringify(cfg));
                    }});
                    cp.appendChild(row);
                }}
            }})();";

                            await webView.CoreWebView2!.ExecuteScriptAsync(script);
                        }
                        catch { }
                    }
                }
            };

            webView.NavigationCompleted += WebView_NavigationCompleted;

            // ── Agregar tab y botón ANTES de iniciar detección ──
            _tabs.Add(webView);

            int index = _tabs.Count - 1;
            var tabBtn = CrearBotonTab("Nueva pestaña", index);
            TabStrip.Children.Add(tabBtn);
            _tabButtons.Add(tabBtn);

            // Iniciar detección de audio DESPUÉS de que el botón existe
            IniciarDeteccionAudioTab(webView);

            ActivarTab(index);
        }
        

        private Button CrearBotonTab(string titulo, int index)
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var favicon = new Image
            {
                Width = 14, Height = 14,
                Margin = new Thickness(0, 0, 6, 0),
                Opacity = 0.8,
                Tag = "favicon",
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(favicon, BitmapScalingMode.HighQuality);
            Grid.SetColumn(favicon, 0);

            var audioIndicator = new TextBlock
            {
                Text = "🔊",
                FontSize = 10,
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = Visibility.Collapsed,
                Tag = "audio",
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(audioIndicator, 1);

            var label = new TextBlock
            {
                Text = titulo,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Tag = "label",
                Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(label, 2);

            var closeBtn = new Button
            {
                Content = "×",
                Width = 18,
                Height = 18,
                Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(closeBtn, 3);

            closeBtn.MouseEnter += (s, e) =>
                closeBtn.Background = new SolidColorBrush(Color.FromArgb(60, 255, 80, 80));
            closeBtn.MouseLeave += (s, e) =>
                closeBtn.Background = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));

            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(closeBtn, true);

            panel.Children.Add(favicon);
            panel.Children.Add(audioIndicator);
            panel.Children.Add(label);
            panel.Children.Add(closeBtn);

            var btn = new Button
            {
                Content = panel,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 0, 8, 0),
                Height = 34,
                MinWidth = 80,
                MaxWidth = 220,
                Cursor = Cursors.Hand,
                AllowDrop = true,
                Tag = label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(255, 18, 16, 32))
            };

            // Template con bordes redondeados arriba
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(7, 7, 0, 0));
            factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            factory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(cpFactory);
            btn.Template = new ControlTemplate(typeof(Button)) { VisualTree = factory };

            // Hover suave
            btn.MouseEnter += (s, e) =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx != _activeTab)
                    btn.Background = new SolidColorBrush(Color.FromArgb(255, 35, 30, 60));
            };
            btn.MouseLeave += (s, e) =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx != _activeTab)
                    btn.Background = new SolidColorBrush(Color.FromArgb(255, 18, 16, 32));
            };

            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(btn, true);

            // ── Click: usar índice dinámico ──
            btn.Click += (s, e) =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx >= 0) ActivarTab(idx);
            };

            // ── Cerrar: índice dinámico ──
            closeBtn.Click += (s, e) =>
            {
                e.Handled = true;
                int idx = _tabButtons.IndexOf(btn);
                if (idx >= 0) CerrarTab(idx);
            };

            // ── Menú contextual clic derecho ──
            var ctxMenu = new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1)
            };

            MenuItem CrearMenuItem(string texto, Action accion)
            {
                var item = new MenuItem
                {
                    Header = texto,
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    FontSize = 12,
                    Padding = new Thickness(12, 6, 12, 6)
                };
                item.Click += (s, e) => accion();
                return item;
            }

            ctxMenu.Items.Add(CrearMenuItem("📄  Duplicar pestaña", () =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx >= 0) AbrirNuevaTab(_tabs[idx].Source?.ToString() ?? _urlNuevaTab);
            }));

            ctxMenu.Items.Add(new Separator
            {
                Background = new SolidColorBrush(Color.FromRgb(42, 26, 78))
            });

            ctxMenu.Items.Add(CrearMenuItem("💤  Hibernar", () =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx < 0 || idx == _activeTab) return;

                var tab = _tabs[idx];
                string urlGuardada = tab.Source?.ToString() ?? "";
                if (string.IsNullOrEmpty(urlGuardada)) return;

                // Reemplazar el WebView con una página en blanco y guardar la URL en el Tag
                tab.CoreWebView2?.Navigate("about:blank");
                btn.Tag = label; // preservar label
                btn.ToolTip = $"💤 {urlGuardada}";

                // Marcar visualmente
                if (btn.Content is StackPanel sp)
                {
                    var lbl = sp.Children.OfType<TextBlock>()
                        .FirstOrDefault(t => t.Tag?.ToString() == "label");
                    if (lbl != null)
                    {
                        lbl.Opacity = 0.4;
                        lbl.Text = "💤 " + lbl.Text.Replace("💤 ", "");
                    }
                }

                // Guardar URL hibernada en el Tag del WebView
                tab.Tag = urlGuardada;
            }));

            ctxMenu.Items.Add(CrearMenuItem("▶  Reactivar", () =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx < 0) return;

                var tab = _tabs[idx];
                string? urlGuardada = tab.Tag as string;
                if (string.IsNullOrEmpty(urlGuardada)) return;

                tab.Source = new Uri(urlGuardada);
                tab.Tag = null;
                btn.ToolTip = null;

                // Quitar marca visual
                if (btn.Content is StackPanel sp)
                {
                    var lbl = sp.Children.OfType<TextBlock>()
                        .FirstOrDefault(t => t.Tag?.ToString() == "label");
                    if (lbl != null)
                    {
                        lbl.Opacity = 1;
                        lbl.Text = lbl.Text.Replace("💤 ", "");
                    }
                }
            }));

            ctxMenu.Items.Add(new Separator
            {
                Background = new SolidColorBrush(Color.FromRgb(42, 26, 78))
            });

            ctxMenu.Items.Add(CrearMenuItem("✕  Cerrar pestaña", () =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx >= 0) CerrarTab(idx);
            }));

            btn.ContextMenu = ctxMenu;

            // ── Drag & drop con umbral para no interferir con clicks ──
            Point _dragStart = default;
            bool _dragging = false;

            btn.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _dragStart = e.GetPosition(btn);
                _dragging = false;
            };

            btn.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || _dragging) return;
                var pos = e.GetPosition(btn);
                if (Math.Abs(pos.X - _dragStart.X) > 8 || Math.Abs(pos.Y - _dragStart.Y) > 8)
                {
                    _dragging = true;
                    DragDrop.DoDragDrop(btn, btn, DragDropEffects.Move);
                    _dragging = false;
                }
            };

            btn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
            };

            btn.Drop += (s, e) =>
            {
                if (e.Data.GetData(typeof(Button)) is Button source && source != btn)
                {
                    int from = _tabButtons.IndexOf(source);
                    int to   = _tabButtons.IndexOf(btn);
                    if (from >= 0 && to >= 0)
                        MoverTab(from, to);
                }
            };

            btn.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(typeof(Button)))
                    e.Effects = DragDropEffects.Move;
            };

            // ── Vista previa al hacer hover ──
            btn.MouseEnter += (s, e) => IniciarHoverPreview(btn);
            btn.MouseLeave += (s, e) =>
            {
                // Solo cerrar si el mouse realmente salió del botón completo
                if (!btn.IsMouseOver)
                    CerrarHoverPreview();
            };

            return btn;
        }

        private void ActualizarEstiloTabs()
        {
            int count = _tabButtons.Count;
            if (count == 0) return;

            // Ancho disponible para las tabs (igual que Chrome)
            double espacioDisponible = TabScrollViewer.ActualWidth - 34; // 34 = botón "+"
            double anchoIdeal   = 200;
            double anchoMinimo  = 60;
            double anchoCalculado = Math.Max(anchoMinimo, Math.Min(anchoIdeal, espacioDisponible / count));

            for (int i = 0; i < count; i++)
            {
                var btn = _tabButtons[i];
                btn.Width = anchoCalculado;

                bool activa = i == _activeTab;
                btn.Background = activa
                    ? new SolidColorBrush(Color.FromArgb(255, 55, 45, 90))
                    : new SolidColorBrush(Color.FromArgb(255, 18, 16, 32));

                // Botón cerrar: siempre visible, con hover en inactivas
                if (btn.Content is Grid sp)
                {
                    var close = sp.Children.OfType<Button>().FirstOrDefault();
                    if (close != null)
                    {
                        close.Visibility = Visibility.Visible;

                        if (!activa)
                        {
                            // Mostrar solo al hacer hover sobre la pestaña
                            btn.MouseEnter -= (s, e) => close.Visibility = Visibility.Visible;
                            btn.MouseLeave -= (s, e) => close.Visibility = Visibility.Collapsed;

                            close.Visibility = btn.IsMouseOver ? Visibility.Visible : Visibility.Collapsed;

                            btn.MouseEnter += (s, e) => close.Visibility = Visibility.Visible;
                            btn.MouseLeave += (s, e) => close.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
        }

        private async void ActivarTab(int index)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                try
                {
                    if (i != index)
                    {
                        // Capturar preview de forma diferida sin bloquear
                        int captureIdx = i;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(200); // esperar a que la tab se colapse
                                using var stream = new MemoryStream();
                                await Dispatcher.InvokeAsync(async () =>
                                {
                                    if (captureIdx < _tabs.Count && _tabs[captureIdx].CoreWebView2 != null)
                                        await _tabs[captureIdx].CoreWebView2.CapturePreviewAsync(
                                            CoreWebView2CapturePreviewImageFormat.Png, stream);
                                }).Task;
                                stream.Position = 0;
                                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                bmp.StreamSource = stream;
                                bmp.EndInit();
                                bmp.Freeze();
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    if (captureIdx < _tabs.Count)
                                        _tabPreviews[captureIdx] = bmp;
                                });
                            }
                            catch { }
                        });

                        _tabs[i].Visibility = Visibility.Collapsed;

                        if (_tabs[i].CoreWebView2 != null)
                        {
                            // Pausar media SOLO si está activado
                            if (_suspenderMediaEnBackground)
                                _ = _tabs[i].CoreWebView2.ExecuteScriptAsync(
                                    "document.querySelectorAll('video,audio').forEach(m=>m.pause())");

                            // Suspender proceso SOLO si NO hay media reproduciéndose
                            // o si el usuario quiere suspender todo
                            if (_perfSuspenderTabs)
                            {
                                if (_suspenderMediaEnBackground)
                                {
                                    // Suspender siempre — la media ya fue pausada arriba
                                    _ = _tabs[i].CoreWebView2.TrySuspendAsync();
                                }
                                else
                                {
                                    // Solo suspender si no hay media activa
                                    var hayMedia = await _tabs[i].CoreWebView2.ExecuteScriptAsync(
                                        @"(function(){
                                            var m = document.querySelectorAll('video,audio');
                                            for(var i=0;i<m.length;i++){
                                                if(!m[i].paused && !m[i].ended && m[i].readyState>2)
                                                    return true;
                                            }
                                            return false;
                                        })()");

                                    if (hayMedia != "true")
                                        _ = _tabs[i].CoreWebView2.TrySuspendAsync();
                                }
                            }
                        }
                    }
                    else
                    {
                        _tabs[i].Visibility = Visibility.Visible;
                        if (_tabs[i].CoreWebView2 != null)
                            _tabs[i].CoreWebView2.Resume();
                    }
                }
                catch { }
            }

            _activeTab = index;
            _ignorarGotFocus = true;
            UrlBar.Text = _tabs[index].Source?.ToString() ?? "";
            _ignorarGotFocus = false;
            ActualizarEstiloTabs();
        }

        private void ActualizarColorBotones()
        {
            ActualizarEstiloTabs();
        }

        private void MoverTab(int from, int to)
        {
            if (from == to) return;
            if (from < 0 || to < 0) return;
            if (from >= _tabs.Count || to >= _tabs.Count) return;

            var tab = _tabs[from];
            _tabs.RemoveAt(from);
            _tabs.Insert(to, tab);

            var btn = _tabButtons[from];
            _tabButtons.RemoveAt(from);
            _tabButtons.Insert(to, btn);

            TabStrip.Children.Remove(btn);
            TabStrip.Children.Insert(to, btn);

            _activeTab = to;
            ActualizarEstiloTabs();
        }

        private void CerrarTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;

            // Guardar URL para poder reabrir
            string urlCerrada = _tabs[index].Source?.ToString() ?? "";
            if (!string.IsNullOrEmpty(urlCerrada) && !urlCerrada.StartsWith("file:///"))
                _tabsRecientes.Add(urlCerrada);
            if (_tabsRecientes.Count > 20)
                _tabsRecientes.RemoveAt(0);

            if (_tabs.Count == 1)
                AbrirNuevaTab();

            var webView = _tabs[index];
            webView.NavigationCompleted -= WebView_NavigationCompleted;
            BrowserContainer.Children.Remove(webView);
            webView.CoreWebView2?.Stop();
            webView.Dispose();

            TabStrip.Children.Remove(_tabButtons[index]);
            _tabs.RemoveAt(index);
            _tabButtons.RemoveAt(index);

            int nuevoIndex = Math.Min(index, _tabs.Count - 1);
            ActivarTab(nuevoIndex);
        }

        private readonly List<string> _tabsRecientes = new();

        private void ReabrirUltimaTab()
        {
            if (_tabsRecientes.Count == 0) return;
            string url = _tabsRecientes[^1];
            _tabsRecientes.RemoveAt(_tabsRecientes.Count - 1);
            AbrirNuevaTab(url);
        }

        public void NavegarUrl(string url)
        {
            Dispatcher.Invoke(() =>
            {
                if (_tabs.Count > 0)
                    _tabs[_activeTab].Source = new Uri(url);
                    // Forzar pérdida de foco con delay para que WebView2 esté listo
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Keyboard.ClearFocus();
                        FocusManager.SetFocusedElement(this, _tabs[_activeTab]);
                        _tabs[_activeTab].Focus();
                    }), System.Windows.Threading.DispatcherPriority.Input);
            });
        }

        public async void InicializarConUrl(string url)
        {
            while (_env == null || _tabs.Count == 0 || _tabs[0].CoreWebView2 == null)
                await Task.Delay(100);
            Dispatcher.Invoke(() => _tabs[0].Source = new Uri(url));
        }

        // FIX 2 — ReindexarTabs eliminado, ya no es necesario

        // ── Navegación ───────────────────────────────────────

        private void WebView_NavigationCompleted(object? sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (sender is not WebView2 webView) return;
            int index = _tabs.IndexOf(webView);
            if (index < 0) return; // ← solo verificar que existe, no si es activa

            Dispatcher.Invoke(() =>
            {
                string url   = webView.Source?.ToString() ?? "";
                string titulo = webView.CoreWebView2?.DocumentTitle ?? "Nueva pestaña";

                // URL bar solo si es la tab activa
                if (index == _activeTab)
                {
                    _ignorarGotFocus = true;
                    UrlBar.Text = url;
                    ActualizarUrlDisplay(url);
                    SugerenciasPopup.IsOpen = false;
                    _ignorarGotFocus = false;
                }

                if (_tabButtons[index].Tag is TextBlock label)
                    label.Text = titulo;

                _historial.Agregar(url, titulo);
                ActualizarFaviconTab(index, url); // ← ahora para todas las tabs
                // Actualizar label si es la tab activa
                if (index == _activeTab)
                    Dispatcher.Invoke(ActualizarZoomLabel);
            });

            ActualizarEstrellaFavorito();
        }

        private async void ActualizarFaviconTab(int index, string url)
        {
            if (index < 0 || index >= _tabButtons.Count) return;
            if (_tabButtons[index].Content is not Grid panel) return;

            var favicon = panel.Children.OfType<Image>()
                .FirstOrDefault(i => i.Tag?.ToString() == "favicon");
            if (favicon == null) return;

            string faviconUrl = "";
            try
            {
                var uri = new Uri(url);
                if (uri.Scheme == "https" || uri.Scheme == "http")
                    faviconUrl = $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32";
            }
            catch { }

            if (string.IsNullOrEmpty(faviconUrl))
            {
                favicon.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(faviconUrl);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (index >= _tabButtons.Count) return;
                    if (_tabButtons[index].Content is not Grid p) return;
                    var f = p.Children.OfType<Image>()
                        .FirstOrDefault(i => i.Tag?.ToString() == "favicon");
                    if (f == null) return;

                    try
                    {
                        var stream = new MemoryStream(bytes);
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = stream;
                        bmp.DecodePixelWidth = 32;
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        f.Source = bmp;
                        f.Visibility = Visibility.Visible;
                    }
                    catch { favicon.Visibility = Visibility.Collapsed; }
                });
            }
            catch
            {
                Dispatcher.Invoke(() => favicon.Visibility = Visibility.Collapsed);
            }
        }

        private void IniciarDeteccionAudioTab(WebView2 webView)
        {
            webView.CoreWebView2.IsDocumentPlayingAudioChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    int i = _tabs.IndexOf(webView);
                    if (i < 0 || i >= _tabButtons.Count) return;
                    if (_tabButtons[i].Content is not Grid panel) return;
                    var indicator = panel.Children.OfType<TextBlock>()
                        .FirstOrDefault(t => t.Tag?.ToString() == "audio");
                    if (indicator != null)
                        indicator.Visibility = webView.CoreWebView2.IsDocumentPlayingAudio
                            ? Visibility.Visible : Visibility.Collapsed;
                });
            };
        }

        private void IniciarBadgesSidebar()
        {
            var timer = new System.Timers.Timer(3000);
            timer.Elapsed += async (s, e) =>
            {
                var tabsCopy = new List<WebView2>();
                Dispatcher.Invoke(() => tabsCopy = _tabs.ToList());

                foreach (var tab in tabsCopy)
                {
                    string url = "";
                    Dispatcher.Invoke(() => url = tab.Source?.ToString() ?? "");

                    if (!url.Contains("discord.com") && !url.Contains("x.com") &&
                        !url.Contains("twitter.com")) continue;

                    try
                    {
                        string badge = await tab.CoreWebView2.ExecuteScriptAsync(
                            url.Contains("discord.com")
                                ? @"(function(){
                                    var b = document.querySelector('[class*=""numberBadge""]');
                                    return b ? b.textContent.trim() : '';
                                })()"
                                : @"(function(){
                                    var b = document.querySelector('[data-testid=""unread-count""]');
                                    return b ? b.textContent.trim() : '';
                                })()");

                        badge = badge.Trim('"');
                        string domain = url.Contains("discord.com") ? "discord.com" : "x.com";
                        Dispatcher.Invoke(() => ActualizarBadgeSidebar(domain, badge));
                    }
                    catch { }
                }
            };
            timer.Start();
        }

        private void ActualizarBadgeSidebar(string domain, string badge)
        {
            foreach (UIElement child in SidebarTop.Children)
            {
                if (child is not Button btn) continue;
                if (btn.ToolTip?.ToString()?.ToLower().Contains(
                    domain.Contains("discord") ? "discord" : "x") != true) continue;

                // Envolver en Grid si aún no lo está
                if (btn.Content is not Grid grid)
                {
                    var icon = btn.Content;
                    grid = new Grid();
                    grid.Children.Add(icon as UIElement ?? new TextBlock());
                    btn.Content = grid;
                }

                var existingBadge = grid.Children.OfType<Border>()
                    .FirstOrDefault(b => b.Tag?.ToString() == "badge");

                if (string.IsNullOrEmpty(badge))
                {
                    if (existingBadge != null) existingBadge.Visibility = Visibility.Collapsed;
                    return;
                }

                if (existingBadge == null)
                {
                    existingBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                        CornerRadius = new CornerRadius(8),
                        MinWidth = 16, Height = 16,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 2, 2, 0),
                        Tag = "badge",
                        Child = new TextBlock
                        {
                            FontSize = 9, FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(3, 0, 3, 0)
                        }
                    };
                    grid.Children.Add(existingBadge);
                }

                ((TextBlock)existingBadge.Child).Text = badge;
                existingBadge.Visibility = Visibility.Visible;
                break;
            }
        }

        private string GetUrlBusqueda(string query)
        {
            return _buscadorActivo switch
            {
                "bing"       => "https://www.bing.com/search?q="       + Uri.EscapeDataString(query),
                "duckduckgo" => "https://duckduckgo.com/?q="           + Uri.EscapeDataString(query),
                "brave"      => "https://search.brave.com/search?q="   + Uri.EscapeDataString(query),
                _            => "https://www.google.com/search?q="     + Uri.EscapeDataString(query),
            };
        }

        private void Navegar(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || _activeTab < 0) return;

            string url;
            if (input.StartsWith("http://") || input.StartsWith("https://") ||
                input.StartsWith("file:///"))
                url = input;
            else if (input.StartsWith("edge://") || input.StartsWith("chrome://") ||
                    input.StartsWith("about:") || input.StartsWith("view-source:"))
                url = input; // protocolos internos del navegador
            else if (EsUrlDirecta(input))
                url = "https://" + input;
            else
                url = GetUrlBusqueda(input);

            _tabs[_activeTab].Source = new Uri(url);
        }

        private bool EsUrlDirecta(string input)
        {
            // Debe tener un punto, sin espacios
            if (input.Contains(" ") || !input.Contains(".")) return false;

            // Extraer el dominio (antes del primer / o ?)
            string dominio = input.Split('/', '?')[0];

            // El TLD debe ser solo letras y tener entre 2 y 6 caracteres
            string[] partes = dominio.Split('.');
            string tld = partes[^1];
            if (tld.Length < 2 || tld.Length > 6 || !tld.All(char.IsLetter)) return false;

            // El dominio principal no puede ser solo números (ej: "3.14" no es URL)
            if (partes.Length >= 2 && partes[^2].All(char.IsDigit)) return false;

            return true;
        }

        // ── Eventos de UI ────────────────────────────────────

        private void BtnNewTab_Click(object sender, RoutedEventArgs e) => AbrirNuevaTab();

        private System.Threading.CancellationTokenSource? _sugCts;

        private void UrlBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SugerenciasPopup.IsOpen = false;
                var texto = UrlBar.Text;
                Navegar(texto);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SugerenciasPopup.IsOpen = false; // doble cierre por si acaso
                    Keyboard.ClearFocus();
                    FocusManager.SetFocusedElement(this, _tabs[_activeTab]);
                    _tabs[_activeTab].Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                SugerenciasPopup.IsOpen = false;
                return;
            }
            if (e.Key == Key.Down && SugerenciasPopup.IsOpen)
            {
                SugerenciasList.Focus();
                if (SugerenciasList.Items.Count > 0)
                    SugerenciasList.SelectedIndex = 0;
                return;
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl  = Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl);
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            bool alt   = Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt);
            string tecla = e.Key.ToString();

            // Ctrl+1 al Ctrl+9 — ir a tab específica (no personalizable)
            if (ctrl && e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                int idx = e.Key - Key.D1;
                if (idx < _tabs.Count) ActivarTab(idx);
                e.Handled = true;
                return;
            }

            if (_atajos.Coincide("nueva_tab",        ctrl, shift, alt, tecla)) { AbrirNuevaTab(); e.Handled = true; }
            else if (_atajos.Coincide("cerrar_tab",  ctrl, shift, alt, tecla)) { CerrarTab(_activeTab); e.Handled = true; }
            else if (_atajos.Coincide("recargar",    ctrl, shift, alt, tecla)) { if (_activeTab >= 0) _tabs[_activeTab].Reload(); e.Handled = true; }
            else if (_atajos.Coincide("recargar_f5", ctrl, shift, alt, tecla)) { if (_activeTab >= 0) _tabs[_activeTab].Reload(); e.Handled = true; }
            else if (_atajos.Coincide("enfocar_url", ctrl, shift, alt, tecla)) { UrlBar.Focus(); UrlBar.SelectAll(); e.Handled = true; }
            else if (_atajos.Coincide("sig_tab",     ctrl, shift, alt, tecla)) { ActivarTab((_activeTab + 1) % _tabs.Count); e.Handled = true; }
            else if (_atajos.Coincide("ant_tab",     ctrl, shift, alt, tecla)) { ActivarTab((_activeTab - 1 + _tabs.Count) % _tabs.Count); e.Handled = true; }
            else if (_atajos.Coincide("reabrir_tab", ctrl, shift, alt, tecla)) { ReabrirUltimaTab(); e.Handled = true; }
            else if (_atajos.Coincide("pantalla_completa", ctrl, shift, alt, tecla))
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                e.Handled = true;
            }
            else if (_atajos.Coincide("zoom_mas", ctrl, shift, alt, tecla))
            {
                if (_activeTab >= 0)
                {
                    _tabs[_activeTab].ZoomFactor = Math.Min(_tabs[_activeTab].ZoomFactor + 0.1, 3.0);
                    ActualizarZoomLabel();
                    string dominio = GetDominioZoom(_tabs[_activeTab].Source?.ToString() ?? "");
                    if (!string.IsNullOrEmpty(dominio)) _zoomPorDominio[dominio] = _tabs[_activeTab].ZoomFactor;
                }
                e.Handled = true;
            }
            else if (_atajos.Coincide("zoom_menos", ctrl, shift, alt, tecla))
            {
                if (_activeTab >= 0)
                {
                    _tabs[_activeTab].ZoomFactor = Math.Max(_tabs[_activeTab].ZoomFactor - 0.1, 0.25);
                    ActualizarZoomLabel();
                    string dominio = GetDominioZoom(_tabs[_activeTab].Source?.ToString() ?? "");
                    if (!string.IsNullOrEmpty(dominio)) _zoomPorDominio[dominio] = _tabs[_activeTab].ZoomFactor;
                }
                e.Handled = true;
            }
            else if (_atajos.Coincide("zoom_reset", ctrl, shift, alt, tecla))
            {
                if (_activeTab >= 0)
                {
                    _tabs[_activeTab].ZoomFactor = 1.0;
                    ActualizarZoomLabel();
                    string dominio = GetDominioZoom(_tabs[_activeTab].Source?.ToString() ?? "");
                    if (!string.IsNullOrEmpty(dominio)) _zoomPorDominio.Remove(dominio);
                }
                e.Handled = true;
            }
            else if (_atajos.Coincide("favoritos",  ctrl, shift, alt, tecla)) { AbrirONavegar(_urlFavoritos); e.Handled = true; }
            else if (_atajos.Coincide("historial",  ctrl, shift, alt, tecla)) { AbrirONavegar(_urlHistorial); e.Handled = true; }
            else if (_atajos.Coincide("descargas",  ctrl, shift, alt, tecla)) { AbrirONavegar(_urlDescargas); e.Handled = true; }
            else if (_atajos.Coincide("nueva_ventana", ctrl, shift, alt, tecla))
            {
                var v = new MainWindow(); v.Show();
                e.Handled = true;
            }
            else if (_atajos.Coincide("captura", ctrl, shift, alt, tecla))
            {
                if (_activeTab >= 0 && _tabs[_activeTab].CoreWebView2 != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var stream = new MemoryStream();
                            await _tabs[_activeTab].CoreWebView2.CapturePreviewAsync(
                                CoreWebView2CapturePreviewImageFormat.Png, stream);
                            var dlg = new Microsoft.Win32.SaveFileDialog
                            {
                                Title = "Guardar captura",
                                FileName = $"captura_{DateTime.Now:yyyyMMdd_HHmmss}",
                                Filter = "PNG|*.png",
                                DefaultExt = ".png"
                            };
                            Dispatcher.Invoke(() =>
                            {
                                if (dlg.ShowDialog() == true)
                                {
                                    stream.Position = 0;
                                    File.WriteAllBytes(dlg.FileName, stream.ToArray());
                                }
                            });
                        }
                        catch { }
                    });
                }
                e.Handled = true;
            }
            else if (_atajos.Coincide("busqueda_rapida", ctrl, shift, alt, tecla))
            {
                PopupBuscadorSidebar.IsOpen = true;
                SidebarBuscadorInput.Focus();
                e.Handled = true;
            }
            else if (_atajos.Coincide("musica_play", ctrl, shift, alt, tecla))
            {
                _ = Task.Run(async () =>
                {
                    var sessions = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                    sessions.GetCurrentSession()?.TryTogglePlayPauseAsync();
                });
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _activeTab >= 0)
            {
                _ignorarGotFocus = true;
                UrlBar.Text = _tabs[_activeTab].Source?.ToString() ?? "";
                _ignorarGotFocus = false;
                SugerenciasPopup.IsOpen = false;
            }
        }

        private void SugerenciasList_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SugerenciasList.SelectedItem is SugerenciaItem sug)
            {
                SugerenciasPopup.IsOpen = false;
                UrlBar.Text = sug.Url;
                Navegar(sug.Url);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Keyboard.ClearFocus();
                    FocusManager.SetFocusedElement(this, _tabs[_activeTab]);
                    _tabs[_activeTab].Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void SugerenciasList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SugerenciasList.SelectedItem is SugerenciaItem sug)
            {
                SugerenciasPopup.IsOpen = false;
                UrlBar.Text = sug.Url;
                Navegar(sug.Url);
            }
            if (e.Key == Key.Escape)
                SugerenciasPopup.IsOpen = false;
        }
        private void UrlBar_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_ignorarGotFocus) return;
            // Ocultar placeholder si estaba visible
            UrlDisplay.Visibility = Visibility.Collapsed;
            UrlBar.Visibility = Visibility.Visible;

            // Si era el placeholder, limpiar para no seleccionarlo
            if (UrlBar.Text == "")
                UrlBar.Text = "";

            _ignorarTextChanged = true;
            UrlBar.SelectAll();
            _ignorarTextChanged = false;

            // Mostrar historial reciente SOLO si el usuario hizo click manualmente
            if (string.IsNullOrWhiteSpace(UrlBar.Text) && _urlBarClickado)
            {
            
                var recientes = _historial.Entradas
                    .Where(h => !string.IsNullOrEmpty(h.Url) &&
                                !h.Url.Contains("google.com/search") &&
                                !h.Url.Contains("bing.com/search"))
                    .Take(6)
                    .ToList();
                if (recientes.Count == 0) return;

                SugerenciasList.Items.Clear();
                foreach (var h in recientes)
                    SugerenciasList.Items.Add(new SugerenciaItem
                {
                    Icono = "🕐",
                    Titulo = h.Titulo ?? h.Url,
                    Subtitulo = h.Url,
                    Url = h.Url,
                    FaviconUrl = $"https://www.google.com/s2/favicons?domain={new Uri(h.Url).Host}&sz=32"
                });

            SugerenciasPopup.PlacementTarget = UrlBarBorder;
            SugerenciasPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            SugerenciasPopup.Width = UrlBarBorder.ActualWidth;
            SugerenciasPopup.IsOpen = true;
            }
            _urlBarClickado = false;
        }

        private void UrlBar_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SugerenciasPopup.IsOpen = false;
                ActualizarUrlDisplay(UrlBar.Text);
            }), System.Windows.Threading.DispatcherPriority.Background);
            _urlBarClickado = false;
        }

        private async void UrlBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignorarTextChanged) return;
            if (_ignorarGotFocus) return;
            var texto = UrlBar.Text.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                // Solo cerrar si no hay recientes visibles
                if (SugerenciasList.Items.Count == 0)
                    SugerenciasPopup.IsOpen = false;
                return;
            }

            _sugCts?.Cancel();
            _sugCts = new System.Threading.CancellationTokenSource();
            var token = _sugCts.Token;

            try
            {
                await Task.Delay(200, token);
                if (token.IsCancellationRequested) return;

                // Sugerencias del historial que coincidan
                var delHistorial = _historial.Entradas
                    .Where(h => !string.IsNullOrEmpty(h.Url) &&
                                !h.Url.Contains("google.com/search") &&
                                !h.Url.Contains("bing.com/search") &&
                                ((h.Titulo ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase)
                                || (h.Url ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase)))
                    .Take(3)
                    .ToList();

                // Sugerencias de Google (solo si no es una URL)
                var delBuscador = new List<string>();
                if (!texto.StartsWith("http"))
                {
                    try
                    {
                        var url = $"https://suggestqueries.google.com/complete/search?client=firefox&q={Uri.EscapeDataString(texto)}";
                        var res = await _httpClient.GetStringAsync(url);
                        if (token.IsCancellationRequested) return;

                        using var doc = System.Text.Json.JsonDocument.Parse(res);
                        delBuscador = doc.RootElement[1].EnumerateArray()
                            .Take(6)
                            .Select(s => s.GetString() ?? "")
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList();
                    }
                    catch { }
                }

                if (token.IsCancellationRequested) return;

                Dispatcher.Invoke(() =>
                {
                    SugerenciasList.Items.Clear();

                    foreach (var h in delHistorial)
                        SugerenciasList.Items.Add(new SugerenciaItem
                        {
                            Icono = "🕐",
                            Titulo = h.Titulo ?? h.Url,
                            Subtitulo = h.Url,
                            Url = h.Url,
                            FaviconUrl = $"https://www.google.com/s2/favicons?domain={new Uri(h.Url).Host}&sz=32"
                        });

                    foreach (var sug in delBuscador)
                        SugerenciasList.Items.Add(new SugerenciaItem
                        {
                            Icono = "🔍",
                            Titulo = sug,
                            Subtitulo = "",
                            Url = sug,
                            FaviconUrl = ""
                        });

                    if (SugerenciasList.Items.Count > 0)
                    {
                        SugerenciasPopup.PlacementTarget = UrlBarBorder;
                        SugerenciasPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                        SugerenciasPopup.Width = UrlBarBorder.ActualWidth;
                        SugerenciasPopup.IsOpen = true;
                    }
                    else
                    {
                        SugerenciasPopup.IsOpen = false;
                    }
                });
            }
            catch (TaskCanceledException) { }
            catch { }
        }

        private void BtnGo_Click(object sender, RoutedEventArgs e) => Navegar(UrlBar.Text);

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab >= 0 && _tabs[_activeTab].CanGoBack)
                _tabs[_activeTab].GoBack();
        }

        private void BtnForward_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab >= 0 && _tabs[_activeTab].CanGoForward)
                _tabs[_activeTab].GoForward();
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab >= 0)
                _tabs[_activeTab].Reload();
        }

        // ── Favoritos ────────────────────────────────────
        private void BtnFavorito_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab < 0) return;
            string url = _tabs[_activeTab].Source?.ToString() ?? "";
            string titulo = (_tabButtons[_activeTab].Tag as TextBlock)?.Text ?? url;

            if (_favoritos.EsFavorito(url))
                _favoritos.Quitar(url);
            else
                _favoritos.Agregar(url, titulo);

            ActualizarEstrellaFavorito();
        }

        private void BtnVerFavoritos_Click(object sender, RoutedEventArgs e)
        {
            PopupFavoritos.IsOpen = false;
            if (_activeTab >= 0) _tabs[_activeTab].Source = new Uri(_urlFavoritos);
        }

        private void ActualizarEstrellaFavorito()
        {
            if (_activeTab < 0) return;
            string url = _tabs[_activeTab].Source?.ToString() ?? "";
            if (_favoritos.EsFavorito(url))
            {
                BtnFavorito.Content = "★";
                BtnFavorito.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 0));
            }
            else
            {
                BtnFavorito.Content = "☆";
                BtnFavorito.Foreground = Brushes.White;
            }
        }

        private void ActualizarPopupFavoritos()
        {
            ListaFavoritosPopup.Children.Clear();

            if (_favoritos.Entradas.Count == 0)
            {
                ListaFavoritosPopup.Children.Add(new TextBlock
                {
                    Text = "No hay favoritos aún.",
                    Foreground = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                    FontSize = 12,
                    Margin = new Thickness(8, 12, 8, 12),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            foreach (var fav in _favoritos.Entradas)
            {
                var btn = new Button
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 6, 8, 6)
                };

                var panel = new StackPanel();
                panel.Children.Add(new TextBlock
                {
                    Text = fav.Titulo,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 240
                });
                panel.Children.Add(new TextBlock
                {
                    Text = fav.Url,
                    Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                    FontSize = 10,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 240
                });

                btn.Content = panel;
                string favUrl = fav.Url;
                btn.Click += (s, e) =>
                {
                    PopupFavoritos.IsOpen = false;
                    if (Uri.TryCreate(favUrl, UriKind.Absolute, out var uri))
                        _tabs[_activeTab].Source = uri;
                };

                ListaFavoritosPopup.Children.Add(btn);
            }
        }

        private void SbFavoritos_Click(object sender, RoutedEventArgs e)
        {
            ActualizarPopupFavoritos();
            PopupFavoritos.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            PopupFavoritos.IsOpen = true;
        }

        private void BtnAjustes_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab >= 0) _tabs[_activeTab].Source = new Uri(_urlAjustes);
        }

        private void AbrirONavegar(string url)
        {
            string destino = url switch
            {
                "nuevatab"  => _urlNuevaTab,
                "historial" => _urlHistorial,
                "favoritos" => _urlFavoritos,
                "ajustes"   => _urlAjustes,
                "descargas" => _urlDescargas, 
                "extensiones" => _urlExtensiones,
                "perfiles"   => _urlPerfiles, 
                _           => url
            };

            if (_activeTab >= 0)
                _tabs[_activeTab].Source = new Uri(destino);
            else
                AbrirNuevaTab(destino);
        }

        private void ActualizarWidgetsSidebar(DatosRendimiento datos)
        {
            if (_sbCpuVal  != null) _sbCpuVal.Text  = $"{datos.Cpu}%";
            if (_sbRamVal  != null) _sbRamVal.Text  = $"{datos.Ram}%";
            if (_sbDiscoVal != null) _sbDiscoVal.Text = $"{datos.Disco}%";
            if (_sbRedVal  != null) _sbRedVal.Text  = $"{datos.Red}KB/s";

            Color ColorBarra(int val) => val > 85
                ? Color.FromRgb(239, 68, 68)
                : val > 60
                    ? Color.FromRgb(249, 115, 22)
                    : Color.FromRgb(124, 58, 237);

            if (_sbCpuBar  != null) _sbCpuBar.Width  = Math.Max(1, datos.Cpu  * 28 / 100);
            if (_sbRamBar  != null) _sbRamBar.Width  = Math.Max(1, datos.Ram  * 28 / 100);
            if (_sbDiscoBar != null) _sbDiscoBar.Width = Math.Max(1, datos.Disco * 28 / 100);

            if (_sbCpuBar  != null) _sbCpuBar.Fill  = new SolidColorBrush(ColorBarra(datos.Cpu));
            if (_sbRamBar  != null) _sbRamBar.Fill  = new SolidColorBrush(ColorBarra(datos.Ram));
            if (_sbDiscoBar != null) _sbDiscoBar.Fill = new SolidColorBrush(ColorBarra(datos.Disco));
        }

        private void RenderizarSidebar()
        {
            SidebarTop.Children.Clear();
            SidebarBottom.Children.Clear();
            var iconColor = Color.FromArgb(180, _accentColor.R, _accentColor.G, _accentColor.B);
            var iconBrush = new SolidColorBrush(iconColor);

            foreach (var item in _sidebar.Items)
            {
                if (!item.Visible) continue;

                if (item.Separador)
                {
                    var sep = new Separator
                    {
                        Margin = new Thickness(12, 6, 12, 6),
                        Background = new SolidColorBrush(Color.FromRgb(26, 16, 48))
                    };
                    SidebarTop.Children.Add(sep);
                    continue;
                }

                var btn = new Button
                {
                    Width = 52, Height = 42,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = item.Nombre,
                };

                // Decidir contenido del botón
                if (!string.IsNullOrEmpty(item.Url) && item.Url.StartsWith("http"))
                {
                    // Favicon con fallback a emoji
                    var img = new Image
                    {
                        Width = 20, Height = 20,
                        Opacity = 0.6
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

                    try
                    {
                        var uri = new Uri(item.Url);
                        var faviconUrl = $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32";
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(faviconUrl);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();

                        // Fallback a emoji si falla la imagen
                        img.Source = bmp;
                        bmp.DownloadFailed += (s2, e2) =>
                        {
                            Dispatcher.Invoke(() => btn.Content = new TextBlock
                            {
                                Text = string.IsNullOrEmpty(item.Emoji) ? "🌐" : item.Emoji,
                                FontSize = 18,
                                Foreground = iconBrush
                            });
                        };
                    }
                    catch
                    {
                        btn.Content = new TextBlock
                        {
                            Text = string.IsNullOrEmpty(item.Emoji) ? "🌐" : item.Emoji,
                            FontSize = 18,
                            Foreground = iconBrush
                        };
                        goto skipImage;
                    }

                    btn.Content = img;
                    skipImage:;
                }
                else if (item.Id is "home" or "favoritos" or "historial" or "descargas" or "ajustes" or "perfiles" or "extensiones" or "atajos" or "privacidad" or "rendimiento" or "nuevatab")
                {
                    // SVG para items de sistema
                    btn.Content = CrearIconoSistema(item.Id, iconBrush);
                }
                else
                {
                    // Emoji fallback
                    btn.Content = new TextBlock
                    {
                        Text = string.IsNullOrEmpty(item.Emoji) ? "🌐" : item.Emoji,
                        FontSize = 18,
                        Foreground = iconBrush
                    };
                }

                var capturedUrl = item.Url;
                btn.Click += (s, e) =>
                {
                    if (capturedUrl == "nuevatab")
                        AbrirONavegar(_urlNuevaTab);
                    else if (capturedUrl == "historial")
                        AbrirONavegar(_urlHistorial);
                    else if (capturedUrl == "favoritos")
                    {
                        ActualizarPopupFavoritos();
                        PopupFavoritos.PlacementTarget = btn;
                        PopupFavoritos.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                        PopupFavoritos.IsOpen = true;
                    }
                    else if (capturedUrl == "descargas")
                        AbrirONavegar(_urlDescargas);
                    else if (!string.IsNullOrEmpty(capturedUrl))
                        AbrirONavegar(capturedUrl);
                };

                if (item.Id == "ajustes")
                    SidebarBottom.Children.Add(btn);
                else
                    SidebarTop.Children.Add(btn);
            }

            // ── Botón AtsukiMusic ──
            var btnMusica = new Button
            {
                Width = 52, Height = 42,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "AtsukiMusic",
                Content = new TextBlock
                {
                    Text = "🎵",
                    FontSize = 18,
                    Foreground = iconBrush
                }
            };
            btnMusica.Click += async (s, e) =>
            {
                await InicializarMusicaWebView();
                _musicaPanelAbierto = !_musicaPanelAbierto;
                MusicaPanel.Visibility = _musicaPanelAbierto
                    ? Visibility.Visible : Visibility.Collapsed;
                SidebarColumn.Width = new GridLength(_musicaPanelAbierto ? 332 : 52);
            };
            SidebarTop.Children.Add(btnMusica);
            // Widget de rendimiento al final
            if (_sbWidgetRendimiento || _sbWidgetReloj)
            {
                var sep = new Separator
                {
                    Margin = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(Color.FromRgb(26, 16, 48))
                };
                SidebarBottom.Children.Add(sep);
            }

            if (_sbWidgetRendimiento)
                SidebarBottom.Children.Add(CrearWidgetRendimientoSidebar());

            if (_sbWidgetReloj)
                SidebarBottom.Children.Add(CrearWidgetRelojSidebar());

            if (_sbWidgetCapturas)
            {

                // Botón de captura de pantalla
                var btnCaptura = new Button
                {
                    Width = 52, Height = 42,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Captura de pantalla",
                    Content = CrearIconoSistema("captura", iconBrush)
                };
                btnCaptura.Click += async (s, e) =>
                {
                    if (_activeTab < 0 || _tabs[_activeTab].CoreWebView2 == null) return;

                    try
                    {
                        // Capturar el WebView como imagen
                        using var stream = new MemoryStream();
                        await _tabs[_activeTab].CoreWebView2.CapturePreviewAsync(
                            CoreWebView2CapturePreviewImageFormat.Png, stream);

                        var dlg = new Microsoft.Win32.SaveFileDialog
                        {
                            Title      = "Guardar captura",
                            FileName   = $"captura_{DateTime.Now:yyyyMMdd_HHmmss}",
                            Filter     = "PNG|*.png",
                            DefaultExt = ".png"
                        };

                        if (dlg.ShowDialog() == true)
                        {
                            stream.Position = 0;
                            File.WriteAllBytes(dlg.FileName, stream.ToArray());

                            // Abrir en el explorador resaltando el archivo
                            System.Diagnostics.Process.Start("explorer.exe",
                                $"/select,\"{dlg.FileName}\"");
                        }
                    }
                    catch { }
                };
                SidebarBottom.Children.Add(btnCaptura);
            }

            // Botón buscador rápido
            if (_sbWidgetBusqueda)
            {
                var btnBuscar = new Button
                {
                    Width = 52, Height = 42,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Búsqueda rápida",
                    Content = CrearIconoSistema("buscador", iconBrush)
                };
                btnBuscar.Click += (s, e) =>
                {
                    PopupBuscadorSidebar.PlacementTarget = btnBuscar;
                    PopupBuscadorSidebar.IsOpen = true;
                    SidebarBuscadorInput.Text = "";
                    SidebarBuscadorInput.Focus();
                };
                SidebarBottom.Children.Add(btnBuscar);
            }
        }
            // Extensiones sidebar — manejadas via SincronizarExtensionesSidebar()
        private void SidebarBuscador_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                EjecutarBusquedaSidebar();
            else if (e.Key == Key.Escape)
                PopupBuscadorSidebar.IsOpen = false;
        }

        private void SidebarBuscador_Click(object sender, RoutedEventArgs e)
            => EjecutarBusquedaSidebar();

        private void EjecutarBusquedaSidebar()
        {
            string texto = SidebarBuscadorInput.Text.Trim();
            if (string.IsNullOrEmpty(texto)) return;

            PopupBuscadorSidebar.IsOpen = false;
            Navegar(texto);
        }

        private void SincronizarExtensionesSidebar()
        {
            var activos = _extensiones.GetWidgetsSidebarActivos()
                .Where(x => x.ext.Widget!.TipoSidebar == "item")
                .Select(x => x.ext)
                .ToList();

            // IDs activos actualmente
            var idsActivos = activos.Select(e => "ext:" + e.Id).ToHashSet();

            // Quitar extensiones que ya no están activas
            _sidebar.Items.RemoveAll(i => i.Id.StartsWith("ext:") && !idsActivos.Contains(i.Id));

            // Agregar solo las que no existen aún (al final, sin tocar el orden de las ya existentes)
            foreach (var ext in activos)
            {
                string extId = "ext:" + ext.Id;
                if (_sidebar.Items.Any(i => i.Id == extId)) continue;

                _sidebar.Items.Add(new SidebarItem
                {
                    Id      = extId,
                    Emoji   = ext.Widget!.Emoji,
                    Nombre  = ext.Nombre,
                    Url     = ext.Widget.Url,
                    Visible = true
                });
            }

            // Guardar el orden actual para persistirlo
            _sidebar.Guardar();
            RenderizarSidebar();
        }

        private void AplicarColorSVG(string colorHex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
            var brushMuted = new SolidColorBrush(Color.FromArgb(180,
                brush.Color.R, brush.Color.G, brush.Color.B));

            // Navbar
            SetPathStroke(BtnBack,     brushMuted);
            SetPathStroke(BtnForward,  brushMuted);
            SetPathStroke(BtnReload,   brushMuted);
            SetPathStroke(BtnFavorito, brushMuted);
            SetPathStroke(BtnAjustes,  brushMuted);
            SetPathStroke(BtnMenu,     brushMuted);

            // Sidebar completo (Top + Bottom)
            foreach (var panel in new[] { SidebarTop.Children, SidebarBottom.Children })
            {
                foreach (UIElement child in panel)
                {
                    if (child is Button b)
                        SetPathStroke(b, brushMuted);
                }
            }
        }

        private void SetPathStroke(Button btn, Brush brush)
        {
            if (btn?.Content is Viewbox vb)
                ApplyStrokeToChildren(vb, brush);
        }

        private void ApplyStrokeToChildren(DependencyObject parent, Brush brush)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Shapes.Path path)
                    path.Stroke = brush;
                else
                    ApplyStrokeToChildren(child, brush);
            }
        }
        private void AplicarTemaUI(Tema t)
        {
            Dispatcher.Invoke(() =>
            {
                var accent   = (Color)ColorConverter.ConvertFromString(t.Accent);
                _accentColor = accent;
                var bg       = (Color)ColorConverter.ConvertFromString(t.Bg);
                var surface  = (Color)ColorConverter.ConvertFromString(t.Surface);
                var surface2 = (Color)ColorConverter.ConvertFromString(t.Surface2);

                var borderColor = Color.FromArgb(60, accent.R, accent.G, accent.B);

                Background = new SolidColorBrush(bg);

                TabBar.Background        = new SolidColorBrush(Color.FromArgb(255, 10, 10, 16));
                TabBarBorder.BorderBrush = new SolidColorBrush(borderColor);

                NavBar.Background = new SolidColorBrush(surface);

                UrlBarBorder.Background  = new SolidColorBrush(surface2);
                UrlBarBorder.BorderBrush = new SolidColorBrush(borderColor);
                UrlAccentBar.Background  = new SolidColorBrush(accent);
                UrlBar.CaretBrush        = new SolidColorBrush(accent);
                BtnGo.Foreground         = new SolidColorBrush(accent);

                Sidebar.Background          = new SolidColorBrush(Color.FromArgb(255, 10, 10, 16));
                Sidebar.BorderBrush         = new SolidColorBrush(borderColor);

                // Texto del sidebar
                var mutedColor = Color.FromArgb(255, 170, 170, 200);
                var accentBrush = new SolidColorBrush(accent);
                var mutedBrush = new SolidColorBrush(mutedColor);

                // Botones minimizar/maximizar/cerrar
                BtnMinimize.Foreground = mutedBrush;
                BtnMaximize.Foreground = mutedBrush;
                BtnClose.Foreground    = mutedBrush;

                // Paths de botones de navegación
                var navStroke = new SolidColorBrush(Color.FromArgb(255,
                    (byte)(accent.R / 2), (byte)(accent.G / 2), (byte)(accent.B / 2 + 80)));

                foreach (var btn in new[] { BtnBack, BtnForward, BtnReload })
                {
                    foreach (var path in FindVisualChildren<System.Windows.Shapes.Path>(btn))
                        path.Stroke = mutedBrush;
                }

                // Texto de la tabbar y sidebar
                TabBar.Background = new SolidColorBrush(bg);

                // Fondo activo del sidebar usando el accent
                foreach (var btn in _tabButtons)
                    btn.Foreground = mutedBrush;
            });
            RenderizarSidebar();
            AplicarColorSVG(t.Accent);
        }

        private void PropagaTema()
        {
            Dispatcher.Invoke(() =>
            {
                string msg = "tema:" + _temas.ToJson();
                foreach (var tab in _tabs)
                    tab.CoreWebView2?.PostWebMessageAsString(msg);
            });
        }

        private void TabBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Solo arrastra si el clic fue directo en el TabBar o el Border, no en un botón de pestaña
            if (e.Source is Button) return;

            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, e);
            }
            else
            {
                this.DragMove();
            }
        }
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                BtnMaximize.Content = "□";
            }
            else
            {
                WindowState = WindowState.Maximized;
                BtnMaximize.Content = "❐";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();

        protected override void OnClosed(EventArgs e)
        {
            // Detener música inmediatamente
            try
            {
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:pause");
                _musicaWebView?.CoreWebView2?.Stop();
                _musicaWebView?.Dispose();
            }
            catch { }

            foreach (var tab in _tabs)
            {
                try
                {
                    tab.CoreWebView2?.Stop();
                    tab.Dispose();
                }
                catch { }
            }
            _tabs.Clear();
            _tabButtons.Clear();
            _rendimiento.Dispose();
            base.OnClosed(e);
        }
        // ── Zoom ────────────────────────────────────────────
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab < 0) return;
           _aplicandoZoom = true;
            _tabs[_activeTab].ZoomFactor = Math.Min(_tabs[_activeTab].ZoomFactor + 0.1, 3.0);
            _aplicandoZoom = false;
            string dominio = GetDominioZoom(_tabs[_activeTab].Source?.ToString() ?? "");
            if (!string.IsNullOrEmpty(dominio)) _zoomPorDominio[dominio] = _tabs[_activeTab].ZoomFactor;
            ActualizarZoomLabel();
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab < 0) return;
            _aplicandoZoom = true;
            _tabs[_activeTab].ZoomFactor = Math.Max(_tabs[_activeTab].ZoomFactor - 0.1, 0.25);
            _aplicandoZoom = false;
            string dominio = GetDominioZoom(_tabs[_activeTab].Source?.ToString() ?? "");
            if (!string.IsNullOrEmpty(dominio)) _zoomPorDominio[dominio] = _tabs[_activeTab].ZoomFactor;
            ActualizarZoomLabel();
        }

        private void ActualizarZoomLabel()
        {
            if (_activeTab < 0) return;
            int pct = (int)Math.Round(_tabs[_activeTab].ZoomFactor * 100);
            ZoomLabel.Text = pct + "%";
        }

        private void ZoomLabel_GotFocus(object sender, RoutedEventArgs e)
        {
            // Quitar el % al enfocar para facilitar edición
            ZoomLabel.Text = ZoomLabel.Text.Replace("%", "");
            ZoomLabel.SelectAll();
        }

        private void ZoomLabel_LostFocus(object sender, RoutedEventArgs e)
        {
            AplicarZoomDesdeLabel();
        }

        private void ZoomLabel_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                AplicarZoomDesdeLabel();
                // Quitar foco del textbox
                System.Windows.Input.Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                ActualizarZoomLabel(); // restaurar valor actual
                System.Windows.Input.Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void AplicarZoomDesdeLabel()
        {
            if (_activeTab < 0) return;
            string raw = ZoomLabel.Text.Replace("%", "").Trim();
            if (int.TryParse(raw, out int pct))
            {
                double factor = Math.Max(0.25, Math.Min(3.0, pct / 100.0));
                _aplicandoZoom = true;
                _tabs[_activeTab].ZoomFactor = factor;
                _aplicandoZoom = false;
                string dominio = GetDominioZoom(_tabs[_activeTab].Source?.ToString() ?? "");
                if (!string.IsNullOrEmpty(dominio)) _zoomPorDominio[dominio] = factor;
            }
            ActualizarZoomLabel();
        }

        private string GetDominioZoom(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                var uri = new Uri(url);
                if (uri.Scheme == "file") return url; // usar URL completa para páginas locales
                return uri.Host;
            }
            catch { return url; }
        }

        private void MenuPerfil_Click(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = false;
            if (_activeTab >= 0)
                _tabs[_activeTab].Source = new Uri(_urlPerfiles);
        }

        private void MenuGestionarPerfiles_Click(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = false;
            if (_activeTab >= 0)
                _tabs[_activeTab].Source = new Uri(_urlPerfiles);
        }

        private void MenuInvitado_Click(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = false;
            CambiarPerfil("invitado");
        }

        // ── Menú tres puntos ────────────────────────────────
        private void BtnMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuPerfilEmoji.Text   = _perfiles.Activo.Emoji;
            MenuPerfilNombre.Text = _perfilActivo.Nombre;
            PopupMenu.IsOpen = true;
        }

        private void MenuHistorial_Click(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = false;
            if (_activeTab >= 0) _tabs[_activeTab].Source = new Uri(_urlHistorial);
        }
        private string _dialPath => Path.Combine(_carpetaPerfil, "dials.json");

        private void GuardarDials()
        {
            File.WriteAllText(_dialPath, System.Text.Json.JsonSerializer.Serialize(_dials));
        }

        private void CargarDials()
        {
            if (File.Exists(_dialPath))
            {
                try
                {
                    _dials = System.Text.Json.JsonSerializer.Deserialize<List<DialEntry>>(
                        File.ReadAllText(_dialPath), _jsonOpts) ?? new();
                }
                catch { _dials = new(); }
            }
        }

        private ControlTemplate CrearTemplateTab()
        {
            var template = new ControlTemplate(typeof(Button));

            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6, 6, 0, 0));
            border.SetValue(Border.PaddingProperty, new Thickness(12, 0, 8, 0));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));

            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);

            template.VisualTree = border;
            return template;
        }

        private UIElement CrearIconoSistema(string id, SolidColorBrush iconBrush = null)
        {
            var color = iconBrush ?? new SolidColorBrush(Color.FromRgb(68, 68, 102));

            // IDs que requieren múltiples paths (Canvas)
            if (id == "historial")
            {
                var canvas = new Canvas { Width = 24, Height = 24 };
                string[] paths = new[]
                {
                    "M12 8v4l3 3",
                    "M3.05 11a9 9 0 1 0 .5-4.5",
                    "M3 3v4h4"
                };
                foreach (var d in paths)
                {
                    canvas.Children.Add(new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse(d),
                        Stroke = color,
                        StrokeThickness = 1.5,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round,
                        Fill = Brushes.Transparent
                    });
                }
                return new Viewbox { Width = 18, Height = 18, Child = canvas };
            }

            if (id == "extensiones")
            {
                // Icono puzzle (extensiones)
                var canvas = new Canvas { Width = 24, Height = 24 };
                string[] paths = new[]
                {
                    "M20.24 12.24a6 6 0 0 0-8.49-8.49L5 10.5V19h8.5z",
                    "M16 8 2 22",
                    "M17.5 15H9"
                };
                foreach (var d in paths)
                {
                    canvas.Children.Add(new System.Windows.Shapes.Path
                    {
                        Data = Geometry.Parse(d),
                        Stroke = color,
                        StrokeThickness = 1.5,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round,
                        Fill = Brushes.Transparent
                    });
                }
                return new Viewbox { Width = 18, Height = 18, Child = canvas };
            }

            string data = id switch
            {
                "home"      => "M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M9 22V12h6v10",
                "favoritos" => "M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z",
                "descargas" => "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3",
                "ajustes"   => "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z",
                "perfiles"  => "M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2 M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8z",
                "captura"   => "M23 19a2 2 0 0 1-2 2H3a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4l2-3h6l2 3h4a2 2 0 0 1 2 2z M12 17a4 4 0 1 0 0-8 4 4 0 0 0 0 8z",
                "buscador"  => "M11 17a6 6 0 1 0 0-12 6 6 0 0 0 0 12z M21 21l-4.35-4.35",
                "atajos"      => "M18 3a3 3 0 0 0-3 3v12a3 3 0 0 0 3 3 3 3 0 0 0 3-3 3 3 0 0 0-3-3H6a3 3 0 0 0-3 3 3 3 0 0 0 3 3 3 3 0 0 0 3-3V6a3 3 0 0 0-3-3 3 3 0 0 0-3 3",
                "privacidad"  => "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z",
                "rendimiento" => "M22 12h-4l-3 9L9 3l-3 9H2",
                "nuevatab"    => "M12 5v14 M5 12h14",
                _           => ""
            };

            if (string.IsNullOrEmpty(data))
                return new TextBlock { Text = "•", FontSize = 18, Foreground = color };

            return new Viewbox
            {
                Width = 18, Height = 18,
                Child = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse(data),
                    Stroke = color,
                    StrokeThickness = 1.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round,
                    Fill = Brushes.Transparent
                }
            };
        }

        private async void InicializarMusica()
        {
            try
            {
                _smtc = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                _smtc.CurrentSessionChanged += (s, e) => ActualizarMusica();
                ActualizarMusica();
            }
            catch { }
        }

        private GlobalSystemMediaTransportControlsSession? _sessionActual;

        private async void ActualizarMusica()
        {
            try
            {
                var session = _smtc?.GetCurrentSession();

                // Si cambió la sesión, re-suscribirse solo una vez
                if (session?.SourceAppUserModelId != _sessionActual?.SourceAppUserModelId)
                {
                    _sessionActual = session;
                    _musicaUltimoTitulo = "";
                    _musicaImagenCache  = "";
                    _musicaFuenteCache  = "";

                    if (session != null)
                    {
                        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                        session.PlaybackInfoChanged    -= OnPlaybackInfoChanged;
                        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
                        session.PlaybackInfoChanged    += OnPlaybackInfoChanged;
                    }
                }

                if (session == null)
                {
                    EnviarMusica("", "", "", false, "");
                    return;
                }

                var info     = await session.TryGetMediaPropertiesAsync();
                var playback = session.GetPlaybackInfo();
                bool playing = playback.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                string titulo  = info.Title  ?? "";
                string artista = info.Artist ?? "";

                // Solo re-descargar imagen si cambió la canción
                if (titulo != _musicaUltimoTitulo)
                {
                    _musicaUltimoTitulo = titulo;
                    _musicaImagenCache  = "";
                    try
                    {
                        var thumb = info.Thumbnail;
                        if (thumb != null)
                        {
                            var stream = await thumb.OpenReadAsync();
                            using var reader = new DataReader(stream);
                            await reader.LoadAsync((uint)stream.Size);
                            var bytes = new byte[stream.Size];
                            reader.ReadBytes(bytes);
                            _musicaImagenCache = "data:image/png;base64," + Convert.ToBase64String(bytes);
                        }
                    }
                    catch { }

                    string fuente = session.SourceAppUserModelId ?? "";
                    if (fuente.Contains("spotify",  StringComparison.OrdinalIgnoreCase)) fuente = "Spotify";
                    else if (fuente.Contains("youtube", StringComparison.OrdinalIgnoreCase)) fuente = "YouTube";
                    else if (fuente.Contains("chrome",  StringComparison.OrdinalIgnoreCase)) fuente = "Chrome";
                    else if (fuente.Contains("firefox", StringComparison.OrdinalIgnoreCase)) fuente = "Firefox";
                    else if (fuente.Length > 20) fuente = "";
                    _musicaFuenteCache = fuente;
                }

                EnviarMusica(titulo, artista, _musicaImagenCache, playing, _musicaFuenteCache);
            }
            catch { }
        }

        private void OnMediaPropertiesChanged(
            GlobalSystemMediaTransportControlsSession s,
            MediaPropertiesChangedEventArgs e) => ActualizarMusica();

        private void OnPlaybackInfoChanged(
            GlobalSystemMediaTransportControlsSession s,
            PlaybackInfoChangedEventArgs e) => ActualizarMusica();

        private void EnviarMusica(string titulo, string artista, string imagen, bool playing, string fuente = "")
        {
            Dispatcher.Invoke(() =>
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    titulo, artista, imagen, playing, fuente
                });
                foreach (var tab in _tabs)
                    tab.CoreWebView2?.PostWebMessageAsString("musica:" + json);
            });
        }

        private UIElement CrearWidgetRendimientoSidebar()
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(6, 8, 6, 8),
                Width = 40
            };

            // Título
            panel.Children.Add(new TextBlock
            {
                Text = "PERF",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            });

            // Función para crear una fila de métrica
            UIElement CrearFila(string label, out TextBlock valBlock, out System.Windows.Shapes.Rectangle bar)
            {
                var filaPanel = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };

                var headerRow = new Grid();
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var labelBlock = new TextBlock
                {
                    Text = label,
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255))
                };
                Grid.SetColumn(labelBlock, 0);

                valBlock = new TextBlock
                {
                    Text = "0%",
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(valBlock, 1);

                headerRow.Children.Add(labelBlock);
                headerRow.Children.Add(valBlock);
                filaPanel.Children.Add(headerRow);

                // Barra de progreso
                var barBg = new Border
                {
                    Height = 3,
                    Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 2, 0, 0)
                };

                bar = new System.Windows.Shapes.Rectangle
                {
                    Height = 3,
                    Fill = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                    RadiusX = 2, RadiusY = 2,
                    Width = 0,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                var barCanvas = new Grid();
                barCanvas.Children.Add(barBg);
                barCanvas.Children.Add(bar);
                filaPanel.Children.Add(barCanvas);

                return filaPanel;
            }

            panel.Children.Add(CrearFila("CPU", out _sbCpuVal,   out _sbCpuBar));
            panel.Children.Add(CrearFila("RAM", out _sbRamVal,   out _sbRamBar));
            panel.Children.Add(CrearFila("DSK", out _sbDiscoVal, out _sbDiscoBar));

            // Red sin barra (valor variable en KB/s)
            var redPanel = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };
            var redHeader = new Grid();
            redHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            redHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var redLabel = new TextBlock
            {
                Text = "NET",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255))
            };
            Grid.SetColumn(redLabel, 0);

            _sbRedVal = new TextBlock
            {
                Text = "0KB/s",
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_sbRedVal, 1);

            redHeader.Children.Add(redLabel);
            redHeader.Children.Add(_sbRedVal);
            redPanel.Children.Add(redHeader);
            panel.Children.Add(redPanel);

            return panel;
        }

        private UIElement CrearWidgetRelojSidebar()
        {
            // Cargar preferencia guardada
            string relojFmtPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsukiBrowser", "reloj_formato.txt");
            bool formato12h = File.Exists(relojFmtPath) && File.ReadAllText(relojFmtPath).Trim() == "12h";

            var panel = new StackPanel
            {
                Margin = new Thickness(6, 4, 6, 8),
                Width = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Clic para cambiar formato"
            };

            var horaBlock = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("Segoe UI"),
            };

            var ampmBlock = new TextBlock
            {
                FontSize = 7,
                Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontFamily = new FontFamily("Segoe UI"),
                Visibility = formato12h ? Visibility.Visible : Visibility.Collapsed
            };

            var fechaBlock = new TextBlock
            {
                FontSize = 8,
                Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Segoe UI"),
            };

            void Actualizar()
            {
                var now = DateTime.Now;
                if (formato12h)
                {
                    horaBlock.Text = now.ToString("h:mm");
                    ampmBlock.Text = now.ToString("tt");
                }
                else
                {
                    horaBlock.Text = now.ToString("HH:mm");
                    ampmBlock.Text = "";
                }
                fechaBlock.Text = now.ToString("dd/MM");
            }

            // Alternar formato al hacer clic
            panel.MouseLeftButtonUp += (s, e) =>
            {
                formato12h = !formato12h;
                ampmBlock.Visibility = formato12h ? Visibility.Visible : Visibility.Collapsed;
                File.WriteAllText(relojFmtPath, formato12h ? "12h" : "24h");
                Actualizar();
            };

            Actualizar();

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Tick += (s, e) =>
            {
                Actualizar();
                timer.Interval = TimeSpan.FromSeconds(60 - DateTime.Now.Second);
            };
            timer.Interval = TimeSpan.FromSeconds(60 - DateTime.Now.Second);
            timer.Start();

            panel.Children.Add(new Separator
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(horaBlock);
            panel.Children.Add(ampmBlock);
            panel.Children.Add(fechaBlock);

            return panel;
        }

        // ── Descargas ────────────────────────────────────

        private void BtnDescargas_Click(object sender, RoutedEventArgs e)
        {
            ActualizarPopupDescargas();
            PopupDescargas.IsOpen = !PopupDescargas.IsOpen;
        }

        private void BtnVerDescargas_Click(object sender, RoutedEventArgs e)
        {
            PopupDescargas.IsOpen = false;
            if (_activeTab >= 0)
                _tabs[_activeTab].Source = new Uri(_urlDescargas);
        }

        private void NotificarDescargasActivas()
        {
            Dispatcher.Invoke(() =>
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    historial = _descargas.Historial,
                    activas   = _descargas.Activas,
                    carpeta   = _descargas.CarpetaDefault
                });
                foreach (var tab in _tabs)
                    tab.CoreWebView2?.PostWebMessageAsString("descargas:" + payload);

                ActualizarBadgeDescargas();
            });
        }

        private void ActualizarBadgeDescargas()
        {
            int activas = _descargas.Activas.Count;
            if (BadgeDescargas != null)
            {
                BadgeDescargas.Visibility = activas > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                if (BadgeDescargasText != null)
                    BadgeDescargasText.Text = activas.ToString();
            }
        }

        private void ActualizarPopupDescargas()
        {
            ListaDescargasPopup.Children.Clear();

            var todas = _descargas.Activas
                .Cast<EntradaDescarga>()
                .Concat(_descargas.Historial.Take(10))
                .ToList();

            if (todas.Count == 0)
            {
                ListaDescargasPopup.Children.Add(new TextBlock
                {
                    Text = "No hay descargas recientes.",
                    Foreground = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                    FontSize = 12,
                    Margin = new Thickness(8, 12, 8, 12),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            foreach (var d in todas)
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();

                info.Children.Add(new TextBlock
                {
                    Text = d.Nombre,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 220
                });

                info.Children.Add(new TextBlock
                {
                    Text = d.Estado == "descargando"
                        ? $"{d.Progreso}% · {d.TamañoStr}"
                        : d.Estado == "completado"
                            ? d.TamañoStr
                            : d.Estado,
                    Foreground = new SolidColorBrush(
                        d.Estado == "completado"
                            ? Color.FromArgb(120, 124, 58, 237)
                            : Color.FromArgb(100, 255, 255, 255)),
                    FontSize = 10,
                    Margin = new Thickness(0, 2, 0, 0)
                });

                if (d.Estado == "descargando" && d.Total > 0)
                {
                    var barBg = new Border
                    {
                        Height = 2,
                        Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                        CornerRadius = new CornerRadius(1),
                        Margin = new Thickness(0, 4, 0, 0)
                    };
                    var barFg = new Border
                    {
                        Height = 2,
                        Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                        CornerRadius = new CornerRadius(1),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = 200.0 * d.Progreso / 100.0
                    };
                    var barGrid = new Grid();
                    barGrid.Children.Add(barBg);
                    barGrid.Children.Add(barFg);
                    info.Children.Add(barGrid);
                }

                Grid.SetColumn(info, 0);
                row.Children.Add(info);

                if (d.Estado == "completado")
                {
                    string ruta = d.Ruta;
                    var abrirBtn = new Button
                    {
                        Content = "📂",
                        FontSize = 14,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Cursor = Cursors.Hand,
                        ToolTip = "Abrir ubicación",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    abrirBtn.Click += (s, e) =>
                    {
                        if (File.Exists(ruta))
                            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{ruta}\"");
                    };
                    Grid.SetColumn(abrirBtn, 1);
                    row.Children.Add(abrirBtn);
                }

                ListaDescargasPopup.Children.Add(row);
                ListaDescargasPopup.Children.Add(new Separator
                {
                    Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }
        }
        private void AplicarPerfConfig(string json)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

                if (doc.TryGetProperty("suspender_tabs", out var st))
                    _perfSuspenderTabs = st.GetBoolean();

                if (doc.TryGetProperty("limpiar_cache", out var lc))
                {
                    _perfLimpiarCache = lc.GetBoolean();
                    if (_perfLimpiarCache)
                    {
                        _cacheTimer ??= new System.Timers.Timer(30 * 60 * 1000);
                        _cacheTimer.Elapsed += (s, e) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                foreach (var tab in _tabs)
                                    tab.CoreWebView2?.Profile.ClearBrowsingDataAsync();
                            });
                        };
                        _cacheTimer.AutoReset = true;
                        _cacheTimer.Start();
                    }
                    else
                    {
                        _cacheTimer?.Stop();
                    }
                }

                if (doc.TryGetProperty("limite_tabs", out var lt))
                    _perfLimiteTabs = lt.GetBoolean();

                if (doc.TryGetProperty("limite_tabs_n", out var ltn))
                    _perfLimiteTabsN = ltn.GetInt32();
                if (doc.TryGetProperty("suspender_media", out var sm))
                    _suspenderMediaEnBackground = sm.GetBoolean();
            }
            catch { }
        }

        private void ActualizarUrlDisplay(string url)
        {
            if (UrlBar.IsFocused) return;

            UrlDisplay.Inlines.Clear();

            // Icono según protocolo
            if (url.StartsWith("https://"))
                UrlIconoPath.Fill = new SolidColorBrush(Color.FromRgb(52, 211, 153));   // verde
            else if (url.StartsWith("http://"))
                UrlIconoPath.Fill = new SolidColorBrush(Color.FromRgb(251, 191, 36));   // amarillo
            else if (url.StartsWith("file:///"))
                UrlIconoPath.Fill = new SolidColorBrush(Color.FromArgb(120, 200, 200, 255)); // azul tenue
            else
                UrlIconoPath.Fill = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)); // blanco tenue

            // Páginas internas
            if (url.StartsWith("file:///"))
            {
                // Nueva tab: barra vacía, como Chrome/Edge
                if (url.Contains("NuevaTab.html"))
                {
                    UrlBar.Text = "";
                    UrlDisplay.Inlines.Clear();
                    UrlDisplay.Inlines.Add(new System.Windows.Documents.Run("Buscar o escribir una URL")
                    {
                        Foreground = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255))
                    });
                    UrlBar.Visibility = Visibility.Collapsed;
                    UrlDisplay.Visibility = Visibility.Visible;
                    return;
                }

                // Otras páginas internas: nombre amigable
                string nombreAmigable = url switch
                {
                    var u when u.Contains("Historial.html")   => "atsuki://historial",
                    var u when u.Contains("Favoritos.html")   => "atsuki://favoritos",
                    var u when u.Contains("Ajustes.html")     => "atsuki://ajustes",
                    var u when u.Contains("Descargas.html")   => "atsuki://descargas",
                    var u when u.Contains("Extensiones.html") => "atsuki://extensiones",
                    var u when u.Contains("Perfiles.html")    => "atsuki://perfiles",
                    _ => url
                };

                UrlDisplay.Inlines.Add(new System.Windows.Documents.Run(nombreAmigable)
                {
                    Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255))
                });
                UrlBar.Visibility = Visibility.Collapsed;
                UrlDisplay.Visibility = Visibility.Visible;
                return;
            }

            // Parsear dominio
            try
            {
                var uri = new Uri(url);
                string scheme = uri.Scheme + "://";
                string host   = uri.Host;
                string resto  = url.Substring(scheme.Length + host.Length);

                var muted  = new SolidColorBrush(Color.FromArgb(100, 170, 170, 204));
                var normal = new SolidColorBrush(Color.FromArgb(180, 170, 170, 204));
                var bright = new SolidColorBrush(Colors.White);

                var partes = host.Split('.');
                string dominioPrincipal = partes.Length >= 2
                    ? string.Join(".", partes[^2], partes[^1])
                    : host;
                string subdominio = host.Length > dominioPrincipal.Length
                    ? host[..^(dominioPrincipal.Length + 1)] + "."
                    : "";

                UrlDisplay.Inlines.Add(new System.Windows.Documents.Run(scheme) { Foreground = muted });
                if (!string.IsNullOrEmpty(subdominio))
                    UrlDisplay.Inlines.Add(new System.Windows.Documents.Run(subdominio) { Foreground = normal });
                UrlDisplay.Inlines.Add(new System.Windows.Documents.Run(dominioPrincipal)
                {
                    Foreground = bright,
                    FontWeight = FontWeights.SemiBold
                });
                UrlDisplay.Inlines.Add(new System.Windows.Documents.Run(resto) { Foreground = muted });
            }
            catch
            {
                UrlDisplay.Inlines.Add(new System.Windows.Documents.Run(url)
                {
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 170, 170, 204))
                });
            }

            UrlBar.Visibility = Visibility.Collapsed;
            UrlDisplay.Visibility = Visibility.Visible;
        }

        private void UrlDisplay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            UrlDisplay.Visibility = Visibility.Collapsed;
            UrlBar.Visibility = Visibility.Visible;
            UrlBar.Focus();
            UrlBar.SelectAll();
        }

        private void IniciarHoverPreview(Button btn)
        {
            int idx = _tabButtons.IndexOf(btn);
            if (idx < 0) return;

            _previewTimer?.Stop();
            _previewTabIdx = idx;

            _previewTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };

            _previewTimer.Tick += async (s, e) =>
            {
                _previewTimer?.Stop();

                if (_previewTabIdx < 0 || _previewTabIdx >= _tabs.Count) return;
                if (!btn.IsMouseOver) return;

                var tab = _tabs[_previewTabIdx];
                if (tab.CoreWebView2 == null) return;

                string url = tab.CoreWebView2.Source ?? "";
                string titulo = tab.CoreWebView2.DocumentTitle is { Length: > 0 } t ? t : "Nueva pestaña";
                bool esTabActiva = _previewTabIdx == _activeTab;

                PreviewTabTitulo.Text = titulo;
                PopupPreviewTab.PlacementTarget = btn;

                try
                {
                    // ── YouTube: thumbnail directo sin capturar ──
                    var ytMatch = System.Text.RegularExpressions.Regex.Match(
                        url, @"(?:v=|youtu\.be/)([A-Za-z0-9_-]{11})");

                    if (ytMatch.Success)
                    {
                        string videoId = ytMatch.Groups[1].Value;
                        string thumbUrl = $"https://img.youtube.com/vi/{videoId}/mqdefault.jpg";
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(thumbUrl);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        if (!btn.IsMouseOver) return;
                        PreviewTabImagen.Source = bmp;
                        PopupPreviewTab.IsOpen = true;
                        return;
                    }

                    // ── Tab activa: capturar en vivo ──
                    if (esTabActiva)
                    {
                        using var stream = new MemoryStream();
                        await tab.CoreWebView2.CapturePreviewAsync(
                            CoreWebView2CapturePreviewImageFormat.Png, stream);
                        stream.Position = 0;
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.StreamSource = stream;
                        bmp.EndInit();
                        bmp.Freeze();
                        if (!btn.IsMouseOver) return;
                        PreviewTabImagen.Source = bmp;
                        PopupPreviewTab.IsOpen = true;
                        return;
                    }

                    // ── Tabs en segundo plano: capturar o usar caché ──
                    if (_tabPreviews.TryGetValue(_previewTabIdx, out var cached))
                    {
                        if (!btn.IsMouseOver) return;
                        PreviewTabImagen.Source = cached;
                        PopupPreviewTab.IsOpen = true;
                    }
                    else
                    {
                        // No hay caché — capturar ahora
                        try
                        {
                            tab.Visibility = Visibility.Visible;
                            using var stream = new MemoryStream();
                            await tab.CoreWebView2.CapturePreviewAsync(
                                CoreWebView2CapturePreviewImageFormat.Png, stream);
                            tab.Visibility = Visibility.Collapsed;
                            stream.Position = 0;
                            var bmp = new System.Windows.Media.Imaging.BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bmp.StreamSource = stream;
                            bmp.EndInit();
                            bmp.Freeze();
                            _tabPreviews[_previewTabIdx] = bmp;
                            if (!btn.IsMouseOver) return;
                            PreviewTabImagen.Source = bmp;
                            PopupPreviewTab.IsOpen = true;
                        }
                        catch { }
                    }
                }
                catch { }
            };

            _previewTimer.Start();
        }
        private void CerrarHoverPreview()
        {
            _previewTimer?.Stop();
            _previewTabIdx = -1;

            // Pequeño delay antes de cerrar para evitar parpadeo al mover entre pestañas
            var closeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            closeTimer.Tick += (s, e) =>
            {
                closeTimer.Stop();
                // Solo cerrar si el mouse no está sobre el popup ni sobre ningún tab button
                bool sobreAlgunTab = _tabButtons.Any(b => b.IsMouseOver);
                bool sobrePopup = PopupPreviewTab.IsMouseOver;
                if (!sobreAlgunTab && !sobrePopup)
                    PopupPreviewTab.IsOpen = false;
            };
            closeTimer.Start();
        }

        private void DetenerHoverPreview()
        {
            _previewTimer?.Stop();
            _previewTabIdx = -1;
            PopupPreviewTab.IsOpen = false;
        }

        private void TabStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
        }
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            if (parent == null) yield break;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var c in FindVisualChildren<T>(child)) yield return c;
            }
        }
        private async Task InicializarMusicaWebView()
        {
            if (_musicaInicializada) return;
            _musicaInicializada = true;

            _musicaWebView = new WebView2();
            _musicaWebView.Width = 2;
            _musicaWebView.Height = 2;
            _musicaWebView.IsHitTestVisible = false;

            // Contenedor con opacidad casi-cero para que Chromium NO lo trate como background
            var musicaContainer = new Border
            {
                Width = 2,
                Height = 2,
                Opacity = 0.01,
                IsHitTestVisible = false,
                Child = _musicaWebView
            };
            Canvas.SetLeft(musicaContainer, 0);
            Canvas.SetTop(musicaContainer, 0);
            BrowserContainer.Children.Add(musicaContainer);

            await _musicaWebView.EnsureCoreWebView2Async(_env);
            // Permitir acceso a archivos locales en el WebView de música
            _musicaWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            // Mapear todas las unidades disponibles
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                string letra = drive.Name[0].ToString().ToLower(); // "c", "d", etc.
                try
                {
                    _musicaWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        $"atsuki-drive-{letra}",
                        drive.RootDirectory.FullName,
                        CoreWebView2HostResourceAccessKind.Allow
                    );
                }
                catch { }
            }

            _musicaWebView.CoreWebView2.WebMessageReceived += (s, args) =>
            {
                string msg = args.TryGetWebMessageAsString();
                if (msg.StartsWith("musica:notify:"))
                {
                    string payload = msg.Substring("musica:notify:".Length);
                    int sep = payload.LastIndexOf('|');
                    string titulo = sep >= 0 ? payload.Substring(0, sep) : payload;
                    string imagen = sep >= 0 ? payload.Substring(sep + 1) : "";
                    Dispatcher.Invoke(() => MostrarNotificacionMusica(titulo, imagen));
                }
                else if (msg.StartsWith("musica:estado:"))
                {
                    string estado = msg.Substring("musica:estado:".Length);
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var json = System.Text.Json.JsonDocument.Parse(estado).RootElement;
                            ActualizarUIMusica(json);
                        }
                        catch { }
                        foreach (var tab in _tabs)
                            tab.CoreWebView2?.PostWebMessageAsString("atsukimusic:estado:" + estado);

                        // Arranque inicial — reproducir aleatoria una sola vez cuando el player ya confirmó su estado
                        if (_musicaArranqueInicial)
                        {
                            _musicaArranqueInicial = false;
                            var todasLasCanciones = _playlists
                                .SelectMany((pl, pi) => pl.canciones.Select((_, ci) => (pi, ci)))
                                .ToList();

                            if (todasLasCanciones.Count > 0)
                            {
                                var rng = new Random();
                                var (playlistIdx, cancionIdx) = todasLasCanciones[rng.Next(todasLasCanciones.Count)];
                                _musicaWebView.CoreWebView2?.PostWebMessageAsString("player:switchplaylist:" + playlistIdx);

                                // Usar DispatcherTimer para que el mensaje llegue desde el hilo UI
                                var arranqueTimer = new System.Windows.Threading.DispatcherTimer
                                {
                                    Interval = TimeSpan.FromMilliseconds(300)
                                };
                                arranqueTimer.Tick += (t, _) =>
                                {
                                    arranqueTimer.Stop();
                                    _musicaWebView.CoreWebView2?.PostWebMessageAsString("player:reproducir:" + cancionIdx);
                                };
                                arranqueTimer.Start();
                            }
                        }
                    });
                }
            };


            _musicaWebView.NavigationCompleted += (s2, e2) =>
            {
                if (!e2.IsSuccess) return;
                EnviarPlaylistsAlPlayer();

                // Aplicar volumen guardado
                string volPath = Path.Combine(_carpetaPerfil, "musica_volumen.txt");
                if (File.Exists(volPath) &&
                    double.TryParse(File.ReadAllText(volPath).Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double vol))
                {
                    MusicaVolumen.Value = Math.Clamp(vol, 0.0, 1.0);
                    _musicaWebView.CoreWebView2?.PostWebMessageAsString(
                        "player:volumen:" + vol.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
            };

            string htmlPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "player.html");
            _musicaWebView.Source = new Uri("file:///" + htmlPath.Replace("\\", "/"));
        }

        private void MostrarNotificacionMusica(string titulo, string imagenUrl = "")
        {
            // Cerrar toast anterior si existe
            _musicaToast?.Close();

            var toast = new Window
            {
                Width = 280, Height = 70,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            var screen = SystemParameters.WorkArea;
            toast.Left = screen.Right - 292;
            toast.Top  = screen.Bottom - 82;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 13, 13, 22)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 124, 58, 237)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 14, 8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(124, 58, 237),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.4
                }
            };

            var panelH = new StackPanel { Orientation = Orientation.Horizontal };

            if (!string.IsNullOrEmpty(imagenUrl))
            {
                try
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagenUrl)),
                        Width = 36, Height = 36,
                        Stretch = System.Windows.Media.Stretch.UniformToFill,
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Clip = new System.Windows.Media.RectangleGeometry
                            { Rect = new Rect(0, 0, 36, 36), RadiusX = 4, RadiusY = 4 }
                    };
                    panelH.Children.Add(img);
                }
                catch { }
            }
            else
            {
                panelH.Children.Add(new TextBlock
                {
                    Text = "🎵",
                    FontSize = 20,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textos.Children.Add(new TextBlock
            {
                Text = "Reproduciendo",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 124, 58, 237))
            });
            textos.Children.Add(new TextBlock
            {
                Text = titulo,
                FontSize = 12,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 190
            });
            panelH.Children.Add(textos);
            border.Child = panelH;
            toast.Content = border;

            _musicaToast = toast;
            toast.Show();

            // Auto-cerrar después de 4 segundos
            var timer = new System.Timers.Timer(4000);
            timer.Elapsed += (s, e) =>
            {
                timer.Stop();
                Dispatcher.Invoke(() => { toast.Close(); });
            };
            timer.Start();
        }

        private void InicializarControlsMusica()
        {
            BtnMusicaPlay.Click += (s, e) =>
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString(
                    _musicaReproduciendo ? "player:pause" : "player:play");

            BtnMusicaAnterior.Click += (s, e) =>
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:anterior");

            BtnMusicaSiguiente.Click += (s, e) =>
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:siguiente");

            // ── NUEVO: Botón aleatorio ──
            BtnMusicaAleatorio.Click += (s, e) =>
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:aleatorio");

            BtnMusicaShuffle.Click += (s, e) =>
            {
                bool shuffle = BtnMusicaShuffle.Foreground is SolidColorBrush b &&
                            b.Color == Color.FromRgb(85, 85, 119);
                BtnMusicaShuffle.Foreground = shuffle
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString(
                    "player:shuffle:" + shuffle.ToString().ToLower());
            };

            BtnMusicaRepeat.Click += (s, e) =>
            {
                string[] modos = { "none", "all", "one" };
                string actual = BtnMusicaRepeat.Tag as string ?? "none";
                string siguiente = modos[(Array.IndexOf(modos, actual) + 1) % modos.Length];
                BtnMusicaRepeat.Tag = siguiente;
                BtnMusicaRepeat.Content = siguiente == "one" ? "↻¹" : "↻";
                BtnMusicaRepeat.Foreground = siguiente != "none"
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:repeat:" + siguiente);
            };

            MusicaVolumen.ValueChanged += (s, e) =>
            {
                double vol = MusicaVolumen.Value;
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString(
                    "player:volumen:" + vol.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                // Guardar volumen
                File.WriteAllText(Path.Combine(_carpetaPerfil, "musica_volumen.txt"),
                    vol.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            };

            MusicaProgreso.ValueChanged += (s, e) =>
            {
                if (MusicaProgreso.IsMouseOver)
                    _musicaWebView?.CoreWebView2?.PostWebMessageAsString(
                        "player:seek:" + MusicaProgreso.Value.ToString("F2",
                            System.Globalization.CultureInfo.InvariantCulture));
            };

            // ── NUEVO: Crear playlist ──
            BtnMusicaNuevaPlaylist.Click += (s, e) => CrearNuevaPlaylist();

            // ── NUEVO: Exportar ──
            BtnMusicaExportar.Click += (s, e) => ExportarMusica();

            // ── NUEVO: Importar ──
            BtnMusicaImportar.Click += async (s, e) => await ImportarMusica();

            BtnMusicaAgregarArchivo.Click += async (s, e) =>
            {
                try
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Title = "Agregar música",
                        Filter = "Audio|*.mp3;*.flac;*.wav;*.ogg;*.aac;*.m4a",
                        Multiselect = true
                    };
                    if (dialog.ShowDialog() != true) return;
                    await InicializarMusicaWebView();
                    foreach (var archivo in dialog.FileNames)
                    {
                        string nombre = Path.GetFileNameWithoutExtension(archivo);
                        string letra = Path.GetPathRoot(archivo)![0].ToString().ToLower();
                        string rutaRelativa = archivo.Substring(3).Replace("\\", "/");
                        string url = $"https://atsuki-drive-{letra}/{rutaRelativa}";
                        _playlists[_playlistActiva].canciones.Add(new MusicaCancion
                        {
                            titulo = nombre,
                            autor  = "",
                            imagen = "",
                            url    = url
                        });
                    }
                    EnviarPlaylistsAlPlayer();
                    GuardarMusicaPlaylist();
                    RenderizarMusicaUI();
                }
                catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
            };

            BtnMusicaAddStream.Click += (s, e) => AgregarMusicaStream();
            MusicaStreamInput.KeyDown += (s, e) => { if (e.Key == Key.Enter) AgregarMusicaStream(); };

            BtnMusicaLimpiar.Click += (s, e) =>
            {
                _playlists[_playlistActiva].canciones.Clear();
                EnviarPlaylistsAlPlayer();
                GuardarMusicaPlaylist();
                RenderizarMusicaUI();
            };

            // Cargar volumen guardado
            string volPath = Path.Combine(_carpetaPerfil, "musica_volumen.txt");
            if (File.Exists(volPath) &&
                double.TryParse(File.ReadAllText(volPath).Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double volGuardado))
            {
                MusicaVolumen.Value = Math.Clamp(volGuardado, 0.0, 1.0);
            }

            if (_playlists == null || _playlists.Count == 0)
                _playlists = new() { new MusicaPlaylist() };
            CargarMusicaPlaylist();
            if (_playlists.Count == 0)
                _playlists.Add(new MusicaPlaylist());
            _playlistActiva = 0;
            RenderizarMusicaUI();
        }

        private void AgregarMusicaStream()
        {
            string url = MusicaStreamInput.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            string nombre = url.Split('/').LastOrDefault() ?? url;
            _playlists[_playlistActiva].canciones.Add(new MusicaCancion { titulo = nombre, url = url });
            MusicaStreamInput.Text = "";
            var json = System.Text.Json.JsonSerializer.Serialize(
                _playlists[_playlistActiva].canciones.Select(p => new { titulo = p.titulo, url = p.url }));
            _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:playlist:" + json);
            GuardarMusicaPlaylist();
            RenderizarMusicaUI();
        }

        private void RenderizarMusicaUI()
        {
            // Guard: garantizar que siempre hay al menos una playlist válida
            if (_playlists == null || _playlists.Count == 0)
                _playlists = new() { new MusicaPlaylist() };
            if (_playlistActiva < 0 || _playlistActiva >= _playlists.Count)
                _playlistActiva = 0;
            // ── Tabs de playlists ──
            MusicaPlaylistTabs.Children.Clear();
            for (int i = 0; i < _playlists.Count; i++)
            {
                int idx = i;
                var pl = _playlists[i];
                var tab = new Border
                {
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 4, 0),
                    CornerRadius = new CornerRadius(4),
                    Cursor = Cursors.Hand,
                    Background = i == _playlistActiva
                        ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                        : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
                };

                var tabGrid = new Grid();
                tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // Miniatura si tiene imagen
                if (!string.IsNullOrEmpty(pl.imagen))
                {
                    try
                    {
                        var thumb = new System.Windows.Controls.Image
                        {
                            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(pl.imagen)),
                            Width = 28, Height = 28,
                            Stretch = System.Windows.Media.Stretch.UniformToFill,
                            Margin = new Thickness(0, 0, 6, 0),
                            Clip = new System.Windows.Media.RectangleGeometry
                                { Rect = new Rect(0, 0, 28, 28), RadiusX = 3, RadiusY = 3 }
                        };
                        itemPanel.Children.Add(thumb);
                    }
                    catch { }
                }

                var textoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textoStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(pl.nombre) ? "(sin nombre)" : pl.nombre,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 180
                });
                itemPanel.Children.Add(textoStack);
                Grid.SetColumn(itemPanel, 0);
                tabGrid.Children.Add(itemPanel);

                // Botón eliminar playlist (no la primera)
                if (i > 0)
                {
                    var btnX = new Button
                    {
                        Content = "×", FontSize = 11,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    btnX.Click += (s, e) =>
                    {
                        e.Handled = true;
                        if (_playlists.Count <= 1) return;
                        _playlists.RemoveAt(idx);
                        if (_playlistActiva >= _playlists.Count) _playlistActiva = _playlists.Count - 1;
                        EnviarPlaylistsAlPlayer();
                        GuardarMusicaPlaylist();
                        RenderizarMusicaUI();
                    };
                    Grid.SetColumn(btnX, 1);
                    tabGrid.Children.Add(btnX);
                }

                tab.Child = tabGrid;
                tab.MouseLeftButtonDown += (s, e) =>
                {
                    _playlistActiva = idx;
                    _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:switchplaylist:" + idx);
                    RenderizarMusicaUI();
                };

                // Click derecho → cambiar imagen
                tab.MouseRightButtonDown += (s, e) =>
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.gif",
                        Title = "Imagen para \"" + pl.nombre + "\""
                    };
                    if (dlg.ShowDialog() != true) return;
                    _playlists[idx].imagen = "file:///" + dlg.FileName.Replace("\\", "/");
                    GuardarMusicaPlaylist();
                    EnviarPlaylistsAlPlayer();
                };

                MusicaPlaylistTabs.Children.Add(tab);
            }

            // ── Imagen de playlist activa ──
            string img = _playlists[_playlistActiva].imagen;
            if (!string.IsNullOrEmpty(img))
            {
                try
                {
                    MusicaPlaylistImagen.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(img));
                    MusicaPlaylistImagen.Visibility = Visibility.Visible;
                }
                catch { MusicaPlaylistImagen.Visibility = Visibility.Collapsed; }
            }
            else
            {
                MusicaPlaylistImagen.Visibility = Visibility.Collapsed;
            }

            // ── Lista de canciones ──
            MusicaPlaylist.Children.Clear();
            var canciones = _playlists[_playlistActiva].canciones;
            if (canciones.Count == 0)
            {
                MusicaPlaylist.Children.Add(new TextBlock
                {
                    Text = "Agrega canciones o streams",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 0)
                });
                return;
            }

            for (int i = 0; i < canciones.Count; i++)
            {
                int idx = i;
                var item = canciones[i];
                var border = new Border
                {
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand,
                    Background = i == _musicaIndiceActivo
                        ? new SolidColorBrush(Color.FromArgb(40, 124, 58, 237))
                        : new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 4, 6, 4)
                };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (!string.IsNullOrEmpty(item.imagen))
                {
                    try
                    {
                        var thumb = new System.Windows.Controls.Image
                        {
                            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(item.imagen)),
                            Width = 28, Height = 28,
                            Stretch = System.Windows.Media.Stretch.UniformToFill,
                            Margin = new Thickness(0, 0, 6, 0),
                            Clip = new System.Windows.Media.RectangleGeometry
                                { Rect = new Rect(0, 0, 28, 28), RadiusX = 3, RadiusY = 3 }
                        };
                        itemPanel.Children.Add(thumb);
                    }
                    catch { }
                }
                var textoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textoStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(item.titulo) ? "(sin título)" : item.titulo,
                    FontSize = 11,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 180
                });
                if (!string.IsNullOrEmpty(item.autor))
                    textoStack.Children.Add(new TextBlock
                    {
                        Text = item.autor,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(150, 200, 180, 255)),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 180
                    });
                itemPanel.Children.Add(textoStack);
                grid.Children.Add(itemPanel);

                var btnDel = new Button
                {
                    Content = "×", FontSize = 12,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(4, 2, 4, 2)
                };
                btnDel.Click += (s, e) =>
                {
                    _playlists[_playlistActiva].canciones.RemoveAt(idx);
                    EnviarPlaylistsAlPlayer();
                    GuardarMusicaPlaylist();
                    RenderizarMusicaUI();
                };
                Grid.SetColumn(btnDel, 1);
                grid.Children.Add(btnDel);
                border.Child = grid;

                border.MouseLeftButtonDown += async (s, e) =>
                {
                    if (e.OriginalSource is Button) return;
                    await InicializarMusicaWebView();
                    EnviarPlaylistsAlPlayer();
                    await Task.Delay(200);
                    _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:reproducir:" + idx);
                };

                border.MouseRightButtonDown += (s, e) => EditarMetadatosCancion(idx);
                MusicaPlaylist.Children.Add(border);
            }
        }

        private void ActualizarUIMusica(System.Text.Json.JsonElement estado)
        {
            _musicaReproduciendo = estado.TryGetProperty("reproduciendo", out var rep) && rep.GetBoolean();
            _musicaIndiceActivo  = estado.TryGetProperty("indice", out var idx) ? idx.GetInt32() : -1;

            BtnMusicaPlay.Content = _musicaReproduciendo ? "⏸" : "▶";

            if (estado.TryGetProperty("titulo", out var titulo))
                MusicaTitulo.Text = titulo.GetString() is { Length: > 0 } t ? t : "Sin reproducción";

            if (estado.TryGetProperty("progreso", out var prog) &&
                estado.TryGetProperty("duracion", out var dur))
            {
                double durVal = dur.GetDouble();
                MusicaProgreso.Maximum = durVal > 0 ? durVal : 1;
                if (!MusicaProgreso.IsMouseOver)
                    MusicaProgreso.Value = prog.GetDouble();

                string fmt(double s) => $"{(int)(s / 60)}:{((int)(s % 60)):D2}";
                MusicaInfo.Text = durVal > 0
                    ? $"{fmt(prog.GetDouble())} / {fmt(durVal)}"
                    : "—";
            }
        }

        private string NormalizarUrlMusica(string url)
        {
            if (url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
            {
                string path = url.Substring(8);
                if (path.Length >= 2 && path[1] == ':')
                {
                    string letra = path[0].ToString().ToLower();
                    string resto = path.Substring(3).Replace("\\", "/");
                    return $"https://atsuki-drive-{letra}/{resto}";
                }
            }
            return url;
        }

        private void GuardarMusicaPlaylist()
        {
            string path = Path.Combine(_carpetaPerfil, "musica_playlists.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(_playlists));
        }

        private void CargarMusicaPlaylist()
        {
            // Migrar archivo viejo si existe
            string pathViejo = Path.Combine(_carpetaPerfil, "musica_playlist.json");
            string path = Path.Combine(_carpetaPerfil, "musica_playlists.json");

            if (!File.Exists(path) && File.Exists(pathViejo))
            {
                // Migración: envolver playlist vieja en estructura nueva
                try
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(File.ReadAllText(pathViejo));
                    if (items != null)
                        foreach (var item in items)
                        {
                            string t = item.GetProperty("titulo").GetString() ?? "";
                            string u = item.GetProperty("url").GetString() ?? "";
                            if (!string.IsNullOrEmpty(u))
                                _playlists[0].canciones.Add(new MusicaCancion { titulo = t, url = NormalizarUrlMusica(u) });
                        }
                }
                catch { }
                return;
            }

            if (!File.Exists(path)) return;
            try
            {
                var raw = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(File.ReadAllText(path));
                if (raw == null) return;
                _playlists.Clear();
                foreach (var p in raw)
                {
                    var pl = new MusicaPlaylist
                    {
                        nombre = p.TryGetProperty("nombre", out var n) ? n.GetString() ?? "Playlist" : "Playlist",
                        imagen = p.TryGetProperty("imagen", out var img) ? img.GetString() ?? "" : ""
                    };
                    if (p.TryGetProperty("canciones", out var canciones))
                        foreach (var c in canciones.EnumerateArray())
                        {
                            string t  = c.TryGetProperty("titulo",  out var tt) ? tt.GetString() ?? "" : "";
                            string au = c.TryGetProperty("autor",   out var aa) ? aa.GetString() ?? "" : "";
                            string im = c.TryGetProperty("imagen",  out var ii) ? ii.GetString() ?? "" : "";
                            string u  = c.TryGetProperty("url",     out var uu) ? uu.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(u))
                                pl.canciones.Add(new MusicaCancion
                                    { titulo = t, autor = au, imagen = im, url = NormalizarUrlMusica(u) });
                        }
                    _playlists.Add(pl);
                }
                if (_playlists.Count == 0)
                    _playlists.Add(new MusicaPlaylist());
            }
            catch { _playlists = new() { new MusicaPlaylist() }; }
            // Migrar URLs viejas al nuevo formato
            foreach (var pl in _playlists)
            {
                foreach (var c in pl.canciones)
                {
                    string urlNorm = NormalizarUrlMusica(c.url);
                    if (c.url.StartsWith("https://atsuki-music/"))
                        urlNorm = $"https://atsuki-drive-c/{c.url.Substring("https://atsuki-music/".Length)}";
                    if (urlNorm != c.url) c.url = urlNorm;
                }
            }
        }

        private void EnviarPlaylistsAlPlayer()
        {
            if (_musicaWebView?.CoreWebView2 == null) return;
            string json = System.Text.Json.JsonSerializer.Serialize(_playlists);
            _musicaWebView.CoreWebView2.PostWebMessageAsString("player:playlists:" + json);
        }

        private void CrearNuevaPlaylist()
        {
            var win = new Window
            {
                Title = "Nueva playlist",
                Width = 340, Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(13, 13, 26)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 124, 58, 237)),
                BorderThickness = new Thickness(1)
            };
            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock
            {
                Text = "Nombre de la playlist",
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            });
            var txt = new TextBox
            {
                Text = "Mi playlist",
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 124, 58, 237)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(txt);

            string imagenElegida = "";
            var btnImg = new Button
            {
                Content = "🖼 Elegir imagen (opcional)",
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 200)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 12)
            };
            btnImg.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.gif",
                    Title = "Imagen para la playlist"
                };
                if (dlg.ShowDialog() != true) return;
                imagenElegida = "file:///" + dlg.FileName.Replace("\\", "/");
                btnImg.Content = "✅ " + Path.GetFileName(dlg.FileName);
            };
            stack.Children.Add(btnImg);

            var btnOk = new Button
            {
                Content = "Crear",
                Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnOk.Click += (s, e) =>
            {
                _playlists.Add(new MusicaPlaylist
                {
                    nombre = string.IsNullOrWhiteSpace(txt.Text) ? "Playlist" : txt.Text.Trim(),
                    imagen = imagenElegida
                });
                GuardarMusicaPlaylist();
                RenderizarMusicaUI();
                win.Close();
            };
            stack.Children.Add(btnOk);
            win.Content = stack;
            win.ShowDialog();
        }

        private void ExportarMusica()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Exportar playlists",
                Filter = "JSON|*.json",
                FileName = "atsuki_musica_backup"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var data = _playlists.Select(p => new
                {
                    nombre = p.nombre,
                    imagen = p.imagen,
                    canciones = p.canciones.Select(c => new { titulo = c.titulo, url = c.url })
                });
                File.WriteAllText(dlg.FileName,
                    System.Text.Json.JsonSerializer.Serialize(data,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                MessageBox.Show("✅ Exportación completada.", "Listo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al exportar: " + ex.Message); }
        }

        private async Task ImportarMusica()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Importar playlists",
                Filter = "JSON|*.json"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var raw = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(
                    File.ReadAllText(dlg.FileName));
                if (raw == null) return;

                var resultado = MessageBox.Show(
                    "¿Reemplazar todo o añadir a las playlists existentes?\n\nSí = Reemplazar  |  No = Añadir",
                    "Importar", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (resultado == MessageBoxResult.Cancel) return;
                if (resultado == MessageBoxResult.Yes) _playlists.Clear();

                foreach (var p in raw)
                {
                    var pl = new MusicaPlaylist
                    {
                        nombre = p.TryGetProperty("nombre", out var n) ? n.GetString() ?? "Playlist" : "Playlist",
                        imagen = p.TryGetProperty("imagen", out var img) ? img.GetString() ?? "" : ""
                    };
                    if (p.TryGetProperty("canciones", out var canciones))
                        foreach (var c in canciones.EnumerateArray())
                        {
                            string t = c.TryGetProperty("titulo", out var tt) ? tt.GetString() ?? "" : "";
                            string u = c.TryGetProperty("url", out var uu) ? uu.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(u)) pl.canciones.Add(new MusicaCancion { titulo = t, url = NormalizarUrlMusica(u) });
                        }
                    _playlists.Add(pl);
                }
                if (_playlists.Count == 0) _playlists.Add(new MusicaPlaylist());

                await InicializarMusicaWebView();
                EnviarPlaylistsAlPlayer();
                GuardarMusicaPlaylist();
                RenderizarMusicaUI();
                MessageBox.Show("✅ Importación completada.", "Listo",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Error al importar: " + ex.Message); }
        }

        private void EditarMetadatosCancion(int idx)
        {
            var cancion = _playlists[_playlistActiva].canciones[idx];

            var win = new Window
            {
                Title = "Editar canción",
                Width = 360, Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(13, 13, 26)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 124, 58, 237)),
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { Margin = new Thickness(20) };

            // Título
            stack.Children.Add(new TextBlock { Text = "Título", Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), FontSize = 11, Margin = new Thickness(0, 0, 0, 4) });
            var txtTitulo = new TextBox
            {
                Text = cancion.titulo,
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 36)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 10),
                CaretBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237))
            };
            stack.Children.Add(txtTitulo);

            // Autor
            stack.Children.Add(new TextBlock { Text = "Autor", Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), FontSize = 11, Margin = new Thickness(0, 0, 0, 4) });
            var txtAutor = new TextBox
            {
                Text = cancion.autor,
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 36)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 10),
                CaretBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237))
            };
            stack.Children.Add(txtAutor);

            // Imagen
            string imagenElegida = cancion.imagen;
            var btnImg = new Button
            {
                Content = string.IsNullOrEmpty(imagenElegida) ? "🖼 Elegir imagen" : "✅ " + Path.GetFileName(imagenElegida.Replace("file:///", "")),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 200)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 16)
            };
            btnImg.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.gif",
                    Title = "Imagen para la canción"
                };
                if (dlg.ShowDialog() != true) return;
                imagenElegida = "file:///" + dlg.FileName.Replace("\\", "/");
                btnImg.Content = "✅ " + Path.GetFileName(dlg.FileName);
            };
            stack.Children.Add(btnImg);

            // Botón guardar
            var btnOk = new Button
            {
                Content = "Guardar",
                Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnOk.Click += (s, e) =>
            {
                cancion.titulo = txtTitulo.Text.Trim();
                cancion.autor  = txtAutor.Text.Trim();
                cancion.imagen = imagenElegida;
                GuardarMusicaPlaylist();
                EnviarPlaylistsAlPlayer();
                RenderizarMusicaUI();
                win.Close();
            };
            stack.Children.Add(btnOk);
            win.Content = stack;
            win.ShowDialog();
        }
    }

    public class DialEntry
    {
        public string nombre { get; set; } = "";
        public string url    { get; set; } = "";
        public string color  { get; set; } = "";
    }
    public class UpdateDialog : Window
    {
        public bool Aceptado { get; private set; } = false;

        public UpdateDialog(string versionActual, string versionNueva, string notas)
        {
            Title = "Actualización disponible";
            Width = 440; Height = 280;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(13, 13, 26));
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 124, 58, 237));
            BorderThickness = new Thickness(1);

            var root = new StackPanel { Margin = new Thickness(24) };

            // Título
            root.Children.Add(new TextBlock
            {
                Text = "✦ Actualización disponible",
                Foreground = new SolidColorBrush(Color.FromRgb(157, 90, 255)),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Versión
            root.Children.Add(new TextBlock
            {
                Text = $"v{versionActual}  →  v{versionNueva}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 221)),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Notas
            var notasBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 20)
            };
            notasBorder.Child = new TextBlock
            {
                Text = notas,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 200)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            root.Children.Add(notasBorder);

            // Botones
            var botones = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnNo = new Button
            {
                Content = "Ahora no",
                Width = 100, Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 170)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            btnNo.Click += (s, e) => { Aceptado = false; Close(); };

            var btnSi = new Button
            {
                Content = "Actualizar ahora",
                Width = 130, Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.SemiBold
            };
            btnSi.Click += (s, e) => { Aceptado = true; Close(); };

            botones.Children.Add(btnNo);
            botones.Children.Add(btnSi);
            root.Children.Add(botones);

            Content = root;
        }
    }
    public class SugerenciaItem
    {
        public string Icono { get; set; } = "";
        public string Titulo { get; set; } = "";
        public string Subtitulo { get; set; } = "";
        public string Url { get; set; } = "";
        public string FaviconUrl { get; set; } = "";
    }
    public class StringToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class UrlToImageConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (string.IsNullOrEmpty(value as string)) 
                return System.Windows.DependencyProperty.UnsetValue;
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(value as string);
                bmp.DecodePixelWidth = 16;
                bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.None;
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnDemand;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return System.Windows.DependencyProperty.UnsetValue; }
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }
    public class MusicaCancion
    {
        public string titulo  { get; set; } = "";
        public string autor   { get; set; } = "";
        public string imagen  { get; set; } = "";
        public string url     { get; set; } = "";
    }

    public class MusicaPlaylist
    {
        public string nombre  { get; set; } = "Nueva playlist";
        public string imagen  { get; set; } = "";
        public List<MusicaCancion> canciones { get; set; } = new();
    }
}
