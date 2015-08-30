using System;
using System.Collections.Generic;

namespace PerfTestConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {

            //Build each scenario to test and execute it
            var scenario1 = new List<TestConfiguration>()
            {
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000,
                    PoolType = PoolType.Custom1
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 10000,
                    PoolType = PoolType.Custom1
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 100000,
                    PoolType = PoolType.Custom1
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000000,
                    PoolType = PoolType.Custom1
                }
            };
            var results = TestExecution.ExecuteTest(scenario1);
            Print(results);
            Console.WriteLine();

            //scenario2
            var scenario2 = new List<TestConfiguration>()
            {
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000,
                    PoolType = PoolType.Custom2
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 10000,
                    PoolType = PoolType.Custom2
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 100000,
                    PoolType = PoolType.Custom2
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000000,
                    PoolType = PoolType.Custom2
                }
            };
            results = TestExecution.ExecuteTest(scenario2);
            Print(results);

            //scenario3
            var scenario3 = new List<TestConfiguration>()
            {
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000,
                    PoolType = PoolType.Default
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 10000,
                    PoolType = PoolType.Default
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 100000,
                    PoolType = PoolType.Default
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000000,
                    PoolType = PoolType.Default
                }
            };
            results = TestExecution.ExecuteTest(scenario3);
            Print(results);
            

            //scenario4
            var scenario4 = new List<TestConfiguration>()
            {
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000,
                    PoolType = PoolType.Custom3
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 10000,
                    PoolType = PoolType.Custom3
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 100000,
                    PoolType = PoolType.Custom3
                },
                new TestConfiguration()
                {
                    MaxThreads = 8,
                    MinThreads = 1,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000000,
                    PoolType = PoolType.Custom3
                }
            };
            results = TestExecution.ExecuteTest(scenario4);
            Print(results);
        }

        private static void Print(List<TestResult> results)
        {
            Console.WriteLine(results.ToStringTable(
                new[] { "Id", "PoolType", "Min", "Max", "WorkItems", "EnqueueTime", "ProcessTime","G0","G1","G2" },
                a => a.Iteration, a => (a.PoolType == PoolType.Default ? ".net" : a.PoolType.ToString()), a => a.MinThreads, a => a.MaxThreads, a => a.NoOfWorkItems, a => a.EnqueueTime, a => a.ProcessingTime, a => a.G0Collect, a => a.G1Collect, a => a.G2Collect));
        }
    }

}
