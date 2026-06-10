using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace atsukibrowser
{
    public partial class MainWindow : Window
    {
        private void ToggleModoZen()
        {
            _modoZen = !_modoZen;
            var duracion = TimeSpan.FromMilliseconds(200);

            // Guardar altura actual de GruposRow para restaurarla después
            double alturaGrupos = GruposBar.Visibility == Visibility.Visible ? 32 : 0;

            if (_modoZen)
            {
                TabBar.Visibility = Visibility.Visible;
                NavBar.Visibility = Visibility.Visible;

                double elapsed = 0;
                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (s, e) =>
                {
                    elapsed += 16;
                    double p = Math.Min(elapsed / duracion.TotalMilliseconds, 1.0);
                    double t = p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;
                    double inv = 1.0 - t;

                    MainGrid.RowDefinitions[0].Height = new GridLength(34 * inv);   // TabBar
                    // Row 1 = GruposRow — no tocar, se maneja sola
                    MainGrid.RowDefinitions[2].Height = new GridLength(40 * inv);   // NavBar
                    double ancho = _musicaPanelAbierto ? 332 : 52;
                    SidebarColumn.Width = new GridLength(ancho * inv);

                    if (p >= 1.0)
                    {
                        timer.Stop();
                        MainGrid.RowDefinitions[0].Height = new GridLength(0);
                        MainGrid.RowDefinitions[2].Height = new GridLength(0);
                        SidebarColumn.Width = new GridLength(0);
                        TabBar.Visibility  = Visibility.Collapsed;
                        NavBar.Visibility  = Visibility.Collapsed;
                        GruposBar.Visibility = Visibility.Collapsed; // ocultar también grupos
                    }
                };
                timer.Start();
            }
            else
            {
                TabBar.Visibility = Visibility.Visible;
                NavBar.Visibility = Visibility.Visible;
                // Restaurar GruposBar si había grupos
                if (_tabGroups.Count > 0 && alturaGrupos > 0)
                    GruposBar.Visibility = Visibility.Visible;

                double elapsed = 0;
                var durMs = duracion.TotalMilliseconds;
                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(16) };
                timer.Tick += (s, e) =>
                {
                    elapsed += 16;
                    double p = Math.Min(elapsed / durMs, 1.0);
                    double t = p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;

                    MainGrid.RowDefinitions[0].Height = new GridLength(34 * t);   // TabBar
                    // Row 1 = GruposRow — no tocar
                    MainGrid.RowDefinitions[2].Height = new GridLength(40 * t);   // NavBar
                    double ancho = _musicaPanelAbierto ? 332 : 52;
                    SidebarColumn.Width = new GridLength(ancho * t);

                    if (p >= 1.0)
                    {
                        timer.Stop();
                        MainGrid.RowDefinitions[0].Height = new GridLength(34);
                        MainGrid.RowDefinitions[2].Height = new GridLength(40);
                        SidebarColumn.Width = new GridLength(ancho);
                    }
                };
                timer.Start();
            }
        }
    }
}
