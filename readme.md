# CustomThreadPool
Custom ThreadPool Implementation for compute work on servers (Sample compute is N th Fibonacci Number).
See benchmark results at the bottom of the page.

## Design Considerations:
1. Server application can choose pool settings like min and max threads, thread idle time etc.
2. Number of worker threads in the pool grows between min to max as workload arrives.
3. Once minimum threads are created, they are kept alive for the lifetime of the pool. However if more threads are required, the thread pool creates new threads. 
   Once worker threads finish executing their activities, they are then returned to the thread pool. If there is no work, threads will self-destroy after approximately 2 minutes of inactivity.
4. Once minimum thread count is reached, pool would create new thread every 100ms, this is important to avoid sudden burst of resources for increased load, because there may be other threads getting released.
   (We may have to tune this, delaying new thread creation may result latency in processing but helps to level the load on system resources (this is double edge sword, might need some further tuning)
5. Be careful on spending too much time in synchronization and locking, this may affect throughput
6. Think about diagnostics in the pool, how would you diagnose issues if you need to, one option is to use ETW events for internal tracing and telemetry on pool.
7. Max thread limits on pool size is .net framework ThreadPool.GetMaxThreads value.
8. Allow caller to cancel all work items in the pool if needed. Also allow cancelling individual work item compute work without cancelling entire pool.
9. User is responsible to implement work item delegate which should honor cancellation support, if pool is requested to be stopped.

## Some Assumptions:
1. User will set appropriate limits of min and max threads in the application as per their expected workload.
2. Work item delegate stack is less than or upto 1MB
3. Pool configuration are set at the time of creation, no dynamic support so far.
4. User implements ThreadPoolWorkItemExceptionHandler to handle work items which fail on compute, see example on error handling for work items.
This delegate invokes on pool worker thread so any long blocking code written on error handler delegate will affect pool performance.

## Installation (coming soon)
You can install `CustomThreadPool` via NuGet! (package not ready yet)

```
PS> Install-package CustomThreadPool
```

## USAGE

You can create a `CustomThreadPool` instance via the following API:

```csharp
using (var threadPool = new CustomThreadPool1(new CustomThreadPoolSettings(10,20),CancellationToken.None))
{
    threadPool.QueueUserWorkItem(() => { ... }));
}
```

This creates a `CustomThreadPool1` object which allocates a fixed minimum number of threads, each will process enqueued work items in parallel.

## Some thoughts on Multi-Threaded Pool

Improper configuration of a thread pool can have a serious impact on the performance of your application. This documentation describes the issues and things you need to consider when designing your application which uses this thread pool implementation.
A thread pool is a collection of threads which is processing the user's work items as they are enqueued. The user work item is a delegate supplied by application code, this delegate contains any compute or IO work that user needs to perform concurrently.
Configuring a thread pool to support min, max limits implies that the application is prepared to dispatch work item operation concurrently.

When using a multi-threaded pool implementation like CustomThreadPool2, the thread scheduling nature is nondeterministic which means that requests may not be processed in the order they were received. Some applications cannot tolerate this behavior, such as a transaction processing server that must guarantee that requests are executed in order.
If your application have strict processing order requirement then CustomThreadPool1 implementation will be good to use, in this implementation all items are serially put in one queue and processed in order.

# Configuring Thread Pool:
This is importent design consideration, how many threads you would put as min and max limits on the pool.
Both implementation of custom thread pool can grow and shrink when necessary in response to changes in an application's work load. All thread pools have at least one thread, but a thread pool can grow as the demand for threads increases, up to the pool's maximum size. Threads may also be terminated automatically when they have been idle for some time.
The dynamic nature is determined by configuration of the thread pool's min and max setting values. The value of ThreadIdleTime determines whether and how quickly a thread pool can shrink to a size of min. limit.

You should do careful analysis of your application to choose an appropriate maximum size for a thread pool. For example, in compute-bound applications it is best to limit the number of threads to the number of physical processor cores or threads on the host machine; adding any more threads will increase context switches and reduces performance. Increasing the size of the pool beyond the number of cores can improve responsiveness when threads can become blocked while waiting for the operating system to complete a task, such as a network or file operation. On the other hand, a thread pool configured with too many threads can have the opposite effect and negatively impact performance. Test your application in a realistic environment for determining the optimum size for a thread pool

# CustomThreadPool1
This implementation have one global queue, and worker threads trying to dequeue items from it and process it, after I wrote this, I quickly realized this solution makes large contention on global queue, as number of threads increase, the performance degrades because it takes long time to enqueue work items and worker threads are waiting for lock releases.
however this works well on low core systems on low concurrency levels.
So I had to think of another approach.

#CustomThreadPool2:
Here I tried to avoid global queue, by partitioning the queue across each worker in the pool, thus I have one queue per worker thread in the pool. With this enqueue contention is mitigated but now question was how to efficiently enqueue new work items? for that I experimented with two approaches
1. round robin across all workers in the pool, this is simple, but not optimal because some work will be stuck behind some long running operations already running on workers
2. minimum assigned task strategy will assign the task to any of the worker threads with the fewest tasks already running. Here I wrote algorithm to randomly choose among the worker with the fewest tasks.

#Next Steps:
Now after having both implementation ready, I decided to test and compare with standard .net threadpool, and realized that the data structures and locking I am using for queue is causing bad performance on enqueue, 
so now I am experimenting on another idea, what if I can create some data structure so that I can enqueue and dequeue work as fast as possible without any locking overhead at all??
next step is to replace .net queue class with a doubly linked list which will be written such a way that queue will not use any locks and will just do a form of a spin lock... let’s see how much it helps, who knows what else is next.

here is perf results so far..

## Performance Benchmark

Calculate 20th Fibonacci number in its series. (approx. compute time for 1 item is 15 ms)

| Id | PoolType | Min | Max | WorkItems | EnqueueTime | ProcessTime |
|:---|:---------|:----|:----|:----------|:------------|:------------|
| 1  | Custom1  | 10  | 100 | 1000      | 0.0468709   | 0.0625564   |
| 2  | Custom1  | 10  | 100 | 10000     | 0.2030604   | 0.5311822   |
| 3  | Custom1  | 10  | 100 | 100000    | 0.5468608   | 5.4061451   |
| 4  | Custom1  | 10  | 100 | 1000000   | 7.5311008   | 57.2411899  |

| Id | PoolType | Min | Max | WorkItems | EnqueueTime | ProcessTime |
|:---|:---------|:----|:----|:----------|:------------|:------------|
| 1  | Custom2  | 10  | 100 | 1000      | 0.0781229   | 0.093751    |
| 2  | Custom2  | 10  | 100 | 10000     | 0.9167963   | 0.9324233   |
| 3  | Custom2  | 10  | 100 | 100000    | 7.7682885   | 8.5339231   |
| 4  | Custom2  | 10  | 100 | 1000000   | 12.4689065  | 57.4889367  |



| Id | PoolType | Min | Max | WorkItems | EnqueueTime | ProcessTime |
|:---|:---------|:----|:----|:----------|:------------|:------------|
| 1  | .net     | 10  | 100 | 1000      | 0           | 0.0781268   |
| 2  | .net     | 10  | 100 | 10000     | 0           | 0.5312566   |
| 3  | .net     | 10  | 100 | 100000    | 0.1093742   | 5.3125669   |
| 4  | .net     | 10  | 100 | 1000000   | 2.7812817   | 53.2116138  |



## Some notes
can we use ReaderWriterLockSlim which allows multiple readers to coexist with a single writer on thread queue? 
how to create non blocking queue??

For workload which has very small execution time, it’s best to keep max limit on pool size to smaller, this gives better performance, when used with workloads with higher execution time, adding more threads to the pool starts to benefit.
After some limit actually bigger pool size affects performance adversely.

## License

See [LICENSE](LICENSE) for details.

Copyright (C) 2015 GitHubSachin
