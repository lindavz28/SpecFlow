using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using TechTalk.SpecFlow.Assist;
using TechTalk.SpecFlow.Async;
using TechTalk.SpecFlow.Infrastructure;

namespace TechTalk.SpecFlow
{
    public interface ITestRunnerManager
    {
        ITestRunner CreateTestRunner(Assembly testAssembly, bool async);
        ITestRunner GetTestRunner(Assembly testAssembly, bool async);
    }

    public class TestRunnerManager : ITestRunnerManager, IDisposable
    {
        // Lindavz - my addition, think this is needed but not sure
        private static readonly ThreadLocal<ITestRunnerManager> _instance = new ThreadLocal<ITestRunnerManager>( () => new TestRunnerManager() );

        public static ITestRunnerManager Instance
        {
            get { return _instance.Value; }
            private set { _instance.Value = value; }
        }
        static TestRunnerManager()
        {
        }

        private readonly ITestRunContainerBuilder _testRunContainerBuilder;

        public TestRunnerManager(ITestRunContainerBuilder testRunContainerBuilder = null)
        {
            _testRunContainerBuilder = testRunContainerBuilder ?? new TestRunContainerBuilder();
        }

        protected class TestRunnerKey
        {
            public readonly Assembly TestAssembly;
            public readonly bool Async;

            public TestRunnerKey(Assembly testAssembly, bool async)
            {
                TestAssembly = testAssembly;
                Async = async;
            }

            public bool Equals(TestRunnerKey other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(other.TestAssembly, TestAssembly) && other.Async.Equals(Async);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != typeof (TestRunnerKey)) return false;
                return Equals((TestRunnerKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (TestAssembly.GetHashCode()*397) ^ Async.GetHashCode();
                }
            }
        }

        private readonly Dictionary<TestRunnerKey, ITestRunner> _testRunnerRegistry = new Dictionary<TestRunnerKey, ITestRunner>();
        private readonly object _syncRoot = new object();

        public ITestRunner CreateTestRunner(Assembly testAssembly, bool async)
        {
            return CreateTestRunner(new TestRunnerKey(testAssembly, async));
        }

        protected virtual ITestRunner CreateTestRunner(TestRunnerKey key)
        {
            var container = _testRunContainerBuilder.CreateContainer();
            if (key.Async)
            {
                //TODO: better support this in the DI container
                container.RegisterTypeAs<AsyncTestRunner, ITestRunner>();
            }
            var factory = container.Resolve<ITestRunnerFactory>();
            return factory.Create(key.TestAssembly);
        }

        public ITestRunner GetTestRunner(Assembly testAssembly, bool async)
        {
            return GetTestRunner(new TestRunnerKey(testAssembly, async));
        }

        protected virtual ITestRunner GetTestRunner(TestRunnerKey key)
        {
            ITestRunner testRunner;
            if (!_testRunnerRegistry.TryGetValue(key, out testRunner))
            {
                lock(_syncRoot)
                {
                    if (!_testRunnerRegistry.TryGetValue(key, out testRunner))
                    {
                        testRunner = CreateTestRunner(key);
                        _testRunnerRegistry.Add(key, testRunner);
                    }
                }
            }
            return testRunner;
        }

        public virtual void Dispose()
        {
            _testRunnerRegistry.Clear();
        }

        #region Static Methods

        public static ITestRunner GetTestRunner()
        {
            return Instance.GetTestRunner(Assembly.GetCallingAssembly(), false);
        }

        public static ITestRunner GetAsyncTestRunner()
        {
            return Instance.GetTestRunner(Assembly.GetCallingAssembly(), true);
        }

        internal static void Reset()
        {
            if (Instance is IDisposable)
                ((IDisposable)Instance).Dispose();
            Instance = new TestRunnerManager();
        }

        #endregion
    }
}