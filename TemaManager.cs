using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace atsukibrowser
{
    public class Tema
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Accent { get; set; } = "#7c3aed";
        public string Bg { get; set; } = "#0d0d14";
        public string Surface { get; set; } = "#13131f";
        public string Surface2 { get; set; } = "#1a1a2e";
        public string Border { get; set; } = "";       // se genera automático desde Accent
        public string Font { get; set; } = "Segoe UI";
        public bool EsCustom { get; set; } = false;
    }

    public class TemaManager
    {
        private readonly string _path;

        // ── Temas predefinidos ───────────────────────────────
        public static readonly List<Tema> TemasPredefinidos = new()
        {
            new Tema
            {
                Id      = "default",
                Nombre  = "Por defecto",
                Accent  = "#7c3aed",
                Bg      = "#0d0d14",
                Surface = "#13131f",
                Surface2= "#1a1a2e",
                Font    = "Segoe UI"
            },
            new Tema
            {
                Id      = "sakura",
                Nombre  = "Sakura",
                Accent  = "#f472b6",
                Bg      = "#0f0a0e",
                Surface = "#1a1118",
                Surface2= "#241820",
                Font    = "Segoe UI"
            },
            new Tema
            {
                Id      = "neon",
                Nombre  = "Neon",
                Accent  = "#00ffe0",
                Bg      = "#050d0d",
                Surface = "#0a1a1a",
                Surface2= "#0f2424",
                Font    = "Segoe UI"
            },
            new Tema
            {
                Id      = "void",
                Nombre  = "Void",
                Accent  = "#a855f7",
                Bg      = "#05050f",
                Surface = "#0b0b1a",
                Surface2= "#11112a",
                Font    = "Segoe UI"
            },
            new Tema
            {
                Id      = "ember",
                Nombre  = "Ember",
                Accent  = "#f97316",
                Bg      = "#0d0800",
                Surface = "#1a1000",
                Surface2= "#251800",
                Font    = "Segoe UI"
            },
            new Tema
            {
                Id      = "arctic",
                Nombre  = "Arctic",
                Accent  = "#38bdf8",
                Bg      = "#060d14",
                Surface = "#0d1a24",
                Surface2= "#122030",
                Font    = "Segoe UI"
            },
            new Tema
            {
                Id      = "monochrome",
                Nombre  = "Monochrome",
                Accent  = "#e2e8f0",
                Bg      = "#0a0a0a",
                Surface = "#141414",
                Surface2= "#1e1e1e",
                Font    = "Segoe UI"
            }
        };

        // ── Estado actual ────────────────────────────────────
        private Tema _temaActivo = TemasPredefinidos[0]; // Atsuki por defecto
        public Tema TemaActivo => _temaActivo;

        public TemaManager(string carpeta)
        {
            _path = Path.Combine(carpeta, "tema.json");
            Cargar();
        }
        // ── Aplicar tema predefinido ─────────────────────────
        public void AplicarPredefinido(string id)
        {
            var tema = TemasPredefinidos.Find(t => t.Id == id);
            if (tema == null) return;
            _temaActivo = tema;
            Guardar();
        }

        // ── Aplicar tema custom ──────────────────────────────
        public void AplicarCustom(string accent, string bg, string surface,
                                  string surface2, string font)
        {
            _temaActivo = new Tema
            {
                Id       = "custom",
                Nombre   = "Custom",
                Accent   = accent,
                Bg       = bg,
                Surface  = surface,
                Surface2 = surface2,
                Font     = font,
                EsCustom = true
            };
            Guardar();
        }

        public void SetAccent(string accent)
        {
            _temaActivo = new Tema
            {
                Id       = "custom",
                Nombre   = "Custom",
                Accent   = accent,
                Bg       = _temaActivo.Bg,
                Surface  = _temaActivo.Surface,
                Surface2 = _temaActivo.Surface2,
                Font     = _temaActivo.Font,
                EsCustom = true
            };
            Guardar();
        }

        // ── Serializar para enviar al HTML ───────────────────
        public string ToJson()
        {
            return JsonSerializer.Serialize(_temaActivo,
                new JsonSerializerOptions { WriteIndented = false });
        }

        // ── Generar color de borde desde el accent ───────────
        // Devuelve "rgba(r,g,b,0.25)" para usar como CSS border
        public static string BorderDesdeAccent(string hex)
        {
            try
            {
                hex = hex.TrimStart('#');
                int r = Convert.ToInt32(hex[..2], 16);
                int g = Convert.ToInt32(hex[2..4], 16);
                int b = Convert.ToInt32(hex[4..6], 16);
                return $"rgba({r},{g},{b},0.25)";
            }
            catch { return "rgba(124,58,237,0.25)"; }
        }

        // ── Persistencia ─────────────────────────────────────
        private void Cargar()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var json = File.ReadAllText(_path);
                var tema = JsonSerializer.Deserialize<Tema>(json);
                if (tema != null) _temaActivo = tema;
            }
            catch { _temaActivo = TemasPredefinidos[0]; }
        }

        private void Guardar()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(_temaActivo,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
