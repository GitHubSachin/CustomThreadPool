using System;
using System.Threading;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// Wraps user supplied work item thats given to process on a worker thread in the pool
    /// </summary>
    internal class ThreadPoolWorkItem
    {
        private readonly Delegate _targetDelegate;
        private readonly System.Threading.CancellationToken _token;
        private readonly object _userData;
        
        public ThreadPoolWorkItem(Action<System.Threading.CancellationToken, object> target,
            object userData,
            System.Threading.CancellationToken cancellationToken)
        {
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            if (cancellationToken == null)
            {
                throw new ArgumentNullException("cancellationToken");
            }

            this._userData = userData;
            this._targetDelegate = target;
            _token = cancellationToken;
        }

        public object UserData { get { return _userData; } }

        public void Execute()
        {
            if (_token.IsCancellationRequested) return;

            _targetDelegate.DynamicInvoke(_token, _userData);
        }

        public ExecutionContext ExecutionCtx { get; set; }
    }

}
