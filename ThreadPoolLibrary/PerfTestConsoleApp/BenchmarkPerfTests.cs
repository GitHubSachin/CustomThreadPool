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
        Custom2 = 2,
        Custom3 = 3
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
        public int G0Collect { get; set; }
        public int G1Collect { get; set; }
        public int G2Collect { get; set; }
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
                    using (var tokenSource = new CancellationTokenSource())
                    {

                        var pool = CreatePool(testConfig.PoolType, new ThreadPoolSettings()
                        {
                            MaxThreads = testConfig.MaxThreads,
                            MinThreads = testConfig.MinThreads,
                        },tokenSource.Token);

                        try
                        {
                            int g0collects = GC.CollectionCount(0);
                            int g1collects = GC.CollectionCount(1);
                            int g2collects = GC.CollectionCount(2);

                            using (var countdown = new CountdownEvent(testConfig.NoOfWorkItems))
                            {
                                DateTime dtStart = DateTime.UtcNow;
                                for (int j = 0; j < testConfig.NoOfWorkItems; j++)
                                {
                                    pool.QueueUserWorkItem((token, userdata) =>
                                    {
                                        CalculateNthFibonaci(NthFibonaciToCalculate);
                                        countdown.Signal(1);

                                    }, j);
                                }
                                result.EnqueueTime = DateTime.UtcNow.Subtract(dtStart).TotalSeconds;
                                countdown.Wait();
                                //finished, record execution time
                                result.ProcessingTime = DateTime.UtcNow.Subtract(dtStart).TotalSeconds;
                            }

                            result.G0Collect = (GC.CollectionCount(0) - g0collects);
                            result.G1Collect = (GC.CollectionCount(1) - g1collects);
                            result.G2Collect = (GC.CollectionCount(2) - g2collects);

                            allresults.Add(result);
                            tokenSource.Cancel();
                        }
                        finally
                        {
                            pool.Dispose();
                            GC.Collect(2);
                            GC.WaitForPendingFinalizers();
                        }
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

        public static CustomThreadPool CreatePool(PoolType type,ThreadPoolSettings config, CancellationToken tk)
        {
            switch (type)
            {
                case PoolType.Custom1:
                    return new CustomThreadPool1(config, tk);
                case PoolType.Custom2:
                    return new CustomThreadPool2(config, tk);
                case PoolType.Custom3:
                    return new CustomThreadPool3(config, tk);
                default:
                    return new DefaultThreadPool(config, tk);
            }
        }
        
    }
   
}
