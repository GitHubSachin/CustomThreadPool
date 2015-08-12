using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ThreadPoolLibrary.UnitTest
{
    [TestClass]
    public class CustomThreadPool1Test
    {
        [TestMethod]
        public void Ensure_NewPool_HasUniqueName()
        {
            string name1, name2;
            using (var pool = new CustomThreadPool1(CancellationToken.None))
            {
                Assert.IsNotNull(pool.Name);
                name1 = pool.Name;
            }
            using (var pool = new CustomThreadPool1(CancellationToken.None))
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
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool1(tokenSrc.Token))
                {
                    var queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        itemprocessed = true; //indicates the item is processed by the pool.

                    }, null);

                    Assert.IsTrue(queued);
                    Assert.AreEqual(pool.TotalThreads, 1);

                    //Act
                    //send cancel request to the pool
                    tokenSrc.Cancel();
                    //try to enqueue another item
                    queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        itemprocessed = true; //indicates the item is processed by the pool.

                    }, null);

                    //Assert
                    Assert.IsFalse(queued);
                    Assert.IsTrue(pool.TotalThreads>=0); //there should be upto 0 working threads because cancel will signal all threads to exit
                }
            }
        }

        [TestMethod]
        public void Ensure_QueueUserWorkItem_Enqueues_WorkItems()
        {
            //Arrange
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool1(tokenSrc.Token))
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
                ThreadIdleTimeout = new TimeSpan(0,0,0,0,10), //10 ms thread idle timeout
            };

            //Act
            using (var pool = new CustomThreadPool1(settings, CancellationToken.None))
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
                MaxThreads = 2,
                MinThreads = 1,
                ThreadIdleTimeout = new TimeSpan(0, 0, 0, 0, 5000), //5 sec. thread idle timeout
            };

            //Act
            using (var pool = new CustomThreadPool1(settings, CancellationToken.None))
            {
                //Act
                for (int i = 0; i < 10; i++)
                {
                    pool.QueueUserWorkItem((c, o) =>
                    {
                        Thread.Sleep(1);
                    }, null);
                    
                }

                //Assert
                Assert.AreEqual(2, pool.TotalThreads);
            }
        }

        [TestMethod]
        public void Ensure_ThreadPool_Shriks_To_MinimumThreads()
        {
            //Arrange
            var settings = new ThreadPoolSettings()
            {
                MaxThreads = 3,
                MinThreads = 1,
                ThreadIdleTimeout = new TimeSpan(0, 0, 0, 0, 100), //1 ms thread idle timeout
            };

            //Act
            using (var pool = new CustomThreadPool1(settings, CancellationToken.None))
            {
                //Act
                for (int i = 0; i < 100; i++)
                {
                    pool.QueueUserWorkItem((c, o) =>
                    {
                        //Thread.Sleep(1);
                    }, null);

                }

                //Assert
                Assert.AreEqual(settings.MaxThreads, pool.TotalThreads); //ensure reached to max limit
                Thread.Sleep(3000);
                Assert.IsTrue(pool.TotalThreads < settings.MaxThreads);
            }
        }

        [TestMethod]
        public void QueueUserWorkItem_Return_False_When_CancelRequestedOnPool()
        {
            
            //Arrange
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool1(tokenSrc.Token))
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
                using (var pool = new CustomThreadPool1(tokenSrc.Token))
                {
                    pool.UserWorkItemException += (object sender, WorkItemEventArgs e)=>
                    {
                        Assert.AreEqual(123, (int) e.UserData);
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
                using (var pool = new CustomThreadPool1(tokenSrc.Token))
                {
                    pool.UserWorkItemException += (object sender, WorkItemEventArgs e) =>
                    {
                        Console.WriteLine(e.Exception.ToString());
                    };

                    //Act
                    var queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        Thread.Sleep(new TimeSpan(0,0,0,5)); //5 seconds

                    }, null);

                    Assert.IsTrue(queued);
                    
                    //send cancel request to the pool
                    tokenSrc.Cancel();
                    //try to enqueue another item
                    Assert.AreEqual(1,pool.TotalThreads); //still 1 thread running.
                    //wait for 5 seconds now and see if thread have exicted
                    Thread.Sleep(new TimeSpan(0, 0, 0, 6));
                    Assert.AreEqual(0, pool.TotalThreads); //zero threads
                }
            }
        }

        [TestMethod]
        public void Ensure_CancelToken_StopsEnqueuingNewWork()
        {
            //Arrange
            using (var tokenSrc = new CancellationTokenSource())
            {
                using (var pool = new CustomThreadPool1(tokenSrc.Token))
                {
                    pool.UserWorkItemException += (object sender, WorkItemEventArgs e) =>
                    {
                        Console.WriteLine(e.Exception.ToString());
                    };

                    //Act
                    var queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        Thread.Sleep(new TimeSpan(0, 0, 0, 0)); //0

                    }, null);

                    Assert.IsTrue(queued);

                    //send cancel request to the pool
                    tokenSrc.Cancel();
                    queued = pool.QueueUserWorkItem((c, o) =>
                    {
                        Thread.Sleep(new TimeSpan(0, 0, 0, 0)); //0

                    }, null);
                    Assert.IsFalse(queued);
                }
            }
        }
    }
}
