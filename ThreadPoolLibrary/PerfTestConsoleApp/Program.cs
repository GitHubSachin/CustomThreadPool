using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000,
                    PoolType = PoolType.Custom1
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 10000,
                    PoolType = PoolType.Custom1
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 100000,
                    PoolType = PoolType.Custom1
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
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
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000,
                    PoolType = PoolType.Custom2
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 10000,
                    PoolType = PoolType.Custom2
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 100000,
                    PoolType = PoolType.Custom2
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
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
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000,
                    PoolType = PoolType.Default
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 10000,
                    PoolType = PoolType.Default
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 100000,
                    PoolType = PoolType.Default
                },
                new TestConfiguration()
                {
                    MaxThreads = 100,
                    MinThreads = 10,
                    NoOfIterations = 1,
                    NoOfWorkItems = 1000000,
                    PoolType = PoolType.Default
                }
            };
            results = TestExecution.ExecuteTest(scenario3);
            Print(results);
        }

        private static void Print(List<TestResult> results)
        {
            Console.WriteLine(results.ToStringTable(
                new[] { "Id", "PoolType", "Min", "Max", "WorkItems", "EnqueueTime", "ProcessTime" },
                a => a.Iteration, a => (a.PoolType == PoolType.Default ? ".net" : a.PoolType.ToString()), a => a.MinThreads, a => a.MaxThreads, a => a.NoOfWorkItems, a => a.EnqueueTime, a => a.ProcessingTime));
        }
    }

}
