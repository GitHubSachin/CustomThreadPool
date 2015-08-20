using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using ThreadPoolLibrary.Logging;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// Design:
    /// 1. When pool is initiated, min number of pool worker threads are created and kept running.
    /// 2. Pool have a global queue and each pool thread have local queue.
    /// 3. When user adds a works item
    ///     3a. if work item is added from pool thread it gets enqueued to pool local queue, this is lock free because its local to that thread.
    ///     3b. if work item is added from non pool thread it is enqueued to global queue.
    /// 4. Pool will auto shrink and expand between min and max number of threads specified in the setting
    ///     4a. When number of queue items becomes more than min pool threads and time lapse since last thread added to pool was more than 5 seconds, a new thread is added to the pool (only if running threads are less than max config)
    ///     4b. when thread is idle for config idle time, it is terminated. This does not apply to min pool threads, they are kept running all times.
    /// 5. pool threads are processing work in this order (1) check local queue for new item, (2) check global queue for new item, (3) get item from neighbour thread.
    /// 6. There is special queue for thread local queue, this queue allows thread to add and remove items lock free and only does locking during LIFO dequeue stage (dequeue last item), means some other thread is idle and wants to take work form another thread.
    /// </summary>
    /// <remarks>
    /// Why there is local queue per thread? and why share work items across any threads?
    /// This is one of the efficiency in work scheduling. Its very useful when a delegate assigned to one thread pool worker adds many work items as a result of its execution. There are two advantages to this
    /// 1. Enqueuing new work from executing delegate is super fast because there is no locking, its local to the thread.
    /// 2. The data and memory on same thread is hot means better performance for execution.
    /// With this approach we get a thread local queue which is lock free for local operations but uses some locking when accessed from another threads, this is how we distribute work efficiently, 
    /// when other threads finish their work, they can start borrowing work from busy running threads and we get good parallelism.
    /// </remarks>
    public sealed class CustomThreadPool3 : CustomThreadPool
    {
        private class WorkerThreadStartParams
        {
            public bool IsPermenant { get; set; }
            public string ThreadName { get; set; }
        }

        public override event EventHandler<WorkItemEventArgs> UserWorkItemException;
        private readonly ThreadPoolSettings _settings;
        private readonly Queue<ThreadPoolWorkItem> _mQueue = new Queue<ThreadPoolWorkItem>();
        private ThreadLocalStealQueue<ThreadPoolWorkItem>[] _mThreadLocalQueues;
        private Thread[] _mThreads;

        /// <summary>
        /// Token to indicate pool shutdown is requested by user.
        /// </summary>
        private CancellationToken _poolStopToken;
        private DateTime _lastThreadAddTime = DateTime.UtcNow;
        private readonly CancellationTokenSource _linkedCts;
        private readonly CancellationTokenRegistration _tokenRegister;

        [ThreadStatic]
        private static ThreadLocalStealQueue<ThreadPoolWorkItem> _mWsq;

        private readonly ConcurrentDictionary<string, Thread> _workerThreads = new ConcurrentDictionary<string, Thread>();

        /// <summary>
        /// Creates new threadpool with given settings and cancel token values
        /// </summary>
        /// <param name="settings">thread pool runtime settings</param>
        /// <param name="cancelToken">cancel token to indicate or send stop event to the pool</param>
        public CustomThreadPool3(ThreadPoolSettings settings, CancellationToken cancelToken)
            : base(settings, cancelToken)
        {
            _settings = settings;
            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
            this._poolStopToken = _linkedCts.Token;
            _tokenRegister = _poolStopToken.Register(OnPoolCancelRequested);

            //start minimum required threads immediately
            _mThreads = new Thread[_settings.MaxThreads];
            _mThreadLocalQueues = new ThreadLocalStealQueue<ThreadPoolWorkItem>[_settings.MaxThreads];

            for (int i = 0; i < _settings.MinThreads; i++)
            {
                StartNewThread(true);
            }

            EtwLogger.Log.PoolStarted(Name, _settings.MinThreads, _settings.MaxThreads);
        }

        /// <summary>
        /// Creates new thread pool with given cancel token to stop the pool when needed
        /// </summary>
        /// <param name="cancelToken">cancellation token which will be used by pool to stop when set</param>
        public CustomThreadPool3(CancellationToken cancelToken)
            : this(new ThreadPoolSettings(), cancelToken)
        {

        }

        /// <summary>
        /// Enqueues new work to be executed in thread pool.
        /// </summary>
        /// <param name="target">delegate which will execute the work item on thread pool thread</param>
        /// <param name="userData">any user data needed by the delegate</param>
        /// <returns>true if work item is enqueued, false otherwise</returns>
        public override bool QueueUserWorkItem(Action<System.Threading.CancellationToken, object> target, object userData)
        {
            if (_poolStopToken.IsCancellationRequested) return false;
            if (target == null) throw new ArgumentNullException("target");

            var work = new ThreadPoolWorkItem(target, userData, _poolStopToken);
            // If execution context flowing is on, capture the caller’s context.
            if (_settings.EnableExecutionContext)
                work.ExecutionCtx = ExecutionContext.Capture();

            // Now insert the work item into the queue, possibly waking a thread.
            ThreadLocalStealQueue<ThreadPoolWorkItem> wsq = _mWsq;
            if (wsq != null)
            {
                // Single TLS to determine this execution is on a pool thread.
                wsq.LocalPush(work);

                if (wsq.Count > _settings.MinThreads)
                {
                    EnsureThreadCapacity();
                }
            }
            else
            {
                //put item in global queue because this is being added from user's thread
                lock (_mQueue)
                {
                    _mQueue.Enqueue(work);

                    if (_mQueue.Count > _settings.MinThreads)
                    {
                        // Make sure the pool is running with optimal number of required threads.
                        EnsureThreadCapacity();
                    }
                }
            }

            return true;
        }

        public override int TotalThreads
        {
            get
            {
                return _workerThreads.Count;
            }
        }

        private void StartNewThread(bool isPermenant)
        {
            var threadName = Guid.NewGuid().ToString();
            var th = new Thread(WorkItemDispatchLoop);
            //th.IsBackground = true;
            th.Name = threadName;
            th.Start(new WorkerThreadStartParams
            {
                IsPermenant = isPermenant,
                ThreadName = threadName,
            });

            _workerThreads.TryAdd(threadName, th);

            EtwLogger.Log.PoolWorkerStart(threadName);
        }


        // Ensures tha threads have begun executing.
        private void EnsureThreadCapacity()
        {
            //check if additional threads are required
            if (DateTime.UtcNow.Subtract(_lastThreadAddTime) < _settings.NewThreadWaitTime)
                return;

            ThreadLocalStealQueue<ThreadPoolWorkItem>[] wsQueues = _mThreadLocalQueues;
            int j;
            int total = 0;
            for (j = 0; j < wsQueues.Length; j++)
            {
                if (wsQueues[j] != null)
                {
                    total = total + wsQueues[j].Count;
                }
            }

            total = total + _mQueue.Count;

            if (total > _settings.MaxThreads)
            {
                lock (_mQueue)
                {
                    if (_workerThreads.Count < _settings.MaxThreads)
                    {
                        StartNewThread(false);
                        _lastThreadAddTime = DateTime.UtcNow;
                    }
                }
            }

        }

        private void OnPoolCancelRequested()
        {
            EtwLogger.Log.PoolCancelled(Name, _mThreadLocalQueues.Length);
        }

        private bool ShouldThreadExit(DateTime lastItemProcesTime, bool isPermenant)
        {
            if (_poolStopToken.IsCancellationRequested)
            {
                return true; //exit has been signaled by caller
            }

            if (isPermenant)
            {
                return false; //thread is a permenant thread, part of minimum required thread
            }

            TimeSpan idleTime = DateTime.UtcNow - lastItemProcesTime;
            if (idleTime.TotalMilliseconds > _settings.ThreadIdleTimeout.TotalMilliseconds)
            {
                return true;
            }
            return false;
        }

        private void WorkItemDispatchLoop(object threadData)
        {
            var startinfo = (WorkerThreadStartParams)threadData;
            // Register a new WSQ.
            var wsq = new ThreadLocalStealQueue<ThreadPoolWorkItem>();
            wsq.Name = startinfo.ThreadName;
            _mWsq = wsq; // Store in TLS.
            AddNewWorkQueue(wsq);

            DateTime lastItemProcesTime = DateTime.UtcNow;
            try
            {
                while (true)
                {
                    if (ShouldThreadExit(lastItemProcesTime, startinfo.IsPermenant))
                    {
                        return;
                    }

                    var wi = default(ThreadPoolWorkItem);

                    // Search order: (1) local queue, (2) global Queue, (3) steal from another thread.
                    if (!wsq.LocalPop(ref wi))
                    {

                        if (ShouldThreadExit(lastItemProcesTime, startinfo.IsPermenant))
                        {
                            return;
                        }
                        lock (_mQueue)
                        {
                            // If shutdown was requested, exit the thread.
                            if (_poolStopToken.IsCancellationRequested)
                                return;

                            // (2) try the global queue.
                            if (_mQueue.Count != 0)
                            {
                                // We found a work item! Grab it …
                                wi = _mQueue.Dequeue();
                            }
                        }

                        if (wi == null)
                        {
                            lock (_mThreadLocalQueues)
                            {

                                // (3) try to steal from neighbour thread.
                                ThreadLocalStealQueue<ThreadPoolWorkItem>[] wsQueues = _mThreadLocalQueues;
                                int i;
                                for (i = 0; i < wsQueues.Length; i++)
                                {
                                    if (wsQueues[i] != null)
                                    {
                                        if (wsQueues[i] != wsq && wsQueues[i].TrySteal(ref wi))
                                            break;
                                    }
                                }
                            }
                        }
                    }

                    try
                    {
                        //we got a work item, now try to invoke it.
                        if (wi != null)
                        {
                            wi.Execute();
                            lastItemProcesTime = DateTime.UtcNow;
                        }
                    }
                    catch (Exception ex)
                    {
                        EtwLogger.Log.WorkItemFailure(ex.ToString());
                        if (UserWorkItemException != null)
                        {
                            UserWorkItemException(this, new WorkItemEventArgs
                            {
                                Exception = ex,
                                UserData = wi.UserData,
                            });
                        }
                    }

                }
            }
            finally
            {
                OnWorkerThreadExit(startinfo.ThreadName);
            }

        }


        private void OnWorkerThreadExit(string threadName)
        {
            EtwLogger.Log.PoolWorkerExit(threadName);
            lock (_workerThreads)
            {
                Thread t;
                _workerThreads.TryRemove(threadName, out t);
            }

            lock (_mThreadLocalQueues)
            {
                for (int i = 0; i < _mThreadLocalQueues.Length; i++)
                {
                    if (_mThreadLocalQueues[i] != null)
                    {
                        if (_mThreadLocalQueues[i].Name == threadName)
                        {
                            _mThreadLocalQueues[i] = null;
                            break;
                        }
                    }
                }
            }

            EtwLogger.Log.PoolWorkerExit(threadName);
        }

        private void AddNewWorkQueue(ThreadLocalStealQueue<ThreadPoolWorkItem> wsq)
        {
            lock (_mThreadLocalQueues)
            {
                for (int i = 0; i < _mThreadLocalQueues.Length; i++)
                {
                    if (_mThreadLocalQueues[i] == null)
                    {
                        _mThreadLocalQueues[i] = wsq;
                        break;
                    }
                    else if (_mThreadLocalQueues[i].Name == wsq.Name)
                    {
                        _mThreadLocalQueues[i] = wsq;
                        break;
                    }
                    else if (i == _mThreadLocalQueues.Length - 1)
                    {
                        //expand the array now.
                        var queues = new ThreadLocalStealQueue<ThreadPoolWorkItem>[_mThreadLocalQueues.Length * 2];
                        Array.Copy(_mThreadLocalQueues, queues, i + 1);
                        queues[i + 1] = wsq;
                        _mThreadLocalQueues = queues;
                    }
                }
            }
        }

        public override void Dispose()
        {
            //ensure all threads are stopped.
            if (_workerThreads != null)
            {
                OnPoolCancelRequested();
            }

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
