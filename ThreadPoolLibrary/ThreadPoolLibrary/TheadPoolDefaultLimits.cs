using System;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// Defines the default limits for various runtime settings on thread pool.
    /// </summary>
    internal static class TheadPoolDefaultLimits
    {
        /// <summary>
        /// Default minimum number of threads the thread pool contains. (0)
        /// </summary>
        public const int DefaultMinimumWorkerThreads = 1;

        /// <summary>
        /// Default maximum number of threads the thread pool contains. (25).
        /// A thread consists of some memory in kernel mode (kernel stacks and object management), some memory in user mode (the thread environment block, thread-local storage), and its stack. 
        /// Mostly here limiting factor will be stack size
        /// </summary>
        public static int DefaultMaximumWorkerThreads = Environment.ProcessorCount;//250;
        
        /// <summary>
        /// Default idle timeout in milliseconds. (One minute)
        /// </summary>
        public const int DefaultThreadIdleTimeout = 120 * 1000; // two minute

        /// <summary>
        /// Default timeout to wait for new work items to arrive in the queue before deciding to exit or shutdown threadpool threads.
        /// </summary>
        public const int DefaultQueueItemArrivalWaitTimeout = 100; //100 ms seconds to wait

        /// <summary>
        /// Default wait time for new thread to be added in the pool. This is to ensure when sudden high workloads arrive we dont create threads in the pool and cause further contention on processing.
        /// </summary>
        public const int DefaultNewThreadWaitTme = 5*1000; //5 seconds
    }
}
