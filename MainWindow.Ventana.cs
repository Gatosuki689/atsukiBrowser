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
using System.Security.Principal;

namespace atsukibrowser
{
    public partial class MainWindow: Window
    {
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


            if (_restaurarSesion)
                RestaurarSesion();
            else if (!string.IsNullOrEmpty(_urlInicio))
                AbrirNuevaTab(_urlInicio);
            else
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
            if (!File.Exists(perfPath))
            File.WriteAllText(perfPath, JsonSerializer.Serialize(new
            {
                suspender_tabs = true,
                limpiar_cache = true,
                intervalo_cache = 30,
                intervalo_suspension = 5,
                limite_tabs = false,
                limite_tabs_n = 10,
                suspender_media = true
            }));
            AplicarPerfConfig(File.ReadAllText(perfPath));
            AplicarTemaUI(_temas.TemaActivo);
            // Cargar preferencia de previews
            string previewsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsukiBrowser", "previews.txt");
            if (File.Exists(previewsPath))
                _recibirPreviews = File.ReadAllText(previewsPath).Trim() == "true";
            // Cargar modo compacto
            string compactoPath = Path.Combine(_carpetaPerfil, "sidebar_compacto.txt");
            if (File.Exists(compactoPath))
                _sidebarCompacto = File.ReadAllText(compactoPath).Trim() == "true";
            if (_sidebarCompacto)
            {
                SidebarColumn.Width = new GridLength(36);
                if (Sidebar.Child is Grid g && g.ColumnDefinitions.Count > 0)
                    g.ColumnDefinitions[0].Width = new GridLength(36);
            }
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
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1, To = 0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            anim.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, anim);
        }

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
            // Guardar estado de música
            try
            {
                var estadoMusica = new
                {
                    playlistActiva = _playlistActiva,
                    indice         = _musicaIndiceActivo,
                    progreso       = _musicaProgreso
                };
                File.WriteAllText(
                    Path.Combine(_carpetaPerfil, "musica_estado.json"),
                    System.Text.Json.JsonSerializer.Serialize(estadoMusica));
            }
            catch { }
            GuardarSesion();
            _tabs.Clear();
            _tabButtons.Clear();
            _rendimiento.Dispose();
            base.OnClosed(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_confirmarCerrar && _tabs.Count > 1)
            {
                var result = MessageBox.Show(
                    $"Tienes {_tabs.Count} pestañas abiertas. ¿Cerrar el navegador?",
                    "Confirmar cierre",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }

        private void GuardarSesion()
        {
            try
            {
                string inicioPath = Path.Combine(_carpetaPerfil, "inicio.json");
                if (File.Exists(inicioPath))
                {
                    var doc = JsonDocument.Parse(File.ReadAllText(inicioPath)).RootElement;
                    string modo = doc.TryGetProperty("modo", out var m) ? m.GetString() ?? "" : "";
                    if (modo != "restaurar") return;
                }
                else return;

                // Páginas internas que NO se deben restaurar
                bool EsPaginaInterna(string u) =>
                    string.IsNullOrEmpty(u) ||
                    u.Contains("NuevaTab.html") ||
                    u.Contains("Ajustes.html") ||
                    u.Contains("Favoritos.html") ||
                    u.Contains("Historial.html") ||
                    u.Contains("Descargas.html") ||
                    u.StartsWith("about:");

                var urls = _tabs
                    .Select(t => t.Source?.ToString() ?? "")
                    .Select(u => EsPaginaInterna(u) ? "nuevatab" : u)
                    .Where(u => !string.IsNullOrEmpty(u))
                    .ToList();

                if (urls.Count == 0) return;
                File.WriteAllText(
                    Path.Combine(_carpetaPerfil, "sesion.json"),
                    JsonSerializer.Serialize(urls));
            }
            catch { }
        }

        private void RestaurarSesion()
        {
            try
            {
                string path = Path.Combine(_carpetaPerfil, "sesion.json");
                if (!File.Exists(path)) { AbrirNuevaTab(_urlNuevaTab); return; }
                var urls = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path));
                if (urls == null || urls.Count == 0) { AbrirNuevaTab(_urlNuevaTab); return; }
                foreach (var url in urls)
                    AbrirNuevaTab(url == "nuevatab" ? _urlNuevaTab : url);
            }
            catch { AbrirNuevaTab(_urlNuevaTab); }
        }
    }
}