using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ThreadPoolLibrary.UnitTest
{
    [TestClass]
    public class CustomThreadPool3Test
    {
        [TestMethod]
        public void Ensure_NewPool_HasUniqueName()
        {
            string name1, name2;
            using (var pool = new CustomThreadPool3(CancellationToken.None))
            {
                Assert.IsNotNull(pool.Name);
                name1 = pool.Name;
            }
            using (var pool = new CustomThreadPool3(CancellationToken.None))
            {
                Assert.IsNotNull(pool.Name);
                name2 = pool.Name;
            }
            Assert.AreNotEqual(name1, name2);
        }

        [TestMethod]
        public void Ensure_CancelToken_StopsThreadPool()
        {
            bool itemprocessed = false;
            //Arrange
            var settings = new ThreadPoolSettings()
            {
                MaxThreads = 2,
                MinThreads = 1,
                ThreadIdleTimeout = new TimeSpan(0, 0, 0, 0, 10), //10 ms thread idle timeout
            };
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool3(settings, tokenSrc.Token))
                {
                    var queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        itemprocessed = true; //indicates the item is processed by the pool.

                    }, null);

                    Assert.IsTrue(queued);

                    Assert.AreEqual(1, pool.TotalThreads);

                    //Act
                    //send cancel request to the pool
                    tokenSrc.Cancel();
                    //try to enqueue another item
                    queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        itemprocessed = true; //indicates the item is processed by the pool.

                    }, null);

                    //Assert
                    Assert.IsFalse(queued); //after cancel, user cant queue new work
                }
            }
        }

        [TestMethod]
        public void Ensure_QueueUserWorkItem_Enqueues_WorkItems()
        {
            //Arrange
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool3(tokenSrc.Token))
                {
                    //Act
                    for (int i = 0; i < 10; i++)
                    {
                        var queued = pool.QueueUserWorkItem((c, o) =>
                        {
                            //just empty work item, nothing to do..
                        }, null);
                        //Assert
                        Assert.IsTrue(queued);
                    }
                }
            }
        }

        [TestMethod]
        public void Ensure_ThreadPool_Runs_MinimumThreads()
        {
            //Arrange
            var settings = new ThreadPoolSettings()
            {
                MaxThreads = 100,
                MinThreads = 10,
                ThreadIdleTimeout = TimeSpan.FromMilliseconds(1), //1sec thread idle timeout
            };

            //Act
            using (var pool = new CustomThreadPool3(settings, CancellationToken.None))
            {
                //Assert
                Assert.AreEqual(10, pool.TotalThreads);
                //wait for 100ms and ensure pool still runs 10 threads
                Thread.Sleep(15);
                //Assert
                Assert.AreEqual(10, pool.TotalThreads);
            }

        }


        [TestMethod]
        public void Ensure_ThreadPool_Runs_MaximumThreads()
        {
            //Arrange
            var settings = new ThreadPoolSettings()
            {
                MaxThreads = 3,
                MinThreads = 1,
                ThreadIdleTimeout = new TimeSpan(0, 0, 0, 0, 5000), //5 sec. thread idle timeout
                NewThreadWaitTime = TimeSpan.FromMilliseconds(0),
            };

            //Act
            using (var pool = new CustomThreadPool3(settings, CancellationToken.None))
            {
                //Act
                for (int i = 0; i < 10000; i++)
                {
                    pool.QueueUserWorkItem((c, o) =>
                    {
                        Thread.Sleep(100);
                    }, null);

                }

                //Assert
                Assert.AreEqual(3, pool.TotalThreads);
            }
        }

        [TestMethod]
        public void Ensure_ThreadPool_Shrinks_To_MinimumThreads()
        {
            //Arrange
            var settings = new ThreadPoolSettings()
            {
                MaxThreads = 4,
                MinThreads = 1,
                ThreadIdleTimeout = new TimeSpan(0, 0, 0, 0, 5), //5 ms thread idle timeout
                NewThreadWaitTime = TimeSpan.FromMilliseconds(0),
            };

            //Act
            using (var pool = new CustomThreadPool3(settings, CancellationToken.None))
            {
                //Act
                for (int i = 0; i < 100000; i++)
                {
                    pool.QueueUserWorkItem((c, o) =>
                    {
                        //Thread.Sleep(0);
                    }, null);

                }
                //Assert
                Assert.AreEqual(4, pool.TotalThreads); //ensure reached to max limit
               //wait till all threads are drained
                while (pool.TotalThreads > settings.MinThreads)
                {
                    //Console.WriteLine(pool.TotalThreads);
                    Thread.Sleep(10);
                }
                //ensure pool reached to min size now
                Assert.AreEqual(1, pool.TotalThreads);
            }
        }

        [TestMethod]
        public void QueueUserWorkItem_Return_False_When_CancelRequestedOnPool()
        {

            //Arrange
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool3(tokenSrc.Token))
                {
                    //Act
                    tokenSrc.Cancel(); //sends cancel signal to the pool
                    var queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        //just empty work item, nothing to do..
                    }, null);
                    //Assert
                    Assert.IsFalse(queued);
                }
            }
        }

        [TestMethod]
        public void Pool_Raises_UserWorkItemException_Event_When_UserWorkItemThrowsUnhandledException()
        {
            bool eventCalled = false;
            //Arrange
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool3(tokenSrc.Token))
                {
                    pool.UserWorkItemException += (object sender, WorkItemEventArgs e) =>
                    {
                        Assert.AreEqual(123, (int)e.UserData);
                        Assert.IsNotNull(e.Exception);
                        eventCalled = true;
                    };

                    //Act
                    var queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        //throw unhandled exception from user's delegate
                        throw new ApplicationException("test user exception");
                    }, 123);
                    //Assert
                    Assert.IsTrue(queued);
                    Thread.Sleep(200); //ensures work item is processed.

                    Assert.IsTrue(eventCalled);
                }
            }
        }

        [TestMethod]
        public void Ensure_CancelToken_StopsLongRunningThread()
        {
            //Arrange
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool3(tokenSrc.Token))
                {
                    //Act
                    var queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(60)); //60 seconds

                    }, null);

                    Assert.IsTrue(queued);
                    Thread.Sleep(TimeSpan.FromMilliseconds(100));
                    //send cancel request to the pool
                    tokenSrc.Cancel();
                    
                    Assert.AreEqual(1, pool.TotalThreads); //still 1 thread running.
                    //wait for 5 seconds now and see if thread have exicted
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    Assert.AreEqual(1, pool.TotalThreads); //since the task is very long running, pool will let it finish before exiting all threads
                }
            }
        }
    }
}
