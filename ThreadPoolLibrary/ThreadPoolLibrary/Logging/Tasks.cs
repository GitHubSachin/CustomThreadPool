using System.Diagnostics.Tracing;

namespace ThreadPoolLibrary.Logging
{
    /// <summary>
    /// ETW event task categories.
    /// </summary>
    internal static class Tasks
    {
        public const EventTask Pool = (EventTask)1;
        public const EventTask PoolWorker = (EventTask)2;
        public const EventTask WorkItem = (EventTask)2;
    }
}
