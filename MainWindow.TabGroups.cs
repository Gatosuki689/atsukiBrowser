using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace atsukibrowser
{
    public partial class MainWindow : Window
    {
        private static readonly Color[] _coloresGrupo =
        {
            Color.FromRgb(124, 58,  237), // púrpura
            Color.FromRgb(239, 68,   68), // rojo
            Color.FromRgb(59,  130, 246), // azul
            Color.FromRgb(16,  185, 129), // verde
            Color.FromRgb(245, 158,  11), // naranja
            Color.FromRgb(236, 72,  153), // rosa
        };
        private int _colorGrupoIdx = 0;

        // ── Menú: añadir tab a grupo ─────────────────────────────────────────
        private void AñadirTabAGrupo(int tabIdx)
        {
            var menu = new ContextMenu
            {
                Background      = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                BorderBrush     = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1)
            };

            var itemNuevo = new MenuItem
            {
                Header          = "➕  Nuevo grupo",
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding         = new Thickness(12, 7, 12, 7),
                Foreground      = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                FontSize        = 12
            };
            itemNuevo.Click += (s, e) => CrearGrupoConTab(tabIdx);
            menu.Items.Add(itemNuevo);

            if (_tabGroups.Count > 0)
            {
                menu.Items.Add(new Separator
                    { Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)) });

                foreach (var grupo in _tabGroups)
                {
                    var g = grupo;
                    var panel = new StackPanel { Orientation = Orientation.Horizontal };
                    panel.Children.Add(new System.Windows.Shapes.Ellipse
                    {
                        Width = 10, Height = 10,
                        Fill = new SolidColorBrush(g.Color),
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    panel.Children.Add(new TextBlock
                    {
                        Text = g.Nombre,
                        Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var itemGrupo = new MenuItem
                    {
                        Header = panel,
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(12, 7, 12, 7)
                    };
                    itemGrupo.Click += (s, e) =>
                    {
                        if (!g.TabIndices.Contains(tabIdx))
                            g.TabIndices.Add(tabIdx);
                        ActualizarEstiloTabs();
                        RenderizarBotonesGrupo();
                    };
                    menu.Items.Add(itemGrupo);
                }

                // Opción quitar si ya está en algún grupo
                var grupoActual = _tabGroups.FirstOrDefault(g => g.TabIndices.Contains(tabIdx));
                if (grupoActual != null)
                {
                    menu.Items.Add(new Separator
                        { Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)) });
                    var itemQuitar = new MenuItem
                    {
                        Header = "✕  Quitar del grupo",
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(12, 7, 12, 7),
                        Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                        FontSize = 12
                    };
                    itemQuitar.Click += (s, e) =>
                    {
                        grupoActual.TabIndices.Remove(tabIdx);
                        if (grupoActual.TabIndices.Count == 0)
                            _tabGroups.Remove(grupoActual);
                        ActualizarEstiloTabs();
                        RenderizarBotonesGrupo();
                    };
                    menu.Items.Add(itemQuitar);
                }
            }

            menu.PlacementTarget = _tabButtons[tabIdx];
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        // ── Crear nuevo grupo ────────────────────────────────────────────────
        private void CrearGrupoConTab(int tabIdx)
        {
            var win = new Window
            {
                Title = "Nuevo grupo",
                Width = 320, Height = 200,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(19, 19, 30)),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stack = new StackPanel { Margin = new Thickness(20) };

            var txtNombre = new TextBox
            {
                Text = "Grupo " + _nextGroupId,
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 14),
                CaretBrush = Brushes.White
            };

            var colorPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            Color colorSeleccionado = _coloresGrupo[_colorGrupoIdx % _coloresGrupo.Length];
            var colorBtns = new List<System.Windows.Shapes.Ellipse>();

            foreach (var col in _coloresGrupo)
            {
                var c = col;
                var elipse = new System.Windows.Shapes.Ellipse
                {
                    Width = 24, Height = 24,
                    Fill = new SolidColorBrush(c),
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    Stroke = c == colorSeleccionado ? Brushes.White : Brushes.Transparent,
                    StrokeThickness = 2
                };
                elipse.MouseLeftButtonDown += (s, e) =>
                {
                    colorSeleccionado = c;
                    foreach (var el in colorBtns)
                        el.Stroke = ((SolidColorBrush)el.Fill).Color == c
                            ? Brushes.White : Brushes.Transparent;
                };
                colorBtns.Add(elipse);
                colorPanel.Children.Add(elipse);
            }

            var btnCrear = new Button
            {
                Content = "Crear grupo",
                Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 8, 16, 8),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };
            btnCrear.Click += (s, e) =>
            {
                var grupo = new TabGroup
                {
                    Id         = _nextGroupId++,
                    Nombre     = string.IsNullOrWhiteSpace(txtNombre.Text)
                                    ? "Grupo " + _nextGroupId : txtNombre.Text.Trim(),
                    Color      = colorSeleccionado,
                    TabIndices = new List<int> { tabIdx }
                };
                _tabGroups.Add(grupo);
                _colorGrupoIdx++;
                win.Close();
                ActualizarEstiloTabs();
                RenderizarBotonesGrupo();

                // DEBUG temporal
                MessageBox.Show($"Grupos: {_tabGroups.Count}, GruposBar null: {GruposBar == null}, GruposStrip null: {GruposStrip == null}");

                ActualizarEstiloTabs();
                RenderizarBotonesGrupo();
            };

            stack.Children.Add(txtNombre);
            stack.Children.Add(colorPanel);
            stack.Children.Add(btnCrear);
            win.Content = stack;
            win.ShowDialog();
        }

        // ── Colapsar / expandir grupo ────────────────────────────────────────
        private void ColapsarGrupo(TabGroup grupo)
        {
            grupo.Colapsado = !grupo.Colapsado;

            foreach (int idx in grupo.TabIndices)
            {
                if (idx < 0 || idx >= _tabButtons.Count) continue;
                _tabButtons[idx].Visibility = grupo.Colapsado
                    ? Visibility.Collapsed : Visibility.Visible;
                _tabs[idx].Visibility = grupo.Colapsado
                    ? Visibility.Collapsed
                    : (idx == _activeTab ? Visibility.Visible : Visibility.Hidden);
            }

            // Si la tab activa quedó colapsada, activar la primera disponible
            if (grupo.Colapsado && grupo.TabIndices.Contains(_activeTab))
            {
                int siguiente = Enumerable.Range(0, _tabs.Count)
                    .FirstOrDefault(i => !_tabGroups.Any(g =>
                        g.Colapsado && g.TabIndices.Contains(i)), 0);
                ActivarTab(siguiente);
            }
        }

        // ── Indicador de color en la tab ─────────────────────────────────────
        // El contenido del btn es un Grid (no StackPanel), así que usamos el borde inferior del btn
        private void AplicarColorGrupoATab(Button btn, int idx)
        {
            var grupo = _tabGroups.FirstOrDefault(g => g.TabIndices.Contains(idx));

            // El borde inferior de color se aplica como BorderBrush con thickness solo abajo
            if (grupo != null)
            {
                btn.BorderBrush     = new SolidColorBrush(grupo.Color);
                btn.BorderThickness = new Thickness(0, 0, 0, 3);
            }
            else
            {
                btn.BorderBrush     = Brushes.Transparent;
                btn.BorderThickness = new Thickness(0);
            }
        }

        // ── Reindexar al cerrar una tab ───────────────────────────────────────
        private void ReindexarGrupos(int tabCerrada)
        {
            foreach (var grupo in _tabGroups.ToList())
            {
                grupo.TabIndices.Remove(tabCerrada);
                grupo.TabIndices = grupo.TabIndices
                    .Select(i => i > tabCerrada ? i - 1 : i)
                    .ToList();
                // NO eliminar el grupo aunque quede vacío
                // Se elimina solo cuando el usuario lo pide explícitamente
            }
            // Limpiar grupos vacíos solo si no tienen nombre personalizado
            // (opcional — por ahora no eliminar automáticamente)
        }

        // ── Renderizar barra de grupos ────────────────────────────────────────
        private void RenderizarBotonesGrupo()
        {
            GruposStrip.Children.Clear();

            if (_tabGroups.Count == 0)
            {
                GruposBar.Visibility = Visibility.Collapsed;
                return;
            }

            GruposBar.Visibility = Visibility.Visible;

            foreach (var grupo in _tabGroups)
            {
                // Mostrar siempre, aunque no tenga tabs
                var g = grupo;

                var lbl = new TextBlock
                {
                    Text = g.Colapsado
                        ? $"{g.Nombre} ({g.TabIndices.Count}) ▶"
                        : $"{g.Nombre} ▾",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 8, Height = 8,
                    Fill = new SolidColorBrush(g.Color),
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                sp.Children.Add(lbl);

                var btnGrupo = new Button
                {
                    Content         = sp,
                    Background      = new SolidColorBrush(Color.FromArgb(40, g.Color.R, g.Color.G, g.Color.B)),
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(80, g.Color.R, g.Color.G, g.Color.B)),
                    BorderThickness = new Thickness(1),
                    Padding         = new Thickness(10, 0, 10, 0),
                    Height          = 24,
                    Cursor          = Cursors.Hand,
                    Margin          = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Click izquierdo — colapsar/expandir
                btnGrupo.Click += (s, e) =>
                {
                    ColapsarGrupo(g);
                    lbl.Text = g.Colapsado
                        ? $"{g.Nombre} ({g.TabIndices.Count}) ▶"
                        : $"{g.Nombre} ▾";
                };

                // Menú contextual click derecho
                var ctx = new ContextMenu
                {
                    Background      = new SolidColorBrush(Color.FromRgb(30, 30, 46)),
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                    BorderThickness = new Thickness(1)
                };

                MenuItem CrearOpcion(string texto, Color? color, Action accion)
                {
                    var item = new MenuItem
                    {
                        Header          = texto,
                        Background      = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        Padding         = new Thickness(12, 7, 12, 7),
                        Foreground      = color.HasValue
                            ? new SolidColorBrush(color.Value)
                            : new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                        FontSize = 12
                    };
                    item.Click += (s, e) => accion();
                    return item;
                }

                ctx.Items.Add(CrearOpcion("✏  Renombrar", null, () =>
                {
                    var win = new Window
                    {
                        Title = "Renombrar grupo",
                        Width = 280, Height = 130,
                        WindowStyle = WindowStyle.ToolWindow,
                        ResizeMode = ResizeMode.NoResize,
                        Background = new SolidColorBrush(Color.FromRgb(19, 19, 30)),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this
                    };
                    var stk = new StackPanel { Margin = new Thickness(16) };
                    var txt = new TextBox
                    {
                        Text = g.Nombre,
                        Background = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                        Foreground = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(61, 42, 110)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(8, 6, 8, 6),
                        FontSize = 13,
                        CaretBrush = Brushes.White,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    var btnOk = new Button
                    {
                        Content = "Guardar",
                        Background = new SolidColorBrush(Color.FromRgb(124, 58, 237)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(14, 7, 14, 7),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        FontFamily = new FontFamily("Segoe UI"),
                        Cursor = Cursors.Hand
                    };
                    btnOk.Click += (s2, e2) =>
                    {
                        if (!string.IsNullOrWhiteSpace(txt.Text))
                            g.Nombre = txt.Text.Trim();
                        win.Close();
                        RenderizarBotonesGrupo();
                        ActualizarEstiloTabs();
                    };
                    txt.KeyDown += (s2, e2) => { if (e2.Key == Key.Enter) btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
                    stk.Children.Add(txt);
                    stk.Children.Add(btnOk);
                    win.Content = stk;
                    win.ShowDialog();
                }));

                ctx.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(42, 26, 78)) });

                ctx.Items.Add(CrearOpcion("✕  Cerrar tabs del grupo",
                    Color.FromRgb(239, 68, 68), () =>
                {
                    var indices = g.TabIndices.OrderByDescending(i => i).ToList();
                    // Desconectar del grupo ANTES de cerrar para que ReindexarGrupos no lo elimine prematuramente
                    g.TabIndices.Clear();
                    _tabGroups.Remove(g);
                    foreach (int idx in indices)
                        if (idx >= 0 && idx < _tabs.Count)
                            CerrarTab(idx);
                    RenderizarBotonesGrupo();
                }));

                ctx.Items.Add(CrearOpcion("🗑  Eliminar grupo (sin cerrar tabs)",
                    Color.FromRgb(239, 68, 68), () =>
                {
                    _tabGroups.Remove(g);
                    ActualizarEstiloTabs();
                    RenderizarBotonesGrupo();
                }));

                btnGrupo.ContextMenu = ctx;
                btnGrupo.MouseRightButtonUp += (s, e) =>
                {
                    ctx.PlacementTarget = btnGrupo;
                    ctx.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                    ctx.IsOpen = true;
                    e.Handled = true;
                };
                btnGrupo.ContextMenuOpening += (s, e) => e.Handled = true;

                GruposStrip.Children.Add(btnGrupo);
            }
        }
    }
}
