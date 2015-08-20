using System;
using System.Threading;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// Provides the settings to be supplied to create new custom thread pool.
    /// </summary>
    public sealed class ThreadPoolSettings
    {

        private int _maxThreads;
        private int _minThreads;

        /// <summary>
        /// Creates new instance of settings with default values. 
        /// </summary>
        public ThreadPoolSettings()
        {
            this.MaxThreads = TheadPoolDefaultLimits.DefaultMaximumWorkerThreads;
            this.MinThreads = TheadPoolDefaultLimits.DefaultMinimumWorkerThreads;
            this.ThreadIdleTimeout = new TimeSpan(0, 0, 0, 0, TheadPoolDefaultLimits.DefaultThreadIdleTimeout);
            this.QueueItemArrivalWaitTimeout = TheadPoolDefaultLimits.DefaultQueueItemArrivalWaitTimeout;
            this.NewThreadWaitTime = TimeSpan.FromMilliseconds(TheadPoolDefaultLimits.DefaultNewThreadWaitTme);
        }

        /// <summary>
        /// Gets or sets minimum number of threads that will be kept running in the thread pool.
        /// Set this number to average number of work items that your application receives so that pool will always have threads ready to process most of the workload pattern.
        /// Setting value too low will suffer from additional work needed to create new threads when load increases,
        /// value of too high will waste resources when load is not high.
        /// Default value is 0
        /// </summary>
        public int MinThreads
        {
            get
            {
                return _minThreads;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("MinThreads must be non-negative", "value");
                }

                if (value > _maxThreads)
                {
                    throw new ArgumentOutOfRangeException("value","MinThreads must be less than or equal to MaxThreads");
                }
                _minThreads = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of threads that will be created in the pool. This will be maximum concurrency limit in the application.
        /// Once this limit is reached all incoming work is enqueued and will be picked up when worker threads in the pool are free.
        /// You should set this value to peak load you anticipate in the application, value too high may also suffer perf degradation because of context switching cost if system is starved of resources.
        /// Default value is 25 * Processor core count
        /// </summary>
        public int MaxThreads
        {
            get
            {
                return _maxThreads;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("MaxThreads must be at least 1", "value");
                }

                //this ensures user does not set excessively high number of max limit.
                //we limit the max value to whatever .net framework max user threads are allowed to be.
                int maxUserThreads, maxIoThreads;
                ThreadPool.GetMaxThreads(out maxUserThreads, out maxIoThreads);
                int maxValue = Math.Min(maxUserThreads, value);

                if (maxValue < _minThreads)
                {
                    throw new ArgumentOutOfRangeException("value", "MaxThreads must be greater than or equal to MinThreads");
                }
                _maxThreads = maxValue;
                this.SizeWarning = (int) Math.Round(0.95*maxValue);
            }
        }

        /// <summary>
        /// This property is a high water mark on the pool. When the number of threads in a pool reaches this value run time logs a warning message.
        /// If you see this warning message frequently, it could indicate that you need to evaluate your workload and its pattern to ensure system is still functioning optimally, and decide to make further configuration changes (either increase max pool size or shape your work load)
        /// </summary>
        internal int SizeWarning { get; private set; }

        /// <summary>
        /// This property specifies the number of seconds that a thread in the thread pool must be idle before it terminates. It is used for shrinking the size of the pool to its minimum limit. If worker thread have not processes any work for this time amount, it is considered to be released and it is distroyed, thus pool size is reduced till min value.
        /// Value should be set as per anticipated load in the application, value too high will prevent idle threads getting released, which too low will cost too much in creating new threads and adding them to pool.
        /// The default value is 2 minutes if this property is not defined. Setting it to TimeSpan.Infinite value disables the termination of idle threads.
        /// </summary>
        public TimeSpan ThreadIdleTimeout { get; set; }

        /// <summary>
        /// This is amount of time a thread will wait for work item arrival in the queue. This is ONLY used in CustomThreadPool1 implemention where we use competing consumer pattern to pick items from one signle queue.
        /// </summary>
        internal int QueueItemArrivalWaitTimeout { get; set; }

        /// <summary>
        /// flag to indicate if user would like to pass execution context to the pool thread or not.
        /// </summary>
        public bool EnableExecutionContext { get; set; }

        /// <summary>
        /// Amount of time in milliseconds to wait before creating new thread since last new thread was added to the pool.
        /// This is to avoid suddenly creating many threads to pools max capacity if there are constant spikes on workload. This time gives some buffer to let existing threads give chance to handle workload before adding new threads, 
        /// because just adding threads continously might cause contention on getting items out of pool queue and affect overall performance adversely.
        /// </summary>
        internal TimeSpan NewThreadWaitTime { get; set; }
    }
}
