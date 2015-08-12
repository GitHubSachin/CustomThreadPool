using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ThreadPoolLibrary.Logging;

namespace ThreadPoolLibrary
{

    /// <summary>
    /// Custom ThreadPool Implementation for compute work on dedicated pool of running threads. The implementation allows user to specify min and max values for the pool limit
    /// User will enqueue the work item and it will be processed in parallel upto max thread pool size limit.
    /// 
    /// Design:
    /// 1. When pool is initiated, min number of pool worker objects are created.
    ///     1a. Each pool worker object contains its own queue as BlockingCollection and dedicated thread processing items in that collection in producer consumer style patern.
    ///         This allows to partition the incoming work items across many queues and avoid contention on all threads trying to add and remove items from one central collection.
    /// 
    /// 2. Each pool worker will get a work item form its own queue and process it
    ///     2a. A timer event keeps checking if there are items processed on pool worker in last QueueItemArrivalWaitTimeout value.
    ///     2b. if thread is idle for ThreadIdleTimeout value, pool worker is decided to be terminated, if its is not part of min dedicated worker.
    /// 
    /// 3. The work item assignment to the pool can be done using two strategies
    ///     3a. we can do round robin across all pool workers and keep assigning work items. This is good when each work item takes similar amount of execution, but when incoming work load is very distributed 
    ///         like one item may take 10ms while other may take few seconds, this strtegy is not good. In that case we can employ min task strategy to assign the work item to the pool.
    ///     3b. When incoming work items execution times are unpredictable, use strategy to assign the task to any of the nodes with the fewest tasks already running.
    ///         This comes at additional perf cost to calculate node with fewest tasks, this should be tested and tuned if needed.
    /// 
    /// 4. if user sends cancel signal, all work is aborted and pool becomes idle. After this pool can be disposed.
    /// </summary>
    public sealed class CustomThreadPool2 : CustomThreadPool, IDisposable
    {

        /// <summary>
        /// Token to indicate pool shutdown is requested by user.
        /// </summary>
        private CancellationToken _poolStopToken;

        /// <summary>
        /// linked cancellation token created from user's token to signal cancel event.
        /// </summary>
        private readonly CancellationTokenSource _linkedCts;

        /// <summary>
        /// cancellation token register to subscribe cancel signal and abort running work items and shutdown the pool.
        /// </summary>
        private CancellationTokenRegistration _tokenRegister;

        private readonly ThreadPoolSettings _settings;
        private readonly List<string> _workerKeys;
        private readonly object _lock = new object();

        private ConcurrentDictionary<string, PoolWorker> _workerThreads = new ConcurrentDictionary<string, PoolWorker>();

        public CustomThreadPool2(CancellationToken cancelToken)
            : this(new ThreadPoolSettings(), cancelToken)
        {
        }


        public CustomThreadPool2(ThreadPoolSettings settings, CancellationToken cancelToken)
            : base(settings, cancelToken)
        {
            if (settings == null) throw new ArgumentNullException("settings");

            this._settings = settings;
            _workerKeys = new List<string>(settings.MaxThreads);

            //initialize min threads immediately
            for (int i = 0; i < settings.MinThreads; i++)
            {
                var w = AllocDelegate();
                w.IsPermenant = true;
                _workerThreads.TryAdd(w.Name, w);
                _workerKeys.Add(w.Name);
            }

            _linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
            this._poolStopToken = _linkedCts.Token;
            _tokenRegister = _poolStopToken.Register(OnPoolCancelRequested);
            EtwLogger.Log.PoolStarted(Name, _settings.MinThreads, _settings.MaxThreads);
        }

        public override int TotalThreads
        {
            get { return _workerKeys.Count; }
        }

        /// <summary>
        /// process the exception handling when user work item fails to execute. Implement this to raise UserWorkItemException event when this happens.
        /// </summary>
        /// <param name="e">WorkItemEventArgs about the work item which thew unhandled exception</param>
        private void OnPoolCancelRequested()
        {
            var runningWorkers = _workerThreads.Keys; //Blocking call to get all keys at once.

            foreach (var runningWorker in runningWorkers)
            {
                PoolWorker w;
                _workerThreads.TryRemove(runningWorker, out w);
                if (w != null)
                {
                    //Console.WriteLine(w.Name);
                    w.Stop();
                    w.Dispose();
                }
            }
            EtwLogger.Log.PoolCancelled(Name, runningWorkers.Count);
        }

        /// <summary>
        /// Event which is fired if user's delegare for worker thread throws an exception in its execution.
        /// </summary>
        public override event EventHandler<WorkItemEventArgs> UserWorkItemException;

