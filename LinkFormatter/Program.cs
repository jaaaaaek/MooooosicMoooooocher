using System.Configuration;
using System.Text;

namespace LinkFormatter
{
    internal class Program
    {
        static void Main()
        {
            PrintHeader();

            try
            {
                string formattedCommands = FormatLinks();
                int commandCount = formattedCommands.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

                PrintSuccess(commandCount);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(formattedCommands);
                Console.ResetColor();
                PrintFooter();
            }
            catch (Exception e)
            {
                PrintError(e.Message);
            }
        }

        static void PrintHeader()
        {
            const int boxWidth = 70;
            string border = new string('═', boxWidth);

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine();
            Console.WriteLine("        .       *           .        *        .    ");
            Console.WriteLine("    *        .        *           .        .       ");
            Console.WriteLine("        .        *        .    *       *           ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"   ╔{border}╗");
            PrintBoxLine("", boxWidth);
            PrintBoxLine("                                    .-.    .-.    .-.", boxWidth);
            PrintBoxLine("                                .--'   '--'   '--'   '--.", boxWidth);
            PrintBoxLine("  ____                        _  ____ _                 _", boxWidth);
            PrintBoxLine(" / ___|  ___  _   _ _ __   __| |/ ___| | ___  _   _  __| |", boxWidth);
            PrintBoxLine(@" \___ \ / _ \| | | | '_ \ / _` | |   | |/ _ \| | | |/ _` |", boxWidth);
            PrintBoxLine("  ___) | (_) | |_| | | | | (_| | |___| | (_) | |_| | (_| |", boxWidth);
            PrintBoxLine(@" |____/ \___/ \__,_|_| |_|\__,_|\____|_|\___/ \__,_|\__,_|", boxWidth);
            PrintBoxLine("                                '--..                ..--'", boxWidth);
            PrintBoxLine("", boxWidth);
            PrintBoxLine("        L I N K   F O R M A A A A A A A A T T E R", boxWidth);
            Console.WriteLine($"   ╚{border}╝");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("        *        .    *       *        .       *   ");
            Console.WriteLine("    .        *           .        .       *        ");
            Console.WriteLine("        .       *           .        *        .    ");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void PrintBoxLine(string content, int width)
        {
            Console.WriteLine($"   ║{content.PadRight(width)}║");
        }

        static void PrintSuccess(int count)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [+] {count} command(s) generated successfully!");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  [>] Paste these into the \"Music\" folder to download.");
            Console.ResetColor();
        }

        static void PrintFooter()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  ──────────────────────────────────────────");
            Console.WriteLine("  Done. Happy listening!");
            Console.ResetColor();
            Console.WriteLine();
        }

        static void PrintError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("  [X] Error: " + message);
            Console.ResetColor();
            Console.WriteLine();
        }

        static string FormatLinks()
        {
            string? authToken = ConfigurationManager.AppSettings["SoundCloudToken"];
            string currentDir = Directory.GetCurrentDirectory();

            if (string.IsNullOrWhiteSpace(authToken))
            {
                throw new Exception("No \"SoundCloudToken\" value found in app.config!");
            }

            List<string> existingLinks = GenerateExistingLinks(currentDir);

            return GenerateFormattedLinks(currentDir, authToken, existingLinks);
        }

        static string GenerateFormattedLinks(string currentDir, string authToken, List<string> existingLinks)
        {
            StringBuilder formattedCommands = new StringBuilder();

            string[] lines = File.ReadAllLines(Path.Combine(currentDir, "Music", "NewDownloads.txt"));

            if (lines.Length == 0 || lines.All(string.IsNullOrWhiteSpace))
            {
                throw new Exception("No links found in NewDownloads.txt! Put some links from soundcloud in there!");
            }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.Count(x => x == '/') != 3 &&
                    !line.Contains("/you/") &&
                    !existingLinks.Any(x => x.Equals(line.Trim())))
                {
                    formattedCommands.AppendLine($"yt-dlp -f ba --extract-audio --audio-format wav {line} --add-header \"Authorization: OAuth {authToken}\" --extractor-retries 10 --retry-sleep extractor:300");
                }
            }

            return formattedCommands.ToString();
        }

        static List<string> GenerateExistingLinks(string currentDir)
        {
            string[] lines = File.ReadAllLines(Path.Combine(currentDir, "Music", "ExistingDownloads.txt"));

            return lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToList();
        }
    }
}
