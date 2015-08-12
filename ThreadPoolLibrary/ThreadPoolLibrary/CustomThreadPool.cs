using System;
using System.Globalization;
using System.Threading;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// Defines the contact for common defination of thread pool functions.
    /// </summary>
    public abstract class CustomThreadPool :IDisposable
    {
        /// <summary>
        /// Event to expose for the user when any work item delegate execution fails.
        /// </summary>
        public abstract event EventHandler<WorkItemEventArgs> UserWorkItemException;

        /// <summary>
        /// Method to enqueue a new user work item to be executed on the thread pool.
        /// </summary>
        /// <param name="target">delegate which defines the compute work needed</param>
        /// <param name="userData">any input object for user data processing.</param>
        /// <returns>true if ork item is enqueued on the pool, otherwise false</returns>
        public abstract bool QueueUserWorkItem(Action<CancellationToken, object> target, object userData);

        /// <summary>
        /// The total number of threads in the pool at the present time.
        /// </summary>
        public abstract int TotalThreads { get; }

        /// <summary>
        /// Gets a unique name of this thread pool.
        /// </summary>
        public virtual string Name { get; private set; }

        /// <summary>
        /// Creates custom worker thread pool with default name and given settings.
        /// </summary>
        protected CustomThreadPool(ThreadPoolSettings settings, CancellationToken cancelToken)
        {
            this.Name = string.Format(CultureInfo.InvariantCulture, "ThreadPool-{0}", Guid.NewGuid());
        }

        public virtual void Dispose()
        {
            
        }
    }
}
