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
using System.Web;

namespace atsukibrowser
{
    public partial class MainWindow : Window
    {

        private bool _mostrarBarraGrupos = false;
        private WebView2? _tabPreCalentada = null;
        private bool _extensionesChromeCargadas = false;
        private List<TabGroup> _tabGroups = new();
        private Dictionary<WebView2, System.Text.StringBuilder> _notesChunks = new();
        private int _nextGroupId = 1;
        private readonly HashSet<int> _tabsHoverInit = new();
        private string _urlCapturas = "";
        private string _urlDocs = "";
        private string _urlNotes = "";
        private string _urlWallpapers = "";
        private string _urlAyuda = "";
        private string _urlDraw = "";
        private string _carpetaCapturas = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "AtsukiBrowser", "Capturas");
        private bool _modoZen = false;
        private bool _sidebarCompacto = false;
        private bool _soloFavoritos = false;
        private Color _accentColor = (Color)ColorConverter.ConvertFromString("#7c3aed");
        private Dictionary<string, System.Windows.Media.Imaging.BitmapImage> _faviconCache = new();
        private System.Windows.Threading.DispatcherTimer? _zoomSaveTimer;
        private string _musicaFondoTipo = "artwork";
        private string _musicaFondoImagenPath = "";
        private bool _musicaAutoplay = false;
        private bool _musicaAleatorioGlobal = false;
        private double _musicaVolumenDefault = 0.8;
        private System.Windows.Controls.Button? _dragBtn = null;
        private bool _confirmarCerrar = false;
        private bool   _restaurarSesion = false;
        private int _intervaloCacheMinutos = 30;
        private string _urlInicio = "";
        private static readonly System.Net.Http.HttpClient _httpClient = new();
        private static string? _appVersionCache;
        private bool _recibirPreviews = false;
        private WebView2? _musicaWebView;
        private bool _musicaInicializada = false;
        private bool _musicaPanelAbierto = false;
        private bool _musicaPlayerInternoActivo = false;
        private double _musicaProgreso = 0;
        private List<(string titulo, string url, string imagen, string autor)> _musicaPlaylist = new();
        private int _musicaIndiceActivo = -1;
        private bool _musicaReproduciendo = false;
        private bool _musicaContinuar = false;
        private bool _aplicandoZoom = false;
        private bool _mostrandoRecientes = false;
        private DateTime _popupAbiertoCuando = DateTime.MinValue;
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
        private bool _guardarHistorial = true;
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
        private CookiesManager _cookies = null!;
        private bool _cookiesAutoAceptar = true;
        private string _urlNuevaTab;
        private readonly string _urlHistorial;
        private readonly string _urlFavoritos;
        private readonly string _urlAjustes;
        private readonly string _urlDescargas;
        private readonly string _urlExtensiones;
        private readonly string _urlPerfiles;
        private string _buscadorActivo = "google";
        private bool _perfSuspenderTabs  = true;
        private int _intervaloSuspension = 5; // minutos
        private Dictionary<int, System.Windows.Threading.DispatcherTimer> _suspensionTimers = new();
        private Dictionary<int, System.Windows.Media.Imaging.BitmapImage> _tabPreviews = new();
        private bool _perfLimpiarCache = false;
        private bool _perfLimiteTabs     = false;
        private int  _perfLimiteTabsN    = 10;
        private bool _suspenderMediaEnBackground = true;
        private System.Timers.Timer? _mediaDetectorTimer;
        private bool _musicaPausadaPorMedia = false;
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
        private readonly string _zoomPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsukiBrowser", "zoom.json");
        private bool _ignorarTextChanged = false;
        private bool _ignorarGotFocus = false;
        private bool _urlBarClickado = false;
        private List<BusquedaHistorial> _busquedas = new();
        private string _busquedasPath = "";
        private static readonly System.Text.Json.JsonSerializerOptions _jsonOpts = new();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

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
                // Forzar menú contextual custom en UrlBar
                UrlBar.AddHandler(
                    PreviewMouseRightButtonDownEvent,
                    new MouseButtonEventHandler((s2, e2) =>
                    {
                        e2.Handled = true;
                        UrlBar_MouseRightButtonDown(s2, e2);
                    }),
                    handledEventsToo: true
                );
                CargarGrupos();
            };
            StateChanged += (s, e) =>
            {
                MainGrid.Margin = WindowState == WindowState.Maximized
                    ? new Thickness(6)
                    : new Thickness(0);
            };
            string res = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            string _resNuevaTabV1 = "file:///" + Path.Combine(res, "NuevaTab.html").Replace("\\", "/");
            string _resNuevaTabV2 = "file:///" + Path.Combine(res, "NuevaTabV2.html").Replace("\\", "/");
            string _layoutPath    = Path.Combine(_carpetaPerfil, "nuevatab_layout.txt");
            string _layoutVal     = File.Exists(_layoutPath) ? File.ReadAllText(_layoutPath).Trim() : "v1";
            _urlNuevaTab = _layoutVal == "v2" ? _resNuevaTabV2 : _resNuevaTabV1;
            _urlHistorial = "file:///" + Path.Combine(res, "Historial.html").Replace("\\", "/");
            _urlFavoritos = "file:///" + Path.Combine(res, "Favoritos.html").Replace("\\", "/");
            _urlAjustes   = "file:///" + Path.Combine(res, "Ajustes.html").Replace("\\", "/");
            _urlDescargas = "file:///" + Path.Combine(res, "Descargas.html").Replace("\\", "/");
            _urlExtensiones = "file:///" + Path.Combine(res, "Extensiones.html").Replace("\\", "/");
            _urlPerfiles = "file:///" + Path.Combine(res, "Perfiles.html").Replace("\\", "/");
            _urlCapturas = "file:///" + Path.Combine(res, "Capturas.html").Replace("\\", "/");
            _urlNotes = "file:///" + Path.Combine(res, "AtsukiNotes.html").Replace("\\", "/");
            _urlAyuda = "file:///" + Path.Combine(res, "Ayuda.html").Replace("\\", "/");
            _urlDraw = "file:///" + Path.Combine(res, "AtsukiDraw.html").Replace("\\", "/");
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
            UrlBar.ContextMenu = null;
            // Cerrar popup al hacer click fuera
            this.PreviewMouseDown += (s, e) =>
            {
                if (!SugerenciasPopup.IsOpen) return;
                if ((DateTime.Now - _popupAbiertoCuando).TotalMilliseconds < 150) return;
                var src = e.OriginalSource as DependencyObject;
                if (src == null) return;
                bool enUrlBar = src == UrlBar || (UrlBar.IsLoaded && UrlBar.IsAncestorOf(src));
                bool enPopup = false;
                var p = src;
                while (p != null)
                {
                    if (p == SugerenciasPopup || p == SugerenciasList) { enPopup = true; break; }
                    p = VisualTreeHelper.GetParent(p);
                }
                if (!enUrlBar && !enPopup)
                {
                    SugerenciasPopup.IsOpen = false;
                    _mostrandoRecientes = false;
                    // ✅ Usar la URL real de la tab, no UrlBar.Text (que puede estar vacío o desactualizado)
                    string urlReal = _activeTab >= 0 && _activeTab < _tabs.Count
                        ? _tabs[_activeTab].Source?.ToString() ?? ""
                        : "";
                    ActualizarUrlDisplay(urlReal);
                    // ✅ Quitar el foco del UrlBar explícitamente
                    Keyboard.ClearFocus();
                    if (_activeTab >= 0 && _activeTab < _tabs.Count)
                        _tabs[_activeTab].Focus();
                }
            };
            // Mostrar badge si es versión preview
            if (AppVersion.Contains("-prev") || AppVersion.Contains("-beta") || AppVersion.Contains("-alpha"))
            {
                BadgePreview.Visibility = Visibility.Visible;
                MarcaAgua.Visibility = Visibility.Visible;
            }
            // Animación de apertura
            Opacity = 0;
            Loaded += (s, e) =>
            {
                var anim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 0, To = 1,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                BeginAnimation(OpacityProperty, anim);
            };
            _ = Task.Run(VerificarActualizaciones);
            VerificarPrimeraEjecucion();
            TabScrollViewer.SizeChanged += (s, e) => ActualizarEstiloTabs();

            // Menú contextual para UrlBar
            var ctxMenu = new ContextMenu();

            var itemCortar = new MenuItem { Header = "Cortar" };
            itemCortar.Click += (s, e) => UrlBar.Cut();

            var itemCopiar = new MenuItem { Header = "Copiar" };
            itemCopiar.Click += (s, e) => UrlBar.Copy();

            var itemPegar = new MenuItem { Header = "Pegar" };
            itemPegar.Click += (s, e) => UrlBar.Paste();

            var itemSelTodo = new MenuItem { Header = "Seleccionar todo" };
            itemSelTodo.Click += (s, e) => UrlBar.SelectAll();

            var sep = new Separator();

            var itemCopiarUrl = new MenuItem { Header = "Copiar URL completa" };
            itemCopiarUrl.Click += (s, e) =>
            {
                string url = _activeTab >= 0 ? _tabs[_activeTab].Source?.ToString() ?? "" : "";
                if (!string.IsNullOrEmpty(url)) Clipboard.SetText(url);
            };

            ctxMenu.Items.Add(itemCortar);
            ctxMenu.Items.Add(itemCopiar);
            ctxMenu.Items.Add(itemPegar);
            ctxMenu.Items.Add(sep);
            ctxMenu.Items.Add(itemSelTodo);
            ctxMenu.Items.Add(itemCopiarUrl);

            UrlBar.ContextMenu = ctxMenu;
        }

        private async void AbrirNotasVersion(string notas = "")
        {
            if (_tabs.Count == 0 || _activeTab < 0)
            {
                await Task.Delay(500);
                if (_tabs.Count == 0 || _activeTab < 0) return;
            }

            AbrirNuevaTab();

            if (_activeTab < 0 || _activeTab >= _tabs.Count) return;

            var webView = _tabs[_activeTab];
            int idx = _activeTab;

            string novedadesPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Resources", "AtsukiNovedades.html");

            await webView.EnsureCoreWebView2Async(_env);
            webView.Source = new Uri("file:///" + novedadesPath.Replace("\\", "/"));

            if (idx >= 0 && idx < _tabButtons.Count && _tabButtons[idx].Tag is TextBlock label)
                label.Text = $"Novedades v{AppVersion}";

            if (idx == _activeTab)
            {
                _ignorarGotFocus = true;
                _ignorarTextChanged = true;
                UrlBar.Text = $"atsuki://novedades/v{AppVersion}";
                _ignorarTextChanged = false;
                ActualizarUrlDisplay($"atsuki://novedades/v{AppVersion}");
                _ignorarGotFocus = false;
            }

            EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = null;
            handler = async (s, e) =>
            {
                webView.NavigationCompleted -= handler;
                await Task.Delay(300);
                var payload = JsonSerializer.Serialize(new {
                    version   = AppVersion,
                    notas     = notas,
                    esPreview = AppVersion.Contains("-")
                });
                webView.CoreWebView2.PostWebMessageAsString("novedades:" + payload);
            };
            webView.NavigationCompleted += handler;
        }

        private bool IsDescendantOfPopup(DependencyObject element, System.Windows.Controls.Primitives.Popup popup)
        {
            var parent = element;
            while (parent != null)
            {
                if (parent == popup || parent == SugerenciasList) return true;
                parent = VisualTreeHelper.GetParent(parent) 
                    ?? LogicalTreeHelper.GetParent(parent);
            }
            return false;
        }

        private System.Threading.CancellationTokenSource? _sugCts;

        private void SugerenciasList_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (SugerenciasList.SelectedItem is SugerenciaItem sug)
            {
                SugerenciasPopup.IsOpen = false;
                UrlBar.Text = sug.Url;
                // Guardar si es búsqueda plana
                if (!sug.Url.StartsWith("http"))
                    GuardarBusqueda(sug.Url);
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
        

        // Versión interna — sin check de foco (para GotFocus)
        private void MostrarSugerenciasRecientesInterno()
        {
            var recientes = _historial.Entradas
                .Where(h => !string.IsNullOrEmpty(h.Url) &&
                            !h.Url.StartsWith("file:///") &&
                            !h.Url.Contains("google.com/search") &&
                            !h.Url.Contains("bing.com/search"))
                .GroupBy(h => h.Url)
                .Select(g => g.First())
                .Take(6)
                .ToList();

            if (recientes.Count == 0)
            {
                SugerenciasPopup.IsOpen = false;
                return;
            }

            SugerenciasList.Items.Clear();
            foreach (var h in recientes)
            {
                try
                {
                    SugerenciasList.Items.Add(new SugerenciaItem
                    {
                        Icono      = "🕐",
                        Titulo     = h.Titulo ?? h.Url,
                        Subtitulo  = h.Url,
                        Url        = h.Url,
                        FaviconUrl = $"https://www.google.com/s2/favicons?domain={new Uri(h.Url).Host}&sz=32"
                    });
                }
                catch { }
            }

            _mostrandoRecientes = true;
            SugerenciasPopup.PlacementTarget = UrlBarBorder;
            SugerenciasPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            SugerenciasPopup.Width = UrlBarBorder.ActualWidth;
            _popupAbiertoCuando = DateTime.Now;
            SugerenciasPopup.IsOpen = true;
        }

        // Versión pública — con check de foco (para TextChanged)
        private void MostrarSugerenciasRecientes()
        {
            if (!UrlBar.IsKeyboardFocused) return;
            MostrarSugerenciasRecientesInterno();
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

        private void MenuItemCopiarUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = _activeTab >= 0 ? _tabs[_activeTab].Source?.ToString() ?? "" : "";
            if (!string.IsNullOrEmpty(url)) Clipboard.SetText(url);
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

        private void MenuAyuda_Click(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = false;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Ayuda.html");
            AbrirNuevaTab("file:///" + path.Replace("\\", "/"));
        }

        private void MenuNovedades_Click(object sender, RoutedEventArgs e)
        {
            PopupMenu.IsOpen = false;
            string notasPath = Path.Combine(_carpetaPerfil, "notas_version.txt");

            if (File.Exists(notasPath))
            {
                string notas = File.ReadAllText(notasPath);
                AbrirNotasVersion(notas);
            }
            else
            {
                // Descargar notas si no existen
                AbrirNotasVersion(); // abre primero vacío
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AtsukiBrowser");
                        string json = await _httpClient.GetStringAsync(
                            $"https://gist.githubusercontent.com/Gatosuki689/f24638ebb9ed77db3a58fc2318103b39/raw/version.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
                        var doc  = JsonSerializer.Deserialize<JsonElement>(json);
                        string notas = doc.TryGetProperty("notas", out var n) ? n.GetString() ?? "" : "";
                        if (AppVersion.Contains("-") && doc.TryGetProperty("preview", out var prev))
                            if (prev.TryGetProperty("notas", out var pn))
                                notas = pn.GetString() ?? notas;
                        File.WriteAllText(notasPath, notas);
                        // Mandar las notas a la página ya abierta
                        var payload = JsonSerializer.Serialize(new {
                            version   = AppVersion,
                            notas     = notas,
                            esPreview = AppVersion.Contains("-")
                        });
                        Dispatcher.Invoke(() => {
                            var wv = _tabs[_activeTab];
                            wv.CoreWebView2?.PostWebMessageAsString("novedades:" + payload);
                        });
                    }
                    catch { }
                });
            }
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
                "capturas"    => _urlCapturas,
                "ayuda"    => _urlAyuda,
                _           => url
            };

            if (_activeTab >= 0)
                _tabs[_activeTab].Source = new Uri(destino);
            else
                AbrirNuevaTab(destino);
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

        private void GuardarPerfJson(string clave, object valor)
        {
            try
            {
                string perfPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AtsukiBrowser", "perf.json");
                var dict = File.Exists(perfPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        File.ReadAllText(perfPath))?.ToDictionary(k => k.Key, k => (object)k.Value) ?? new()
                    : new Dictionary<string, object>();
                dict[clave] = valor;
                File.WriteAllText(perfPath, JsonSerializer.Serialize(dict));
            }
            catch { }
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


        private GlobalSystemMediaTransportControlsSession? _sessionActual;
        

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

        private void DescargarArchivoConProgreso(string url, string destino, WebView2? webViewNotificar = null, string? notifyPrefix = null)
        {
            string nombre = Path.GetFileName(destino);
            var entrada = _descargas.IniciarDescarga(url, nombre);
            entrada.Ruta = destino;
            Dispatcher.Invoke(() => { ActualizarBadgeDescargas(); NotificarDescargasActivas(); });

            _ = Task.Run(async () =>
            {
                try
                {
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AtsukiBrowser");

                    using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    entrada.Total = resp.Content.Headers.ContentLength ?? 0;

                    using var stream = await resp.Content.ReadAsStreamAsync();
                    Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
                    using var fs = File.Create(destino);

                    var buffer = new byte[81920];
                    int read;
                    long total = 0;
                    while ((read = await stream.ReadAsync(buffer)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read));
                        total += read;
                        entrada.Recibido = total;
                        Dispatcher.Invoke(NotificarDescargasActivas);
                    }

                    _descargas.CompletarDescarga(entrada.Id);
                    Dispatcher.Invoke(() =>
                    {
                        NotificarDescargasActivas();
                        ActualizarBadgeDescargas();
                        if (webViewNotificar != null && notifyPrefix != null)
                            webViewNotificar.CoreWebView2.PostWebMessageAsString(notifyPrefix + entrada.Ruta);
                    });
                }
                catch (Exception ex)
                {
                    _descargas.CancelarDescarga(entrada.Id);
                    Dispatcher.Invoke(() =>
                    {
                        NotificarDescargasActivas();
                        ActualizarBadgeDescargas();
                        if (webViewNotificar != null && notifyPrefix != null)
                            webViewNotificar.CoreWebView2.PostWebMessageAsString("wallhaven:descarga-error:" + ex.Message);
                    });
                }
            });
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

                string url, titulo;
                bool esTabActiva;
                try
                {
                    if (tab.CoreWebView2 == null) return;
                    url         = tab.CoreWebView2.Source ?? "";
                    titulo      = tab.CoreWebView2.DocumentTitle is { Length: > 0 } t ? t : "Nueva pestaña";
                    esTabActiva = _previewTabIdx == _activeTab;
                }
                catch (ObjectDisposedException) { return; }

                PreviewTabTitulo.Text        = titulo;
                PopupPreviewTab.PlacementTarget = btn;

                try
                {
                    // YouTube: thumbnail directo
                    var ytMatch = System.Text.RegularExpressions.Regex.Match(
                        url, @"(?:v=|youtu\.be/)([A-Za-z0-9_-]{11})");

                    if (ytMatch.Success)
                    {
                        string videoId  = ytMatch.Groups[1].Value;
                        string thumbUrl = $"https://img.youtube.com/vi/{videoId}/mqdefault.jpg";
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource   = new Uri(thumbUrl);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        if (!btn.IsMouseOver) return;
                        PreviewTabImagen.Source  = bmp;
                        PopupPreviewTab.IsOpen   = true;
                        return;
                    }

                    // Tab activa: capturar en vivo
                    if (esTabActiva)
                    {
                        try
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
                            PopupPreviewTab.IsOpen  = true;
                        }
                        catch (ObjectDisposedException) { return; }
                        return;
                    }

                    // Tabs en segundo plano: caché o capturar
                    if (_tabPreviews.TryGetValue(_previewTabIdx, out var cached))
                    {
                        if (!btn.IsMouseOver) return;
                        PreviewTabImagen.Source = cached;
                        PopupPreviewTab.IsOpen  = true;
                    }
                    else
                    {
                        try
                        {
                            tab.Visibility = Visibility.Visible;
                            using var stream = new MemoryStream();
                            await tab.CoreWebView2.CapturePreviewAsync(
                                CoreWebView2CapturePreviewImageFormat.Png, stream);
                            tab.Visibility  = Visibility.Collapsed;
                            stream.Position = 0;
                            var bmp = new System.Windows.Media.Imaging.BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bmp.StreamSource = stream;
                            bmp.EndInit();
                            bmp.Freeze();
                            _tabPreviews[_previewTabIdx] = bmp;
                            if (!btn.IsMouseOver) return;
                            PreviewTabImagen.Source = bmp;
                            PopupPreviewTab.IsOpen  = true;
                        }
                        catch (ObjectDisposedException) { return; }
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

        private static void CopiarCarpetaExt(string origen, string destino)
        {
            foreach (var file in Directory.GetFiles(origen))
                File.Copy(file, Path.Combine(destino, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(origen))
            {
                var sub = Path.Combine(destino, Path.GetFileName(dir));
                Directory.CreateDirectory(sub);
                CopiarCarpetaExt(dir, sub);
            }
        }

    }

    public class DialEntry
    {
        public string nombre { get; set; } = "";
        public string url    { get; set; } = "";
        public string color  { get; set; } = "";
    }
    public class BusquedaHistorial
    {
        public string Query     { get; set; } = "";
        public DateTime Fecha   { get; set; } = DateTime.Now;
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
        public string titulo   { get; set; } = "";
        public string autor    { get; set; } = "";
        public string imagen   { get; set; } = "";
        public string url      { get; set; } = "";
        public bool   favorito { get; set; } = false;
        public bool   esYoutube { get; set; } = false;
    }

    public class MusicaPlaylist
    {
        public string nombre  { get; set; } = "Nueva playlist";
        public string imagen  { get; set; } = "";
        public List<MusicaCancion> canciones { get; set; } = new();
    }

    public class TabGroup
    {
        public int    Id      { get; set; }
        public string Nombre  { get; set; } = "Grupo";
        public Color  Color   { get; set; } = Color.FromRgb(124, 58, 237);
        public bool   Colapsado { get; set; } = false;
        public List<int> TabIndices { get; set; } = new(); // índices de tabs en el grupo
    }
}
