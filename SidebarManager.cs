using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace atsukibrowser
{
    public class SidebarItem
    {
        public string Id      { get; set; } = "";
        public string Emoji   { get; set; } = "";
        public string Nombre  { get; set; } = "";
        public string Url     { get; set; } = "";
        public bool   Visible { get; set; } = true;
        public bool   Separador { get; set; } = false;
        public bool EsExtension { get; set; } = false;
        public string ExtensionId { get; set; } = "";
    }

    public class SidebarManager
    {
        private readonly string _path;

        private static readonly List<SidebarItem> _defaults = new()
        {
            new() { Id="home",        Emoji="🏠", Nombre="Inicio",      Url="nuevatab",               Visible=true },
            new() { Id="youtube",     Emoji="▶",  Nombre="YouTube",     Url="https://youtube.com",    Visible=true },
            new() { Id="twitter",     Emoji="𝕏",  Nombre="Twitter",     Url="https://x.com",          Visible=true },
            new() { Id="discord",     Emoji="💬", Nombre="Discord",     Url="https://discord.com/app",Visible=true },
            new() { Id="sep1",        Separador=true,                                                  Visible=true },
            new() { Id="favoritos",   Emoji="🔖", Nombre="Favoritos",   Url="favoritos",              Visible=true },
            new() { Id="historial",   Emoji="🕐", Nombre="Historial",   Url="historial",              Visible=true },
            new() { Id="descargas",   Emoji="⬇",  Nombre="Descargas",   Url="descargas",              Visible=true },
            new() { Id="extensiones", Emoji="🧩", Nombre="Extensiones", Url="extensiones",            Visible=true },
            new() { Id="sep2",        Separador=true,                                                  Visible=true },
            new() { Id="perfiles",    Emoji="👤", Nombre="Perfiles",    Url="perfiles",               Visible=true },
            new() { Id="ajustes",     Emoji="⚙",  Nombre="Ajustes",     Url="ajustes",                Visible=true },
        };

        public List<SidebarItem> Items { get; set; } = new();

        public SidebarManager(string carpeta)
        {
            _path = Path.Combine(carpeta, "sidebar.json");
            Cargar();
        }

        public void Cargar()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    Items = JsonSerializer.Deserialize<List<SidebarItem>>(json) ?? new(_defaults);
                }
                else
                {
                    Items = new(_defaults);
                }
            }
            catch { Items = new(_defaults); }
        }

        public void Guardar()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(Items,
                new JsonSerializerOptions { WriteIndented = true }));
        }

        public string ToJson() => JsonSerializer.Serialize(Items);
    }
}