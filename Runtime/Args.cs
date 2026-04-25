using System.Globalization;

namespace DevConsole.Runtime
{
    public static class Args
    {
        public static bool TryParseInt(string s, out int n)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
        }

        public static bool TryParseFloat(string s, out float n)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n);
        }

        public static bool RequireArgs(string[] args, int n, DevConsoleHost host, string usage)
        {
            if (args.Length == n)
                return true;
            host.AppendLine($"usage: {usage}");
            return false;
        }

        public static bool RequireArgsAtLeast(
            string[] args,
            int n,
            DevConsoleHost host,
            string usage
        )
        {
            if (args.Length >= n)
                return true;
            host.AppendLine($"usage: {usage}");
            return false;
        }
    }
}
