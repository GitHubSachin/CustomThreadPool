using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Timers;
using ThreadPoolLibrary.Logging;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// represents a dedicated pool worker which will be processing user work items.
    /// </summary>
    internal class PoolWorker : IDisposable
    {
        /// <summary>
        /// queue to store user work items for processing.
        /// </summary>
        private BlockingCollection<ThreadPoolWorkItem> _queue = new BlockingCollection<ThreadPoolWorkItem>();

        private readonly ThreadPoolSettings _settings;
        private readonly CancellationToken _cancelToken;
        private PoolWorkerStatus _status;

        /// <summary>
        /// Delegate for exceptions raised while processing thread pool work item.
        /// </summary>
        public delegate void ThreadPoolThreadExitHandler(string threadName, PoolWorker poolWorker);

        private int _taskCount;

        private DateTime _lastItemProcesTime = DateTime.UtcNow;

        // To avoid confusion with other Timer classes, we use the fully-qualified name of System.Timers.Timer instead of a using statement for System.Timers. 
        private System.Timers.Timer _timer;

        /// <summary>
        /// dedicated thread which processes the work items in this pool worker.
        /// </summary>
        private System.Threading.Thread _thread;

        /// <summary>
        /// When this work item is part of min pool threads, its is Permenant and cant be stopped.
        /// </summary>
        public bool IsPermenant { get; set; }

        public event EventHandler<WorkItemEventArgs> WorkItemExceptionHandler;

        public event EventHandler<ThreadExitEventArgs> WorkerThreadExitHandler;

        public string Name { get; set; }

        /// <summary>
        /// Creates a new instance of pool worker thread with "ready" status
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="cancelToken"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public PoolWorker(ThreadPoolSettings settings, CancellationToken cancelToken)
        {
            _settings = settings;
            _cancelToken = cancelToken;
            Name = Guid.NewGuid().ToString();
            _status = PoolWorkerStatus.Ready;
            _timer = new System.Timers.Timer()
            {
                AutoReset = true,
                Enabled = true,
            };
            _timer.Interval = settings.ThreadIdleTimeout.TotalMilliseconds;
            _timer.Elapsed += OnIdleTimeoutCheckEvent;
            StartThread();
        }

        public void Stop()
        {
            try
            {
                lock (_queue)
                {
                    //stops enumurating and poping out new work items from the queue
                    _queue.CompleteAdding();
                }

                //closes the timer event to watch for idle timeout, there is no need because cancel token is already set.
                _timer.Close();
                EtwLogger.Log.PoolThreadAborted(this.Name);
            }
            catch (Exception exc)
            {
                EtwLogger.Log.PoolThreadAbortFailure(this.Name, exc.ToString());
            }
        }

        public bool EnqueueWorkItem(ThreadPoolWorkItem item)
        {
            lock (_queue)
            {
                if (_status != PoolWorkerStatus.Running)
                {
                    //make sure to initialize the thread.
                    return false;
                }


                _queue.Add(item);
                Interlocked.Increment(ref _taskCount);
                return true;
            }
        }

        internal int TaskCount { get { return _taskCount; } }

        internal PoolWorkerStatus Status { get { return _status; } }

        private void OnIdleTimeoutCheckEvent(object sender, ElapsedEventArgs e)
        {
            //check if thread is idle for long time, and if so, signal exit.
            if (ShouldThreadExit())
            {
                this._status = PoolWorkerStatus.Exiting;
                lock (_queue)
                {
                    _queue.CompleteAdding();
                }
            }
        }

        private void StartThread()
        {
            _thread = new System.Threading.Thread(ProcessWorkItems)
            {
                Name = this.Name,
                IsBackground = true
            };
            _thread.Start();
            _status = PoolWorkerStatus.Running;
        }

        private void ProcessWorkItems()
        {
            try
            {

                foreach (var job in _queue.GetConsumingEnumerable(_cancelToken))
                {
                    ExecuteJob(job);
                    _lastItemProcesTime = DateTime.UtcNow;
                }

            }
            catch (OperationCanceledException op)
            {
                OnWorkItemException(null, op);
            }
            finally
            {
                _status = PoolWorkerStatus.Exiting;
                WorkerThreadExited();
                lock (_queue)
                {
                   _queue.Dispose();
                }
            }
        }

        private void WorkerThreadExited()
        {
            if (WorkerThreadExitHandler != null)
            {
                WorkerThreadExitHandler(this, new ThreadExitEventArgs() { ThreadName = this.Name });
            }
        }

        private bool ShouldThreadExit()
        {
            if (_cancelToken.IsCancellationRequested)
            {
                return true; //exit has been signaled by caller
            }

            if (IsPermenant) return false; //permenant min pool thread and should never exit.

            TimeSpan idleTime = DateTime.UtcNow - _lastItemProcesTime;

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
                OnWorkItemException(job, e);
            }
            finally
            {
                Interlocked.Decrement(ref _taskCount);
            }
        }

        private void OnWorkItemException(ThreadPoolWorkItem job, Exception e)
        {
            if (WorkItemExceptionHandler != null)
            {
                WorkItemExceptionHandler(this, new WorkItemEventArgs()
                {
                    Exception = e,
                    UserData = job == null ? null : job.UserData,
                });
            }
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Dispose();
            }
        }
    }
}
