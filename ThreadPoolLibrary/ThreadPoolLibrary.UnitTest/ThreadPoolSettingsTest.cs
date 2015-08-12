using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ThreadPoolLibrary.UnitTest
{
    [TestClass]
    public class ThreadPoolSettingsTest
    {
        [TestMethod]
        public void NewSettings_object_contains_DefaultValues()
        {
            ThreadPoolSettings target = new ThreadPoolSettings();
            Assert.AreEqual(target.MinThreads, 1); //default min threads are always 1
            Assert.AreEqual(target.MaxThreads, Environment.ProcessorCount); //default max threads are always 1
            Assert.AreEqual(target.ThreadIdleTimeout,
                new TimeSpan(0, 0, 0, 0, TheadPoolDefaultLimits.DefaultThreadIdleTimeout));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MinThreads_Throws_ArgumentException_When_Negative()
        {
            var target = new ThreadPoolSettings()
            {
                MinThreads = -1,
            };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MinThreads_Throws_ArgumentException_When_Zero()
        {
            var target = new ThreadPoolSettings()
            {
                MinThreads = 0,
            };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void MinThreads_Throws_ArgumentOutOfRangeException_When_GreaterThanMaxThreads()
        {
            var target = new ThreadPoolSettings()
            {
                MaxThreads=10,
                MinThreads = 20,
            };
        }


        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void MaxThreads_Throws_ArgumentException_When_Negative()
        {
            var target = new ThreadPoolSettings()
            {
                MaxThreads = -1,
            };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void maxThreads_Throws_ArgumentException_When_Zero()
        {
            var target = new ThreadPoolSettings()
            {
                MaxThreads = 0,
            };
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void MaxThreads_Throws_ArgumentOutOfRangeException_When_GreaterThanMaxThreads()
        {
            var target = new ThreadPoolSettings()
            {
                MaxThreads = int.MaxValue,
                MinThreads = int.MaxValue,
            };
        }
    }
}
