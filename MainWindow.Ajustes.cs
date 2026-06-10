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
        private void AplicarPerfConfig(string json)
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

                if (doc.TryGetProperty("suspender_tabs", out var st))
                    _perfSuspenderTabs = st.GetBoolean();

                if (doc.TryGetProperty("intervalo_cache", out var ic))
                    _intervaloCacheMinutos = ic.GetInt32();

                if (doc.TryGetProperty("limpiar_cache", out var lc))
                {
                    bool perfLimpiarCacheAnterior = _perfLimpiarCache;
                    int intervaloAnterior = _intervaloCacheMinutos;

                    _perfLimpiarCache = lc.GetBoolean();

                    if (_perfLimpiarCache)
                    {
                        // Solo recrear el timer si cambió algo o no existía
                        bool necesitaReiniciar = _cacheTimer == null
                            || !perfLimpiarCacheAnterior
                            || intervaloAnterior != _intervaloCacheMinutos;

                        if (necesitaReiniciar)
                        {
                            _cacheTimer?.Stop();
                            _cacheTimer = new System.Timers.Timer(_intervaloCacheMinutos * 60 * 1000);
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
                    }
                    else
                    {
                        _cacheTimer?.Stop();
                        _cacheTimer = null;
                    }
                }

                if (doc.TryGetProperty("limite_tabs", out var lt))
                    _perfLimiteTabs = lt.GetBoolean();

                if (doc.TryGetProperty("limite_tabs_n", out var ltn))
                    _perfLimiteTabsN = ltn.GetInt32();
                if (doc.TryGetProperty("suspender_media", out var sm))
                    _suspenderMediaEnBackground = sm.GetBoolean();
                if (doc.TryGetProperty("intervalo_suspension", out var itvl))
                    _intervaloSuspension = itvl.GetInt32();
            }
            catch { }
        }

        private void InicializarManagers()
        {
            try
            {
                _carpetaPerfil = _perfiles.CarpetaActiva();
                _perfilActivo  = _perfiles.Activo;
                Directory.CreateDirectory(_carpetaPerfil);
                _historial   = new HistorialManager(_carpetaPerfil);
                _favoritos   = new FavoritosManager(_carpetaPerfil);
                _temas       = new TemaManager(_carpetaPerfil);
                _sidebar     = new SidebarManager(_carpetaPerfil);
                _descargas   = new DescargasManager(_carpetaPerfil);
                _extensiones = new ExtensionesManager(_carpetaPerfil);
                _atajos      = new AtajosManager(_carpetaPerfil);
                _busquedasPath = Path.Combine(_carpetaPerfil, "busquedas.json");
                CargarBusquedas();
                CargarZoom();

                string confirmarPath = Path.Combine(_carpetaPerfil, "confirmar_cerrar.txt");
                if (File.Exists(confirmarPath))
                    _confirmarCerrar = File.ReadAllText(confirmarPath).Trim() == "true";

                string capturasCarpetaPath = Path.Combine(_carpetaPerfil, "capturas_carpeta.txt");
                if (File.Exists(capturasCarpetaPath))
                    _carpetaCapturas = File.ReadAllText(capturasCarpetaPath).Trim();

                string inicioPath = Path.Combine(_carpetaPerfil, "inicio.json");
                if (File.Exists(inicioPath))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(File.ReadAllText(inicioPath)).RootElement;
                        string modo = doc.TryGetProperty("modo", out var m) ? m.GetString() ?? "nuevatab" : "nuevatab";
                        string url  = doc.TryGetProperty("url",  out var u) ? u.GetString() ?? "" : "";
                        if (modo == "restaurar") _restaurarSesion = true;
                        else if (modo == "personalizada" && !string.IsNullOrEmpty(url)) _urlInicio = url;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al inicializar perfil:\n{ex.Message}\n\nSe usará configuración por defecto.",
                    "AtsukiBrowser", MessageBoxButton.OK, MessageBoxImage.Warning);
                string fallback = Path.Combine(Path.GetTempPath(), "AtsukiBrowser_fallback");
                Directory.CreateDirectory(fallback);
                _carpetaPerfil = fallback;
                _historial   = new HistorialManager(fallback);
                _favoritos   = new FavoritosManager(fallback);
                _temas       = new TemaManager(fallback);
                _sidebar     = new SidebarManager(fallback);
                _descargas   = new DescargasManager(fallback);
                _extensiones = new ExtensionesManager(fallback);
                _atajos      = new AtajosManager(fallback);
            }
            string nuevatabLayoutPath = Path.Combine(_carpetaPerfil, "nuevatab_layout.txt");
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
    }
}