        /// <summary>
        /// if there are N nodes able to perform some task in the pool, and you want to assign the task to any of the nodes with the fewest tasks already running.  
        /// Randomly choose among the nodes with the fewest tasks.
        /// </summary>
        /// <returns>random node in the thread pool which have min tasks assigned</returns>
        private PoolWorker SelectMinLoadRandomNode()
        {
            int minTaskCount = int.MaxValue;
            Random random = new Random();
            int count = 0;
            int totalKeys = 0;
            PoolWorker selectedNode = null;
            while (true)
            {
                totalKeys = _workerKeys.Count;
                try
                {
                    for (int i = 0; i < totalKeys; i++)
                    {
                        PoolWorker n;
                        if (_workerThreads.TryGetValue(_workerKeys[i], out n))
                        {
                            if (n != null)
                            {
                                if (n.TaskCount > minTaskCount)
                                {
                                    continue;
                                }
                                if (n.TaskCount < minTaskCount)
                                {
                                    minTaskCount = n.TaskCount;
                                    count = 0;
                                }

                                count++;
                                if (random.Next(1, count + 1) == count)
                                    selectedNode = n;
                            }
                        }
                    }

                    return selectedNode;
                }
                catch (IndexOutOfRangeException)
                {
                    EtwLogger.Log.PoolWorkerAssignmentFailed(Name, totalKeys);
                    //we dont lock keys collection so node may be removed, ignore and just continue with another iteration.
                    continue;
                }
            }

        }

        private int _index;

        private PoolWorker SelectRoundRobinNode()
        {
            PoolWorker n = null;
            while (n == null)
            {
                int i;

                unchecked
                {
                    _index = (_index + 1);
                    //need to wrap bitwise operations in parens to preserve order, otherwise this won't round-robin
                    i = (_index & 0x7fffffff) % _workerKeys.Count;
                }

                _workerThreads.TryGetValue(_workerKeys[i], out n);
            }
            return n;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private PoolWorker AllocDelegate()
        {
            int currentSize = _workerKeys.Count;
            //log size warning if needed
            if (currentSize >= _settings.SizeWarning)
            {
                EtwLogger.Log.PoolSizeWarning(this.Name, currentSize, _settings.MaxThreads);
            }
            var poolItem = new PoolWorker(_settings, _poolStopToken);
            poolItem.WorkerThreadExitHandler += poolItem_ThreadExit;
            poolItem.WorkItemExceptionHandler += poolItem_WorkItemExceptionHandler;
            
            //Console.WriteLine("New Thread Worker Created {0}", poolItem.Name);

            EtwLogger.Log.PoolWorkerStart(poolItem.Name);
            return poolItem;
        }

        void poolItem_WorkItemExceptionHandler(object sender, WorkItemEventArgs e)
        {
            EtwLogger.Log.WorkItemFailure(e.ToString());
            if (UserWorkItemException != null)
            {
                UserWorkItemException(this, e);
            }
        }

        void poolItem_ThreadExit(object sender, ThreadExitEventArgs e)
        {
            //Console.WriteLine("Pool worker exited: {0}", e.ThreadName);
            EtwLogger.Log.PoolWorkerExit(e.ThreadName);
            PoolWorker w;
            _workerThreads.TryRemove(e.ThreadName, out w);
            lock (_lock)
            {
                _workerKeys.Remove(e.ThreadName);
                //Monitor.Pulse(_lock);
            }

            if (w != null)
            {
                w.Dispose();
            }
        }

        public override bool QueueUserWorkItem(Action<CancellationToken, object> target, object userData)
        {
            //User signaled to stop all processing so return false and dont add any new work items.
            if (_poolStopToken.IsCancellationRequested) return false;

            var job = new ThreadPoolWorkItem(target, userData, _poolStopToken);
            while (true)
            {
                //var w = SelectMinLoadRandomNode();
                var w = SelectRoundRobinNode();
                if (w.TaskCount != 0)
                {
                    lock (_lock)
                    {
                        //new threads are needed. check if pool have capacity and add them now.
                        if (_workerKeys.Count < _settings.MaxThreads)
                        {
                            w = AllocDelegate();
                            _workerThreads.TryAdd(w.Name, w);
                            _workerKeys.Add(w.Name);
                            //Console.WriteLine(_workerKeys.Count);
                        }
                    }
                }

                if (w.Status != PoolWorkerStatus.Exiting)
                {
                    w.EnqueueWorkItem(job);
                    EtwLogger.Log.PoolWorkerSelected(w.Name, w.TaskCount);
                    //Console.WriteLine("Assigned to worker: {0}", w.Name);
                    return true;
                }
                else
                {
                    EtwLogger.Log.PoolWorkerAssignmentFailed(w.Name, w.TaskCount);
                    //Console.WriteLine("failed to assign to worker: {0}", w.Name);
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
