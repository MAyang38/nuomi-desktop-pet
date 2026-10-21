using System;
using System.Globalization;
using System.IO;

namespace DesktopKitten
{
    // Keep the original three lines readable by older releases.
    public sealed class PetSettings
    {
        public double Size = .9;
        public bool Roaming = true;
        public bool Topmost = true;
        public double? Left;
        public double? Top;

        public static PetSettings Load(string path)
        {
            PetSettings settings = new PetSettings();
            if (!File.Exists(path)) return settings;
            string[] lines = File.ReadAllLines(path);
            double number;
            bool flag;
            if (lines.Length > 0 && Number(lines[0], out number) && number >= .6 && number <= 1.3) settings.Size = number;
            if (lines.Length > 1 && Boolean.TryParse(lines[1].Trim(), out flag)) settings.Roaming = flag;
            if (lines.Length > 2 && Boolean.TryParse(lines[2].Trim(), out flag)) settings.Topmost = flag;
            if (lines.Length > 4 && Number(lines[3], out number))
            {
                double top;
                if (Number(lines[4], out top)) { settings.Left = number; settings.Top = top; }
            }
            return settings;
        }

        public void Save(string path)
        {
            string[] lines = {
                Size.ToString(CultureInfo.InvariantCulture),
                Roaming ? "true" : "false", Topmost ? "true" : "false",
                Left.HasValue ? Left.Value.ToString("R", CultureInfo.InvariantCulture) : "",
                Top.HasValue ? Top.Value.ToString("R", CultureInfo.InvariantCulture) : ""
            };
            string temporary = path + ".tmp";
            try
            {
                File.WriteAllLines(temporary, lines);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static bool Number(string text, out double value)
        {
            return Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !Double.IsNaN(value) && !Double.IsInfinity(value);
        }
    }
}
