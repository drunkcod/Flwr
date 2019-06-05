using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Flwr
{
	public class PlanNode
	{
		public int Id;
		public string Name;
		public int[] DependsOn;
		public int[] RequriedBy;
		public Delegate Delegate;
		public object Invoke(object[] args) => Delegate.DynamicInvoke(args);
	}

	public class GraphNode : IGraphNode
	{
		public int Id { get; }
		public readonly string Name;
		public readonly int[] DependsOn;
		public Delegate Delegate;


		public GraphNode(int id, string name, int[] dependsOn, Delegate fun)
		{
			this.Id = id;
			this.Name = name;
			this.DependsOn = dependsOn;
			this.Delegate = fun;
		}
	}

	public interface IGraphNode
	{
		int Id { get; }
	}

	public interface IGraphNode<out T> : IGraphNode { }

	public class GraphNode<T> : GraphNode, IGraphNode<T>
	{
		public GraphNode(int id, string name, int[] dependsOn, Delegate fun) : base(id, name, dependsOn, fun) { }
	}

	public class PlanBuilder
	{
		struct NoReturn { }

		readonly List<GraphNode> nodes = new List<GraphNode>();

		public IReadOnlyList<GraphNode> Nodes => nodes;

		public GraphNode Add(string name, Action fun, params IGraphNode[] dependsOn) => AddDelegate<NoReturn>(name, fun, dependsOn);
		public GraphNode<T> Add<T>(string name, Func<T> fun) => AddDelegate<T>(name, fun);
		public GraphNode Add<TArg0>(string name, Action<TArg0> act, IGraphNode<TArg0> arg0) => AddDelegate<NoReturn>(name, act, arg0);

		GraphNode<T> AddDelegate<T>(string name, Delegate fun, params IGraphNode[] dependsOn)
		{
			var node = new GraphNode<T>(nodes.Count, name, Array.ConvertAll(dependsOn, x => x.Id), fun);
			nodes.Add(node);
			return node;
		}

		public Plan Build()
		{
			var requiredBy = new List<int>[nodes.Count];
			for (var i = 0; i != nodes.Count; ++i)
			{
				var current = nodes[i];
				requiredBy[i] = new List<int>();
				foreach (var item in current.DependsOn)
					requiredBy[item].Add(current.Id);
			}
			return new Plan(nodes.Select(x => new PlanNode
			{
				Id = x.Id,
				Name = x.Name,
				DependsOn = x.DependsOn,
				RequriedBy = requiredBy[x.Id].ToArray(),
				Delegate = x.Delegate,
			}).ToArray());
		}
	}
}
