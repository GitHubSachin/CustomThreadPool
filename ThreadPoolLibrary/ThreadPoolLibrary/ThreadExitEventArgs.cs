using System;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// event args that wraps the thread exit event data.
    /// </summary>
    internal class ThreadExitEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the thread that exit the pool.
        /// </summary>
        public string ThreadName { get; set; }
    }
}
