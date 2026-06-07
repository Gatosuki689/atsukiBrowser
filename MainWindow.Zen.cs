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
        private void ToggleModoZen()
        {
            _modoZen = !_modoZen;
            var duracion = TimeSpan.FromMilliseconds(200);

            if (_modoZen)
            {
                // Animar con DoubleAnimation sobre un helper y actualizar manualmente
                TabBar.Visibility = Visibility.Visible;
                NavBar.Visibility = Visibility.Visible;

                var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 0, duracion)
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
                };

                anim.CurrentTimeInvalidated += (s, e) =>
                {
                    var clock = s as System.Windows.Media.Animation.AnimationClock;
                    double p = clock?.CurrentProgress ?? 1.0;
                    double inv = 1.0 - p;
                    MainGrid.RowDefinitions[0].Height = new GridLength(34 * inv);
                    MainGrid.RowDefinitions[2].Height = new GridLength(40 * inv);
                    double anchoActual = _musicaPanelAbierto ? 332 : 52;
                    SidebarColumn.Width = new GridLength(anchoActual * inv);
                };

                anim.Completed += (s, e) =>
                {
                    MainGrid.RowDefinitions[0].Height = new GridLength(0);
                    MainGrid.RowDefinitions[1].Height = new GridLength(0);
                    SidebarColumn.Width = new GridLength(0);
                    TabBar.Visibility = Visibility.Collapsed;
                    NavBar.Visibility = Visibility.Collapsed;
                };

                // Animar un elemento dummy para robar el tick
                var dummy = new System.Windows.Media.Animation.DoubleAnimation(0, 1, duracion);
                MarcaAgua.BeginAnimation(OpacityProperty, dummy);

                // Usar DispatcherTimer como driver de la animación
                double elapsed = 0;
                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (s, e) =>
                {
                    elapsed += 16;
                    double p = Math.Min(elapsed / duracion.TotalMilliseconds, 1.0);
                    // Ease in-out cúbico manual
                    double t = p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;
                    double inv = 1.0 - t;
                    MainGrid.RowDefinitions[0].Height = new GridLength(34 * inv);
                    MainGrid.RowDefinitions[1].Height = new GridLength(40 * inv);
                    double ancho = _musicaPanelAbierto ? 332 : 52;
                    SidebarColumn.Width = new GridLength(ancho * inv);

                    if (p >= 1.0)
                    {
                        timer.Stop();
                        TabBar.Visibility = Visibility.Collapsed;
                        NavBar.Visibility = Visibility.Collapsed;
                    }
                };
                timer.Start();
            }
            else
            {
                TabBar.Visibility = Visibility.Visible;
                NavBar.Visibility = Visibility.Visible;

                double elapsed = 0;
                var durMs = duracion.TotalMilliseconds;
                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (s, e) =>
                {
                    elapsed += 16;
                    double p = Math.Min(elapsed / durMs, 1.0);
                    double t = p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;
                    MainGrid.RowDefinitions[0].Height = new GridLength(34 * t);
                    MainGrid.RowDefinitions[1].Height = new GridLength(40 * t);
                    double ancho = _musicaPanelAbierto ? 332 : 52;
                    SidebarColumn.Width = new GridLength(ancho * t);

                    if (p >= 1.0)
                    {
                        timer.Stop();
                        // Restaurar valores exactos
                        MainGrid.RowDefinitions[0].Height = new GridLength(34);
                        MainGrid.RowDefinitions[1].Height = new GridLength(40);
                        SidebarColumn.Width = new GridLength(ancho);
                    }
                };
                timer.Start();
            }
        }

    }
}