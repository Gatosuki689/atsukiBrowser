using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace atsukibrowser
{
    public class Perfil
    {
        public string Id      { get; set; } = "";
        public string Nombre  { get; set; } = "";
        public string Emoji   { get; set; } = "👤";
        public string Icono   { get; set; } = ""; // ruta a imagen local
        public bool   EsInvitado { get; set; } = false;
        public DateTime CreadoEn { get; set; } = DateTime.Now;
    }

    public class PerfilManager
    {
        private static readonly string _baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsukiBrowser");

        private static readonly string _perfilesDir = Path.Combine(_baseDir, "perfiles");
        private static readonly string _activoPath  = Path.Combine(_baseDir, "perfil_activo.txt");

        private List<Perfil> _perfiles = new();
        private Perfil _activo = null!;

        public IReadOnlyList<Perfil> Perfiles => _perfiles;
        public Perfil Activo => _activo;

        public PerfilManager()
        {
            Directory.CreateDirectory(_perfilesDir);
            Cargar();
        }

        // ── Obtener carpeta de datos de un perfil ────────────
        public string CarpetaPerfil(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "invitado")
                return Path.Combine(Path.GetTempPath(), "AtsukiBrowser_invitado");

            return Path.Combine(_perfilesDir, SanitizarId(id));
        }

        public string CarpetaActiva() => CarpetaPerfil(_activo.Id);

        // ── Crear perfil ─────────────────────────────────────
        public Perfil Crear(string nombre, string emoji = "👤", string icono = "")
        {
            var perfil = new Perfil
            {
                Id      = Guid.NewGuid().ToString("N")[..8],
                Nombre  = nombre,
                Emoji   = emoji,
                Icono   = icono,
                CreadoEn = DateTime.Now
            };

            Directory.CreateDirectory(CarpetaPerfil(perfil.Id));
            _perfiles.Add(perfil);
            Guardar();
            return perfil;
        }

        // ── Perfil invitado ──────────────────────────────────
        public Perfil CrearInvitado()
        {
            return new Perfil
            {
                Id          = "invitado",
                Nombre      = "Invitado",
                Emoji       = "🕶",
                EsInvitado  = true
            };
        }

        // ── Cambiar perfil activo ────────────────────────────
        public void CambiarA(string id)
        {
            if (id == "invitado")
            {
                _activo = CrearInvitado();
                // Limpiar SIEMPRE la carpeta del invitado al entrar
                var tmpDir = CarpetaPerfil("invitado");
                if (Directory.Exists(tmpDir))
                    Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);
                // NO guardar en _activoPath para que no persista
                return;
            }
            else
            {
                var perfil = _perfiles.Find(p => p.Id == id);
                if (perfil == null) return;
                _activo = perfil;
            }

            File.WriteAllText(_activoPath, _activo.Id);
        }

        // ── Eliminar perfil ──────────────────────────────────
        public void Eliminar(string id)
        {
            if (id == "default" || id == "invitado") return;
            _perfiles.RemoveAll(p => p.Id == id);

            var carpeta = CarpetaPerfil(id);
            if (Directory.Exists(carpeta))
                Directory.Delete(carpeta, true);

            // Si era el activo, volver al default
            if (_activo.Id == id)
                CambiarA(_perfiles.Count > 0 ? _perfiles[0].Id : "default");

            Guardar();
        }

        // ── Editar perfil ────────────────────────────────────
        public void Editar(string id, string nombre, string emoji, string icono)
        {
            var perfil = _perfiles.Find(p => p.Id == id);
            if (perfil == null) return;
            perfil.Nombre = nombre;
            perfil.Emoji  = emoji;
            perfil.Icono  = icono;
            if (_activo.Id == id)
            {
                _activo.Nombre = nombre;
                _activo.Emoji  = emoji;
                _activo.Icono  = icono;
            }
            Guardar();
        }

        // ── Serializar para HTML ─────────────────────────────
        public string ToJson() => JsonSerializer.Serialize(new
        {
            activo   = _activo,
            perfiles = _perfiles
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        // ── Persistencia ─────────────────────────────────────
        private void Cargar()
        {
            var metaPath = Path.Combine(_perfilesDir, "perfiles.json");
            try
            {
                if (File.Exists(metaPath))
                    _perfiles = JsonSerializer.Deserialize<List<Perfil>>(
                        File.ReadAllText(metaPath)) ?? new();
            }
            catch { _perfiles = new(); }

            // Asegurar perfil default
            if (!_perfiles.Exists(p => p.Id == "default"))
            {
                _perfiles.Insert(0, new Perfil
                {
                    Id      = "default",
                    Nombre  = "Principal",
                    Emoji   = "⭐",
                    CreadoEn = DateTime.Now
                });
                Directory.CreateDirectory(CarpetaPerfil("default"));
                Guardar();
            }

            // Cargar activo
            string activoId = "default";
            if (File.Exists(_activoPath))
                activoId = File.ReadAllText(_activoPath).Trim();

            if (activoId == "invitado")
            {
                // Limpiar carpeta temporal siempre al entrar como invitado
                var tmpDir = CarpetaPerfil("invitado");
                if (Directory.Exists(tmpDir))
                    Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);
                _activo = CrearInvitado();
            }
            else
            {
                _activo = _perfiles.Find(p => p.Id == activoId) ?? _perfiles[0];
            }
        }

        private void Guardar()
        {
            var metaPath = Path.Combine(_perfilesDir, "perfiles.json");
            Directory.CreateDirectory(_perfilesDir);
            File.WriteAllText(metaPath, JsonSerializer.Serialize(_perfiles,
                new JsonSerializerOptions { WriteIndented = true }));
        }

        private static string SanitizarId(string id) =>
            string.Concat(id.Split(Path.GetInvalidFileNameChars()));
    }
}