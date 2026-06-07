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

        private void ActualizarColorBotones()
        {
            ActualizarEstiloTabs();
        }
    }
}