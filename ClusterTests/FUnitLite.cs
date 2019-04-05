using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using log4net;
using NUnit.Framework;

namespace ClusterTests
{
	public class FUnitLite
	{
		public void AddTestFixture<T>(T testFixture)
		{
			testFixtures.Add(new TestFixture<T>(testFixture));
		}

		public void RunAndReport()
		{
			var results = new List<(string, bool)>();
			foreach (var testFixture in testFixtures)
				results.AddRange(testFixture.Run().Select(p => ($"{testFixture.Name}.{p.Item1}", p.Item2)));

			var oldColor = Console.ForegroundColor;
			foreach (var (name, isSuccess) in results)
			{
				Console.ForegroundColor = isSuccess ? ConsoleColor.Green : ConsoleColor.Red;
				Console.WriteLine($"[{name}] {(isSuccess ? "Passed" : "Faulted")}");
			}
			Console.ForegroundColor = oldColor;
		}

		private readonly List<ITestFixture> testFixtures = new List<ITestFixture>();

		private interface ITestFixture
		{
			string Name { get; }
			IEnumerable<(string, bool)> Run();
		}

		private class TestFixture<T> : ITestFixture
		{
			public TestFixture(T instance)
			{
				Name = typeof(T).Name;

				setUp = GetMethodsWithAttribute<SetUpAttribute>(instance).SingleOrDefault();
				tearDown = GetMethodsWithAttribute<TearDownAttribute>(instance).SingleOrDefault();
				var tests = GetMethodsWithAttribute<TestAttribute>(instance);

				foreach (var method in tests)
					this.tests.Add(method);
			}

			public IEnumerable<(string, bool)> Run()
			{
				foreach (var test in tests)
				{
					if (setUp.Action != null && !setUp.Action.Try(out var setUpEx))
						throw new InvalidOperationException($"[{Name}.{test.Name}] SetUp faulted", setUpEx);

					if (test.Action.Try(out var ex))
					{
						yield return (test.Name, true);
					}
					else
					{
						Log.Error($"[{Name}.{test.Name}]", ex);
						yield return (test.Name, false);
					}

					if (tearDown.Action != null && !tearDown.Action.Try(out var tearDownEx))
						throw new InvalidOperationException($"[{Name}.{test.Name}] TearDown faulted", tearDownEx);
				}
			}

			public string Name { get; }
			private readonly List<Method> tests = new List<Method>();
			private readonly Method setUp;
			private readonly Method tearDown;

			private static IEnumerable<Method> GetMethodsWithAttribute<TAttribute>(T testFixture)
				where TAttribute : Attribute
			{
				return typeof(T).GetMethods()
					.Where(m => m.GetCustomAttributes<TAttribute>().Any())
					.Where(m => m.GetParameters().Length == 0)
					.Select(m => new Method(m, testFixture));
			}

			private class Method
			{
				public readonly string Name;
				public readonly Action Action;

				public Method(MethodInfo method, object instance)
					: this(method.Name, () => method.Invoke(instance, new object[0]))
				{ }

				public Method(string name, Action action)
				{
					Name = name;
					Action = action;
				}
			}
		}

		private static readonly ILog Log = LogManager.GetLogger(typeof(FUnitLite));
	}
}