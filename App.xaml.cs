using System.Windows;
using System;
using System.IO;

namespace atsukibrowser
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            string perfilId = "default";
            foreach (var arg in e.Args)
                if (arg.StartsWith("--perfil="))
                    perfilId = arg.Substring("--perfil=".Length);

            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AtsukiBrowser", "perfil_activo.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, perfilId);   // ← ANTES de base.OnStartup()

            base.OnStartup(e);
        }
    }
}