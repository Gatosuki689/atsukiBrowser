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
        private async void VerificarActualizaciones()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AtsukiBrowser");
                string json = await _httpClient.GetStringAsync(
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

                if (string.IsNullOrEmpty(ultima) || !EsVersionMasNueva(ultima, AppVersion)) return;

                Dispatcher.Invoke(() => MostrarNotificacionUpdate(ultima, url, notas));
            }
            catch { }
        }

        private bool EsVersionMasNueva(string candidata, string actual)
        {
            static (Version ver, int pre) Parsear(string v)
            {
                var limpia = v.TrimStart('v');
                var partes = limpia.Split('-');
                var ver = Version.TryParse(partes[0], out var vv) ? vv : new Version(0, 0);
                int pre = 0;
                if (partes.Length > 1)
                {
                    // Extraer número del sufijo: "prev1" → 1, "pre2" → 2, "beta3" → 3
                    var match = System.Text.RegularExpressions.Regex.Match(partes[1], @"\d+");
                    pre = match.Success ? int.Parse(match.Value) : 0;
                }
                return (ver, pre);
            }

            var (vCand, preCand) = Parsear(candidata);
            var (vAct,  preAct)  = Parsear(actual);

            if (vCand > vAct) return true;
            if (vCand < vAct) return false;

            // Misma versión numérica
            bool candTieneSufijo = candidata.Contains("-");
            bool actTieneSufijo  = actual.Contains("-");

            if (!candTieneSufijo && actTieneSufijo) return true;  // stable > preview
            if (candTieneSufijo && !actTieneSufijo) return false; // preview < stable
            if (candTieneSufijo && actTieneSufijo)  return preCand > preAct; // prev2 > prev1

            return false;
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

                // Mostrar progreso
                Dispatcher.Invoke(() => {
                    _ignorarTextChanged = true;
                    UrlBar.Text = $"Descargando actualización v{version}...";
                    _ignorarTextChanged = false;
                });

                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AtsukiBrowser");
                var bytes = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(temp, bytes);

                Dispatcher.Invoke(() => {
                    _ignorarTextChanged = true;
                    UrlBar.Text = "✅ Descarga completada. Instalando...";
                    _ignorarTextChanged = false;
                });
                
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
                    _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AtsukiBrowser");
                    string json = await _httpClient.GetStringAsync(
                        $"https://gist.githubusercontent.com/Gatosuki689/f24638ebb9ed77db3a58fc2318103b39/raw/version.json?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

                    var doc  = JsonSerializer.Deserialize<JsonElement>(json);
                    string notas = doc.TryGetProperty("notas", out var n) ? n.GetString() ?? "" : "";

                    Dispatcher.Invoke(() => AbrirNotasVersion(AppVersion, notas));
                }
                catch { }
            });
        }
    }
}