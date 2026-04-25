using System.Reflection;
using Artitas.Utils;
using log4net;

namespace DevConsole
{
    public class ModConstants
    {
        public const string LogPrefix = "[DevConsole]";

        public static readonly ILog Log = ArtitasLogger.GetLogger(
            MethodBase.GetCurrentMethod()!.DeclaringType
        );

        public static readonly bool IsWarnEnabled = Log.IsWarnEnabled;
        public static readonly bool IsInfoEnabled = Log.IsInfoEnabled;
    }
}
