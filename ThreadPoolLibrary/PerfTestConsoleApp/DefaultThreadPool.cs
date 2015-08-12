using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PerfTestConsoleApp
{
    /// <summary>
    /// Uses default .ner thread pool to process user supplied delegate.
    /// To be used to test benchmark againest my implementation of pools
    /// </summary>
    public class DefaultThreadPool : ThreadPoolLibrary.CustomThreadPool
    {
        private CancellationToken _cancel;
        public DefaultThreadPool(ThreadPoolLibrary.ThreadPoolSettings settings, CancellationToken cancelToken)
            : base(settings, cancelToken)
        {
            ThreadPool.SetMinThreads(settings.MinThreads, settings.MinThreads);
            ThreadPool.SetMaxThreads(settings.MaxThreads, settings.MaxThreads);
            this._cancel = cancelToken;
        }

        public override event EventHandler<ThreadPoolLibrary.WorkItemEventArgs> UserWorkItemException;

        public override bool QueueUserWorkItem(Action<System.Threading.CancellationToken, object> target, object userData)
        {
            return ThreadPool.QueueUserWorkItem((o) =>
            {
                target(_cancel, userData);
            });
        }

        public override int TotalThreads
        {

            get
            {
                throw new NotImplementedException();
            }
        }

    }
}
