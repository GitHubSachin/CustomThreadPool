using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ThreadPoolLibrary.Logging;

namespace ThreadPoolLibrary
{

    /// <summary>
    /// Custom ThreadPool Implementation for compute work on dedicated running threads. The implementation allows user to specify min max values for the pool
    /// User will enqueue the work item and it will be processed in parallel upto max thread pool size limit.
    /// Design:
    /// 1. There is a global queue for the pool (type ConcurrentQueue) which stores all incoming work items
    /// 2. When pool is initiated, min number of threads are created waiting for items to arrive.
    /// 3. Each thread will try to dequeue work item form the main pool queue and process it
    ///     3a. if worker thread does not get item from the queue, it will wait for QueueItemArrivalWaitTimeout value of time and try to dequeue again.
    ///     3b. if thread is idle for ThreadIdleTimeout value, then it is terminated, till total left threads in the pool each min value.
    /// 4. if user sends cancel signal, all work is aborted and pool becomes idle. After this pool can be disposed.
    /// </summary>
    public sealed class CustomThreadPool1 : CustomThreadPool, IDisposable
    {

        /// <summary>
        /// state lock for concurrency on local instance variables.
        /// </summary>
        private readonly object _stateLock = new object();


        /// <summary>
        /// Token to indicate pool shutdown is requested by user.
        /// </summary>
        private CancellationToken _poolStopToken;

        /// <summary>
        /// linked cancellation token created from user's token to signal cancel event.
        /// </summary>
        private readonly CancellationTokenSource _linkedCts;

        /// <summary>
        /// runtime settings for thread pool
        /// </summary>
        private readonly ThreadPoolSettings _settings;

        /// <summary>
        /// cancellation token register to subscribe cancel signal and abort running work items and shutdown the pool.
        /// </summary>
        private CancellationTokenRegistration _tokenRegister;

        /// <summary>
        /// queue to store user work items.
        /// </summary>
        private readonly ConcurrentQueue<ThreadPoolWorkItem> _queue = new ConcurrentQueue<ThreadPoolWorkItem>();

        /// <summary>
        /// running threads in the pool, some may be idle or processing work items.
        /// </summary>
        private readonly Dictionary<string, Thread> _runningThreads;


        /// <summary>
        /// Creates custom worker thread pool with default name and default settings.
        /// </summary>
        public CustomThreadPool1(CancellationToken cancelToken)
            : this(new ThreadPoolSettings(), cancelToken)
        {

        }

        /// <summary>
        /// Creates new threadpool with threadpool custom setting values.
        /// </summary>
        /// <param name="settings">Threadpool settings</param>
        /// <param name="cancelToken">cancellation token to cancel all processing on the pool threads</param>
        public CustomThreadPool1(ThreadPoolSettings settings, CancellationToken cancelToken)
            : base(settings, cancelToken)
        {
            //this.Name = "ThreadPool-" + Guid.NewGuid().ToString();

            this._settings = settings;
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
            this._poolStopToken = _linkedCts.Token;
            _tokenRegister = _poolStopToken.Register(OnPoolCancelRequested);
            _runningThreads = new Dictionary<string, Thread>(_settings.MaxThreads);
            StartMinThreads();

            EtwLogger.Log.PoolStarted(this.Name, _settings.MinThreads, _settings.MaxThreads);
        }

        /// <summary>
        /// The total number of threads in the pool at the present time.
        /// </summary>
        public override int TotalThreads
        {
            get
            {
                lock (_stateLock)
                {
                    return _runningThreads.Count;
                }
            }
        }

        public override event EventHandler<WorkItemEventArgs> UserWorkItemException;

        /// <summary>
        /// Enqueues a new work on the thread pool. The work will be either immediately processed by available worker thread in the pool or scheduled as per load on the pool.
        /// </summary>
        /// <param name="target">delegate which will perform the compute work needed by the work item user</param>
        /// <param name="userData">Any user specific data needed by target delegate for processing. This can be null or any user supplied value.</param>
        /// <returns>true if work item is enqueued successfully otherwise false.</returns>
        public override bool QueueUserWorkItem(Action<CancellationToken, object> target, object userData)
        {
            //User signaled to stop all processing so return false and dont add any new work items.
            if (_poolStopToken.IsCancellationRequested) return false;

            var job = new ThreadPoolWorkItem(target, userData, _poolStopToken);
            _queue.Enqueue(job);
            bool newThreadNeeded = (_queue.Count > TotalThreads) && (TotalThreads < _settings.MaxThreads);
            if (newThreadNeeded)
            {
                StartWorkerThread(_settings.MaxThreads);
            }
            return true;
        }

