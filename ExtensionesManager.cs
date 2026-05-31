using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.IO.Compression;
using System.Linq;

namespace atsukibrowser
{
    public class WidgetConfig
    {
        public string Archivo { get; set; } = "";
        public string Id      { get; set; } = "";
        public string Titulo  { get; set; } = "";
        public string Ancho   { get; set; } = "normal";
        public string Alto    { get; set; } = "";      // ← agregar esta
        public string Destino { get; set; } = "";
        public string TipoSidebar { get; set; } = "";
        public string Emoji   { get; set; } = "";
        public string Url     { get; set; } = "";
    }

    public class ManifestExtension
    {
        public string Id          { get; set; } = "";
        public string Nombre      { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Version     { get; set; } = "1.0";
        public string Icono       { get; set; } = "";
        public string Tipo        { get; set; } = "atsuki";
        public bool   Activa      { get; set; } = true;
        public string RutaCarpeta { get; set; } = "";
        public WidgetConfig? Widget { get; set; } = null;
    }

    public class ExtensionesManager
    {
        private static readonly string _extDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtsukiBrowser", "Extensions");

        private readonly string _estadoPath = null!;

        public List<ManifestExtension> Extensiones { get; private set; } = new();

        public ExtensionesManager(string carpeta)
        {
            _estadoPath = Path.Combine(carpeta, "extensiones_estado.json");
            Directory.CreateDirectory(_extDir);
            CrearAdblockSiNoExiste();
            Cargar();
        }

