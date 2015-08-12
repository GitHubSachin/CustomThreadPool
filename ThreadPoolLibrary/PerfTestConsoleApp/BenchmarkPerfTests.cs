using System;
using System.Collections.Generic;
using System.Threading;
using ThreadPoolLibrary;

namespace PerfTestConsoleApp
{
    internal enum PoolType
    {
        Default = 0,
        Custom1 = 1,
        Custom2 = 2
    }

    internal class TestConfiguration
    {
        public int NoOfWorkItems { get; set; }
        public int MinThreads { get; set; }
        public int MaxThreads { get; set; }
        public int NoOfIterations { get; set; }
        public PoolType PoolType { get; set; }
    }

    internal class TestResult : TestConfiguration
    {
        public int Iteration { get; set; }
        public double EnqueueTime { get; set; }
        public double ProcessingTime { get; set; }
    }

    internal class TestExecution
    {
        private const int NthFibonaciToCalculate = 20;

        public static List<TestResult> ExecuteTest(List<TestConfiguration> testConfigs)
        {
            var allresults = new List<TestResult>();
            foreach (var testConfig in testConfigs)
            {

                for (int i = 0; i < testConfig.NoOfIterations; i++)
                {
                    var result = new TestResult()
                    {
                        NoOfWorkItems = testConfig.NoOfWorkItems,
                        NoOfIterations = testConfig.NoOfIterations,
                        MaxThreads = testConfig.MaxThreads,
                        MinThreads = testConfig.MinThreads,
                        PoolType = testConfig.PoolType,
                        Iteration = (i + 1), //i is zero based
                    };
                    var pool = CreatePool(testConfig.PoolType, new ThreadPoolSettings()
                    {
                        MaxThreads = testConfig.MaxThreads,
                        MinThreads = testConfig.MinThreads,
                    });

                    try
                    {
                        int count = 0;
                        DateTime dtStart = DateTime.UtcNow;
                        for (int j = 0; j < testConfig.NoOfWorkItems; j++)
                        {
                            pool.QueueUserWorkItem((token, userdata) =>
                            {
                                CalculateNthFibonaci(NthFibonaciToCalculate);
                                Interlocked.Increment(ref count);
                            }, j);
                        }
                        //enqueued all items, calculate end time.
                        result.EnqueueTime = DateTime.UtcNow.Subtract(dtStart).TotalSeconds;
                        //now wait
                        while (count < testConfig.NoOfWorkItems)
                        {
                            Thread.Sleep(1);
                        }
                        //finished, record execution time
                        result.ProcessingTime = DateTime.UtcNow.Subtract(dtStart).TotalSeconds;
                        allresults.Add(result);
                    }
                    finally
                    {
                        pool.Dispose();
                    }
                }
            }
            return allresults;
        }

        public static long CalculateNthFibonaci(int n)
        {
            //Thread.Sleep(n);
            //return 0;
            if (n <= 1)
            {
                return n;
            }
            return CalculateNthFibonaci((n - 1)) + CalculateNthFibonaci((n - 2));
        }

        public static CustomThreadPool CreatePool(PoolType type,ThreadPoolSettings config)
        {
            switch (type)
            {
                case PoolType.Custom1:
                    return new CustomThreadPool1(config,CancellationToken.None);
                case PoolType.Custom2:
                    return new CustomThreadPool2(config, CancellationToken.None);
                default:
                    return new DefaultThreadPool(config,CancellationToken.None);
            }
        }
        
    }
   
}
