using System.Threading;

namespace ThreadPoolLibrary
{
    internal class LockFreeQueue<T>
    {
        private class Node
        {
            public T Data;
            public volatile Node next;
            public volatile Node prev;
            public int id;
        }

        private volatile Node head;
        private volatile Node tail;

        public LockFreeQueue()
        {
            head = new Node();
            tail = new Node();
            head = tail;
        }

        public int UnsafeCount
        {
            get
            {
                return tail.id - head.id;
            }
        }

        /// <summary>
        /// Get's the value indicating if the Queue is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return head.next == null; }
        }

        /// <summary>
        /// Get's the tail
        /// </summary>
        /// <returns>Tail.</returns>
        private Node GetTail()
        {
            Node localTail = tail;
            Node localNext = localTail.next;

            //if some other thread moved the tail we need to set to the right possition.
            while (localNext != null)
            {
                //set the tail.
                Interlocked.CompareExchange(ref tail, localNext, localTail);
                localTail = tail;
                localNext = localTail.next;
            }

            return tail;
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
                newNode.id = localTail.id + 1;
                newNode.prev = localTail;
            }
            // if we arent null, then this means that some other thread interffered and we need to start over.
            while (Interlocked.CompareExchange(ref localTail.next, newNode, null) != null);
            // if we finally are at the tail and we are the same, then we switch the values to the new node, phew! :)
            Interlocked.CompareExchange(ref tail, newNode, localTail);
        }

        public T Dequeue()
        {
            // keep spining until we catch the propper head.
            while (true)
            {
                Node localHead = head;
                Node localNext = localHead.next;
                Node localTail = tail;

                // if the queue is empty then return the default for that
                // typeparam.
                if (localNext == null)
                {
                    return default(T);
                }
                else if (localHead == localTail)
                {
                    Interlocked.CompareExchange(ref tail, localHead, localTail);
                }
                else
                {
                    localNext.prev = localHead.prev;

                    // if no other thread changed the head then we are good to
                    // go and we can return the local value;
                    if (Interlocked.CompareExchange(ref head, localNext, localHead) == localHead)
                    {
                        return localNext.Data;
                    }
                }
            }
        }


    }
}
