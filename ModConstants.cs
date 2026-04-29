using System.Reflection;
using Artitas.Utils;
using log4net;

namespace DevConsole
{
    public static class ModConstants
    {
        public const string LogPrefix = "[DevConsole]";

        // Rich-text colors used in console output. Wrap with Cmd/Key for highlights.
        public const string CommandColor = "#ffb86c";
        public const string ArgColor = "#9ec5e8";
        public const string ShortcutColor = "#a78bfa";

        public static string Cmd(string name) => $"<color={CommandColor}>{name}</color>";

        public static string Key(string name) => $"<color={ShortcutColor}>{name}</color>";

        public static string Sig(string signature)
        {
            var space = signature.IndexOf(' ');
            if (space < 0)
                return Cmd(signature);
            return Cmd(signature.Substring(0, space))
                + $"<color={ArgColor}>{signature.Substring(space)}</color>";
        }

        public static readonly ILog Log = ArtitasLogger.GetLogger(
            MethodBase.GetCurrentMethod()!.DeclaringType
        );

        public static readonly bool IsWarnEnabled = Log.IsWarnEnabled;
        public static readonly bool IsInfoEnabled = Log.IsInfoEnabled;
    }
}
