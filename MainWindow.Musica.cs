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

        private void InicializarControlsMusica()
        {

            BtnTabCanciones.Click += (s, e) =>
            {
                PanelTabCanciones.Visibility = Visibility.Visible;
                PanelTabPlaylists.Visibility = Visibility.Collapsed;
                BtnTabCanciones.Background = new SolidColorBrush(Color.FromRgb(42, 26, 90));
                BtnTabCanciones.Foreground = Brushes.White;
                BtnTabPlaylists.Background = new SolidColorBrush(Color.FromRgb(26, 10, 58));
                BtnTabPlaylists.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 119));
            };

            BtnTabPlaylists.Click += (s, e) =>
            {
                PanelTabCanciones.Visibility = Visibility.Collapsed;
                PanelTabPlaylists.Visibility = Visibility.Visible;
                BtnTabPlaylists.Background = new SolidColorBrush(Color.FromRgb(42, 26, 90));
                BtnTabPlaylists.Foreground = Brushes.White;
                BtnTabCanciones.Background = new SolidColorBrush(Color.FromRgb(26, 10, 58));
                BtnTabCanciones.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 119));
                RenderizarPanelPlaylists();
            };
            BtnMusicaPlay.Click += (s, e) =>
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString(
                    _musicaReproduciendo ? "player:pause" : "player:play");

            BtnMusicaAnterior.Click += (s, e) =>
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:anterior");

            BtnMusicaSiguiente.Click += (s, e) =>
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:siguiente");

            // ── Botón aleatorio ──
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
                if (!string.IsNullOrEmpty(_carpetaPerfil))
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

            // ── Crear playlist ──
            BtnMusicaNuevaPlaylist.Click += (s, e) => CrearNuevaPlaylist();

            // ── Exportar ──
            BtnMusicaExportar.Click += (s, e) => ExportarMusica();

            // ── Importar ──
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
                        string url    = "file:///" + archivo.Replace("\\", "/");
                        string imagen = "";
                        string autor  = "";

                        try
                        {
                            var tag = TagLib.File.Create(archivo);
                            if (!string.IsNullOrEmpty(tag.Tag.Title))
                                nombre = tag.Tag.Title;
                            autor = tag.Tag.FirstPerformer ?? "";
                            if (tag.Tag.Pictures.Length > 0)
                            {
                                var pic = tag.Tag.Pictures[0];
                                imagen = $"data:{pic.MimeType};base64,{Convert.ToBase64String(pic.Data.Data)}";
                            }
                        }
                        catch { }

                        _playlists[_playlistActiva].canciones.Add(new MusicaCancion 
                        { 
                            titulo = nombre, 
                            url    = url, 
                            imagen = imagen, 
                            autor  = autor 
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
            BtnMusicaContinuar.Click += (s, e) =>
            {
                _musicaContinuar = !_musicaContinuar;
                BtnMusicaContinuar.Content    = _musicaContinuar ? "●" : "○";
                BtnMusicaContinuar.Foreground = _musicaContinuar
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromRgb(85, 85, 119));
                GuardarMusicaConfig();
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
            // ── Ajustes ──
            BtnMusicaAjustes.Click += (s, e) =>
            {
                MusicaAjustesPanel.Visibility = MusicaAjustesPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
            };

            BtnMusicaFondoArtwork.Click += (s, e) => SetMusicaFondo("artwork");
            BtnMusicaFondoImagen.Click  += (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Elegir imagen de fondo",
                    Filter = "Imágenes|*.jpg;*.jpeg;*.png;*.webp"
                };
                if (dialog.ShowDialog() == true)
                {
                    _musicaFondoImagenPath = dialog.FileName;
                    SetMusicaFondo("imagen");
                }
            };
            BtnMusicaFondoNinguno.Click += (s, e) => SetMusicaFondo("ninguno");

            BtnMusicaAutoplay.Click += (s, e) =>
            {
                _musicaAutoplay = !_musicaAutoplay;
                BtnMusicaAutoplay.Content    = _musicaAutoplay ? "●" : "○";
                BtnMusicaAutoplay.Foreground = _musicaAutoplay
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
                GuardarMusicaConfig();
            };

            BtnMusicaAleatorioGlobal.Click += (s, e) =>
            {
                _musicaAleatorioGlobal = !_musicaAleatorioGlobal;
                BtnMusicaAleatorioGlobal.Content    = _musicaAleatorioGlobal ? "●" : "○";
                BtnMusicaAleatorioGlobal.Foreground = _musicaAleatorioGlobal
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
                GuardarMusicaConfig();
            };

            // Búsqueda en playlist
            MusicaBusqueda.TextChanged += (s, e) =>
            {
                string query = MusicaBusqueda.Text.Trim().ToLower();
                BtnMusicaBusquedaClear.Visibility = string.IsNullOrEmpty(query) 
                    ? Visibility.Collapsed : Visibility.Visible;
                FiltrarMusicaPlaylist(query);
            };

            BtnMusicaBusquedaClear.Click += (s, e) =>
            {
                MusicaBusqueda.Text = "";
                MusicaBusqueda.Focus();
            };

            // Velocidad de reproducción
            var botonesVel = new[] { BtnVel05, BtnVel1, BtnVel15, BtnVel2 };
            foreach (var btn in botonesVel)
            {
                btn.Click += (s, e) =>
                {
                    string vel = (s as Button)?.Tag?.ToString() ?? "1.0";
                    _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:velocidad:" + vel);

                    // Resaltar botón activo
                    foreach (var b in botonesVel)
                    {
                        b.Background = new SolidColorBrush(Color.FromRgb(26, 10, 58));
                        b.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 119));
                        b.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 26, 74));
                    }
                    var btnActivo = s as Button;
                    if (btnActivo != null)
                    {
                        btnActivo.Background  = new SolidColorBrush(Color.FromRgb(42, 26, 90));
                        btnActivo.Foreground  = Brushes.White;
                        btnActivo.BorderBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237));
                    }
                };
            }

            BtnMusicaFavoritos.Click += (s, e) =>
            {
                _soloFavoritos = !_soloFavoritos;
                BtnMusicaFavoritos.Background = _soloFavoritos
                    ? new SolidColorBrush(Color.FromRgb(42, 26, 90))
                    : new SolidColorBrush(Color.FromRgb(26, 10, 58));
                BtnMusicaFavoritos.Foreground = _soloFavoritos
                    ? new SolidColorBrush(Color.FromRgb(255, 200, 0))
                    : new SolidColorBrush(Color.FromRgb(85, 85, 119));
                RenderizarMusicaUI();
            };

            BtnMusicaOrdenar.Click += (s, e) =>
            {
                var menu = new ContextMenu
                {
                    Background = new SolidColorBrush(Color.FromRgb(22, 18, 40)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(80, 124, 58, 237))
                };

                MenuItem CrearOpcion(string texto, Action accion)
                {
                    var item = new MenuItem { Header = texto, Foreground = Brushes.White,
                        Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                        Padding = new Thickness(12, 6, 12, 6) };
                    item.Click += (s2, e2) => accion();
                    return item;
                }

                var canciones = _playlists[_playlistActiva].canciones;

                menu.Items.Add(CrearOpcion("🔤  A → Z (título)", () =>
                {
                    canciones.Sort((a, b) => string.Compare(a.titulo, b.titulo, StringComparison.OrdinalIgnoreCase));
                    GuardarMusicaPlaylist(); EnviarPlaylistsAlPlayer(); RenderizarMusicaUI();
                }));
                menu.Items.Add(CrearOpcion("🔤  Z → A (título)", () =>
                {
                    canciones.Sort((a, b) => string.Compare(b.titulo, a.titulo, StringComparison.OrdinalIgnoreCase));
                    GuardarMusicaPlaylist(); EnviarPlaylistsAlPlayer(); RenderizarMusicaUI();
                }));
                menu.Items.Add(CrearOpcion("👤  A → Z (autor)", () =>
                {
                    canciones.Sort((a, b) => string.Compare(a.autor, b.autor, StringComparison.OrdinalIgnoreCase));
                    GuardarMusicaPlaylist(); EnviarPlaylistsAlPlayer(); RenderizarMusicaUI();
                }));
                menu.Items.Add(CrearOpcion("👤  Z → A (autor)", () =>
                {
                    canciones.Sort((a, b) => string.Compare(b.autor, a.autor, StringComparison.OrdinalIgnoreCase));
                    GuardarMusicaPlaylist(); EnviarPlaylistsAlPlayer(); RenderizarMusicaUI();
                }));
                menu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)) });
                menu.Items.Add(CrearOpcion("⭐  Favoritos primero", () =>
                {
                    canciones.Sort((a, b) => b.favorito.CompareTo(a.favorito));
                    GuardarMusicaPlaylist(); EnviarPlaylistsAlPlayer(); RenderizarMusicaUI();
                }));
                menu.Items.Add(CrearOpcion("🔀  Aleatorio", () =>
                {
                    var rng = new Random();
                    for (int i = canciones.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (canciones[i], canciones[j]) = (canciones[j], canciones[i]);
                    }
                    GuardarMusicaPlaylist(); EnviarPlaylistsAlPlayer(); RenderizarMusicaUI();
                }));

                menu.PlacementTarget = BtnMusicaOrdenar;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            };

            MusicaVolumenDefault.ValueChanged += (s, e) =>
            {
                _musicaVolumenDefault = MusicaVolumenDefault.Value;
                GuardarMusicaConfig();
            };

            if (_playlists == null || _playlists.Count == 0)
                _playlists = new() { new MusicaPlaylist() };
            // Scroll horizontal con rueda del mouse en tabs de playlists
            var svPlaylists = MusicaPlaylistTabs.Parent as ScrollViewer;
            if (svPlaylists != null)
            {
                svPlaylists.PreviewMouseWheel += (s, e) =>
                {
                    svPlaylists.ScrollToHorizontalOffset(svPlaylists.HorizontalOffset + e.Delta * -0.5);
                    e.Handled = true;
                };
            }
            CargarMusicaPlaylist();
            CargarMusicaConfig();
            // Autoplay al inicio
            if (_playlists.Count == 0)
                _playlists.Add(new MusicaPlaylist());
            _playlistActiva = 0;
            RenderizarMusicaUI();
        }

        private void GuardarEstadoMusica()
        {
            try
            {
                if (_musicaWebView?.CoreWebView2 == null) return;
                // El estado ya lo tenemos en _musicaIndiceActivo y _playlistActiva
                var estado = new
                {
                    playlistActiva  = _playlistActiva,
                    indice          = _musicaIndiceActivo,
                    progreso        = _musicaProgreso // necesitas guardar este valor
                };
                File.WriteAllText(
                    Path.Combine(_carpetaPerfil, "musica_estado.json"),
                    System.Text.Json.JsonSerializer.Serialize(estado));
            }
            catch { }
        }

        private void IniciarDetectorMediaExterna()
        {
            _mediaDetectorTimer = new System.Timers.Timer(2000);
            _mediaDetectorTimer.Elapsed += async (s, e) =>
            {
                if (!_suspenderMediaEnBackground) return;
                try
                {
                    if (_musicaWebView?.CoreWebView2 == null) return;
                    if (_activeTab < 0 || _activeTab >= _tabs.Count) return;
                    var tab = _tabs[_activeTab];
                    if (tab.CoreWebView2 == null) return;

                    var hayMedia = await tab.CoreWebView2.ExecuteScriptAsync(@"
                        (function(){
                            var m = document.querySelectorAll('video,audio');
                            for(var i=0;i<m.length;i++){
                                if(!m[i].paused && !m[i].ended && m[i].readyState>2)
                                    return true;
                            }
                            return false;
                        })()");

                    Dispatcher.Invoke(() =>
                    {
                        if (hayMedia == "true" && _musicaReproduciendo && !_musicaPausadaPorMedia)
                        {
                            _musicaPausadaPorMedia = true;
                            _musicaWebView.CoreWebView2?.PostWebMessageAsString("player:pause");
                        }
                        else if (hayMedia != "true" && _musicaPausadaPorMedia)
                        {
                            _musicaPausadaPorMedia = false;
                            _musicaWebView.CoreWebView2?.PostWebMessageAsString("player:play");
                        }
                    });
                }
                catch { }
            };
            _mediaDetectorTimer.AutoReset = true;
            _mediaDetectorTimer.Start();
        }

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

                // Resetear fuente si cambió la sesión activa
                if (titulo != _musicaUltimoTitulo)
                    _musicaFuenteCache = "";

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
                    if (fuente.Contains("spotify",  StringComparison.OrdinalIgnoreCase))
                        fuente = "Spotify";
                    else if (fuente.Contains("youtube", StringComparison.OrdinalIgnoreCase))
                        fuente = "YouTube";
                    else if (fuente.Contains("chrome",  StringComparison.OrdinalIgnoreCase) &&
                             !fuente.Contains("msedge", StringComparison.OrdinalIgnoreCase))
                        fuente = "Chrome";
                    else if (fuente.Contains("firefox", StringComparison.OrdinalIgnoreCase))
                        fuente = "Firefox";
                    else if (fuente.Contains("msedge",  StringComparison.OrdinalIgnoreCase) ||
                             fuente.Contains("webview", StringComparison.OrdinalIgnoreCase))
                    {
                        var urls = new List<string>();
                        foreach (var tab in _tabs)
                        {
                            string u = tab.Source?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(u)) urls.Add(u);
                        }
                        if      (urls.Any(u => u.Contains("youtube.com")))    fuente = "YouTube";
                        else if (urls.Any(u => u.Contains("spotify.com")))    fuente = "Spotify";
                        else if (urls.Any(u => u.Contains("twitch.tv")))      fuente = "Twitch";
                        else if (urls.Any(u => u.Contains("soundcloud.com"))) fuente = "SoundCloud";
                        else                                                   fuente = "AtsukiBrowser";
                    }
                    else if (fuente.Length > 20)
                        fuente = "AtsukiBrowser";

                    _musicaFuenteCache = fuente;
                }
                if (_musicaPlayerInternoActivo) return;

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
                // No pisar el widget si el player interno está activo
                if (_musicaPlayerInternoActivo) return;

                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    titulo, artista, imagen, playing, fuente
                });
                foreach (var tab in _tabs)
                    tab.CoreWebView2?.PostWebMessageAsString("musica:" + json);
            });
        }

        private async Task InicializarMusicaWebView()
        {
            if (_musicaInicializada) return;
            _musicaInicializada = true;

            _musicaWebView = new WebView2();
            _musicaWebView.Width  = 2;
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

            if (_env == null) { _musicaInicializada = false; return; }
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

                            bool reproduciendo = json.TryGetProperty("reproduciendo", out var rep) && rep.GetBoolean();
                            string titulo      = json.TryGetProperty("titulo",         out var tit) ? tit.GetString() ?? "" : "";
                            string plImagen    = json.TryGetProperty("playlistImagen", out var pli) ? pli.GetString() ?? "" : "";
                            string plNombre    = json.TryGetProperty("playlistNombre", out var pln) ? pln.GetString() ?? "" : "";
                            double progreso    = json.TryGetProperty("progreso",       out var pro) ? pro.GetDouble() : 0;
                            _musicaProgreso = progreso;
                            double duracion    = json.TryGetProperty("duracion",       out var dur) ? dur.GetDouble() : 0;
                            double progress    = duracion > 0 ? (progreso / duracion * 100) : 0;
                            int indice         = json.TryGetProperty("indice",         out var idx) ? idx.GetInt32() : -1;
                            int plActiva       = json.TryGetProperty("playlistActiva", out var pla) ? pla.GetInt32() : _playlistActiva;

                            // Actualizar flag de prioridad
                            _musicaPlayerInternoActivo = !string.IsNullOrEmpty(titulo);

                            // Prioridad: reproduciendo siempre, pausado solo si no hay otra sesión activa
                            if (reproduciendo)
                                _musicaPlayerInternoActivo = !string.IsNullOrEmpty(titulo);
                            else if (!string.IsNullOrEmpty(titulo))
                            {
                                // Pausado — ceder solo si hay otra app reproduciendo en el SMTC
                                var smtcSession = _smtc?.GetCurrentSession();
                                bool otroReproduciendo = smtcSession != null &&
                                    smtcSession.GetPlaybackInfo().PlaybackStatus ==
                                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                                _musicaPlayerInternoActivo = !otroReproduciendo;
                            }
                            else
                                _musicaPlayerInternoActivo = false;

                            string autor        = json.TryGetProperty("autor",  out var aut) ? aut.GetString() ?? "" : "";
                            string imagenCancion = json.TryGetProperty("imagen", out var img) ? img.GetString() ?? "" : "";
                            string imagenFinal  = !string.IsNullOrEmpty(imagenCancion) ? imagenCancion
                                                : !string.IsNullOrEmpty(plImagen)       ? plImagen
                                                : "";

                            foreach (var tab in _tabs)
                                tab.CoreWebView2?.PostWebMessageAsString("atsukimusic:estado:" + estado);

                            if (!string.IsNullOrEmpty(titulo))
                            {
                                var musicaWidget = System.Text.Json.JsonSerializer.Serialize(new
                                {
                                    titulo,
                                    artista  = autor,
                                    imagen   = imagenFinal,
                                    playing  = reproduciendo,
                                    fuente   = "AtsukiBrowser Music",
                                    progress
                                });
                                foreach (var tab in _tabs)
                                    tab.CoreWebView2?.PostWebMessageAsString("musica:" + musicaWidget);
                            }
                        }
                        catch { }

                        // Arranque inicial
                        if (_musicaArranqueInicial)
                        {
                            _musicaArranqueInicial = false;
                            if (_musicaAutoplay)
                            {
                                string estadoPath = Path.Combine(_carpetaPerfil, "musica_estado.json");
                                bool restaurado = false;

                                if (_musicaContinuar && File.Exists(estadoPath))
                                {
                                    try
                                    {
                                        var doc = System.Text.Json.JsonDocument.Parse(
                                            File.ReadAllText(estadoPath)).RootElement;
                                        int plIdx  = doc.TryGetProperty("playlistActiva", out var pl)  ? pl.GetInt32()    : 0;
                                        int idx    = doc.TryGetProperty("indice",         out var id)  ? id.GetInt32()    : 0;
                                        double pos = doc.TryGetProperty("progreso",       out var pr)  ? pr.GetDouble()   : 0;

                                        if (plIdx < _playlists.Count && idx < _playlists[plIdx].canciones.Count)
                                        {
                                            var arranqueTimer = new System.Windows.Threading.DispatcherTimer
                                                { Interval = TimeSpan.FromMilliseconds(400) };
                                            arranqueTimer.Tick += (t, _) =>
                                            {
                                                arranqueTimer.Stop();
                                                _musicaWebView.CoreWebView2?.PostWebMessageAsString("player:switchplaylist:" + plIdx);
                                                var timer2 = new System.Windows.Threading.DispatcherTimer
                                                    { Interval = TimeSpan.FromMilliseconds(150) };
                                                timer2.Tick += (t2, _) =>
                                                {
                                                    timer2.Stop();
                                                    ReproducirCancion(idx);
                                                    // Restaurar posición si es significativa
                                                    if (pos > 3)
                                                    {
                                                        var timer3 = new System.Windows.Threading.DispatcherTimer
                                                            { Interval = TimeSpan.FromMilliseconds(500) };
                                                        timer3.Tick += (t3, _) =>
                                                        {
                                                            timer3.Stop();
                                                            _musicaWebView.CoreWebView2?.PostWebMessageAsString(
                                                                "player:seek:" + pos.ToString("F1",
                                                                System.Globalization.CultureInfo.InvariantCulture));
                                                        };
                                                        timer3.Start();
                                                    }
                                                };
                                                timer2.Start();
                                            };
                                            arranqueTimer.Start();
                                            restaurado = true;
                                        }
                                    }
                                    catch { }
                                }

                                // Si no hay estado guardado, reproducir aleatoria
                                if (!restaurado)
                                {
                                    if (_musicaAleatorioGlobal)
                                    {
                                        // aleatorio de todas las playlists
                                        var todasLasCanciones = _playlists
                                            .SelectMany((pl, pi) => pl.canciones.Select((_, ci) => (pi, ci)))
                                            .ToList();

                                        if (todasLasCanciones.Count > 0)
                                        {
                                            var rng = new Random();
                                            var (playlistIdx, cancionIdx) = todasLasCanciones[rng.Next(todasLasCanciones.Count)];
                                            var arranqueTimer = new System.Windows.Threading.DispatcherTimer
                                                { Interval = TimeSpan.FromMilliseconds(400) };
                                            arranqueTimer.Tick += (t, _) =>
                                            {
                                                arranqueTimer.Stop();
                                                _playlistActiva = playlistIdx; // ← sincronizar estado C#
                                                _musicaWebView.CoreWebView2?.PostWebMessageAsString("player:switchplaylist:" + playlistIdx);
                                                var timer2 = new System.Windows.Threading.DispatcherTimer
                                                    { Interval = TimeSpan.FromMilliseconds(150) };
                                                timer2.Tick += (t2, _) =>
                                                {
                                                    timer2.Stop();
                                                    _musicaIndiceActivo = cancionIdx; // ← sincronizar índice
                                                    ReproducirCancion(cancionIdx);
                                                    RenderizarMusicaUI();
                                                };
                                                timer2.Start();
                                            };
                                            arranqueTimer.Start();
                                        }
                                    }
                                    else
                                    {
                                        // reproducir desde el inicio de la playlist activa
                                        var arranqueTimer = new System.Windows.Threading.DispatcherTimer
                                            { Interval = TimeSpan.FromMilliseconds(400) };
                                        arranqueTimer.Tick += (t, _) =>
                                        {
                                            arranqueTimer.Stop();
                                            ReproducirCancion(0);
                                        };
                                        arranqueTimer.Start();
                                    }
                                }
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
            IniciarDetectorMediaExterna();
        }

        private void MostrarNotificacionMusica(string titulo, string imagenUrl = "")
        {
            _musicaToast?.Close();

            var toast = new Window
            {
                Width = 300, Height = 72,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            var screen = SystemParameters.WorkArea;
            // Posición inicial fuera de pantalla a la derecha (estilo Minecraft)
            toast.Left = screen.Right + 10;
            toast.Top  = screen.Top + 12;

            var border = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(230, 13, 13, 22)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(200, 124, 58, 237)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(10, 8, 14, 8),
                Effect          = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = Color.FromRgb(124, 58, 237),
                    BlurRadius  = 16,
                    ShadowDepth = 0,
                    Opacity     = 0.5
                }
            };

            var panelH = new StackPanel { Orientation = Orientation.Horizontal };

            if (!string.IsNullOrEmpty(imagenUrl))
            {
                try
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Source    = new System.Windows.Media.Imaging.BitmapImage(new Uri(imagenUrl)),
                        Width     = 38, Height = 38,
                        Stretch   = System.Windows.Media.Stretch.UniformToFill,
                        Margin    = new Thickness(0, 0, 10, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Clip      = new System.Windows.Media.RectangleGeometry
                            { Rect = new Rect(0, 0, 38, 38), RadiusX = 5, RadiusY = 5 }
                    };
                    panelH.Children.Add(img);
                }
                catch { }
            }
            else
            {
                panelH.Children.Add(new TextBlock
                {
                    Text = "🎵", FontSize = 22,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textos.Children.Add(new TextBlock
            {
                Text       = "Reproduciendo",
                FontSize   = 9,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 124, 58, 237)),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            });
            textos.Children.Add(new TextBlock
            {
                Text         = titulo,
                FontSize     = 12,
                Foreground   = Brushes.White,
                FontFamily   = new System.Windows.Media.FontFamily("Segoe UI"),
                FontWeight   = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth     = 200
            });
            panelH.Children.Add(textos);
            border.Child  = panelH;
            toast.Content = border;
            _musicaToast  = toast;
            toast.Show();

            // Animación slide-in desde la derecha
            double posFinal = screen.Right - 312;
            var slideIn = new System.Windows.Threading.DispatcherTimer
                { Interval = TimeSpan.FromMilliseconds(16) };
            double velocidad = 28;
            slideIn.Tick += (s, e) =>
            {
                if (toast.Left > posFinal)
                {
                    double diff = toast.Left - posFinal;
                    toast.Left -= Math.Max(velocidad, diff * 0.25);
                    if (toast.Left <= posFinal)
                    {
                        toast.Left = posFinal;
                        slideIn.Stop();

                        // Esperar 3.5s y slide-out
                        var hold = new System.Windows.Threading.DispatcherTimer
                            { Interval = TimeSpan.FromMilliseconds(3500) };
                        hold.Tick += (s2, e2) =>
                        {
                            hold.Stop();
                            var slideOut = new System.Windows.Threading.DispatcherTimer
                                { Interval = TimeSpan.FromMilliseconds(16) };
                            slideOut.Tick += (s3, e3) =>
                            {
                                toast.Left += Math.Max(velocidad, (screen.Right - toast.Left) * 0.2);
                                if (toast.Left >= screen.Right + 10)
                                {
                                    slideOut.Stop();
                                    toast.Close();
                                }
                            };
                            slideOut.Start();
                        };
                        hold.Start();
                    }
                }
            };
            slideIn.Start();
        }

        

        private void SetMusicaFondo(string tipo)
        {
            _musicaFondoTipo = tipo;
            BtnMusicaFondoArtwork.Foreground = tipo == "artwork"
                ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
            BtnMusicaFondoImagen.Foreground = tipo == "imagen"
                ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
            BtnMusicaFondoNinguno.Foreground = tipo == "ninguno"
                ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
            AplicarFondoMusica();
            GuardarMusicaConfig();
        }

        private void RenderizarPanelPlaylists()
        {
            MusicaPlaylistsPanel.Children.Clear();
            foreach (var pl in _playlists)
            {
                // Header de playlist
                var header = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(26, 10, 58)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                if (!string.IsNullOrEmpty(pl.imagen))
                {
                    try
                    {
                        var img = new System.Windows.Controls.Image
                        {
                            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(pl.imagen)),
                            Width = 24, Height = 24,
                            Stretch = System.Windows.Media.Stretch.UniformToFill,
                            Margin = new Thickness(0, 0, 6, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            Clip = new System.Windows.Media.RectangleGeometry
                                { Rect = new Rect(0, 0, 24, 24), RadiusX = 3, RadiusY = 3 }
                        };
                        headerPanel.Children.Add(img);
                    }
                    catch { }
                }
                headerPanel.Children.Add(new TextBlock
                {
                    Text = pl.nombre,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 140, 255)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"  {pl.canciones.Count} canciones",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 119)),
                    VerticalAlignment = VerticalAlignment.Center
                });
                header.Child = headerPanel;
                MusicaPlaylistsPanel.Children.Add(header);

                // Canciones de la playlist
                foreach (var c in pl.canciones)
                {
                    var item = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(18, 14, 32)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(4, 0, 0, 2),
                        Cursor = Cursors.Hand
                    };
                    var itemPanel = new StackPanel();
                    itemPanel.Children.Add(new TextBlock
                    {
                        Text = c.titulo,
                        FontSize = 10,
                        Foreground = Brushes.White,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                    if (!string.IsNullOrEmpty(c.autor))
                        itemPanel.Children.Add(new TextBlock
                        {
                            Text = c.autor,
                            FontSize = 9,
                            Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 119)),
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                    item.Child = itemPanel;

                    // Click para reproducir
                    var cancion = c;
                    var playlist = pl;
                    item.MouseLeftButtonDown += (s, e) =>
                    {
                        int plIdx = _playlists.IndexOf(playlist);
                        int cIdx = playlist.canciones.IndexOf(cancion);
                        _playlistActiva = plIdx;
                        _musicaWebView?.CoreWebView2?.PostWebMessageAsString($"player:switchplaylist:{plIdx}");
                        ReproducirCancion(cIdx);
                        // Cambiar a tab de Canciones y reflejar la nueva playlist activa
                        PanelTabCanciones.Visibility = Visibility.Visible;
                        PanelTabPlaylists.Visibility = Visibility.Collapsed;
                        BtnTabCanciones.Background = new SolidColorBrush(Color.FromRgb(42, 26, 90));
                        BtnTabCanciones.Foreground = Brushes.White;
                        BtnTabPlaylists.Background = new SolidColorBrush(Color.FromRgb(26, 10, 58));
                        BtnTabPlaylists.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 119));
                        RenderizarMusicaUI();
                    };

                    item.MouseEnter += (s, e) =>
                        item.Background = new SolidColorBrush(Color.FromRgb(35, 25, 60));
                    item.MouseLeave += (s, e) =>
                        item.Background = new SolidColorBrush(Color.FromRgb(18, 14, 32));

                    MusicaPlaylistsPanel.Children.Add(item);
                }

                // Separador entre playlists
                MusicaPlaylistsPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromArgb(40, 124, 58, 237)),
                    Margin = new Thickness(0, 4, 0, 8)
                });
            }
        }

        private void AplicarFondoMusica()
        {
            if (_musicaFondoTipo == "ninguno")
            {
                MusicaPanel.Background = new SolidColorBrush(Color.FromRgb(13, 13, 24));
                return;
            }
            if (_musicaFondoTipo == "imagen" && File.Exists(_musicaFondoImagenPath))
            {
                var brush = new ImageBrush
                {
                    ImageSource = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri(_musicaFondoImagenPath)),
                    Stretch  = Stretch.UniformToFill,
                    Opacity  = 0.15
                };
                MusicaPanel.Background = brush;
                return;
            }
            // artwork — se aplica cuando cambia la canción desde ActualizarUIMusica
        }

        private void GuardarMusicaConfig()
        {
            string path = Path.Combine(_carpetaPerfil, "musica_config.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
            {
                fondoTipo       = _musicaFondoTipo,
                fondoImagenPath = _musicaFondoImagenPath,
                autoplay        = _musicaAutoplay,
                aleatorioGlobal = _musicaAleatorioGlobal,
                volumenDefault  = _musicaVolumenDefault,
                continuar       = _musicaContinuar
            }));
        }

        private void CargarMusicaConfig()
        {
            string path = Path.Combine(_carpetaPerfil, "musica_config.json");
            if (!File.Exists(path)) return;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement;
                if (doc.TryGetProperty("fondoTipo",       out var ft)) _musicaFondoTipo        = ft.GetString() ?? "artwork";
                if (doc.TryGetProperty("fondoImagenPath", out var fi)) _musicaFondoImagenPath  = fi.GetString() ?? "";
                if (doc.TryGetProperty("autoplay",        out var ap)) _musicaAutoplay         = ap.GetBoolean();
                if (doc.TryGetProperty("aleatorioGlobal", out var ag)) _musicaAleatorioGlobal  = ag.GetBoolean();
                if (doc.TryGetProperty("volumenDefault",  out var vd)) _musicaVolumenDefault   = vd.GetDouble();
                if (doc.TryGetProperty("continuar", out var co)) _musicaContinuar = co.GetBoolean();

                BtnMusicaContinuar.Content    = _musicaContinuar ? "●" : "○";
                BtnMusicaContinuar.Foreground = _musicaContinuar
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
                BtnMusicaAutoplay.Content       = _musicaAutoplay        ? "●" : "○";
                BtnMusicaAleatorioGlobal.Content = _musicaAleatorioGlobal ? "●" : "○";
                BtnMusicaAutoplay.Foreground       = _musicaAutoplay
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
                BtnMusicaAleatorioGlobal.Foreground = _musicaAleatorioGlobal
                    ? new SolidColorBrush(Color.FromRgb(124, 58, 237))
                    : new SolidColorBrush(Color.FromArgb(180, 85, 85, 119));
                MusicaVolumenDefault.Value = _musicaVolumenDefault;
                SetMusicaFondo(_musicaFondoTipo);
            }
            catch { }
        }

        private async void AgregarMusicaStream()
        {
            string input = MusicaStreamInput.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;

            MusicaStreamInput.IsEnabled = false;
            MusicaStreamInput.Text = "Procesando...";

            var cancion = await DetectarTipoUrl(input);

            _playlists[_playlistActiva].canciones.Add(cancion);
            MusicaStreamInput.Text = "";
            MusicaStreamInput.IsEnabled = true;

            EnviarPlaylistsAlPlayer();
            GuardarMusicaPlaylist();
            RenderizarMusicaUI();
        }

        private async Task<MusicaCancion> DetectarTipoUrl(string url)
        {
            // ── YouTube ──
            var ytMatch = System.Text.RegularExpressions.Regex.Match(
                url, @"(?:v=|youtu\.be/)([A-Za-z0-9_-]{11})");
            if (ytMatch.Success)
            {
                string videoId = ytMatch.Groups[1].Value;
                string thumb   = $"https://img.youtube.com/vi/{videoId}/mqdefault.jpg";
                string titulo  = url; // fallback
                try
                {
                    // Intentar obtener título via oEmbed
                    string oembedUrl = $"https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={videoId}&format=json";
                    var resp = await _httpClient.GetStringAsync(oembedUrl);
                    var doc  = System.Text.Json.JsonDocument.Parse(resp).RootElement;
                    titulo = doc.TryGetProperty("title",  out var t) ? t.GetString() ?? url : url;
                    string autor = doc.TryGetProperty("author_name", out var a) ? a.GetString() ?? "" : "";
                    return new MusicaCancion { titulo = titulo, url = url, imagen = thumb, autor = autor, esYoutube = true };
                }
                catch { }
                // Extraer URL de audio directo
                string audioUrl = await ExtraerUrlYoutube(url);
                return new MusicaCancion { titulo = $"YouTube ({videoId})", url = audioUrl, imagen = thumb, autor = "" };
            }

            // ── Archivo de audio directo ──
            string[] extensiones = { ".mp3", ".mp4", ".wav", ".ogg", ".flac", ".m4a", ".aac", ".opus", ".webm" };
            if (extensiones.Any(ext => url.ToLower().Contains(ext)))
            {
                string nombre = url.Split('/').LastOrDefault()?.Split('?')[0] ?? url;
                nombre = Uri.UnescapeDataString(nombre);
                return new MusicaCancion { titulo = nombre, url = url, imagen = "", autor = "" };
            }

            // ── Stream (radio, HLS, etc.) ──
            if (url.Contains(".m3u8") || url.Contains(".pls") || url.Contains("stream") || url.Contains("radio"))
            {
                string nombre = url.Split('/').LastOrDefault()?.Split('?')[0] ?? "Stream";
                return new MusicaCancion { titulo = $"▶ {nombre}", url = url, imagen = "", autor = "Stream" };
            }

            // ── URL genérica ──
            string fallback = url.Split('/').LastOrDefault()?.Split('?')[0] ?? url;
            return new MusicaCancion { titulo = fallback, url = url, imagen = "", autor = "" };
        }

        private async Task<string> ExtraerUrlYoutube(string urlYoutube)
        {
            try
            {
                string ytdlp = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Resources", "yt-dlp.exe");
                if (!File.Exists(ytdlp)) return urlYoutube;

                // Pedir la URL + los headers necesarios en formato JSON
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName               = ytdlp,
                    Arguments              = $"--no-playlist --dump-json \"{urlYoutube}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var proc = System.Diagnostics.Process.Start(psi)!;
                string json = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                var doc = System.Text.Json.JsonDocument.Parse(json).RootElement;

                // Buscar el mejor formato de audio compatible con WebView2
                string[] formatosPref = { "140", "m4a", "mp4a" };
                if (doc.TryGetProperty("formats", out var formats))
                {
                    foreach (var fmt in formats.EnumerateArray().Reverse())
                    {
                        string fmtId  = fmt.TryGetProperty("format_id", out var fi) ? fi.GetString() ?? "" : "";
                        string ext    = fmt.TryGetProperty("ext",       out var fe) ? fe.GetString() ?? "" : "";
                        string vcodec = fmt.TryGetProperty("vcodec",    out var fv) ? fv.GetString() ?? "" : "";
                        bool soloAudio = vcodec == "none" || string.IsNullOrEmpty(vcodec);

                        if (soloAudio && (formatosPref.Contains(fmtId) || formatosPref.Contains(ext)))
                        {
                            if (fmt.TryGetProperty("url", out var furl))
                                return furl.GetString() ?? urlYoutube;
                        }
                    }

                    // Fallback: cualquier formato solo-audio
                    foreach (var fmt in formats.EnumerateArray().Reverse())
                    {
                        string vcodec = fmt.TryGetProperty("vcodec", out var fv) ? fv.GetString() ?? "" : "";
                        bool soloAudio = vcodec == "none" || string.IsNullOrEmpty(vcodec);
                        if (soloAudio && fmt.TryGetProperty("url", out var furl))
                            return furl.GetString() ?? urlYoutube;
                    }
                }

                // Último fallback: url directa del JSON raíz
                if (doc.TryGetProperty("url", out var rootUrl))
                    return rootUrl.GetString() ?? urlYoutube;

                return urlYoutube;
            }
            catch { return urlYoutube; }
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
                    if (e.OriginalSource is Button || 
                        (e.OriginalSource as FrameworkElement)?.Parent is Button) return;
                    _playlistActiva = idx;
                    _musicaWebView?.CoreWebView2?.PostWebMessageAsString("player:switchplaylist:" + idx);
                    RenderizarMusicaUI();
                };

                // Click derecho → menú contextual
                tab.MouseRightButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    var menu = new ContextMenu();
                    menu.Background = new SolidColorBrush(Color.FromRgb(22, 18, 40));
                    menu.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 124, 58, 237));

                    var itemRenombrar = new MenuItem { Header = "✏  Renombrar", Foreground = Brushes.White };
                    itemRenombrar.Click += (s2, e2) =>
                    {
                        var ventana = new Window
                        {
                            Title = "Renombrar playlist",
                            Width = 320, Height = 130,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this,
                            ResizeMode = ResizeMode.NoResize,
                            Background = new SolidColorBrush(Color.FromRgb(22, 18, 40)),
                            WindowStyle = WindowStyle.ToolWindow
                        };
                        var panel = new StackPanel { Margin = new Thickness(16) };
                        var txt = new TextBox
                        {
                            Text = _playlists[idx].nombre,
                            Background = new SolidColorBrush(Color.FromRgb(35, 28, 60)),
                            Foreground = Brushes.White,
                            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 124, 58, 237)),
                            Padding = new Thickness(8, 6, 8, 6),
                            Margin = new Thickness(0, 0, 0, 10),
                            CaretBrush = Brushes.White
                        };
                        txt.SelectAll();
                        var btn = new Button
                        {
                            Content = "Guardar",
                            Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                            Foreground = Brushes.White,
                            BorderThickness = new Thickness(0),
                            Padding = new Thickness(12, 6, 12, 6),
                            Cursor = Cursors.Hand,
                            HorizontalAlignment = HorizontalAlignment.Right
                        };
                        btn.Click += (s3, e3) =>
                        {
                            if (!string.IsNullOrWhiteSpace(txt.Text))
                            {
                                _playlists[idx].nombre = txt.Text.Trim();
                                GuardarMusicaPlaylist();
                                EnviarPlaylistsAlPlayer();
                                RenderizarMusicaUI();
                            }
                            ventana.Close();
                        };
                        txt.KeyDown += (s3, e3) => { if (e3.Key == System.Windows.Input.Key.Enter) btn.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent)); };
                        panel.Children.Add(txt);
                        panel.Children.Add(btn);
                        ventana.Content = panel;
                        ventana.Loaded += (s3, e3) => txt.Focus();
                        ventana.ShowDialog();
                    };

                    var itemImagen = new MenuItem { Header = "🖼  Cambiar imagen", Foreground = Brushes.White };
                    itemImagen.Click += (s2, e2) =>
                    {
                        var dlg = new Microsoft.Win32.OpenFileDialog
                        {
                            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.gif",
                            Title = "Imagen para \"" + _playlists[idx].nombre + "\""
                        };
                        if (dlg.ShowDialog() != true) return;
                        _playlists[idx].imagen = "file:///" + dlg.FileName.Replace("\\", "/");
                        GuardarMusicaPlaylist();
                        EnviarPlaylistsAlPlayer();
                        RenderizarMusicaUI();
                    };

                    var itemExportar = new MenuItem { Header = "📤  Exportar playlist", Foreground = Brushes.White };
                    itemExportar.Click += (s2, e2) =>
                    {
                        var dlg = new Microsoft.Win32.SaveFileDialog
                        {
                            Filter = "Playlist AtsukiBrowser|*.atsuki-playlist",
                            FileName = _playlists[idx].nombre,
                            Title = "Exportar playlist"
                        };
                        if (dlg.ShowDialog() != true) return;
                        var pl2 = _playlists[idx];
                        var json = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            nombre   = pl2.nombre,
                            imagen   = pl2.imagen,
                            canciones = pl2.canciones.Select(c => new
                                { titulo = c.titulo, url = c.url, imagen = c.imagen, autor = c.autor })
                        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(dlg.FileName, json);
                    };

                    var itemImportar = new MenuItem { Header = "📥  Importar playlist", Foreground = Brushes.White };
                    itemImportar.Click += (s2, e2) =>
                    {
                        var dlg = new Microsoft.Win32.OpenFileDialog
                        {
                            Filter = "Playlist AtsukiBrowser|*.atsuki-playlist",
                            Title = "Importar playlist"
                        };
                        if (dlg.ShowDialog() != true) return;
                        try
                        {
                            var json = File.ReadAllText(dlg.FileName);
                            var doc  = System.Text.Json.JsonDocument.Parse(json).RootElement;
                            var nueva = new MusicaPlaylist
                            {
                                nombre = doc.TryGetProperty("nombre", out var n) ? n.GetString() ?? "Playlist" : "Playlist",
                                imagen = doc.TryGetProperty("imagen", out var img) ? img.GetString() ?? "" : ""
                            };
                            if (doc.TryGetProperty("canciones", out var canciones))
                                foreach (var c in canciones.EnumerateArray())
                                    nueva.canciones.Add(new MusicaCancion
                                    {
                                        titulo = c.TryGetProperty("titulo", out var t) ? t.GetString() ?? "" : "",
                                        url    = c.TryGetProperty("url",    out var u) ? u.GetString() ?? "" : "",
                                        imagen = c.TryGetProperty("imagen", out var i) ? i.GetString() ?? "" : "",
                                        autor  = c.TryGetProperty("autor",  out var a) ? a.GetString() ?? "" : ""
                                    });
                            _playlists.Add(nueva);
                            _playlistActiva = _playlists.Count - 1;
                            GuardarMusicaPlaylist();
                            EnviarPlaylistsAlPlayer();
                            RenderizarMusicaUI();
                        }
                        catch { MessageBox.Show("No se pudo importar la playlist.", "Error"); }
                    };

                    menu.Items.Add(itemRenombrar);
                    menu.Items.Add(itemImagen);
                    menu.Items.Add(new Separator());
                    menu.Items.Add(itemExportar);
                    menu.Items.Add(itemImportar);
                    menu.PlacementTarget = tab;
                    menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    menu.IsOpen = true;
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
                if (_soloFavoritos && !item.favorito) continue;
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
                border.Tag = item;
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

                // Estrella favorito
                var btnFav = new Button
                {
                    Content = item.favorito ? "★" : "☆",
                    FontSize = 12,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = item.favorito
                        ? new SolidColorBrush(Color.FromRgb(255, 200, 0))
                        : new SolidColorBrush(Color.FromRgb(85, 85, 119)),
                    Cursor = Cursors.Hand,
                    Padding = new Thickness(4, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                btnFav.Click += (s, e) =>
                {
                    canciones[idx].favorito = !canciones[idx].favorito;
                    GuardarMusicaPlaylist();
                    RenderizarMusicaUI();
                };

                // Agregar btnFav al grid antes del btnDel
                var btnFavCol = new ColumnDefinition { Width = GridLength.Auto };
                grid.ColumnDefinitions.Insert(1, btnFavCol);
                Grid.SetColumn(btnFav, 1);
                Grid.SetColumn(btnDel, 2);
                grid.Children.Add(btnFav);

                border.MouseLeftButtonDown += async (s, e) =>
                {
                    if (e.OriginalSource is Button) return;
                    await InicializarMusicaWebView();
                    EnviarPlaylistsAlPlayer();
                    await Task.Delay(200);
                    ReproducirCancion(idx);
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

            if (estado.TryGetProperty("autor", out var autor) && !string.IsNullOrEmpty(autor.GetString()))
                MusicaAutor.Text = autor.GetString()!;
            else
                MusicaAutor.Text = "";

            // Fondo artwork dinámico
            if (_musicaFondoTipo == "artwork" && 
                estado.TryGetProperty("imagen", out var img) && 
                !string.IsNullOrEmpty(img.GetString()))
            {
                try
                {
                    string base64 = img.GetString()!.Split(',').Last();
                    var bytes = Convert.FromBase64String(base64);
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(bytes);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    MusicaPanel.Background = new ImageBrush
                    {
                        ImageSource = bmp,
                        Stretch = Stretch.UniformToFill,
                        Opacity = 0.15
                    };
                }
                catch { }
            }
            else if (_musicaFondoTipo == "artwork")
            {
                MusicaPanel.Background = new SolidColorBrush(Color.FromRgb(13, 13, 24));
            }

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
            // Actualizar imagen de playlist/canción
            if (estado.TryGetProperty("imagen", out var imgCancion) && 
                !string.IsNullOrEmpty(imgCancion.GetString()))
            {
                try
                {
                    string base64 = imgCancion.GetString()!.Split(',').Last();
                    var bytes = Convert.FromBase64String(base64);
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(bytes);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    MusicaPlaylistImagen.Source = bmp;
                    MusicaPlaylistImagen.Visibility = Visibility.Visible;
                }
                catch { }
            }
            else if (estado.TryGetProperty("playlistImagen", out var imgPlaylist) &&
                    !string.IsNullOrEmpty(imgPlaylist.GetString()))
            {
                try
                {
                    string base64 = imgPlaylist.GetString()!.Split(',').Last();
                    var bytes = Convert.FromBase64String(base64);
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(bytes);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    MusicaPlaylistImagen.Source = bmp;
                    MusicaPlaylistImagen.Visibility = Visibility.Visible;
                }
                catch { }
            }
            else
            {
                MusicaPlaylistImagen.Source = null;
                MusicaPlaylistImagen.Visibility = Visibility.Collapsed;
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
            string path = Path.Combine(_carpetaPerfil, "musica_playlist.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(
                _playlists.Select(p => new
                {
                    nombre   = p.nombre,
                    imagen   = p.imagen,
                    canciones = p.canciones.Select(c => new
                        { titulo = c.titulo, url = c.url, imagen = c.imagen, autor = c.autor, favorito = c.favorito })
                })));
        }

        private void CargarMusicaPlaylist()
        {
            string path = Path.Combine(_carpetaPerfil, "musica_playlist.json");
            if (!File.Exists(path)) return;
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement;
                if (doc.ValueKind != System.Text.Json.JsonValueKind.Array) return;

                _playlists.Clear();
                foreach (var pl in doc.EnumerateArray())
                {
                    var playlist = new MusicaPlaylist
                    {
                        nombre = pl.TryGetProperty("nombre", out var n) ? n.GetString() ?? "Playlist" : "Playlist",
                        imagen = pl.TryGetProperty("imagen", out var img) ? img.GetString() ?? "" : ""
                    };
                    if (pl.TryGetProperty("canciones", out var canciones))
                    {
                        foreach (var c in canciones.EnumerateArray())
                        {
                            string titulo = c.TryGetProperty("titulo", out var t) ? t.GetString() ?? "" : "";
                            string url    = c.TryGetProperty("url",    out var u) ? u.GetString() ?? "" : "";
                            string imagen = c.TryGetProperty("imagen", out var i) ? i.GetString() ?? "" : "";
                            string autor    = c.TryGetProperty("autor",    out var a)  ? a.GetString() ?? "" : "";
                            bool   favorito = c.TryGetProperty("favorito", out var fav) && fav.GetBoolean();
                            if (!string.IsNullOrEmpty(url))
                                playlist.canciones.Add(new MusicaCancion { titulo = titulo, url = url, imagen = imagen, autor = autor, favorito = favorito });
                        }
                    }
                    _playlists.Add(playlist);
                }

                if (_playlists.Count == 0)
                    _playlists.Add(new MusicaPlaylist { nombre = "Nueva playlist" });
            }
            catch
            {
                _playlists.Clear();
                _playlists.Add(new MusicaPlaylist { nombre = "Nueva playlist" });
            }
        }

        private void EnviarPlaylistsAlPlayer()
        {
            if (_musicaWebView?.CoreWebView2 == null) return;
            string json = System.Text.Json.JsonSerializer.Serialize(
                _playlists.Select(p => new {
                    nombre   = p.nombre,
                    imagen   = p.imagen,
                    canciones = p.canciones.Select(c => new 
                        { titulo = c.titulo, url = c.url, imagen = c.imagen, autor = c.autor, esYoutube = c.esYoutube })
                }));
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
                    canciones = p.canciones.Select(c => new { titulo = c.titulo, url = c.url, imagen = c.imagen, autor = c.autor })
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

        private void FiltrarMusicaPlaylist(string query)
        {
            foreach (var child in MusicaPlaylist.Children.OfType<Border>())
            {
                if (child.Tag is MusicaCancion cancion)
                {
                    bool visible = string.IsNullOrEmpty(query)
                        || cancion.titulo.ToLower().Contains(query)
                        || cancion.autor.ToLower().Contains(query);
                    child.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void ReproducirCancion(int idx)
        {
            var cancion = _playlists[_playlistActiva].canciones.ElementAtOrDefault(idx);
            if (cancion == null) return;

            if (cancion.esYoutube)
            {
                _ = Task.Run(async () =>
                {
                    string streamUrl = await ExtraerUrlYoutube(cancion.url);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        string js = $"(function(){{" +
                                    $"  const a = document.getElementById('audio');" +
                                    $"  a.src = {System.Text.Json.JsonSerializer.Serialize(streamUrl)};" +
                                    $"  a.load();" +
                                    $"  a.play().catch(e => window.chrome.webview.postMessage('musica:notify:ERROR: ' + e.message + '|'));" +
                                    $"}})()";
                        _musicaWebView?.CoreWebView2?.ExecuteScriptAsync(js);
                    });
                });
            }
            else
            {
                _musicaWebView?.CoreWebView2?.PostWebMessageAsString($"player:reproducir:{idx}");
            }
        }
    }
}