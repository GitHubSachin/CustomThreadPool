using System;

namespace ThreadPoolLibrary
{
    internal class ThreadExitEventArgs : EventArgs
    {
        public string ThreadName { get; set; }
    }
}
