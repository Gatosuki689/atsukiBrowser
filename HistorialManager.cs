using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace atsukibrowser
{
    public class EntradaHistorial
    {
        public string Url { get; set; } = "";
        public string Titulo { get; set; } = "";
        public DateTime Fecha { get; set; }
    }

    public class HistorialManager
    {
        private readonly string _path;
        private List<EntradaHistorial> _entradas = new();
        public IReadOnlyList<EntradaHistorial> Entradas => _entradas;

        private System.Threading.Timer? _guardadoTimer;

        public HistorialManager(string carpeta)
        {
            _path = Path.Combine(carpeta, "historial.json");
            Cargar();
        }

        public void Agregar(string url, string titulo)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (url.StartsWith("file:///")) return;

            _entradas.RemoveAll(e => e.Url == url);
            _entradas.Insert(0, new EntradaHistorial
            {
                Url    = url,
                Titulo = string.IsNullOrWhiteSpace(titulo) ? url : titulo,
                Fecha  = DateTime.Now
            });

            if (_entradas.Count > 500)
                _entradas.RemoveAt(_entradas.Count - 1);

            GuardarConDebounce();
        }

        private void GuardarConDebounce()
        {
            _guardadoTimer?.Dispose();
            _guardadoTimer = new System.Threading.Timer(_ => Guardar(), null, 1500, System.Threading.Timeout.Infinite);
        }

        public void Limpiar()
        {
            _entradas.Clear();
            _guardadoTimer?.Dispose();
            Guardar();
        }

        private void Cargar()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var json = File.ReadAllText(_path);
                _entradas = JsonSerializer.Deserialize<List<EntradaHistorial>>(json) ?? new();
            }
            catch { _entradas = new(); }
        }

        private void Guardar()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var json = JsonSerializer.Serialize(_entradas,
                    new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
            }
            catch { }
        }
    }
}