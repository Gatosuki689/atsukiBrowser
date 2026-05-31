using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace atsukibrowser
{
    public class EntradaDescarga
    {
        public string Id        { get; set; } = Guid.NewGuid().ToString();
        public string Nombre    { get; set; } = "";
        public string Ruta      { get; set; } = "";
        public string Url       { get; set; } = "";
        public long   Total     { get; set; } = 0;
        public long   Recibido  { get; set; } = 0;
        public string Estado    { get; set; } = "descargando"; // descargando, completado, cancelado, error
        public DateTime Fecha   { get; set; } = DateTime.Now;

        public int Progreso => Total > 0 ? (int)(Recibido * 100 / Total) : 0;
        public string TamañoStr => Total > 0 ? $"{Recibido / 1024 / 1024:0.0} / {Total / 1024 / 1024:0.0} MB" : $"{Recibido / 1024} KB";
    }

    public class DescargasManager
    {
        private readonly string _histPath = null!;
        private readonly string _configPath = null!;

        public List<EntradaDescarga> Activas  { get; } = new();
        public List<EntradaDescarga> Historial { get; private set; } = new();
        public string CarpetaDefault { get; private set; }

        public DescargasManager(string carpeta)
        {
            _histPath   = Path.Combine(carpeta, "descargas.json");
            _configPath = Path.Combine(carpeta, "descargas_config.json");
            CarpetaDefault = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            CargarConfig();
            CargarHistorial();
        }
        public void SetCarpeta(string ruta)
        {
            CarpetaDefault = ruta;
            GuardarConfig();
        }

        public EntradaDescarga IniciarDescarga(string url, string nombre)
        {
            var entrada = new EntradaDescarga
            {
                Url    = url,
                Nombre = nombre,
                Ruta   = Path.Combine(CarpetaDefault, nombre),
                Estado = "descargando"
            };
            Activas.Add(entrada);
            return entrada;
        }

        public void CompletarDescarga(string id)
        {
            var entrada = Activas.Find(e => e.Id == id);
            if (entrada == null) return;
            entrada.Estado = "completado";
            Activas.Remove(entrada);
            Historial.Insert(0, entrada);
            if (Historial.Count > 200) Historial.RemoveAt(Historial.Count - 1);
            GuardarHistorial();
        }

        public void CancelarDescarga(string id)
        {
            var entrada = Activas.Find(e => e.Id == id);
            if (entrada == null) return;
            entrada.Estado = "cancelado";
            Activas.Remove(entrada);
            Historial.Insert(0, entrada);
            GuardarHistorial();
        }

        public void LimpiarHistorial()
        {
            Historial.Clear();
            GuardarHistorial();
        }

        public string ToJsonHistorial() => JsonSerializer.Serialize(Historial);
        public string ToJsonActivas()   => JsonSerializer.Serialize(Activas);

        private void CargarConfig()
        {
            try
            {
                if (!File.Exists(_configPath)) return;
                var json = File.ReadAllText(_configPath);
                var doc  = JsonSerializer.Deserialize<JsonElement>(json);
                if (doc.TryGetProperty("carpeta", out var c))
                    CarpetaDefault = c.GetString() ?? CarpetaDefault;
            }
            catch { }
        }

        private void GuardarConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            File.WriteAllText(_configPath, JsonSerializer.Serialize(new { carpeta = CarpetaDefault }));
        }

        private void CargarHistorial()
        {
            try
            {
                if (!File.Exists(_histPath)) return;
                Historial = JsonSerializer.Deserialize<List<EntradaDescarga>>(
                    File.ReadAllText(_histPath)) ?? new();
            }
            catch { }
        }

        private void GuardarHistorial()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_histPath)!);
            File.WriteAllText(_histPath, JsonSerializer.Serialize(Historial,
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}