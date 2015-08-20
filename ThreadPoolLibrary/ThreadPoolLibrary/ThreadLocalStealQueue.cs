using System.Threading;

namespace ThreadPoolLibrary
{
    /// <summary>
    /// Represents a thread local queue which allows lock free enqueue and dequeue operations
    /// and also support taking item out of the queue by another thread. This is useful when other threads
    /// in the pool are idle and they can borrow work items from threads which have their outstanding items in their local queues.
    /// It helps to distribute work evenly across the pool and let all threads help each others
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ThreadLocalStealQueue<T>
    {

        private const int InitialSize = 32;

        private T[] _mArray = new T[InitialSize];

        private int _mMask = InitialSize - 1;

        private volatile int _mHeadIndex = 0;

        private volatile int _mTailIndex = 0;

        private readonly object _mForeignLock = new object();

        public string Name { get; set; }

        public bool IsEmpty
        {

            get { return _mHeadIndex >= _mTailIndex; }

        }
        
        public int Count
        {

            get { return _mTailIndex - _mHeadIndex; }

        }

        /// <summary>
        /// Adds item to local queue from the thread.
        /// </summary>
        /// <param name="obj"></param>
        public void LocalPush(T obj)
        {

            int tail = _mTailIndex;

            if (tail < _mHeadIndex + _mMask)
            {

                _mArray[tail & _mMask] = obj;

                _mTailIndex = tail + 1;

            }

            else
            {

                lock (_mForeignLock)
                {

                    int head = _mHeadIndex;

                    int count = _mTailIndex - _mHeadIndex;

                    if (count >= _mMask)
                    {

                        T[] newArray = new T[_mArray.Length << 1];

                        for (int i = 0; i < _mArray.Length; i++)

                            newArray[i] = _mArray[(i + head) & _mMask];

                        // Reset the field values, incl. the mask.

                        _mArray = newArray;

                        _mHeadIndex = 0;

                        _mTailIndex = tail = count;

                        _mMask = (_mMask << 1) | 1;

                    }

                    _mArray[tail & _mMask] = obj;

                    _mTailIndex = tail + 1;

                }

            }

        }


        /// <summary>
        /// Takes the item out from local queue
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public bool LocalPop(ref T obj)
        {

            int tail = _mTailIndex;

            if (_mHeadIndex >= tail)

                return false;

            tail -= 1;

            Interlocked.Exchange(ref _mTailIndex, tail);

            if (_mHeadIndex <= tail)
            {

                obj = _mArray[tail & _mMask];

                return true;

            }

            else
            {

                lock (_mForeignLock)
                {

                    if (_mHeadIndex <= tail)
                    {

                        // Element still available. Take it.

                        obj = _mArray[tail & _mMask];

                        return true;

                    }

                    else
                    {

                        // We lost the race, element was stolen, restore the tail.

                        _mTailIndex = tail + 1;

                        return false;

                    }

                }

            }

        }


        public bool TrySteal(ref T obj, int millisecondsTimeout = 100)
        {

            bool taken = false;

            try
            {

                taken = Monitor.TryEnter(_mForeignLock, millisecondsTimeout);

                if (taken)
                {

                    int head = _mHeadIndex;

                    Interlocked.Exchange(ref _mHeadIndex, head + 1);

                    if (head < _mTailIndex)
                    {

                        obj = _mArray[head & _mMask];

                        return true;

                    }

                    else
                    {

                        _mHeadIndex = head;

                        return false;

                    }

                }

            }

            finally
            {

                if (taken)
                {

                    Monitor.Exit(_mForeignLock);
                }
            }

            return false;

        }

    }
}