        /// <summary>
        /// Ensures that the pool has at least the minimum number of threads.
        /// </summary>
        private void StartMinThreads()
        {
            while (_runningThreads.Count < _settings.MinThreads)
            {
                StartWorkerThread(_settings.MinThreads);
            }
        }

        /// <summary>
        /// Starts a new worker thread.
        /// </summary>
        private void StartWorkerThread(int limit)
        {
            System.Threading.Thread thread = null;
            lock (_stateLock)
            {
                if (_runningThreads.Count >= limit)
                {
                    return;//reached pool capacity
                }
                string threadName = Name + " thread " + (_runningThreads.Count + 1);
                thread = new System.Threading.Thread(() => WorkerThreadStart(threadName));
                thread.Name = threadName;
                _runningThreads.Add(thread.Name, thread);
            }

            thread.IsBackground = true;
            thread.Start();

            EtwLogger.Log.PoolWorkerStart(thread.Name);
        }

        internal void OnUserWorkItemException(WorkItemEventArgs e)
        {
            EtwLogger.Log.WorkItemFailure(e.ToString());
            if (UserWorkItemException != null)
            {
                UserWorkItemException(this, e);
            }
        }

        /// <summary>
        /// Method to handle pool cancel request or signal from the user.
        /// </summary>
        internal void OnPoolCancelRequested()
        {
            int runningCount;
            //Stop all working threads and cancel all work items in the queue.
            lock (_stateLock)
            {
                var keys = _runningThreads.Keys;
                runningCount = keys.Count;

                //We can call thread.abort here but its risky to call abort on running thread, what if use's delegate was running something which may leave system unstable if you abort???
                //Its best not to call abort and leave responsibility to the use to gracefully cancel long runnig user work item delegate.
                //we can add some enhancement here to expose an event to pool owner so he gets notified after all pool threads are gracefully exicted, then he can dispose the pool.
            }

            EtwLogger.Log.PoolCancelled(this.Name, runningCount);
        }

        private void WorkerThreadStart(string threadName)
        {
            try
            {
                DateTime lastItemProcesTime = DateTime.UtcNow;
                while (true)
                {
                    if (TotalThreads > _settings.MaxThreads)
                    {
                        return; //reached the capacity so exit worker threads.
                    }

                    ThreadPoolWorkItem job = GetNextJobToProcess();
                    if (job == null)
                    {
                        // decide if thread should die/exit depending on idle timeout setting because there are no items in queue.
                        if (ShouldThreadExit(lastItemProcesTime))
                        {
                            return;
                        }
                    }
                    else
                    {
                        ExecuteJob(job);
                        lastItemProcesTime = DateTime.UtcNow;
                    }
                }
            }
            finally
            {
                WorkerThreadExited(threadName);
            }
        }

        private bool ShouldThreadExit(DateTime lastItemProcesTime)
        {
            if (_poolStopToken.IsCancellationRequested)
            {
                return true; //exit has been signaled by caller
            }

            if (TotalThreads <= _settings.MinThreads)
            {
                return false;
            }

            TimeSpan idleTime = DateTime.UtcNow - lastItemProcesTime;
            if (idleTime.TotalMilliseconds > _settings.ThreadIdleTimeout.TotalMilliseconds)
            {
                return true;
            }
            return false;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")] //"needed to avoid crashing of pool thread because of bad code in user's work delegate execution"
        private void ExecuteJob(ThreadPoolWorkItem job)
        {
            try
            {
                job.Execute();
            }
            catch (Exception e)
            {
                OnUserWorkItemException(new WorkItemEventArgs()
                {
                    Exception = e,
                    UserData = job.UserData
                });

                EtwLogger.Log.WorkItemFailure(e.ToString());
            }
        }

        private void WorkerThreadExited(string threadName)
        {
            lock (_stateLock)
            {
                _runningThreads.Remove(threadName);
            }

            EtwLogger.Log.PoolWorkerExit(threadName);
        }

        private ThreadPoolWorkItem GetNextJobToProcess()
        {
            ThreadPoolWorkItem work = null;
            _queue.TryDequeue(out work);
            if (work == null)
            {
                lock (_stateLock)
                {
                    System.Threading.Monitor.Wait(_stateLock, _settings.QueueItemArrivalWaitTimeout); //wait to see if anything arrives in the queue before exiting this thread.
                }
                _queue.TryDequeue(out work);
            }
            return work;
        }

        /// <summary>
        /// Clears any managed resources held by the class.
        /// </summary>
        public override void Dispose()
        {
            if (_tokenRegister != null)
            {
                _tokenRegister.Dispose();
            }

            if (_linkedCts != null)
            {
                _linkedCts.Dispose();
            }
        }

    }
}
