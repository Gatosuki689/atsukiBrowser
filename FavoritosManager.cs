using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace atsukibrowser
{
    public class EntradaFavorito
    {
        public string Url { get; set; } = "";
        public string Titulo { get; set; } = "";
        public DateTime Fecha { get; set; }
    }

    public class FavoritosManager
    {
        private readonly string _path;
        private List<EntradaFavorito> _entradas = new();
        public IReadOnlyList<EntradaFavorito> Entradas => _entradas;

        public FavoritosManager(string carpeta)
        {
            _path = Path.Combine(carpeta, "favoritos.json");
            Cargar();
        }

        public bool EsFavorito(string url) =>
            _entradas.Exists(e => e.Url == url);

        public void Agregar(string url, string titulo)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            if (EsFavorito(url)) return;
            _entradas.Insert(0, new EntradaFavorito
            {
                Url = url,
                Titulo = string.IsNullOrWhiteSpace(titulo) ? url : titulo,
                Fecha = DateTime.Now
            });
            Guardar();
        }

        public void Quitar(string url)
        {
            _entradas.RemoveAll(e => e.Url == url);
            Guardar();
        }

        public void Limpiar()
        {
            _entradas.Clear();
            Guardar();
        }

        private void Cargar()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var json = File.ReadAllText(_path);
                _entradas = JsonSerializer.Deserialize<List<EntradaFavorito>>(json) ?? new();
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