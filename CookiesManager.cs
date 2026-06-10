using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace atsukibrowser
{
    public class CookieRegla
    {
        public string Dominio    { get; set; } = "";
        public bool   Bloqueado  { get; set; } = false; // true = nunca aceptar
    }

    public class CookiesManager
    {
        private readonly string _path;
        private List<CookieRegla> _reglas = new();

        public IReadOnlyList<CookieRegla> Reglas => _reglas;

        public CookiesManager(string carpetaPerfil)
        {
            _path = Path.Combine(carpetaPerfil, "cookie_reglas.json");
            Cargar();
        }

        public bool EstaBloqueado(string dominio)
        {
            string host = ExtraerDominio(dominio);
            return _reglas.Exists(r => host.EndsWith(r.Dominio, StringComparison.OrdinalIgnoreCase)
                                    && r.Bloqueado);
        }

        public void SetRegla(string dominio, bool bloqueado)
        {
            dominio = ExtraerDominio(dominio);
            var existente = _reglas.Find(r => r.Dominio == dominio);
            if (existente != null)
                existente.Bloqueado = bloqueado;
            else
                _reglas.Add(new CookieRegla { Dominio = dominio, Bloqueado = bloqueado });
            Guardar();
        }

        public void EliminarRegla(string dominio)
        {
            dominio = ExtraerDominio(dominio);
            _reglas.RemoveAll(r => r.Dominio == dominio);
            Guardar();
        }

        private void Cargar()
        {
            try
            {
                if (File.Exists(_path))
                    _reglas = JsonSerializer.Deserialize<List<CookieRegla>>(
                        File.ReadAllText(_path)) ?? new();
            }
            catch { _reglas = new(); }
        }

        private void Guardar()
        {
            try { File.WriteAllText(_path, JsonSerializer.Serialize(_reglas,
                new JsonSerializerOptions { WriteIndented = true })); }
            catch { }
        }

        private static string ExtraerDominio(string input)
        {
            try
            {
                if (!input.StartsWith("http")) input = "https://" + input;
                var uri = new Uri(input);
                string host = uri.Host.ToLower();
                // Guardar solo dominio+TLD (quitar subdominio)
                var partes = host.Split('.');
                return partes.Length >= 2
                    ? string.Join(".", partes[^2], partes[^1])
                    : host;
            }
            catch { return input.ToLower(); }
        }

        public string ToJson() => JsonSerializer.Serialize(_reglas,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}