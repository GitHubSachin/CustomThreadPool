using System.Diagnostics.Tracing;

namespace ThreadPoolLibrary.Logging
{
    /// <summary>
    /// ETW event keywords
    /// </summary>
    internal static class Keywords
    {
        public const EventKeywords Page = (EventKeywords)1;
        public const EventKeywords DataBase = (EventKeywords)2;
        public const EventKeywords Diagnostic = (EventKeywords)4;
        public const EventKeywords Perf = (EventKeywords)8;
    }
}
