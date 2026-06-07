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
        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab < 0) return;
           _aplicandoZoom = true;
            _tabs[_activeTab].ZoomFactor = Math.Min(_tabs[_activeTab].ZoomFactor + 0.1, 3.0);
            _aplicandoZoom = false;
            string dominio = GetDominioZoom(_tabs[_activeTab].Source?.ToString() ?? "");
            if (!string.IsNullOrEmpty(dominio))
            {
                _zoomPorDominio[dominio] = _tabs[_activeTab].ZoomFactor;
                GuardarZoomDebounced();
            }
            ActualizarZoomLabel();
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTab < 0) return;
            _aplicandoZoom = true;
            _tabs[_activeTab].ZoomFactor = Math.Max(_tabs[_activeTab].ZoomFactor - 0.1, 0.25);
            _aplicandoZoom = false;
            string dominio = GetDominioZoom(_tabs[_activeTab].Source?.ToString() ?? "");
            if (!string.IsNullOrEmpty(dominio))
            {
                _zoomPorDominio[dominio] = _tabs[_activeTab].ZoomFactor;
                GuardarZoomDebounced();
            }
            ActualizarZoomLabel();
        }

        private void ActualizarZoomLabel()
        {
            if (_activeTab < 0) return;
            // Usar dispatcher con baja prioridad para leer después de que WebView2 aplique el zoom
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
            {
                if (_activeTab < 0 || _activeTab >= _tabs.Count) return;
                int pct = (int)Math.Round(_tabs[_activeTab].ZoomFactor * 100);
                ZoomLabel.Text = pct + "%";
            });
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
                if (uri.Scheme is "about" or "data") return "";
                if (uri.Scheme == "file")
                {
                    // Usar el nombre del archivo como clave para páginas internas
                    string archivo = Path.GetFileNameWithoutExtension(uri.LocalPath);
                    return string.IsNullOrEmpty(archivo) ? "" : $"__local__{archivo}";
                }
                return uri.Host;
            }
            catch { return ""; }
        }

        private void GuardarZoom()
        {
            try
            {
                File.WriteAllText(_zoomPath,
                    JsonSerializer.Serialize(_zoomPorDominio));
            }
            catch { }
        }

        private void CargarZoom()
        {
            try
            {
                if (File.Exists(_zoomPath))
                    _zoomPorDominio = JsonSerializer.Deserialize<Dictionary<string, double>>(
                        File.ReadAllText(_zoomPath)) ?? new();
            }
            catch { }
        }

        private void GuardarZoomDebounced()
        {
            _zoomSaveTimer?.Stop();
            _zoomSaveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            _zoomSaveTimer.Tick += (s, e) =>
            {
                _zoomSaveTimer.Stop();
                GuardarZoom();
            };
            _zoomSaveTimer.Start();
        }
    }
}