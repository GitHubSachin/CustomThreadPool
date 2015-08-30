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
5. Not handling ExecutionContext for now, but to be added in future.

## Installation (coming soon)
You can install `CustomThreadPool` via NuGet! (package not ready yet)

```
PS> Install-package CustomThreadPool
```

## USAGE

You can create a `CustomThreadPool` instance via the following API:

```csharp
using (var threadPool = new CustomThreadPool1(new CustomThreadPoolSettings(1,8),CancellationToken.None))
{
    threadPool.QueueUserWorkItem(() => { ... }));
}
```

This creates a `CustomThreadPool1` object which allocates a fixed minimum number of threads, each will process enqueued work items in parallel.

# CustomThreadPool1:
This implementation have one global queue, and worker threads trying to dequeue items from it and process it, after I wrote this, I quickly realized this solution makes large contention on global queue, as number of threads increase, the performance degrades because it takes long time to enqueue work items and worker threads are waiting for lock releases.
however this works well on low core systems on low concurrency levels.
So I had to think of another approach.

#CustomThreadPool2:
Here I tried to avoid global queue, by partitioning the queue across each worker in the pool, thus I have one queue per worker thread in the pool. With this enqueue contention is mitigated but now question was how to efficiently enqueue new work items? for that I experimented with two approaches
1. round robin across all workers in the pool, this is simple, but not optimal because some work will be stuck behind some long running operations already running on workers
2. minimum assigned task strategy will assign the task to any of the worker threads with the fewest tasks already running. Here I wrote algorithm to randomly choose among the worker with the fewest tasks.

#CustomThreadPool3:
This implementation I am using both approaches of #1 and #2 above with addition to some twist on optimizing the work scheduling and assignment. It turns out that most of the multithreaded applications involve some sort of recursive divide and conquer kind of work loads, in such cases, it’s important to pay attention on how you enqueue the work, for example the work enqueued from the pool threads can be added to its local queue, this will be very fast because there will be no locking on local queue. However new work coming from the outside pool thread can go to global queue (with some locking involved). With this approach we need to design a thread local queue which is lock free for local operations but uses some locking when accessed from another threads, this is how we distribute work efficiently. The algorithm works like this:

1. There is One global queue
2. each thread have local queue (special type if queue we will talk in details later)
3. when work is added from pool thread it goes to its local queue
4. when work is added from non pool thread it goes to global queue
5. pool threads are processing work in this order
(1) check local queue for new item, 
(2) check global queue for new item, 
(3) get item from neighbour thread.

## Performance Benchmark

Calculate nth Fibonacci number in its series.

| Id | PoolType | Min | Max | WorkItems | EnqueueTime | ProcessTime | G0  | G1 | G2 |
|:---|:---------|:----|:----|:----------|:------------|:------------|:----|:---|:---|
| 1  | Custom1  | 1   | 8   | 1000      | 0           | 0.0937704   | 0   | 0  | 0  |
| 1  | Custom1  | 1   | 8   | 10000     | 0           | 0.8906363   | 1   | 0  | 0  |
| 1  | Custom1  | 1   | 8   | 100000    | 0.0312329   | 8.9634327   | 11  | 1  | 0  |
| 1  | Custom1  | 1   | 8   | 1000000   | 0.4375067   | 100.9626534 | 142 | 16 | 2  |

| Id | PoolType | Min | Max | WorkItems | EnqueueTime | ProcessTime | G0  | G1 | G2 |
|:---|:---------|:----|:----|:----------|:------------|:------------|:----|:---|:---|
| 1  | Custom2  | 1   | 8   | 1000      | 0.078124    | 0.0937495   | 0   | 0  | 0  |
| 1  | Custom2  | 1   | 8   | 10000     | 0.0781263   | 0.5781337   | 1   | 0  | 0  |
| 1  | Custom2  | 1   | 8   | 100000    | 0.1875028   | 5.4219523   | 11  | 1  | 0  |
| 1  | Custom2  | 1   | 8   | 1000000   | 2.2937311   | 58.9468594  | 142 | 12 | 2  |

| Id | PoolType | Min | Max | WorkItems | EnqueueTime | ProcessTime | G0 | G1 | G2 |
|:---|:---------|:----|:----|:----------|:------------|:------------|:---|:---|:---|
| 1  | .net     | 1   | 8   | 1000      | 0           | 0.0625011   | 0  | 0  | 0  |
| 1  | .net     | 1   | 8   | 10000     | 0           | 0.5468821   | 0  | 0  | 0  |
| 1  | .net     | 1   | 8   | 100000    | 0.1093778   | 6.2934229   | 3  | 1  | 0  |
| 1  | .net     | 1   | 8   | 1000000   | 1.6597138   | 55.7233629  | 31 | 15 | 3  |

| Id | PoolType | Min | Max | WorkItems | EnqueueTime | ProcessTime | G0  | G1 | G2 |
|:---|:---------|:----|:----|:----------|:------------|:------------|:----|:---|:---|
| 1  | Custom3  | 1   | 8   | 1000      | 0           | 0.2031273   | 0   | 0  | 0  |
| 1  | Custom3  | 1   | 8   | 10000     | 0.0156236   | 1.2266493   | 0   | 0  | 0  |
| 1  | Custom3  | 1   | 8   | 100000    | 0.0156293   | 10.5210545  | 12  | 2  | 0  |
| 1  | Custom3  | 1   | 8   | 1000000   | 0.4062564   | 96.386409   | 139 | 15 | 1  |

## Some thoughts on Multi-Threaded Pool
The right implementation depends on type of problem you are trying to solve, Before you adventure to create your own pool, try CLR team's default threadpool, it might just work right. In special cases you may have to create your own implementation.
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


## Some notes
can we use ReaderWriterLockSlim which allows multiple readers to coexist with a single writer on thread queue? 
how to create non blocking queue??

For workload which has very small execution time, it’s best to keep max limit on pool size to smaller, this gives better performance, when used with workloads with higher execution time, adding more threads to the pool starts to benefit.
After some limit actually bigger pool size affects performance adversely.

