using System;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// Event args for user work item exception events
    /// </summary>
    public class WorkItemEventArgs : EventArgs
    {
        /// <summary>
        /// Exception object that happened during execution of user's work item
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// User data that was supplied to enqueued delegate (work item)
        /// </summary>
        public object UserData { get; set; }
    }
}
