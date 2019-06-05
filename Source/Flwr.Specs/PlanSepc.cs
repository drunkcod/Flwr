using Cone;
using System;

namespace Flwr.Specs
{
	[Describe(typeof(Plan))]
	public class PlanSpec
	{
		class MyDisposable : IDisposable
		{
			public bool IsDisposed { get; private set; }

			public void Dispose() { IsDisposed = true; }
		}
		public void Disposes_results() {
			var plan = new PlanBuilder();
			var aDisposable = new MyDisposable();
			var create = plan.Add("Create", () => aDisposable);
			plan.Add("Use", (MyDisposable _) => { }, create);

			plan.Build().Invoke();
			Check.That(()=> aDisposable.IsDisposed); 
		}
	}
}
