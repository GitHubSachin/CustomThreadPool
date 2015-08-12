using System;

namespace ThreadPoolLibrary
{
    public class WorkItemEventArgs : EventArgs
    {
        public Exception Exception { get; set; }

        public object UserData { get; set; }
    }
}
