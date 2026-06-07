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
    public partial class MainWindow: Window
    {
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

        private void WebView_NavigationCompleted(object? sender,
            CoreWebView2NavigationCompletedEventArgs e)
        {
            if (sender is not WebView2 webView) return;
            int index = _tabs.IndexOf(webView);
            if (index < 0) return;

            Dispatcher.Invoke(() =>
            {
                string url    = webView.Source?.ToString() ?? "";
                string titulo = webView.CoreWebView2?.DocumentTitle ?? "Nueva pestaña";

                if (index == _activeTab)
                {
                    _ignorarTextChanged = true;
                    UrlBar.Text = url;
                    _ignorarTextChanged = false;
                    ActualizarUrlDisplay(url);
                    SugerenciasPopup.IsOpen = false;
                    _ignorarGotFocus = false;
                }

                if (_tabButtons[index].Tag is TextBlock label)
                    label.Text = titulo;

                _historial.Agregar(url, titulo);
                ActualizarFaviconTab(index, url);
                if (index == _activeTab)
                {
                    Dispatcher.Invoke(ActualizarZoomLabel);
                    string tituloVentana = url.Contains("NuevaTab.html")    ? "Nueva pestaña" :
                                           url.Contains("Ajustes.html")     ? "Ajustes" :
                                           url.Contains("Favoritos.html")   ? "Favoritos" :
                                           url.Contains("Historial.html")   ? "Historial" :
                                           url.Contains("Descargas.html")   ? "Descargas" :
                                           url.Contains("Capturas.html")   ? "Editor de Capturas" :
                                           url.Contains("AtsukiDocs.html")   ? "AtsukiDocs" :
                                           url.Contains("AtsukiNotes.html")   ? "AtsukiNotes" :
                                           titulo;
                    this.Title = tituloVentana == "Nueva pestaña"
                        ? "AtsukiBrowser"
                        : $"{tituloVentana} — AtsukiBrowser";
                }

                string dominioZoom = GetDominioZoom(url);
                if (!string.IsNullOrEmpty(dominioZoom))
                {
                    _aplicandoZoom = true;
                    webView.ZoomFactor = _zoomPorDominio.TryGetValue(dominioZoom, out double z) ? z : 1.0;
                    _aplicandoZoom = false;
                }
            });

            ActualizarEstrellaFavorito();
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
            else if (_atajos.Coincide("modo_zen", ctrl, shift, alt, tecla)) { ToggleModoZen(); e.Handled = true; }
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
                    if (!string.IsNullOrEmpty(dominio))
                    {
                        _zoomPorDominio[dominio] = _tabs[_activeTab].ZoomFactor;
                        GuardarZoomDebounced();
                    }
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
                    if (!string.IsNullOrEmpty(dominio))
                    {
                        _zoomPorDominio[dominio] = _tabs[_activeTab].ZoomFactor;
                        GuardarZoomDebounced();
                    }
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
                    if (!string.IsNullOrEmpty(dominio))
                    {
                        _zoomPorDominio.Remove(dominio);
                        GuardarZoomDebounced();
                    }
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
                SugerenciasPopup.IsOpen = false;
                _mostrandoRecientes = false;
                // Solo restaurar URL si el UrlBar está en foco
                if (UrlBar.IsKeyboardFocused)
                {
                    string urlActual = _tabs[_activeTab].Source?.ToString() ?? "";
                    // No mostrar rutas internas file:///
                    if (urlActual.StartsWith("file:///"))
                        urlActual = "";
                    _ignorarGotFocus = true;
                    _ignorarTextChanged = true;
                    UrlBar.Text = urlActual;
                    _ignorarTextChanged = false;
                    ActualizarUrlDisplay(urlActual);
                    _ignorarGotFocus = false;
                    Keyboard.ClearFocus();
                    _tabs[_activeTab].Focus();
                }
            }
        }

        private void GuardarBusqueda(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            if (query.StartsWith("http")) return; // no guardar URLs

            // Evitar duplicados consecutivos
            if (_busquedas.Count > 0 && _busquedas[0].Query == query) return;

            // Eliminar si ya existe y moverla al tope
            _busquedas.RemoveAll(b => b.Query.Equals(query, StringComparison.OrdinalIgnoreCase));
            _busquedas.Insert(0, new BusquedaHistorial { Query = query, Fecha = DateTime.Now });

            // Máximo 50 búsquedas
            if (_busquedas.Count > 50) _busquedas = _busquedas.Take(50).ToList();

            try { File.WriteAllText(_busquedasPath, JsonSerializer.Serialize(_busquedas)); }
            catch { }
        }

        private void CargarBusquedas()
        {
            try
            {
                if (File.Exists(_busquedasPath))
                    _busquedas = JsonSerializer.Deserialize<List<BusquedaHistorial>>(
                        File.ReadAllText(_busquedasPath)) ?? new();
            }
            catch { _busquedas = new(); }
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
                    _ignorarTextChanged = true;
                    UrlBar.Text = "";
                    _ignorarTextChanged = false;
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
                    var u when u.Contains("Capturas.html")    => "atsuki://capturas",
                    var u when u.Contains("AtsukiDocs.html")    => "atsuki://documentos",
                    var u when u.Contains("AtsukiNotes.html") => "atsuki://notes",
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

        private void UrlBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SugerenciasPopup.IsOpen = false;
                var texto = UrlBar.Text;
                Navegar(texto);
                // Guardar búsqueda si no es una URL
                if (!texto.StartsWith("http") && !texto.StartsWith("file") && !EsUrlDirecta(texto))
                    GuardarBusqueda(texto);
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

        private void UrlBar_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            string paginaUrl = _activeTab >= 0 ? _tabs[_activeTab].Source?.ToString() ?? "" : "";

            var ctxMenu = new ContextMenu
            {
                Background      = new SolidColorBrush(Color.FromRgb(22, 18, 40)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(0, 4, 0, 4)
            };

            MenuItem CrearItem(string texto, string atajo, Action accion)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var txtHeader = new TextBlock
                {
                    Text = texto,
                    Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                    FontSize = 12, VerticalAlignment = VerticalAlignment.Center
                };
                var txtAtajo = new TextBlock
                {
                    Text = atajo,
                    Foreground = new SolidColorBrush(Color.FromArgb(100, 180, 160, 255)),
                    FontSize = 11, Margin = new Thickness(24, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(txtAtajo, 1);
                grid.Children.Add(txtHeader);
                grid.Children.Add(txtAtajo);
                var item = new MenuItem
                {
                    Header = grid,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(12, 7, 12, 7)
                };
                item.Click += (s, ev) => accion();
                return item;
            }

            Separator CrearSep() => new Separator
            {
                Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)),
                Margin = new Thickness(0, 2, 0, 2)
            };

            ctxMenu.Items.Add(CrearItem("✂  Cortar",           "Ctrl+X", () => UrlBar.Cut()));
            ctxMenu.Items.Add(CrearItem("📋  Copiar",           "Ctrl+C", () => UrlBar.Copy()));
            ctxMenu.Items.Add(CrearItem("📋  Pegar",            "Ctrl+V", () => UrlBar.Paste()));
            ctxMenu.Items.Add(CrearSep());
            ctxMenu.Items.Add(CrearItem("🔠  Seleccionar todo", "Ctrl+A", () => UrlBar.SelectAll()));
            ctxMenu.Items.Add(CrearItem("🔗  Copiar URL completa", "", () =>
            {
                if (!string.IsNullOrEmpty(paginaUrl)) Clipboard.SetText(paginaUrl);
            }));

            ctxMenu.PlacementTarget = UrlBar;
            ctxMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            ctxMenu.IsOpen = true;
        }

        private void UrlBar_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_ignorarGotFocus) return;
            UrlDisplay.Visibility = Visibility.Collapsed;
            UrlBar.Visibility = Visibility.Visible;

            _ignorarTextChanged = true;
            UrlBar.SelectAll();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _ignorarTextChanged = false;
                // Mostrar recientes sin verificar IsKeyboardFocused aquí
                // porque en este punto el foco puede aún estar en transición
                MostrarSugerenciasRecientesInterno();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UrlBar_LostFocus(object sender, RoutedEventArgs e)
        {
            _urlBarClickado = false;
            _mostrandoRecientes = false;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (SugerenciasPopup.IsOpen && !SugerenciasPopup.IsKeyboardFocusWithin)
                    SugerenciasPopup.IsOpen = false;

                if (!SugerenciasPopup.IsOpen)
                {
                    // ✅ Siempre usar la URL real de la tab, nunca UrlBar.Text
                    string urlReal = _activeTab >= 0 && _activeTab < _tabs.Count
                        ? _tabs[_activeTab].Source?.ToString() ?? ""
                        : "";
                    ActualizarUrlDisplay(urlReal);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private async void UrlBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_ignorarTextChanged) return;
            if (_ignorarGotFocus) return;

            var texto = UrlBar.Text.Trim();

            if (string.IsNullOrEmpty(texto))
            {
                if (UrlBar.IsKeyboardFocused)
                    MostrarSugerenciasRecientes();
                else
                    SugerenciasPopup.IsOpen = false;
                return;
            }

            // Si hay texto, ya no estamos en modo recientes
            _mostrandoRecientes = false;

            _sugCts?.Cancel();
            _sugCts = new System.Threading.CancellationTokenSource();
            var token = _sugCts.Token;

            try
            {
                await Task.Delay(80, token);
                if (token.IsCancellationRequested) return;

                // Sugerencias del historial que coincidan
                var delHistorial = _historial.Entradas
                    .Where(h => !string.IsNullOrEmpty(h.Url) &&
                                !h.Url.Contains("google.com/search") &&
                                !h.Url.Contains("bing.com/search") &&
                                ((h.Titulo ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase)
                                || (h.Url ?? "").Contains(texto, StringComparison.OrdinalIgnoreCase)))
                    .Take(4)
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
    }
}