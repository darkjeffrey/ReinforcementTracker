using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ReinforcementTracker
{
    internal static class Log
    {
        /// <summary>Message-log line, only when "Debug messages" is on. Debug text isn't translated.</summary>
        public static void Debug(string text)
        {
            if (!RtSettings.DebugMessages) return;
            InformationManager.DisplayMessage(new InformationMessage("[ReinforcementTracker] " + text, Colors.Gray));
        }

        public static void Error(string where, Exception e)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=rt_err_generic}[ReinforcementTracker] error ({WHERE}): {ERROR}")
                    .SetTextVariable("WHERE", where)
                    .SetTextVariable("ERROR", e.Message)
                    .ToString(),
                Colors.Red));
        }

        public static void Info(TextObject text, Color color)
        {
            InformationManager.DisplayMessage(new InformationMessage(text.ToString(), color));
        }
    }
}
