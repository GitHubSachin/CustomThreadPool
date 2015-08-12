using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThreadPoolLibrary
{
    //TODO: Implement this as doubly linked list queue for each thread.
    public class CustomThreadPool3 : CustomThreadPool
    {

        public override event EventHandler<WorkItemEventArgs> UserWorkItemException;

        public CustomThreadPool3() : base(new ThreadPoolSettings(), CancellationToken.None)
        {

        }

        public override bool QueueUserWorkItem(Action<System.Threading.CancellationToken, object> target, object userData)
        {
            throw new NotImplementedException();
        }

        public override int TotalThreads
        {
            get { throw new NotImplementedException(); }
        }
    }
}
