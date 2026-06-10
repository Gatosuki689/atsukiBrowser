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
        public void AbrirNuevaTabPublic(string url) => AbrirNuevaTab(url);
        private async void PreCalentarTab()
        {
            if (_tabPreCalentada != null) return;
            if (_env == null) return;

            var wv = new WebView2();
            wv.Visibility = Visibility.Collapsed;
            BrowserContainer.Children.Add(wv);
            try
            {
                await wv.EnsureCoreWebView2Async(_env);
                wv.CoreWebView2.Settings.IsStatusBarEnabled        = false;
                wv.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                wv.CoreWebView2.Settings.IsGeneralAutofillEnabled  = false;
                wv.CoreWebView2.Profile.PreferredTrackingPreventionLevel =
                    Microsoft.Web.WebView2.Core.CoreWebView2TrackingPreventionLevel.None;

                // Cargar extensiones Chrome una sola vez en el perfil
                if (!_perfiles.Activo.EsInvitado && !_extensionesChromeCargadas)
                {
                    var rutas = _extensiones.GetExtensionesChrome();
                    if (rutas.Count > 0)
                    {
                        await Task.WhenAll(rutas.Select(async ruta =>
                        {
                            try { await wv.CoreWebView2.Profile.AddBrowserExtensionAsync(ruta); }
                            catch { }
                        }));
                    }
                    _extensionesChromeCargadas = true;
                }

                _tabPreCalentada = wv;
            }
            catch { BrowserContainer.Children.Remove(wv); }
        }
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

            WebView2 webView;
            if (_tabPreCalentada != null)
            {
                webView = _tabPreCalentada;
                _tabPreCalentada = null;
                webView.Visibility = Visibility.Hidden;
                // Lanzar pre-calentado del siguiente en background
                _ = Task.Run(() => Dispatcher.BeginInvoke(PreCalentarTab));
            }
            else
            {
                webView = new WebView2();
                webView.Visibility = Visibility.Hidden;
                BrowserContainer.Children.Add(webView);
            }

            if (_env != null)
            {
                await webView.EnsureCoreWebView2Async(_env);
                // Optimizaciones por pestaña
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                webView.CoreWebView2.Profile.PreferredTrackingPreventionLevel =
                    Microsoft.Web.WebView2.Core.CoreWebView2TrackingPreventionLevel.None;

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

                    // ── Picture-in-Picture (Ctrl+Shift+P) ────────────
                    if (_atajos.Coincide("pip", ctrl, shift, alt, tecla))
                    {
                        _ = webView.CoreWebView2.ExecuteScriptAsync("""
                            (function() {
                                // Buscar el video activo más relevante
                                const videos = Array.from(document.querySelectorAll('video'))
                                    .filter(v => !v.paused || v.readyState > 0)
                                    .sort((a, b) => (b.videoWidth * b.videoHeight) - (a.videoWidth * a.videoHeight));

                                const video = videos[0] ?? document.querySelector('video');
                                if (!video) return;

                                if (document.pictureInPictureElement) {
                                    document.exitPictureInPicture().catch(() => {});
                                } else {
                                    video.requestPictureInPicture().catch(() => {});
                                }
                            })();
                        """);
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

                        if (idx == _activeTab)
                            this.Title = titulo == "Nueva pestaña"
                                ? "AtsukiBrowser"
                                : $"{titulo} — AtsukiBrowser";
                    });
                };
                webView.CoreWebView2.SourceChanged += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        int idx = _tabs.IndexOf(webView);
                        if (idx < 0 || idx != _activeTab) return;

                        string urlActual = webView.Source?.ToString() ?? "";

                        if (UrlBar.IsFocused) return;

                        _ignorarGotFocus = true;
                        _ignorarTextChanged = true;
                        UrlBar.Text = urlActual;
                        _ignorarTextChanged = false;
                        ActualizarUrlDisplay(urlActual);
                        _ignorarGotFocus = false;

                        ActualizarEstrellaFavorito();
                    });
                };
            }

           webView.CoreWebView2.ContextMenuRequested += async (s, args) =>
            {
                var deferral = args.GetDeferral();
                args.Handled = true;
                var target = args.ContextMenuTarget;

                // ── Leer TODAS las propiedades ANTES del await ──
                string linkUrlInicial = "";
                string imgUrl         = "";
                string textoSel       = "";
                bool   editable       = false;
                string paginaUrl      = webView.Source?.ToString() ?? "";
                double clickX         = args.Location.X;
                double clickY         = args.Location.Y;

                try { linkUrlInicial = target.LinkUri ?? ""; }         catch { }
                try
                {
                    if (!string.IsNullOrEmpty(target.SourceUri) &&
                        target.Kind == CoreWebView2ContextMenuTargetKind.Image)
                        imgUrl = target.SourceUri;
                }
                catch { }
                try { textoSel = target.HasSelection ? target.SelectionText ?? "" : ""; } catch { }
                try { editable = target.IsEditable; }                  catch { }

                // ── Ahora sí, resolver link dinámico via JS ──
                string linkUrl = linkUrlInicial;
                if (string.IsNullOrEmpty(linkUrl))
                {
                    try
                    {
                        string? jsResult = await webView.CoreWebView2.ExecuteScriptAsync(@"
                            (function() {
                                var el = document.elementFromPoint(" + clickX + @", " + clickY + @");
                                while (el && el.tagName !== 'A') el = el.parentElement;
                                return el ? (el.href || '') : '';
                            })()");
                        if (!string.IsNullOrEmpty(jsResult))
                            linkUrl = jsResult.Trim('"').Replace("\\u0026", "&");
                    }
                    catch { }
                }

                Dispatcher.InvokeAsync(() =>
                {
                    var ctxMenu = new ContextMenu
                    {
                        Background    = new SolidColorBrush(Color.FromRgb(22, 18, 40)),
                        BorderBrush   = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                        BorderThickness = new Thickness(1),
                        Padding       = new Thickness(0, 4, 0, 4)
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
                            FontSize = 12,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(txtHeader, 0);
                        var txtAtajo = new TextBlock
                        {
                            Text = atajo,
                            Foreground = new SolidColorBrush(Color.FromArgb(100, 180, 160, 255)),
                            FontSize = 11,
                            Margin = new Thickness(24, 0, 0, 0),
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
                        item.Click += (s2, e2) => accion();
                        return item;
                    }

                    Separator CrearSep() => new Separator
                    {
                        Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)),
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    // ── Link ──
                    if (!string.IsNullOrEmpty(linkUrl))
                    {
                        ctxMenu.Items.Add(CrearItem("🔗  Abrir en nueva pestaña",  "", () => AbrirNuevaTab(linkUrl)));
                        ctxMenu.Items.Add(CrearItem("🪟  Abrir en nueva ventana",  "", () =>
                        {
                            var nuevaVentana = new MainWindow();
                            nuevaVentana.Show();
                            nuevaVentana.AbrirNuevaTabPublic(linkUrl);
                        }));
                        ctxMenu.Items.Add(CrearItem("📋  Copiar enlace", "", () => Clipboard.SetText(linkUrl)));
                        ctxMenu.Items.Add(CrearSep());
                    }

                    // ── Imagen ──
                    if (!string.IsNullOrEmpty(imgUrl))
                    {
                        ctxMenu.Items.Add(CrearItem("🖼  Guardar imagen como...", "", () =>
                        {
                            var dlg = new Microsoft.Win32.SaveFileDialog
                            {
                                FileName = Path.GetFileName(new Uri(imgUrl).LocalPath),
                                Filter = "Imagen|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.svg"
                            };
                            if (dlg.ShowDialog() == true)
                                _ = Task.Run(async () =>
                                {
                                    var bytes = await _httpClient.GetByteArrayAsync(imgUrl);
                                    File.WriteAllBytes(dlg.FileName, bytes);
                                });
                        }));
                        ctxMenu.Items.Add(CrearItem("📋  Copiar dirección de imagen", "", () => Clipboard.SetText(imgUrl)));
                        ctxMenu.Items.Add(CrearItem("🔗  Abrir imagen en nueva pestaña", "", () => AbrirNuevaTab(imgUrl)));
                        ctxMenu.Items.Add(CrearItem("📋  Copiar imagen", "", async () =>
                        {
                            try
                            {
                                var bytes = await _httpClient.GetByteArrayAsync(imgUrl);
                                using var ms = new MemoryStream(bytes);
                                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                bmp.StreamSource = ms;
                                bmp.EndInit();
                                bmp.Freeze();
                                Clipboard.SetImage(bmp);
                            }
                            catch { }
                        }));
                        ctxMenu.Items.Add(CrearItem("🔍  Buscar imagen en Google", "", () =>
                            AbrirNuevaTab("https://lens.google.com/uploadbyurl?url=" + Uri.EscapeDataString(imgUrl))));
                        ctxMenu.Items.Add(CrearSep());
                    }

                    // ── Texto seleccionado ──
                    if (!string.IsNullOrWhiteSpace(textoSel))
                    {
                        string preview = textoSel.Length > 20 ? textoSel[..20] + "…" : textoSel;
                        ctxMenu.Items.Add(CrearItem("📋  Copiar", "Ctrl+C", () => Clipboard.SetText(textoSel)));
                        ctxMenu.Items.Add(CrearItem($"🔍  Buscar \"{preview}\"", "", () =>
                            AbrirNuevaTab("https://www.google.com/search?q=" + Uri.EscapeDataString(textoSel))));
                        ctxMenu.Items.Add(CrearItem("📝  Copiar como Markdown", "", () =>
                        {
                            // Si hay link bajo el cursor, copiar como [texto](url)
                            string md = !string.IsNullOrEmpty(linkUrl)
                                ? $"[{textoSel}]({linkUrl})"
                                : textoSel;
                            Clipboard.SetText(md);
                        }));
                        ctxMenu.Items.Add(CrearSep());
                    }

                    // ── Input editable ──
                    if (editable)
                    {
                        ctxMenu.Items.Add(CrearItem("📋  Copiar", "Ctrl+C", () =>
                            webView.CoreWebView2.ExecuteScriptAsync("document.execCommand('copy')")));
                        ctxMenu.Items.Add(CrearItem("📋  Pegar", "Ctrl+V", () =>
                            webView.CoreWebView2.ExecuteScriptAsync("document.execCommand('paste')")));
                        ctxMenu.Items.Add(CrearSep());
                    }

                    // ── Página general ──
                    ctxMenu.Items.Add(CrearItem("🔄  Recargar",          "Ctrl+R", () => webView.Reload()));
                    ctxMenu.Items.Add(CrearItem("📄  Nueva pestaña",     "Ctrl+T", () => AbrirNuevaTab()));
                    ctxMenu.Items.Add(CrearItem("⭐  Añadir a favoritos","",       () => BtnFavorito_Click(this, new RoutedEventArgs())));
                    ctxMenu.Items.Add(CrearItem("📋  Copiar URL",        "",       () => Clipboard.SetText(paginaUrl)));
                    ctxMenu.Items.Add(CrearSep());
                    ctxMenu.Items.Add(CrearItem(_modoZen ? "🧘  Salir del Modo Zen" : "🧘  Modo Zen", 
                        "Ctrl+Shift+Z", () => ToggleModoZen()));
                        ctxMenu.Items.Add(CrearItem(                                          // ← agregar aquí
                        _mostrarBarraGrupos ? "🗂  Ocultar barra de grupos" : "🗂  Mostrar barra de grupos",
                        "", () =>
                    {
                        _mostrarBarraGrupos = !_mostrarBarraGrupos;
                        GruposBar.Visibility = _mostrarBarraGrupos ? Visibility.Visible : Visibility.Collapsed;
                        GuardarGrupos();
                    }));
                    ctxMenu.Items.Add(CrearItem("📄  Ver código fuente", "", () =>
                        AbrirNuevaTab("view-source:" + paginaUrl)));
                    ctxMenu.Items.Add(CrearItem("📌  Añadir al sidebar", "", () =>
                    {
                        string titulo = webView.CoreWebView2?.DocumentTitle ?? paginaUrl;
                        string emoji  = "🌐";
                        _sidebar.Items.Add(new SidebarItem
                        {
                            Id      = "user:" + Guid.NewGuid().ToString("N")[..6],
                            Emoji   = emoji,
                            Nombre  = titulo.Length > 20 ? titulo[..20] : titulo,
                            Url     = paginaUrl,
                            Visible = true
                        });
                        _sidebar.Guardar();
                        RenderizarSidebar();
                    }));
                    ctxMenu.Items.Add(CrearItem("🔧  Inspeccionar",      "F12",    () =>
                        webView.CoreWebView2.OpenDevToolsWindow()));

                    ctxMenu.PlacementTarget = webView;
                    ctxMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    ctxMenu.IsOpen = true;
                    deferral.Complete();
                });
            };

            // Cerrar popup de sugerencias cuando el WebView recibe el foco
            // pero NO si el UrlBar sigue siendo el foco lógico
            webView.GotFocus += (s, e) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!UrlBar.IsKeyboardFocused)
                    {
                        // No cerrar si el popup se acaba de abrir (menos de 300ms)
                        if ((DateTime.Now - _popupAbiertoCuando).TotalMilliseconds < 500) return;
                        SugerenciasPopup.IsOpen = false;
                        _mostrandoRecientes = false;
                        ActualizarUrlDisplay(UrlBar.Text);
                    }
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
                else if (msg == "navigate:capturas")
                {
                    string capPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                "Resources", "Capturas.html");
                    Dispatcher.Invoke(() =>
                        webView.Source = new Uri("file:///" + capPath.Replace("\\", "/")));
                }
                else if (msg == "navigate:docs")
                {
                    string capPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                "Resources", "AtsukiDocs.html");
                    Dispatcher.Invoke(() =>
                        webView.Source = new Uri("file:///" + capPath.Replace("\\", "/")));
                }
                else if (msg == "navigate:notes")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlNotes));
                }
                else if (msg == "navigate:wallpapers")
                {
                    string capPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                "Resources", "AtsukiWallpapers.html");
                    Dispatcher.Invoke(() =>
                        webView.Source = new Uri("file:///" + capPath.Replace("\\", "/")));
                }
                else if (msg == "navigate:palette")
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "AtsukiPalette.html");
                    Dispatcher.Invoke(() => webView.Source = new Uri("file:///" + path.Replace("\\", "/")));
                }
                else if (msg == "navigate:ayuda")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlAyuda));
                }
                else if (msg == "navigate:draw")
                {
                    Dispatcher.Invoke(() => webView.Source = new Uri(_urlDraw));
                }
                else if (msg == "get:dials")
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(_dials);
                    webView.CoreWebView2.PostWebMessageAsString("dials:" + json);
                }
                else if (msg.StartsWith("navigate:"))
                {
                    string navUrl = msg.Substring("navigate:".Length);
                    if (!navUrl.StartsWith("http://") && !navUrl.StartsWith("https://")
                        && !navUrl.StartsWith("file:///") && !navUrl.StartsWith("chrome-extension://"))
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
                else if (msg.StartsWith("palette:aplicar-tema:"))
                {
                    var data   = JsonSerializer.Deserialize<JsonElement>(msg.Substring("palette:aplicar-tema:".Length));
                    string accent = data.GetProperty("accent").GetString() ?? "#7c3aed";
                    _temas.SetAccent(accent);
                    AplicarTemaUI(_temas.TemaActivo);
                    PropagaTema();
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
                else if (msg == "extension:instalar:chrome")
                {
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Title           = "Selecciona el manifest.json de la extensión",
                            Filter          = "Manifest (manifest.json)|manifest.json",
                            CheckFileExists = true
                        };
                        if (dialog.ShowDialog() != true) return;

                        string carpeta = Path.GetDirectoryName(dialog.FileName)!;

                        // Leer manifest Chrome original
                        string nombre  = Path.GetFileName(carpeta);
                        string version = "1.0";
                        string desc    = "";
                        try
                        {
                            var manifest = JsonDocument.Parse(File.ReadAllText(dialog.FileName)).RootElement;
                            if (manifest.TryGetProperty("name",        out var n)) nombre  = n.GetString() ?? nombre;
                            if (manifest.TryGetProperty("version",     out var v)) version = v.GetString() ?? version;
                            if (manifest.TryGetProperty("description", out var d)) desc    = d.GetString() ?? "";
                        }
                        catch { }

                        // Resolver __MSG_*__ desde _locales
                        nombre = ResolverMsgChrome(nombre, carpeta);
                        desc   = ResolverMsgChrome(desc,   carpeta);

                        // Copiar carpeta completa SIN tocar manifest.json
                        string id      = Guid.NewGuid().ToString("N")[..8];
                        string destino = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "AtsukiBrowser", "Extensions", id);
                        Directory.CreateDirectory(destino);
                        CopiarCarpetaExt(carpeta, destino);

                        // Cargar en todas las tabs y obtener el ID real
                        string extensionId = "";
                        foreach (var tab in _tabs)
                        {
                            try
                            {
                                var ext = await tab.CoreWebView2.Profile.AddBrowserExtensionAsync(destino);
                                if (!string.IsNullOrEmpty(ext.Id))
                                    extensionId = ext.Id;
                            }
                            catch { }
                        }

                        // Actualizar meta con el ID real y la página de opciones
                        string optionsPage = "";
                        try
                        {
                            var chromeManifest = JsonDocument.Parse(File.ReadAllText(dialog.FileName)).RootElement;
                            if (chromeManifest.TryGetProperty("options_page", out var op))
                                optionsPage = op.GetString() ?? "";
                            else if (chromeManifest.TryGetProperty("options_ui", out var oui))
                                if (oui.TryGetProperty("page", out var ouip))
                                    optionsPage = ouip.GetString() ?? "";
                        }
                        catch { }

                        var meta = new {
                            Nombre      = nombre,
                            Descripcion = desc,
                            Version     = version,
                            Tipo        = "chrome",
                            Activa      = true,
                            Icono       = "",
                            ExtensionId = extensionId,
                            OptionsPage = optionsPage
                        };
                        File.WriteAllText(
                            Path.Combine(destino, "atsuki_meta.json"),
                            JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

                        _extensiones.Cargar();
                        _extensionesChromeCargadas = false;
                        webView.CoreWebView2.PostWebMessageAsString("extensiones:" + _extensiones.ToJson());
                        SincronizarExtensionesSidebar();
                    }));
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
                else if (msg.StartsWith("wallpaper:set:"))
                {
                    string url = msg.Substring("wallpaper:set:".Length);
                    _ = Task.Run(async () => {
                        try {
                            // Descargar imagen a carpeta de perfil
                            string ext      = System.IO.Path.GetExtension(new Uri(url).AbsolutePath);
                            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                            string destino  = System.IO.Path.Combine(_carpetaPerfil, "wallpaper" + ext);

                            using var http   = new System.Net.Http.HttpClient();
                            var bytes        = await http.GetByteArrayAsync(url);
                            await System.IO.File.WriteAllBytesAsync(destino, bytes);

                            // Guardar como fondo activo y propagar
                            string fondoPath = System.IO.Path.Combine(_carpetaPerfil, "fondo.txt");
                            await System.IO.File.WriteAllTextAsync(fondoPath, destino);

                            Dispatcher.Invoke(() => {
                                foreach (var tab in _tabs)
                                    tab.CoreWebView2?.PostWebMessageAsString("fondo:" + destino);
                            });
                        } catch { }
                    });
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

                            var doc       = JsonSerializer.Deserialize<JsonElement>(json);
                            string ultima = doc.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
                            string url    = doc.TryGetProperty("url",     out var u) ? u.GetString() ?? "" : "";
                            string notas  = doc.TryGetProperty("notas",   out var n) ? n.GetString() ?? "" : "";

                            bool esVersionPreview = AppVersion.Contains("-prev") ||
                                                    AppVersion.Contains("-beta") ||
                                                    AppVersion.Contains("-alpha");

                            // Datos del canal preview
                            string prevVersion = "";
                            string prevUrl     = "";
                            string prevNotas   = "";
                            bool   hayPrev     = false;

                            if (doc.TryGetProperty("preview", out var prev))
                            {
                                prevVersion = prev.TryGetProperty("version", out var pv) ? pv.GetString() ?? "" : "";
                                prevUrl     = prev.TryGetProperty("url",     out var pu) ? pu.GetString() ?? "" : "";
                                prevNotas   = prev.TryGetProperty("notas",   out var pn) ? pn.GetString() ?? "" : "";
                                hayPrev     = EsVersionMasNueva(prevVersion, AppVersion);
                            }

                            bool hayUpdateEstable = EsVersionMasNueva(ultima, AppVersion);

                            // Construir respuesta
                            object respuesta;
                            if (esVersionPreview)
                            {
                                // Preview: SOLO canal preview, ignorar estable
                                respuesta = new
                                {
                                    esPreview  = true,
                                    hayUpdate  = hayPrev,
                                    version    = prevVersion,
                                    url        = prevUrl,
                                    notas      = prevNotas
                                };
                            }
                            else
                            {
                                // Estable: canal estable siempre
                                // Si tiene previews activado y hay preview más nueva, mostrar también
                                respuesta = new
                                {
                                    esPreview        = false,
                                    hayUpdate        = hayUpdateEstable,
                                    version          = ultima,
                                    url,
                                    notas,
                                    // Preview como card separada (solo si está activado)
                                    mostrarPreview   = _recibirPreviews,
                                    hayUpdatePreview = _recibirPreviews && hayPrev,
                                    versionPreview   = prevVersion,
                                    urlPreview       = prevUrl,
                                    notasPreview     = prevNotas
                                };
                            }
                            string responseJson = JsonSerializer.Serialize(respuesta);
                            Dispatcher.Invoke(() =>
                                webView.CoreWebView2?.PostWebMessageAsString("update:" + responseJson));
                        }
                        catch (Exception ex)
                        {
                            var err = JsonSerializer.Serialize(new { error = ex.Message });
                            Dispatcher.Invoke(() =>
                                webView.CoreWebView2?.PostWebMessageAsString("update:" + err));
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
                    if (_musicaPlayerInternoActivo) return;

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
                                    else                                                   _musicaFuenteCache = "AtsukiBrowser";
                                }
                                else
                                    _musicaFuenteCache = appId.Length > 20 ? "AtsukiBrowser" : appId;
                            }

                            // ── Imagen — reintentar si cambió canción O si aún no tenemos imagen ──
                            bool cancionNueva = titulo != _musicaUltimoTitulo;
                            if (cancionNueva)
                            {
                                _musicaUltimoTitulo = titulo;
                                _musicaImagenCache  = "";
                            }

                            if (string.IsNullOrEmpty(_musicaImagenCache))
                            {
                                // 1. Intentar thumbnail del SMTC
                                try
                                {
                                    var thumb = info.Thumbnail;
                                    if (thumb != null)
                                    {
                                        var thumbStream = await thumb.OpenReadAsync();
                                        if (thumbStream.Size > 0)
                                        {
                                            using var reader = new Windows.Storage.Streams.DataReader(thumbStream);
                                            await reader.LoadAsync((uint)thumbStream.Size);
                                            var bytes = new byte[thumbStream.Size];
                                            reader.ReadBytes(bytes);
                                            _musicaImagenCache = "data:image/png;base64," +
                                                Convert.ToBase64String(bytes);
                                        }
                                    }
                                }
                                catch { }

                                // 2. Fallback: thumbnail de YouTube por videoId
                                if (string.IsNullOrEmpty(_musicaImagenCache))
                                {
                                    try
                                    {
                                        string? videoId = null;
                                        await Dispatcher.InvokeAsync(() =>
                                        {
                                            foreach (var tab in _tabs)
                                            {
                                                string u = tab.Source?.ToString() ?? "";
                                                if (u.Contains("youtube.com"))
                                                {
                                                    var uri = new Uri(u);
                                                    var query = uri.Query.TrimStart('?')
                                                        .Split('&')
                                                        .Select(p => p.Split('='))
                                                        .Where(p => p.Length == 2)
                                                        .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
                                                    if (query.TryGetValue("v", out var vid) && !string.IsNullOrEmpty(vid))
                                                    {
                                                        videoId = vid;
                                                        break;
                                                    }
                                                }
                                            }
                                        });

                                        if (!string.IsNullOrEmpty(videoId))
                                        {
                                            string[] calidades = { "maxresdefault", "hqdefault", "mqdefault" };
                                            foreach (var cal in calidades)
                                            {
                                                try
                                                {
                                                    var imgBytes = await _httpClient.GetByteArrayAsync(
                                                        $"https://i.ytimg.com/vi/{videoId}/{cal}.jpg");
                                                    if (imgBytes.Length > 5000)
                                                    {
                                                        _musicaImagenCache = "data:image/jpeg;base64," +
                                                            Convert.ToBase64String(imgBytes);
                                                        _musicaFuenteCache = "YouTube";
                                                        break;
                                                    }
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                    catch { }
                                }
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
                    var doc2 = System.Text.Json.JsonDocument.Parse(json).RootElement;
                    if (doc2.TryGetProperty("suspender_media", out var sm))
                        _suspenderMediaEnBackground = sm.GetBoolean();
                }
                else if (msg == "get:intervalo_suspension")
                {
                    webView.CoreWebView2.PostWebMessageAsString("intervalo_suspension:" + _intervaloSuspension);
                }
                else if (msg.StartsWith("set:intervalo_suspension:"))
                {
                    if (int.TryParse(msg.Substring("set:intervalo_suspension:".Length), out int val))
                    {
                        _intervaloSuspension = val;
                        // Guardar en perf.json
                        string perfPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "AtsukiBrowser", "perf.json");
                        var cfg = File.Exists(perfPath)
                            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(perfPath)) ?? new()
                            : new Dictionary<string, JsonElement>();
                        // Actualizar y reserializar
                        var dict = cfg.ToDictionary(k => k.Key, k => (object?)k.Value.ToString());
                        dict["intervalo_suspension"] = val;
                        File.WriteAllText(perfPath, JsonSerializer.Serialize(dict));
                    }
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
                        _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:" + cmd);
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
                else if (msg == "get:inicio")
                {
                    string path = Path.Combine(_carpetaPerfil, "inicio.json");
                    string json = File.Exists(path) ? File.ReadAllText(path) : "{\"modo\":\"nuevatab\",\"url\":\"\"}";
                    webView.CoreWebView2.PostWebMessageAsString("inicio:" + json);
                }
                else if (msg.StartsWith("set:inicio:"))
                {
                    string json = msg.Substring("set:inicio:".Length);
                    File.WriteAllText(Path.Combine(_carpetaPerfil, "inicio.json"), json);
                }
                else if (msg == "get:confirmar_cerrar")
                {
                    string path = Path.Combine(_carpetaPerfil, "confirmar_cerrar.txt");
                    bool val = File.Exists(path) && File.ReadAllText(path).Trim() == "true";
                    webView.CoreWebView2.PostWebMessageAsString("confirmar_cerrar:" + val.ToString().ToLower());
                }
                else if (msg.StartsWith("set:confirmar_cerrar:"))
                {
                    string val = msg.Substring("set:confirmar_cerrar:".Length);
                    File.WriteAllText(Path.Combine(_carpetaPerfil, "confirmar_cerrar.txt"), val);
                    _confirmarCerrar = val == "true";
                }
                else if (msg == "get:intervalo_cache")
                {
                    webView.CoreWebView2.PostWebMessageAsString("intervalo_cache:" + _intervaloCacheMinutos);
                }
                else if (msg.StartsWith("set:intervalo_cache:"))
                {
                    if (int.TryParse(msg.Substring("set:intervalo_cache:".Length), out int val))
                    {
                        _intervaloCacheMinutos = val;
                        // Reiniciar el timer con el nuevo intervalo
                        if (_perfLimpiarCache && _cacheTimer != null)
                        {
                            _cacheTimer.Stop();
                            _cacheTimer.Interval = val * 60 * 1000;
                            _cacheTimer.Start();
                        }
                        // Guardar en perf.json
                        GuardarPerfJson("intervalo_cache", val);
                    }
                }
                else if (msg == "get:capturas")
                {
                    var archivos = Directory.Exists(_carpetaCapturas)
                        ? (IEnumerable<object>)Directory.GetFiles(_carpetaCapturas, "*.png")
                            .Select(f => (object)new {
                                ruta       = f.Replace('\\', '/'),
                                nombre     = Path.GetFileName(f),
                                fecha      = File.GetCreationTime(f).ToString("o"),
                                tamaño     = new FileInfo(f).Length,
                                url_origen = ""
                            }).OrderByDescending(f => ((dynamic)f).fecha)
                        : Enumerable.Empty<object>();

                    var json = JsonSerializer.Serialize(new { capturas = archivos, carpeta = _carpetaCapturas });
                    Dispatcher.Invoke(() => webView.CoreWebView2.PostWebMessageAsString("capturas:" + json));
                }
                else if (msg.StartsWith("capturas:cargar:"))
                {
                    string ruta = msg.Substring("capturas:cargar:".Length);
                    _ = Task.Run(async () => {
                        try {
                            var bytes = await File.ReadAllBytesAsync(ruta);
                            var b64 = "data:image/png;base64," + Convert.ToBase64String(bytes);
                            Dispatcher.Invoke(() => webView.CoreWebView2.PostWebMessageAsString("capturas:dataurl:" + b64));
                        } catch { }
                    });
                }
                else if (msg.StartsWith("capturas:eliminar:"))
                {
                    string ruta = msg.Substring("capturas:eliminar:".Length);
                    try { if (File.Exists(ruta)) File.Delete(ruta); } catch { }
                    // reenviar lista actualizada
                    webView.CoreWebView2.PostWebMessageAsString("get:capturas"); // reutiliza el handler
                }
                else if (msg == "capturas:carpeta:elegir")
                {
                    Dispatcher.Invoke(() => {
                        var dlg = new Microsoft.Win32.OpenFolderDialog
                        {
                            Title = "Carpeta de capturas",
                            InitialDirectory = _carpetaCapturas
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            _carpetaCapturas = dlg.FolderName;
                            File.WriteAllText(Path.Combine(_carpetaPerfil, "capturas_carpeta.txt"), _carpetaCapturas);
                            webView.CoreWebView2.PostWebMessageAsString("capturas:carpeta:" + _carpetaCapturas);
                        }
                    });
                }
                else if (msg.StartsWith("capturas:guardar:"))
                {
                    // formato: capturas:guardar:RUTA:data:image/png;base64,...
                    var sin = msg.Substring("capturas:guardar:".Length);
                    var sepIdx = sin.IndexOf(":data:");
                    if (sepIdx > 0)
                    {
                        string ruta   = sin.Substring(0, sepIdx);
                        string base64 = sin.Substring(sepIdx + 6).Replace("data:image/png;base64,", "");
                        try { File.WriteAllBytes(ruta, Convert.FromBase64String(base64)); } catch { }
                    }
                }
                else if (msg.StartsWith("capturas:guardar-como:"))
                {
                    string base64 = msg.Substring("capturas:guardar-como:".Length)
                                    .Replace("data:image/png;base64,", "");
                    Dispatcher.Invoke(() => {
                        var dlg = new Microsoft.Win32.SaveFileDialog
                            { Title = "Guardar captura", Filter = "PNG|*.png", DefaultExt = ".png" };
                        if (dlg.ShowDialog() == true)
                            try { File.WriteAllBytes(dlg.FileName, Convert.FromBase64String(base64)); } catch { }
                    });
                }
                else if (msg == "capturas:limpiar")
                {
                    if (Directory.Exists(_carpetaCapturas))
                        foreach (var f in Directory.GetFiles(_carpetaCapturas, "*.png"))
                            try { File.Delete(f); } catch { }
                }
                else if (msg == "capturas:capturar")
                {
                    // Reutiliza la lógica de captura existente del sidebar
                    Dispatcher.Invoke(async () => {
                        if (_activeTab < 0 || _tabs[_activeTab].CoreWebView2 == null) return;
                        try {
                            string nombre = $"captura_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                            string ruta   = Path.Combine(_carpetaCapturas, nombre);
                            Directory.CreateDirectory(_carpetaCapturas);
                            using var stream = new MemoryStream();
                            await _tabs[_activeTab].CoreWebView2.CapturePreviewAsync(
                                CoreWebView2CapturePreviewImageFormat.Png, stream);
                            File.WriteAllBytes(ruta, stream.ToArray());
                            webView.CoreWebView2.PostWebMessageAsString("get:capturas");
                        } catch { }
                    });
                }
                else if (msg == "docs:recientes:cargar")
                {
                    var ruta = System.IO.Path.Combine(_carpetaPerfil, "docs_recientes.json");
                    var json = File.Exists(ruta) ? File.ReadAllText(ruta) : "[]";
                    Dispatcher.Invoke(() => webView.CoreWebView2.PostWebMessageAsString("docs:recientes:" + json));
                }
                else if (msg.StartsWith("docs:recientes:guardar:"))
                {
                    var json = msg.Substring("docs:recientes:guardar:".Length);
                    var ruta = System.IO.Path.Combine(_carpetaPerfil, "docs_recientes.json");
                    try { File.WriteAllText(ruta, json); } catch { }
                }
                else if (msg == "docs:abrir")
                {
                    Dispatcher.Invoke(() =>
                    {
                        var dlg = new Microsoft.Win32.OpenFileDialog
                        {
                            Title  = "Abrir documento",
                            Filter = "Texto|*.txt|HTML|*.html;*.htm|Markdown|*.md|Word|*.docx|Todos|*.*"
                        };
                        if (dlg.ShowDialog(Application.Current.MainWindow) != true) return;

                        var ruta   = dlg.FileName;
                        var nombre = System.IO.Path.GetFileName(ruta);
                        var ext    = System.IO.Path.GetExtension(ruta).ToLower();
                        string html;

                        if (ext == ".docx")
                            html = LeerDocx(ruta);
                        else
                        {
                            var texto = File.ReadAllText(ruta);
                            html = ext switch
                            {
                                ".html" or ".htm" => texto,
                                ".md"             => ConvertirMarkdownAHtml(texto),
                                _                 => "<pre>" + System.Web.HttpUtility.HtmlEncode(texto) + "</pre>"
                            };
                        }

                        var payload = JsonSerializer.Serialize(new { nombre, ruta, html });
                        webView.CoreWebView2.PostWebMessageAsString("docs:contenido:" + payload);
                    });
                }
                else if (msg.StartsWith("docs:abrir-ruta:"))
                {
                    var ruta = msg.Substring("docs:abrir-ruta:".Length);
                    if (!File.Exists(ruta)) {
                        Dispatcher.Invoke(() => webView.CoreWebView2.PostWebMessageAsString("docs:error:no-existe:" + ruta));
                        return;
                    }
                    _ = Task.Run(() => {
                        try {
                            var nombre = System.IO.Path.GetFileName(ruta);
                            var ext    = System.IO.Path.GetExtension(ruta).ToLower();
                            string html;
                            if (ext == ".docx") {
                                html = LeerDocx(ruta);
                            } else {
                                var texto = File.ReadAllText(ruta);
                                html = ext switch {
                                    ".html" or ".htm" => texto,
                                    ".md"             => ConvertirMarkdownAHtml(texto),
                                    _                 => "<pre>" + System.Web.HttpUtility.HtmlEncode(texto) + "</pre>"
                                };
                            }
                            var payload = JsonSerializer.Serialize(new { nombre, ruta, html });
                            Dispatcher.Invoke(() => webView.CoreWebView2.PostWebMessageAsString("docs:contenido:" + payload));
                        } catch { }
                    });
                }
                else if (msg.StartsWith("docs:guardar:"))
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(msg.Substring("docs:guardar:".Length));
                    _ = Task.Run(() => {
                        try {
                            var ruta  = data.GetProperty("ruta").GetString();
                            var ext   = System.IO.Path.GetExtension(ruta).ToLower();
                            var html  = data.GetProperty("html").GetString();
                            var texto = data.GetProperty("texto").GetString();
                            switch (ext) {
                                case ".html": case ".htm":
                                    File.WriteAllText(ruta, html); break;
                                case ".docx":
                                    Dispatcher.Invoke(() => GuardarDocx(ruta, html)); break;
                                default:
                                    File.WriteAllText(ruta, texto); break;
                            }
                        } catch { }
                    });
                }
                else if (msg.StartsWith("docs:guardar-como:"))
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(msg.Substring("docs:guardar-como:".Length));
                    Dispatcher.Invoke(() => {
                        var nombre = data.GetProperty("nombre").GetString();
                        var html   = data.GetProperty("html").GetString();
                        var texto  = data.GetProperty("texto").GetString();
                        var dlg = new Microsoft.Win32.SaveFileDialog
                        {
                            Title      = "Guardar documento",
                            FileName   = nombre,
                            Filter     = "Texto|*.txt|HTML|*.html|Markdown|*.md|Word|*.docx|Todos|*.*",
                            DefaultExt = ".txt"
                        };
                        if (dlg.ShowDialog() != true) return;
                        var ruta = dlg.FileName;
                        var ext  = System.IO.Path.GetExtension(ruta).ToLower();
                        switch (ext) {
                            case ".html": case ".htm":
                                File.WriteAllText(ruta, html); break;
                            case ".docx":
                                GuardarDocx(ruta, html); break;
                            default:
                                File.WriteAllText(ruta, texto); break;
                        }
                        var payload = JsonSerializer.Serialize(new { ruta });
                        webView.CoreWebView2.PostWebMessageAsString("docs:guardado:" + payload);
                    });
                }
                else if (msg.StartsWith("docs:exportar:"))
                {
                    var data = JsonSerializer.Deserialize<JsonElement>(msg.Substring("docs:exportar:".Length));
                    Dispatcher.Invoke(() => {
                        var nombre = data.GetProperty("nombre").GetString();
                        var html   = data.GetProperty("html").GetString();
                        var dlg = new Microsoft.Win32.SaveFileDialog
                        {
                            Title      = "Exportar documento",
                            FileName   = nombre,
                            Filter     = "HTML|*.html|Texto|*.txt|Word|*.docx",
                            DefaultExt = ".html"
                        };
                        if (dlg.ShowDialog() != true) return;
                        var ruta = dlg.FileName;
                        var ext  = System.IO.Path.GetExtension(ruta).ToLower();
                        switch (ext) {
                            case ".html": case ".htm":
                                File.WriteAllText(ruta, html); break;
                            case ".docx":
                                GuardarDocx(ruta, html); break;
                            default:
                                File.WriteAllText(ruta, System.Web.HttpUtility.HtmlDecode(
                                    System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", ""))); break;
                        }
                        File.WriteAllText(ruta, ext == ".html" ? html : System.Web.HttpUtility.HtmlDecode(
                            System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", "")));
                        webView.CoreWebView2.PostWebMessageAsString("docs:guardado:" + JsonSerializer.Serialize(new { ruta }));
                    });
                }
                else if (msg == "notes:cargar")
                {
                    string path = Path.Combine(_carpetaPerfil, "notes.json");
                    string json = File.Exists(path)
                        ? File.ReadAllText(path)
                        : "{\"notas\":[],\"etiquetas\":[]}";

                    try
                    {
                        webView.CoreWebView2.PostWebMessageAsString("notes:debug:" + path + "|len:" + json.Length);
                        webView.CoreWebView2.PostWebMessageAsString("notes:datos:" + json);
                    }
                    catch (Exception ex)
                    {
                        webView.CoreWebView2.PostWebMessageAsString("notes:debug:ERROR:" + ex.Message);
                    }
                }
                else if (msg.StartsWith("notes:guardar:"))
                {
                    string json = msg.Substring("notes:guardar:".Length);
                    File.WriteAllText(Path.Combine(_carpetaPerfil, "notes.json"), json);
                }
                else if (msg.StartsWith("notes:chunk:"))
                {
                    // formato: notes:chunk:índice:total:datos
                    var partes = msg.Split(':', 5);
                    int idx   = int.Parse(partes[2]);
                    int total = int.Parse(partes[3]);
                    string datos = partes[4];

                    if (!_notesChunks.ContainsKey(webView))
                        _notesChunks[webView] = new System.Text.StringBuilder();

                    _notesChunks[webView].Append(datos);

                    if (idx == total - 1)
                    {
                        string json = _notesChunks[webView].ToString();
                        _notesChunks.Remove(webView);
                        File.WriteAllText(Path.Combine(_carpetaPerfil, "notes.json"), json);
                    }
                }
                else if (msg.StartsWith("nuevatab:layout:"))
                {
                    string val = msg.Substring("nuevatab:layout:".Length);
                    File.WriteAllText(Path.Combine(_carpetaPerfil, "nuevatab_layout.txt"), val);

                    // Actualizar _urlNuevaTab en caliente
                    string res2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
                    _urlNuevaTab = val == "v2"
                        ? "file:///" + Path.Combine(res2, "NuevaTabV2.html").Replace("\\", "/")
                        : "file:///" + Path.Combine(res2, "NuevaTab.html").Replace("\\", "/");

                    // Notificar a todas las nuevaTab abiertas para que cambien en caliente
                    foreach (var tab in _tabs)
                        tab.CoreWebView2?.PostWebMessageAsString("nuevatab:layout:" + val);
                }
                else if (msg == "get:v2:posiciones")
                {
                    string path = Path.Combine(_carpetaPerfil, "v2_posiciones.json");
                    string val = File.Exists(path) ? File.ReadAllText(path).Trim() : "{}";
                    webView.CoreWebView2.PostWebMessageAsString("v2:posiciones:" + val);
                }
                else if (msg.StartsWith("v2:posiciones:"))
                {
                    string val = msg.Substring("v2:posiciones:".Length);
                    File.WriteAllText(Path.Combine(_carpetaPerfil, "v2_posiciones.json"), val);
                }
                else if (msg.StartsWith("wallhaven:search:"))
                {
                    string queryParams = msg.Substring("wallhaven:search:".Length);
                    _ = Task.Run(async () => {
                        try {
                            using var http = new System.Net.Http.HttpClient();
                            http.DefaultRequestHeaders.Add("User-Agent", "AtsukiBrowser/1.0");
                            var url      = "https://wallhaven.cc/api/v1/search?" + queryParams;
                            var response = await http.GetStringAsync(url);
                            Dispatcher.Invoke(() =>
                                webView.CoreWebView2.PostWebMessageAsString("wallhaven:results:" + response));
                        } catch (Exception ex) {
                            Dispatcher.Invoke(() =>
                                webView.CoreWebView2.PostWebMessageAsString("wallhaven:error:" + ex.Message));
                        }
                    });
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

                // Actualizar label solo si es la tab activa
                if (idx == _activeTab)
                    Dispatcher.Invoke(ActualizarZoomLabel);
            };

            webView.Source = new Uri(url);
            webView.NavigationCompleted += async (s, e) =>
            {
                if (!e.IsSuccess) return;
                Dispatcher.Invoke(() =>
                {
                    int idx = _tabs.IndexOf(webView);
                    if (idx < 0) return;

                    string url    = webView.Source?.ToString() ?? "";
                    string titulo = webView.CoreWebView2?.DocumentTitle ?? "Nueva pestaña";
                    if (string.IsNullOrWhiteSpace(titulo)) titulo = "Nueva pestaña";

                    if (idx == _activeTab)
                    {
                        _ignorarTextChanged = true;
                        UrlBar.Text = url;
                        _ignorarTextChanged = false;
                        _ignorarGotFocus = false;
                    }

                    if (idx < _tabButtons.Count && _tabButtons[idx].Tag is TextBlock label)  // ← guarda añadida
                        label.Text = titulo;

                    _historial.Agregar(url, titulo);
                    ActualizarEstrellaFavorito();
                });

                webView.CoreWebView2?.PostWebMessageAsString("tema:" + _temas.ToJson());
                webView.CoreWebView2?.PostWebMessageAsString("version:" + AppVersion);
                await Task.Delay(500);
                webView.CoreWebView2?.PostWebMessageAsString("previews:" + (_recibirPreviews ? "true" : "false"));
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
                if (urlActual.Contains("AtsukiNotes.html"))
                {
                    string notesPath = Path.Combine(_carpetaPerfil, "notes.json");
                    string notesJson = File.Exists(notesPath)
                        ? File.ReadAllText(notesPath)
                        : "{\"notas\":[],\"etiquetas\":[]}";
                    
                    const int CHUNK = 512 * 1024;
                    if (notesJson.Length <= CHUNK)
                        webView.CoreWebView2?.PostWebMessageAsString("notes:datos:" + notesJson);
                    else
                    {
                        int total = (int)Math.Ceiling((double)notesJson.Length / CHUNK);
                        for (int i = 0; i < total; i++)
                        {
                            string chunk = notesJson.Substring(i * CHUNK, Math.Min(CHUNK, notesJson.Length - i * CHUNK));
                            webView.CoreWebView2?.PostWebMessageAsString($"notes:chunk-load:{i}:{total}:{chunk}");
                        }
                    }
                }
                else if (urlActual.Contains("AtsukiDocs.html"))
                {
                    string recPath = Path.Combine(_carpetaPerfil, "docs_recientes.json");
                    string recJson = File.Exists(recPath) ? File.ReadAllText(recPath) : "[]";
                    webView.CoreWebView2?.PostWebMessageAsString("docs:recientes:" + recJson);
                }
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

            webView.NavigationCompleted += (s, e) =>
            {
                if (webView.Visibility == Visibility.Hidden)
                    Dispatcher.Invoke(() => webView.Visibility = Visibility.Visible);
            };

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
            ToolTipService.SetIsEnabled(btn, false);

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

            MenuItem CrearMenuItem(string texto, string atajo, Action accion)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txtHeader = new TextBlock
                {
                    Text = texto,
                    Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txtHeader, 0);

                var txtAtajo = new TextBlock
                {
                    Text = atajo,
                    Foreground = new SolidColorBrush(Color.FromArgb(100, 180, 160, 255)),
                    FontSize = 11,
                    Margin = new Thickness(24, 0, 0, 0),
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
                item.Click += (s, e) => accion();
                return item;
            }

            ctxMenu.Items.Add(CrearMenuItem("📄  Duplicar pestaña", "Ctrl+D", () =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx >= 0) AbrirNuevaTab(_tabs[idx].Source?.ToString() ?? _urlNuevaTab);
            }));

            ctxMenu.Items.Add(new Separator
                { Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)) });

            ctxMenu.Items.Add(CrearMenuItem("🗂  Añadir a grupo", "", () =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx >= 0) AñadirTabAGrupo(idx);
            }));

            ctxMenu.Items.Add(new Separator
            {
                Background = new SolidColorBrush(Color.FromRgb(42, 26, 78))
            });

            ctxMenu.Items.Add(CrearMenuItem("💤  Hibernar", "", () =>
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

            ctxMenu.Items.Add(CrearMenuItem("▶  Reactivar", "", () =>
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

            ctxMenu.Items.Add(CrearMenuItem("✕  Cerrar pestaña", "Ctrl+W", () =>
            {
                int idx = _tabButtons.IndexOf(btn);
                if (idx >= 0) CerrarTab(idx);
            }));

            btn.ContextMenu = ctxMenu;
            btn.MouseRightButtonUp += (s, e) =>
            {
                ctxMenu.PlacementTarget = btn;
                ctxMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                ctxMenu.IsOpen = true;
                e.Handled = true;
            };
            btn.ContextMenuOpening += (s, e) => e.Handled = true;

            // ── Drag & drop estilo Chrome ────────────────────────
            Point _dragStart = default;
            bool _dragging = false;
            double _dragOffsetX = 0;

            btn.PreviewMouseLeftButtonDown += (s, e) =>
            {
                var src = e.OriginalSource as DependencyObject;
                while (src != null && src != btn)
                {
                    if (src == closeBtn) return;
                    src = System.Windows.Media.VisualTreeHelper.GetParent(src);
                }
                _dragStart   = e.GetPosition(TabStrip);
                _dragOffsetX = e.GetPosition(btn).X;
                _dragBtn     = btn;
                _dragging    = false;
                btn.CaptureMouse();
            };

            btn.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || _dragBtn != btn) return;

                var pos = e.GetPosition(TabStrip);
                if (!_dragging && Math.Abs(pos.X - _dragStart.X) > 6)
                {
                    _dragging = true;
                    btn.RenderTransform = new TranslateTransform();
                    Panel.SetZIndex(btn, 999);
                }

                if (!_dragging) return;

                var tt = (TranslateTransform)btn.RenderTransform;
                int fromIdx = _tabButtons.IndexOf(btn);
                double anchoTab = btn.ActualWidth;

                // Limitar el movimiento al rango de tabs existentes
                double minX = -fromIdx * anchoTab;
                double maxX = (_tabButtons.Count - 1 - fromIdx) * anchoTab;
                tt.X = Math.Max(minX, Math.Min(maxX, pos.X - _dragStart.X));

                // Empujar tabs vecinas
                for (int i = 0; i < _tabButtons.Count; i++)
                {
                    var t = _tabButtons[i];
                    if (t == btn) continue;

                    if (t.RenderTransform is not TranslateTransform ttt)
                    {
                        t.RenderTransform = new TranslateTransform();
                        ttt = (TranslateTransform)t.RenderTransform;
                    }

                    double btnCentro = fromIdx * anchoTab + tt.X + anchoTab / 2;
                    double tOrigX    = i * anchoTab;

                    double targetX = 0;
                    if (i < fromIdx && btnCentro < tOrigX + anchoTab)
                        targetX = anchoTab;
                    else if (i > fromIdx && btnCentro > tOrigX)
                        targetX = -anchoTab;

                    var anim = new System.Windows.Media.Animation.DoubleAnimation(
                        ttt.X, targetX, TimeSpan.FromMilliseconds(120))
                    {
                        EasingFunction = new System.Windows.Media.Animation.CubicEase
                            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    ttt.BeginAnimation(TranslateTransform.XProperty, anim);
                }

                e.Handled = true;
            };

            btn.PreviewMouseLeftButtonUp += (s, e) =>
            {
                bool fueDrag = _dragging;
                _dragging = false;
                _dragBtn  = null;
                btn.ReleaseMouseCapture();

                if (!fueDrag)
                {
                    int idx = _tabButtons.IndexOf(btn);
                    if (idx >= 0) ActivarTab(idx);
                    return;
                }

                var pos = e.GetPosition(TabStrip);
                double anchoTab = btn.ActualWidth;
                int fromIdx = _tabButtons.IndexOf(btn);

                // Calcular destino clampado al rango real
                int toIdx = Math.Max(0, Math.Min(_tabButtons.Count - 1,
                    (int)Math.Round(pos.X / anchoTab)));

                // Limpiar transforms
                foreach (var t in _tabButtons)
                {
                    if (t.RenderTransform is TranslateTransform ttt)
                    {
                        ttt.BeginAnimation(TranslateTransform.XProperty, null);
                        ttt.X = 0;
                    }
                    t.RenderTransform = new TranslateTransform(0, 0);
                }
                Panel.SetZIndex(btn, 0);

                if (fromIdx != toIdx)
                    MoverTab(fromIdx, toIdx);
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

        private void ReiniciarTimerSuspension(int tabIdx)
        {
            if (!_perfSuspenderTabs || _intervaloSuspension <= 0) return;

            // Cancelar timer existente si hay
            if (_suspensionTimers.TryGetValue(tabIdx, out var timerViejo))
            {
                timerViejo.Stop();
                _suspensionTimers.Remove(tabIdx);
            }

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(_intervaloSuspension)
            };
            timer.Tick += async (s, e) =>
            {
                timer.Stop();
                _suspensionTimers.Remove(tabIdx);
                if (tabIdx == _activeTab) return; // no suspender la activa
                if (tabIdx >= _tabs.Count) return;
                var tab = _tabs[tabIdx];
                if (tab.CoreWebView2 == null) return;
                try { await tab.CoreWebView2.TrySuspendAsync(); }
                catch { }
            };
            timer.Start();
            _suspensionTimers[tabIdx] = timer;
        }

        private void BtnNewTab_Click(object sender, RoutedEventArgs e) => AbrirNuevaTab();
        private void ActualizarEstiloTabs()
        {
            int count = _tabButtons.Count;
            if (count == 0) return;

            double espacioDisponible = TabScrollViewer.ActualWidth - 34;
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

                if (btn.Content is Grid sp)
                {
                    var close = sp.Children.OfType<Button>().FirstOrDefault();
                    if (close != null)
                    {
                        close.Visibility = Visibility.Visible;

                        if (!activa)
                        {
                            close.Visibility = btn.IsMouseOver ? Visibility.Visible : Visibility.Collapsed;

                            if (!_tabsHoverInit.Contains(i))
                            {
                                _tabsHoverInit.Add(i);
                                btn.MouseEnter += (s, e) => close.Visibility = Visibility.Visible;
                                btn.MouseLeave += (s, e) =>
                                {
                                    int btnIdx = _tabButtons.IndexOf(btn);
                                    if (btnIdx != _activeTab)
                                        close.Visibility = Visibility.Collapsed;
                                };
                            }
                        }
                    }
                }
                AplicarColorGrupoATab(btn, i);
            }
            RenderizarBotonesGrupo();
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

            // Reiniciar timers de suspensión para tabs inactivas
            for (int i = 0; i < _tabs.Count; i++)
                if (i != index) ReiniciarTimerSuspension(i);
            // Cancelar timer de la tab que se activa
            if (_suspensionTimers.TryGetValue(index, out var t)) { t.Stop(); _suspensionTimers.Remove(index); }

            _activeTab = index;
            _ignorarGotFocus = true;
            string urlTab = _tabs[index].Source?.ToString() ?? "";
            _ignorarTextChanged = true;
            UrlBar.Text = urlTab;
            ActualizarUrlDisplay(urlTab);
            _ignorarTextChanged = false;
            ActualizarEstrellaFavorito();
            _ignorarGotFocus = false;
            ActualizarEstiloTabs();
            ActualizarZoomLabel();
            string urlActiva = _tabs[index].Source?.ToString() ?? "";
            string tituloTab = urlActiva.Contains("NuevaTab.html")    ? "Nueva pestaña" :
                            urlActiva.Contains("Ajustes.html")     ? "Ajustes" :
                            urlActiva.Contains("Favoritos.html")   ? "Favoritos" :
                            urlActiva.Contains("Historial.html")   ? "Historial" :
                            urlActiva.Contains("Descargas.html")   ? "Descargas" :
                            urlActiva.Contains("Perfiles.html")    ? "Perfiles" :
                            _tabs[index].CoreWebView2?.DocumentTitle ?? "";

            // Si el DocumentTitle está vacío o es genérico, leerlo del botón de tab
            // que ya fue actualizado por DocumentTitleChanged
            if (string.IsNullOrEmpty(tituloTab) || tituloTab == "Nueva pestaña")
            {
                if (_tabButtons[index].Tag is TextBlock lbl && !string.IsNullOrEmpty(lbl.Text))
                    tituloTab = lbl.Text;
            }

            this.Title = string.IsNullOrEmpty(tituloTab) || tituloTab == "Nueva pestaña"
                ? "AtsukiBrowser"
                : $"{tituloTab} — AtsukiBrowser";
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

            _activeTab = _tabButtons.IndexOf(btn);
            ActualizarEstiloTabs();
            foreach (var t in _tabButtons)
            {
                if (t.RenderTransform is TranslateTransform tt)
                {
                    tt.BeginAnimation(TranslateTransform.XProperty, null);
                    tt.X = 0;
                }
                t.RenderTransform = new TranslateTransform(0, 0);
            }
        }

        private void CerrarTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;

            // Detener preview timer si apunta a esta tab
            if (_previewTabIdx == index)
            {
                _previewTimer?.Stop();
                _previewTabIdx = -1;
                PopupPreviewTab.IsOpen = false;
            }

            string urlCerrada = _tabs[index].Source?.ToString() ?? "";
            if (!string.IsNullOrEmpty(urlCerrada) && !urlCerrada.StartsWith("file:///"))
                _tabsRecientes.Add(urlCerrada);
            if (_tabsRecientes.Count > 20)
                _tabsRecientes.RemoveAt(0);

            if (_tabs.Count == 1)
                AbrirNuevaTab();

            var webView = _tabs[index];

           // Cancelar timer de la tab cerrada
            if (_suspensionTimers.TryGetValue(index, out var timerSuspension))
            {
                timerSuspension.Stop();
                _suspensionTimers.Remove(index);
            }

            // Desconectar eventos ANTES de remover
            webView.NavigationCompleted -= WebView_NavigationCompleted;
            try { webView.CoreWebView2?.Stop(); } catch { }

            // Remover de UI y listas ANTES de activar nueva tab
            BrowserContainer.Children.Remove(webView);
            TabStrip.Children.Remove(_tabButtons[index]);
            _tabs.RemoveAt(index);
            _tabButtons.RemoveAt(index);

            // Reindexar suspension timers — todos los índices > index bajan uno
            var nuevosTimers = new Dictionary<int, System.Windows.Threading.DispatcherTimer>();
            foreach (var kvp in _suspensionTimers)
            {
                if (kvp.Key < index) nuevosTimers[kvp.Key] = kvp.Value;
                else if (kvp.Key > index) nuevosTimers[kvp.Key - 1] = kvp.Value;
            }
            _suspensionTimers = nuevosTimers;

            // Reindexar tab previews igual
            var nuevasPreviews = new Dictionary<int, System.Windows.Media.Imaging.BitmapImage>();
            foreach (var kvp in _tabPreviews)
            {
                if (kvp.Key < index) nuevasPreviews[kvp.Key] = kvp.Value;
                else if (kvp.Key > index) nuevasPreviews[kvp.Key - 1] = kvp.Value;
            }
            _tabPreviews = nuevasPreviews;

            // Activar nueva tab DESPUÉS de que la lista ya está limpia
            int nuevoIndex = Math.Min(index, _tabs.Count - 1);
            ActivarTab(nuevoIndex);

            // Dispose diferido — ya no está en ninguna lista, es seguro
            _ = Task.Run(async () =>
            {
                await Task.Delay(500); // dar tiempo a que ActivarTab termine
                try { await webView.Dispatcher.InvokeAsync(() => webView.Dispose()); }
                catch { }
            });
            
            // Reindexar hover init
            var nuevosHover = new HashSet<int>();
            foreach (var idx in _tabsHoverInit)
            {
                if (idx < index) nuevosHover.Add(idx);
                else if (idx > index) nuevosHover.Add(idx - 1);
            }
            _tabsHoverInit.Clear();
            foreach (var idx in nuevosHover) _tabsHoverInit.Add(idx);

            ReindexarGrupos(index);
            RenderizarBotonesGrupo();
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
                Dispatcher.Invoke(() => favicon.Visibility = Visibility.Collapsed);
                return;
            }

            // ── Usar caché si ya lo tenemos ──
            if (_faviconCache.TryGetValue(faviconUrl, out var cached))
            {
                Dispatcher.Invoke(() =>
                {
                    if (index >= _tabButtons.Count) return;
                    favicon.Source = cached;
                    favicon.Visibility = Visibility.Visible;
                });
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

                        // Guardar en caché
                        _faviconCache[faviconUrl] = bmp;

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

        private string ConvertirMarkdownAHtml(string md)
        {
            var html = System.Web.HttpUtility.HtmlEncode(md);
            // Headings
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^### (.+)$", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^## (.+)$",  "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
            html = System.Text.RegularExpressions.Regex.Replace(html, @"^# (.+)$",   "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);
            // Bold / italic
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            html = System.Text.RegularExpressions.Regex.Replace(html, @"\*(.+?)\*",     "<em>$1</em>");
            // Line breaks
            html = html.Replace("\n", "<br>");
            return html;
        }

        private string LeerDocx(string ruta)
        {
            using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(ruta, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return "";

            var sb = new System.Text.StringBuilder();
            foreach (var elem in body.Elements())
            {
                if (elem is DocumentFormat.OpenXml.Wordprocessing.Paragraph para)
                {
                    var texto = para.InnerText;
                    // Detectar heading
                    var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                    if (style == "Heading1") sb.AppendLine($"<h1>{System.Web.HttpUtility.HtmlEncode(texto)}</h1>");
                    else if (style == "Heading2") sb.AppendLine($"<h2>{System.Web.HttpUtility.HtmlEncode(texto)}</h2>");
                    else if (style == "Heading3") sb.AppendLine($"<h3>{System.Web.HttpUtility.HtmlEncode(texto)}</h3>");
                    else if (string.IsNullOrWhiteSpace(texto)) sb.AppendLine("<br>");
                    else sb.AppendLine($"<p>{System.Web.HttpUtility.HtmlEncode(texto)}</p>");
                }
                else if (elem is DocumentFormat.OpenXml.Wordprocessing.Table table)
                {
                    sb.AppendLine("<table border='1' cellpadding='4' style='border-collapse:collapse'>");
                    foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                    {
                        sb.AppendLine("<tr>");
                        foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                            sb.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(cell.InnerText)}</td>");
                        sb.AppendLine("</tr>");
                    }
                    sb.AppendLine("</table>");
                }
            }
            return sb.ToString();
        }

        private void GuardarDocx(string ruta, string htmlContenido)
        {
            // Extraer texto plano del HTML para el docx básico
            var texto = System.Text.RegularExpressions.Regex.Replace(htmlContenido, "<.*?>", "");
            texto = System.Web.HttpUtility.HtmlDecode(texto);

            using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
                ruta, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());

            foreach (var linea in texto.Split('\n'))
            {
                var para = body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());
                var run  = para.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run());
                run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(linea.Trim()));
            }

            mainPart.Document.Save();
        }

        private string ResolverMsgChrome(string valor, string carpeta)
        {
            if (!valor.StartsWith("__MSG_")) return valor;
            string key = valor.Replace("__MSG_", "").Replace("__", "");
            foreach (var locale in new[] { "es", "en", "en_US" })
            {
                string path = Path.Combine(carpeta, "_locales", locale, "messages.json");
                if (!File.Exists(path)) continue;
                try
                {
                    var doc = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
                    if (doc.TryGetProperty(key, out var msg) &&
                        msg.TryGetProperty("message", out var m))
                        return m.GetString() ?? valor;
                }
                catch { }
            }
            return valor;
        }
    }
}