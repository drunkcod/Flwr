using System;
using System.Collections.Generic;
using System.Threading;

namespace Flwr
{
	public class Plan
	{
		readonly PlanNode[] nodes;

		public Plan(PlanNode[] nodes)
		{
			this.nodes = nodes;
		}

		public void Invoke()
		{
			var results = new object[nodes.Length];
			var readyToRun = new Queue<int>();
			var waitCount = new int[nodes.Length];
			var refCount = new int[nodes.Length];

			foreach (var item in nodes) {
				if(item.DependsOn.Length == 0)
					readyToRun.Enqueue(item.Id);
				else waitCount[item.Id] = item.DependsOn.Length;

				refCount[item.Id] = item.RequriedBy.Length;
			}

			while (readyToRun.Count != 0) {
				var item = nodes[readyToRun.Dequeue()];
				Console.WriteLine(item.Name);
				var args = new object[item.Delegate.Method.GetParameters().Length];
				for(var i = 0; i != args.Length; ++i)
					args[i] = results[i];
				results[item.Id] = item.Invoke(args);
				
				foreach(var waiter in item.RequriedBy)
					if(Interlocked.Decrement(ref waitCount[waiter]) == 0)
						readyToRun.Enqueue(waiter);

				foreach(var input in item.DependsOn)
					if(Interlocked.Decrement(ref refCount[input]) == 0) {
						var obj = results[input];
						results[input] = null;
						(obj as IDisposable)?.Dispose();
					}
			}
		}
	}
}
