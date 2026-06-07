using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace atsukibrowser
{
    public class Atajo
    {
        public string Accion      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public bool   Ctrl        { get; set; }
        public bool   Shift       { get; set; }
        public bool   Alt         { get; set; }
        public string Tecla       { get; set; } = ""; // ej: "T", "F5", "Tab"
    }

    public class AtajosManager
    {
        private readonly string _path;
        private Dictionary<string, Atajo> _atajos = new();
        public IReadOnlyDictionary<string, Atajo> Atajos => _atajos;

        // ── Defaults ─────────────────────────────────────────
        private static readonly List<Atajo> _defaults = new()
        {
            new() { Accion="nueva_tab",        Descripcion="Nueva pestaña",           Ctrl=true,  Shift=false, Alt=false, Tecla="T"      },
            new() { Accion="cerrar_tab",        Descripcion="Cerrar pestaña",          Ctrl=true,  Shift=false, Alt=false, Tecla="W"      },
            new() { Accion="recargar",          Descripcion="Recargar página",         Ctrl=true,  Shift=false, Alt=false, Tecla="R"      },
            new() { Accion="recargar_f5",       Descripcion="Recargar (F5)",           Ctrl=false, Shift=false, Alt=false, Tecla="F5"     },
            new() { Accion="enfocar_url",       Descripcion="Enfocar barra URL",       Ctrl=true,  Shift=false, Alt=false, Tecla="L"      },
            new() { Accion="sig_tab",           Descripcion="Siguiente pestaña",       Ctrl=true,  Shift=false, Alt=false, Tecla="Tab"    },
            new() { Accion="ant_tab",           Descripcion="Pestaña anterior",        Ctrl=true,  Shift=true,  Alt=false, Tecla="Tab"    },
            new() { Accion="reabrir_tab",       Descripcion="Reabrir pestaña cerrada", Ctrl=true,  Shift=true,  Alt=false, Tecla="T"      },
            new() { Accion="pantalla_completa", Descripcion="Pantalla completa",       Ctrl=false, Shift=false, Alt=false, Tecla="F11"    },
            new() { Accion="zoom_mas",          Descripcion="Zoom +",                  Ctrl=true,  Shift=false, Alt=false, Tecla="OemPlus"},
            new() { Accion="zoom_menos",        Descripcion="Zoom -",                  Ctrl=true,  Shift=false, Alt=false, Tecla="OemMinus"},
            new() { Accion="zoom_reset",        Descripcion="Zoom normal",             Ctrl=true,  Shift=false, Alt=false, Tecla="D0"     },
            new() { Accion="favoritos",         Descripcion="Ir a favoritos",          Ctrl=true,  Shift=false, Alt=false, Tecla="D1" },
            new() { Accion="historial",         Descripcion="Ir a historial",          Ctrl=true,  Shift=false, Alt=false, Tecla="H"      },
            new() { Accion="descargas",         Descripcion="Ir a descargas",          Ctrl=true,  Shift=false, Alt=false, Tecla="J"      },
            new() { Accion="nueva_ventana",     Descripcion="Nueva ventana",           Ctrl=true,  Shift=false, Alt=false, Tecla="N"      },
            new() { Accion="captura",           Descripcion="Captura de pantalla",     Ctrl=true,  Shift=false, Alt=false, Tecla="P"      },
            new() { Accion="busqueda_rapida",   Descripcion="Búsqueda rápida sidebar", Ctrl=true,  Shift=false, Alt=false, Tecla="K"      },
            new() { Accion="musica_play",       Descripcion="Play/Pause música",       Ctrl=false, Shift=false, Alt=false, Tecla="MediaPlayPause" },
            new() { Accion="pip", Descripcion="Picture-in-Picture", Ctrl=true, Shift=true, Alt=false, Tecla="P" },
            new() { Accion="modo_zen", Descripcion="Modo Zen", Ctrl=true, Shift=true, Alt=false, Tecla="Z"},
        };

        public AtajosManager(string carpeta)
        {
            _path = Path.Combine(carpeta, "atajos.json");
            Cargar();
        }

        public bool Coincide(string accion, bool ctrl, bool shift, bool alt, string tecla)
        {
            if (!_atajos.TryGetValue(accion, out var a)) return false;
            return a.Ctrl == ctrl && a.Shift == shift && a.Alt == alt &&
                   a.Tecla.Equals(tecla, StringComparison.OrdinalIgnoreCase);
        }

        public void Establecer(string accion, bool ctrl, bool shift, bool alt, string tecla)
        {
            if (!_atajos.ContainsKey(accion)) return;
            _atajos[accion] = new Atajo
            {
                Accion      = accion,
                Descripcion = _atajos[accion].Descripcion,
                Ctrl        = ctrl,
                Shift       = shift,
                Alt         = alt,
                Tecla       = tecla
            };
            Guardar();
        }

        public void Restablecer(string accion)
        {
            var def = _defaults.Find(d => d.Accion == accion);
            if (def != null) _atajos[accion] = def;
            Guardar();
        }

        public void RestablecerTodos()
        {
            foreach (var d in _defaults)
                _atajos[d.Accion] = d;
            Guardar();
        }

        public string ToJson() => JsonSerializer.Serialize(
            new List<Atajo>(_atajos.Values),
            new JsonSerializerOptions { WriteIndented = false });

        private void Cargar()
        {
            // Partir de defaults
            foreach (var d in _defaults)
                _atajos[d.Accion] = d;

            try
            {
                if (!File.Exists(_path)) return;
                var lista = JsonSerializer.Deserialize<List<Atajo>>(File.ReadAllText(_path));
                if (lista == null) return;
                foreach (var a in lista)
                    if (_atajos.ContainsKey(a.Accion))
                        _atajos[a.Accion] = a;
            }
            catch { }
        }

        private void Guardar()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(
                    new List<Atajo>(_atajos.Values),
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}