using System.Diagnostics.Tracing;

namespace ThreadPoolLibrary.Logging
{
    /// <summary>
    /// This class defines the logging methods needed for tracing.
    /// Note for Optimizing Performance for High Volume Events:
    /// The EventSource class has a number of overloads for WriteEvent, including one for variable number of arguments. When none of the other overloads matches, the “params” method is called. Unfortunately, the “params” overload is relatively expensive.
    /// if you add new log event methods, avoid params if its high volume event.
    /// </summary>
    [EventSource(Name = "ThreadPoolLibraryEventSource")]
    internal sealed class ThreadPoolLibraryEventSource : EventSource
    {

        [Event(1, Message = "Application Failure: {0}", Level = EventLevel.Error, Keywords = Keywords.Diagnostic)]
        public void Failure(string message) { WriteEvent(1, message); }

        [Event(2, Message = "New CustomThreadPool Started:{0} MinThreads:{1} MaxThreads:{2}", Opcode = EventOpcode.Start, Task = Tasks.Pool, Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void PoolStarted(string poolName, int min, int max)
        {
            WriteEvent(2, poolName, min, max);
        }

        [Event(3, Message = "CustomThreadPool Cancelled:{0} WorkingThreads:{1}", Opcode = EventOpcode.Suspend, Task = Tasks.Pool, Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void PoolCancelled(string poolName, int runningWorkerCount)
        {
            WriteEvent(3, poolName, runningWorkerCount);
        }

        [Event(4, Message = "New pool thread started: {0}", Opcode = EventOpcode.Start, Task = Tasks.PoolWorker, Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void PoolWorkerStart(string threadName)
        {
            WriteEvent(4, threadName);
        }

        [Event(5, Message = "Pool thread released: {0}", Opcode = EventOpcode.Stop, Task = Tasks.PoolWorker, Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void PoolWorkerExit(string threadName)
        {
            WriteEvent(5, threadName);
        }

        [Event(6, Opcode = EventOpcode.Receive, Task = Tasks.PoolWorker, Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void PoolWorkerSelected(string threadName, int taskCount)
        {
            WriteEvent(6, threadName, taskCount);
        }

        [Event(7, Message = "Work item assignment failed for thread: {0} total running threads: {1}", Opcode = EventOpcode.Receive, Task = Tasks.PoolWorker, Keywords = Keywords.Diagnostic, Level = EventLevel.Critical)]
        public void PoolWorkerAssignmentFailed(string threadName, int taskCount)
        {
            WriteEvent(7, threadName, taskCount);
        }

        [Event(8, Message = "User work item delegate failed with exception: {0}", Level = EventLevel.Error,Keywords = Keywords.Diagnostic)]
        public void WorkItemFailure(string message)
        {
            WriteEvent(8, message);
        }

        [Event(9, Message = "Pool thread aborted: {0}", Opcode = EventOpcode.Suspend, Task = Tasks.PoolWorker, Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void PoolThreadAborted(string threadName)
        {
            WriteEvent(9, threadName);
        }

        [Event(10, Message = "Pool thread aborted exception: {0} {1}", Opcode = EventOpcode.Suspend, Task = Tasks.PoolWorker, Keywords = Keywords.Diagnostic, Level = EventLevel.Informational)]
        public void PoolThreadAbortFailure(string threadName,string exception)
        {
            WriteEvent(10, threadName, exception);
        }

        [Event(11, Message = "Thread pool limit warning, pool name: {0} current size: {1} max size: {2}", Opcode = EventOpcode.Info, Task = Tasks.Pool, Keywords = Keywords.Diagnostic, Level = EventLevel.Warning)]
        public void PoolSizeWarning(string poolName,int currentSize, int maxLimit)
        {
            WriteEvent(11, poolName, currentSize, maxLimit);
        }

    }
}
