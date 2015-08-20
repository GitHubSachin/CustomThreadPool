
namespace ThreadPoolLibrary
{
    /// <summary>
    /// Enum to indicate the status of worker in the pool
    /// </summary>
    internal enum PoolWorkerStatus
    {
        Ready = 0,
        Running = 1,
        Exiting = 2,
    }
}
