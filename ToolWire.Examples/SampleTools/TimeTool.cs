using System.ComponentModel;
using ToolWire.Tools;

namespace ToolWire.SampleTools
{
    public class TimeTool
    {
        [Tool]
        [Description("Get the current UTC time from the system clock.")]
        public static string GetUtcNow()
        {
            return DateTime.UtcNow.ToString("O"); // ISO-8601, JSON-safe
        }
    }
}
