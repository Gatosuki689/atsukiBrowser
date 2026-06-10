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
        private void RenderizarSidebar()
        {
            SidebarTop.Children.Clear();
            SidebarBottom.Children.Clear();
            var iconColor = Color.FromArgb(180, _accentColor.R, _accentColor.G, _accentColor.B);
            var iconBrush = new SolidColorBrush(iconColor);

            double btnW = _sidebarCompacto ? 36 : 52;
            double btnH = _sidebarCompacto ? 32 : 42;
            double iconSize = _sidebarCompacto ? 14 : 18;
            double imgSize  = _sidebarCompacto ? 16 : 20;

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
                    Width = btnW, Height = btnH,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = item.Nombre,
                    AllowDrop = true,
                    Tag = item
                };

                // ── Drag & drop vertical ──────────────────────────────
                Point sbDragStart = default;
                bool  sbDragging  = false;

                btn.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    sbDragStart = e.GetPosition(btn);
                    sbDragging  = false;
                };

                btn.PreviewMouseMove += (s, e) =>
                {
                    if (e.LeftButton != MouseButtonState.Pressed || sbDragging) return;
                    var pos = e.GetPosition(btn);
                    if (Math.Abs(pos.Y - sbDragStart.Y) > 8)
                    {
                        sbDragging = true;
                        btn.Opacity = 0.4;
                        DragDrop.DoDragDrop(btn, item.Id, DragDropEffects.Move);
                        btn.Opacity = 1.0;
                        sbDragging = false;
                    }
                };

                btn.DragOver += (s, e) =>
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;

                    // Indicador visual de posición de drop
                    btn.BorderThickness = e.GetPosition(btn).Y < btn.ActualHeight / 2
                        ? new Thickness(0, 2, 0, 0)
                        : new Thickness(0, 0, 0, 2);
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(124, 58, 237));
                };

                btn.DragLeave += (s, e) =>
                {
                    btn.BorderThickness = new Thickness(0);
                };

                btn.Drop += (s, e) =>
                {
                    btn.BorderThickness = new Thickness(0);
                    if (e.Data.GetData(typeof(string)) is not string idOrigen) return;
                    if (idOrigen == item.Id) return;

                    int from = _sidebar.Items.FindIndex(i => i.Id == idOrigen);
                    int to   = _sidebar.Items.FindIndex(i => i.Id == item.Id);
                    if (from < 0 || to < 0) return;

                    // Insertar arriba o abajo según posición del cursor
                    bool arriba = e.GetPosition(btn).Y < btn.ActualHeight / 2;
                    int destino = arriba ? to : to + 1;
                    if (destino > from) destino--;

                    var itemMover = _sidebar.Items[from];
                    _sidebar.Items.RemoveAt(from);
                    _sidebar.Items.Insert(Math.Clamp(destino, 0, _sidebar.Items.Count), itemMover);
                    _sidebar.Guardar();
                    RenderizarSidebar();
                    e.Handled = true;
                };

                // ── Menú contextual clic derecho ─────────────────────
                var ctxSidebar = new ContextMenu
                {
                    Background      = new SolidColorBrush(Color.FromRgb(22, 18, 40)),
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                    BorderThickness = new Thickness(1),
                    Padding         = new Thickness(0, 4, 0, 4)
                };

                MenuItem CrearItemSidebar(string texto, Action accion)
                {
                    var mi = new MenuItem
                    {
                        Header          = texto,
                        Background      = Brushes.Transparent,
                        Foreground      = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                        BorderThickness = new Thickness(0),
                        Padding         = new Thickness(12, 7, 12, 7),
                        FontSize        = 12
                    };
                    mi.Click += (s2, e2) => accion();
                    return mi;
                }

                // Abrir en nueva pestaña (solo si tiene URL)
                if (!string.IsNullOrEmpty(item.Url) && item.Url.StartsWith("http"))
                {
                    string urlSidebar = item.Url;
                    if (!urlSidebar.StartsWith("http://") && !urlSidebar.StartsWith("https://"))
                        urlSidebar = "https://" + urlSidebar;

                    ctxSidebar.Items.Add(CrearItemSidebar(
                        "🔗  Abrir en nueva pestaña",
                        () => AbrirNuevaTab(urlSidebar)));
                }

                // Mover arriba / abajo
                ctxSidebar.Items.Add(CrearItemSidebar("⬆  Mover arriba", () =>
                {
                    int idx = _sidebar.Items.FindIndex(i => i.Id == item.Id);
                    if (idx <= 0) return;
                    (_sidebar.Items[idx], _sidebar.Items[idx - 1]) =
                        (_sidebar.Items[idx - 1], _sidebar.Items[idx]);
                    _sidebar.Guardar();
                    RenderizarSidebar();
                }));

                ctxSidebar.Items.Add(CrearItemSidebar("⬇  Mover abajo", () =>
                {
                    int idx = _sidebar.Items.FindIndex(i => i.Id == item.Id);
                    if (idx < 0 || idx >= _sidebar.Items.Count - 1) return;
                    (_sidebar.Items[idx], _sidebar.Items[idx + 1]) =
                        (_sidebar.Items[idx + 1], _sidebar.Items[idx]);
                    _sidebar.Guardar();
                    RenderizarSidebar();
                }));

                ctxSidebar.Items.Add(new Separator
                    { Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)) });

                // Renombrar
                ctxSidebar.Items.Add(CrearItemSidebar("✏  Renombrar", () =>
                {
                    var dlg = new RenombrarDialog(item.Nombre);
                    if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.NuevoNombre))
                    {
                        item.Nombre = dlg.NuevoNombre;
                        btn.ToolTip = dlg.NuevoNombre;
                        _sidebar.Guardar();
                        RenderizarSidebar();
                    }
                }));

                // Ocultar / Mostrar
                ctxSidebar.Items.Add(CrearItemSidebar(
                    item.Visible ? "👁  Ocultar" : "👁  Mostrar", () =>
                {
                    item.Visible = !item.Visible;
                    _sidebar.Guardar();
                    RenderizarSidebar();
                }));

                // Eliminar (solo items de usuario, no los del sistema)
                if (item.Id.StartsWith("user:") || item.Id.StartsWith("ext:"))
                {
                    ctxSidebar.Items.Add(new Separator
                        { Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)) });
                    ctxSidebar.Items.Add(CrearItemSidebar("🗑  Eliminar", () =>
                    {
                        _sidebar.Items.RemoveAll(i => i.Id == item.Id);
                        _sidebar.Guardar();
                        RenderizarSidebar();
                    }));
                }

                btn.ContextMenu = ctxSidebar;

                // Decidir contenido del botón
                if (!string.IsNullOrEmpty(item.Url) && item.Url.StartsWith("http"))
                {
                    // Favicon con fallback a emoji
                    var img = new Image
                    {
                        Width = imgSize, Height = imgSize,
                        Opacity = 0.6
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);

                    bool imagenOk = false;
                    try
                    {
                        var uri = new Uri(item.Url);
                        var faviconUrl = $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=32";
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(faviconUrl);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnDemand;
                        bmp.EndInit();

                        bmp.DownloadFailed += (s2, e2) =>
                        {
                            Dispatcher.Invoke(() => btn.Content = new TextBlock
                            {
                                Text = string.IsNullOrEmpty(item.Emoji) ? "🌐" : item.Emoji,
                                FontSize = iconSize,
                                Foreground = iconBrush
                            });
                        };

                        img.Source = bmp;
                        btn.Content = img;
                        imagenOk = true;
                    }
                    catch { }

                    if (!imagenOk)
                        btn.Content = new TextBlock
                        {
                            Text = string.IsNullOrEmpty(item.Emoji) ? "🌐" : item.Emoji,
                            FontSize = 18,
                            Foreground = iconBrush
                        };
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

            // ── Botón Musica ──
            var btnMusica = new Button
            {
                Width = btnW, Height = btnH,
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

                double anchoBase = _sidebarCompacto ? 36 : 52;

                // Detener animación activa antes de iniciar otra
                SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);

                if (_musicaPanelAbierto)
                    MusicaPanel.Visibility = Visibility.Visible;

                var anim = new GridLengthAnimation
                {
                    From = new GridLength(SidebarColumn.Width.Value),
                    To   = new GridLength(_musicaPanelAbierto ? 332 : anchoBase),
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                anim.Completed += (_, _) =>
                {
                    // Liberar la animación y fijar el valor final manualmente
                    SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
                    SidebarColumn.Width = new GridLength(_musicaPanelAbierto ? 332 : anchoBase);
                    if (!_musicaPanelAbierto)
                        MusicaPanel.Visibility = Visibility.Collapsed;
                };

                SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, anim);
            };
            SidebarTop.Children.Add(btnMusica);
            // Widget de rendimiento al final
            if (_sbWidgetRendimiento || _sbWidgetReloj)
            {
                var sep = new Separator
                {
                    Margin = _sidebarCompacto ? new Thickness(4, 4, 4, 4) : new Thickness(12, 6, 12, 6),
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
                    Width = btnW, Height = btnH,
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

            var btnCompacto = new Button
            {
                Width = btnW, Height = btnH,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = _sidebarCompacto ? "Modo normal" : "Modo compacto",
                Content = new TextBlock
                {
                    Text = _sidebarCompacto ? "⇔" : "⇒",
                    FontSize = iconSize,
                    Foreground = iconBrush
                }
            };
            btnCompacto.Click += (s, e) =>
            {
                _sidebarCompacto = !_sidebarCompacto;
                AplicarModoCompactoSidebar();
            };
            SidebarBottom.Children.Add(btnCompacto);

            // Botón buscador rápido
            if (_sbWidgetBusqueda)
            {
                var btnBuscar = new Button
                {
                    Width = btnW, Height = btnH,
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

        private void AplicarModoCompactoSidebar()
        {
            double anchoBase = _sidebarCompacto ? 36 : 52;

            // Detener cualquier animación activa antes de asignar
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            SidebarColumn.Width = new GridLength(_musicaPanelAbierto ? 332 : anchoBase);

            if (Sidebar.Child is Grid g && g.ColumnDefinitions.Count > 0)
                g.ColumnDefinitions[0].Width = new GridLength(anchoBase);

            RenderizarSidebar();

            File.WriteAllText(
                Path.Combine(_carpetaPerfil, "sidebar_compacto.txt"),
                _sidebarCompacto ? "true" : "false");
        }
    }
}