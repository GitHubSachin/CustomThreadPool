using System.Threading;

namespace ThreadPoolLibrary
{
    internal class LockFreeQueue<T>
    {
#pragma warning disable 0420
        private class Node
        {
            public T Data;
            public volatile Node next;
            public volatile Node prev;
            //public int id;
        }

        private volatile Node _head;
        private volatile Node _tail;

        public LockFreeQueue()
        {
            _head = new Node();
            _tail = new Node();
            _head = _tail;
        }

        public int Count
        {
            get
            {
                int count = 0;
                for (var curr = _head.next;
                    curr != null;
                    curr = curr.next)
                {
                    count++;
                }
                return count;
            }
        }
        /// <summary>
        /// Get's the value indicating if the Queue is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return _head.next == null; }
        }

        /// <summary>
        /// Get's the tail
        /// </summary>
        /// <returns>Tail.</returns>
        private Node GetTail()
        {
            Node localTail = _tail;
            Node localNext = localTail.next;

            //if some other thread moved the tail we need to set to the right possition.
            while (localNext != null)
            {
                //set the tail.
                Interlocked.CompareExchange(ref _tail, localNext, localTail);
                localTail = _tail;
                localNext = localTail.next;
            }

            return _tail;
        }

        /// <summary>
        /// Adds a new item on the Queue.
        /// </summary>
        /// <param name="obj">The value to be queued.</param>
        public void Enqueue(T obj)
        {
            Node localTail = null;
            Node newNode = new Node();
            newNode.Data = obj;
            //keep spinning till you catch the running tail.
            do
            {
                //get the tail.
                localTail = GetTail();
                newNode.next = localTail.next;
                newNode.prev = localTail;
            }
            // if we arent null, then this means that some other thread interffered and we need to start over.
            while (Interlocked.CompareExchange(ref localTail.next, newNode, null) != null);
            // if we finally are at the tail and we are the same, then we switch the values to the new node, phew! :)
            Interlocked.CompareExchange(ref _tail, newNode, localTail);
        }

        public bool TryDequeue(out T value)
        {

            // keep spining until we catch the propper head.
            while (true)
            {
                Node localHead = _head;
                Node localNext = localHead.next;
                //Node localTail = tail;

                // if the queue is empty then return the default for that type.
                if (localNext == null)
                {
                    value = default(T);
                    return false;
                }
                //else if (localHead == localTail)
                //{
                //    Interlocked.CompareExchange(ref tail, localHead, localTail);
                //}
                else
                {
                    localNext.prev = localHead.prev;

                    // if no other thread changed the head then we are good to
                    // go and we can return the local value;
                    if (Interlocked.CompareExchange(ref _head, localNext, localHead) == localHead)
                    {
                        //Note: This read would be unsafe in c++, but c# have GC and it will keep localnext alive because it keeps reference to it.
                        
                        value = localNext.Data;
                        return true;
                    }
                }
            }
        }

        public bool TryPeek(out T value)
        {
            Node current = _head.next;
            if (current == null)
            {
                value = default(T);
                return false;
            }
            else
            {
                value = current.Data;
                return true;
            }
        }

    }
}
