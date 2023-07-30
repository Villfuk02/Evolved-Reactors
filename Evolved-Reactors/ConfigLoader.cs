using System;
using System.IO;

namespace Fission
{
    internal static class ConfigLoader
    {
        public static string LoadConfig(string path)
        {
            StreamReader file;
            try
            {
                file = new(path);
            }
            catch
            {
                return $"Could not open file \"{path}\", check if the specified path is correct.";
            }

            string line = file.ReadLine();
            if (string.IsNullOrEmpty(line)) return "File was empty.";
            Console.WriteLine($"Loading config {line.Trim()}");

            line = file.ReadLine();
            if (line == null) return "Unexpected end of file.";
            if (line.Trim() != "") return "Second line must be empty.";

            while (true)
            {
                line = file.ReadLine();
                if (line == null || line.Trim() == "")
                    break;
                string[] parts = line.Trim().Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    return $"Invalid cooler configuration \"{line.Trim()}\"";
                string name = parts[0].Trim() + " Cooler";
                var cooler = Block.ALL.Find(b => string.Equals(b.Name, name, StringComparison.InvariantCultureIgnoreCase));
                if (cooler == null)
                    return $"{name} is not a valid cooler name.";
                if (!int.TryParse(parts[1].Trim(), out int c)) return $"Cooling rate \"{c}\" is not a valid integer.";
                cooler.Cooling = c;
            }

            var o = new AddFuelOption();
            while (true)
            {
                line = file.ReadLine();
                if (line == null || line.Trim() == "")
                    return null;
                if (!o.Parse(line, out var fuel))
                    return $"Invalid fuel configuration \"{line.Trim()}\"";
            }
        }
    }
}
