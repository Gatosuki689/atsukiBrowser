using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace atsukibrowser
{
    public class RenombrarDialog : Window
    {
        public string NuevoNombre { get; private set; } = "";
        private TextBox _input;

        public RenombrarDialog(string nombreActual)
        {
            Title = "Renombrar";
            Width = 300; Height = 140;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(22, 18, 40));

            var panel = new StackPanel { Margin = new Thickness(16) };

            var label = new TextBlock
            {
                Text = "Nuevo nombre:",
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };

            _input = new TextBox
            {
                Text = nombreActual,
                Background = new SolidColorBrush(Color.FromRgb(30, 26, 56)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                CaretBrush = Brushes.White,
                Margin = new Thickness(0, 0, 0, 12)
            };
            _input.SelectAll();
            _input.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) Confirmar();
                if (e.Key == System.Windows.Input.Key.Escape) DialogResult = false;
            };

            var btnOk = new Button
            {
                Content = "Guardar",
                Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 7, 16, 7),
                FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            btnOk.Click += (s, e) => Confirmar();

            panel.Children.Add(label);
            panel.Children.Add(_input);
            panel.Children.Add(btnOk);
            Content = panel;

            Loaded += (s, e) => _input.Focus();
        }

        private void Confirmar()
        {
            NuevoNombre = _input.Text.Trim();
            DialogResult = true;
        }
    }
}