        public void Cargar()
        {
            Extensiones.Clear();

            // Cargar estados guardados
            var estados = new Dictionary<string, bool>();
            try
            {
                if (File.Exists(_estadoPath))
                {
                    estados = JsonSerializer.Deserialize<Dictionary<string, bool>>(
                        File.ReadAllText(_estadoPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
            }
            catch { }

            // Leer carpetas de extensiones
            foreach (var dir in Directory.GetDirectories(_extDir))
            {
                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath)) continue;

                try
                {
                    var manifest = JsonSerializer.Deserialize<ManifestExtension>(
                        File.ReadAllText(manifestPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                    manifest.Id = Path.GetFileName(dir);
                    manifest.RutaCarpeta = dir;

                    // Restaurar estado guardado
                    if (estados.TryGetValue(manifest.Id, out bool activa))
                        manifest.Activa = activa;

                    Extensiones.Add(manifest);
                }
                catch { }
            }
        }

        public void SetActiva(string id, bool activa)
        {
            var ext = Extensiones.Find(e => e.Id == id);
            if (ext != null) ext.Activa = activa;
            GuardarEstados();
        }

        public void Instalar(string rutaOrigen)
        {
            string id = Path.GetFileName(rutaOrigen);
            string destino = Path.Combine(_extDir, id);
            CopiarCarpeta(rutaOrigen, destino);
            Cargar();
        }

        public void Desinstalar(string id)
        {
            string ruta = Path.Combine(_extDir, id);
            if (Directory.Exists(ruta))
                Directory.Delete(ruta, true);
            Cargar();
            GuardarEstados();
        }

        // Devuelve el contenido JS de todas las extensiones atsuki activas
        public List<string> GetScriptsActivos()
        {
            var scripts = new List<string>();
            foreach (var ext in Extensiones)
            {
                if (!ext.Activa || ext.Tipo != "atsuki") continue;
                string jsPath = Path.Combine(ext.RutaCarpeta, "content.js");
                if (File.Exists(jsPath))
                    scripts.Add(File.ReadAllText(jsPath));
            }
            return scripts;
        }

        // Devuelve rutas de extensiones Chrome/Edge activas
        public List<string> GetExtensionesChrome()
        {
            var rutas = new List<string>();
            foreach (var ext in Extensiones)
            {
                if (!ext.Activa || ext.Tipo != "chrome") continue;
                rutas.Add(ext.RutaCarpeta);
            }
            return rutas;
        }

        public string ToJson()
        {
            var lista = Extensiones.Select(ext => new
            {
                ext.Id,
                ext.Nombre,
                ext.Descripcion,
                ext.Version,
                ext.Tipo,
                ext.Activa,
                ext.Widget,
                Icono     = ext.Icono,
                IconoData = GetIconoBase64(ext.Id)
            });
            return JsonSerializer.Serialize(lista,
                new JsonSerializerOptions { WriteIndented = false });
        }

        private void GuardarEstados()
        {
            var estados = new Dictionary<string, bool>();
            foreach (var ext in Extensiones)
                estados[ext.Id] = ext.Activa;
            File.WriteAllText(_estadoPath, JsonSerializer.Serialize(estados));
        }

        private void CrearAdblockSiNoExiste()
        {
            string adblockDir = Path.Combine(_extDir, "adblock");
            if (Directory.Exists(adblockDir)) return;

            Directory.CreateDirectory(adblockDir);

            // manifest.json
            var manifest = new
            {
                Nombre      = "Bloqueador de anuncios",
                Descripcion = "Oculta anuncios y elementos de tracking en todas las páginas.",
                Version     = "1.0",
                Tipo        = "atsuki",
                Activa      = true,
                Icono       = ""
            };
            File.WriteAllText(
                Path.Combine(adblockDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            // content.js — CSS injection adblock
            File.WriteAllText(Path.Combine(adblockDir, "content.js"), AdblockScript);
        }

        private static void CopiarCarpeta(string origen, string destino)
        {
            Directory.CreateDirectory(destino);
            foreach (var file in Directory.GetFiles(origen))
                File.Copy(file, Path.Combine(destino, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(origen))
                CopiarCarpeta(dir, Path.Combine(destino, Path.GetFileName(dir)));
        }

        public List<(ManifestExtension ext, string html)> GetWidgetsActivos()
        {
            var widgets = new List<(ManifestExtension, string)>();
            foreach (var ext in Extensiones)
            {
                if (!ext.Activa || ext.Widget == null) continue;
                string htmlPath = Path.Combine(ext.RutaCarpeta, ext.Widget.Archivo);
                if (!File.Exists(htmlPath)) continue;
                widgets.Add((ext, File.ReadAllText(htmlPath)));
            }
            return widgets;
        }

        public List<(ManifestExtension ext, string contenido)> GetWidgetsSidebarActivos()
        {
            var widgets = new List<(ManifestExtension, string)>();
            foreach (var ext in Extensiones)
            {
                if (!ext.Activa || ext.Widget == null) continue;
                if (ext.Widget.Destino != "sidebar") continue;
                string path = Path.Combine(ext.RutaCarpeta, ext.Widget.Archivo);
                if (!File.Exists(path)) continue;
                widgets.Add((ext, File.ReadAllText(path)));
            }
            return widgets;
        }

        public void InstalarDesdeAtsuki(string rutaArchivo)
        {
            string id = Path.GetFileNameWithoutExtension(rutaArchivo);
            string destino = Path.Combine(_extDir, id);

            // Si ya existe, borrar primero para reemplazar limpiamente
            if (Directory.Exists(destino))
                Directory.Delete(destino, true);

            System.IO.Compression.ZipFile.ExtractToDirectory(rutaArchivo, destino);
            Cargar();
        }

        public void ExportarAtsuki(string id, string rutaDestino)
        {
            string carpeta = Path.Combine(_extDir, id);
            if (!Directory.Exists(carpeta)) return;

            // Borrar si ya existe el archivo destino
            if (File.Exists(rutaDestino))
                File.Delete(rutaDestino);

            System.IO.Compression.ZipFile.CreateFromDirectory(carpeta, rutaDestino);
        }

        public string? GetIconoBase64(string id)
        {
            var ext = Extensiones.Find(e => e.Id == id);
            if (ext == null) return null;

            string[] extensiones = { ".png", ".jpg", ".jpeg", ".webp", ".gif" };
            foreach (var archivo in Directory.GetFiles(ext.RutaCarpeta))
            {
                string extArchivo = Path.GetExtension(archivo).ToLower();
                if (Array.IndexOf(extensiones, extArchivo) >= 0)
                {
                    string base64 = Convert.ToBase64String(File.ReadAllBytes(archivo));
                    string mime = extArchivo == ".jpg" || extArchivo == ".jpeg"
                        ? "image/jpeg" : $"image/{extArchivo.TrimStart('.')}";
                    return $"data:{mime};base64,{base64}";
                }
            }
            return null;
        }

        // ── Script del bloqueador de anuncios ────────────────
        private const string AdblockScript = """
(function() {
    const SELECTORS = [
        // Genéricos
        '[id^="ad-"],[id^="ads-"],[class^="ad-"],[class^="ads-"]',
        '#header-banner, .masthead-ad-control',
        '[class*="sponsor"],[id*="sponsor"]',
        '[class*="promo"],[id*="promo"]',
        '[class*="advertisement"],[id*="advertisement"]',
        '[class*="advert"],[id*="advert"]',
        '[data-ad],[data-ads],[data-advertisement]',
        'ins.adsbygoogle',
        '#google_ads_frame',

        // YouTube
        '.ytp-ad-module',
        '.ytd-ad-slot-renderer',
        'ytd-promoted-sparkles-web-renderer',
        'ytd-banner-promo-renderer',
        '#masthead-ad',
        'ytd-statement-banner-renderer',
        'ytd-in-feed-ad-layout-renderer',
        '.ytd-display-ad-renderer',

        // Twitch
        '.tw-ad-unit',
        '[data-a-target="tw-ad"]',

        // Reddit
        '.promotedlink',
        'shreddit-ad-post',

        // Genérico tracking / popups
        '[class*="popup"],[id*="popup"]',
        '[class*="overlay"][class*="ad"]',
        'iframe[src*="doubleclick"]',
        'iframe[src*="googlesyndication"]',
        'iframe[src*="adnxs"]',
        'iframe[src*="amazon-adsystem"]',
    ];

    function bloquear() {
        const style = document.getElementById('__atsuki_adblock__');
        if (style) return;

        const css = SELECTORS.join(',\n') + ' { display: none !important; visibility: hidden !important; opacity: 0 !important; pointer-events: none !important; height: 0 !important; min-height: 0 !important; }';

        const el = document.createElement('style');
        el.id = '__atsuki_adblock__';
        el.textContent = css;
        (document.head || document.documentElement).appendChild(el);
    }

    // Aplicar inmediatamente y al cargar el DOM
    bloquear();
    document.addEventListener('DOMContentLoaded', bloquear);

    // Observer para elementos añadidos dinámicamente
    let _timer = null;
    const observer = new MutationObserver(() => {
        if (_timer) return;
        _timer = setTimeout(() => {
            _timer = null;
            SELECTORS.forEach(sel => {
                try {
                    document.querySelectorAll(sel).forEach(el => {
                        el.style.cssText = 'display:none!important;height:0!important;';
                    });
                } catch {}
            });
        }, 300);
    });

    observer.observe(document.documentElement, {
        childList: true,
        subtree: true
    });

    observer.observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();
""";
    }
}