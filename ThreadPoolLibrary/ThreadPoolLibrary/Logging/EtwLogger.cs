
namespace ThreadPoolLibrary.Logging
{
    /// <summary>
    /// Class provides the access to ETW logging infastructure which can be used to do diagnostic tracing in the code.
    /// </summary>
    internal static class EtwLogger
    {
        private static readonly ThreadPoolLibraryEventSource _log = new ThreadPoolLibraryEventSource();

        /// <summary>
        /// Logger singleton instance for logging diagnostic events from the code.
        /// </summary>
        public static ThreadPoolLibraryEventSource Log
        {
            get { return _log; }
        }
    }
}
