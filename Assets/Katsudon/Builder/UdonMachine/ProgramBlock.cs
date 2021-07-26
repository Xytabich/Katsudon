using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Katsudon.Builder
{
	public class ProgramBlock : IUAssemblyBlock, IComparer<ProgramBlock.IMethodBuilder>
	{
		public delegate void BuildMethodDelegate(UBehMethodInfo info, UdonMachine udonMachine, PropertiesBlock properties);

		int IUAssemblyBlock.order => 1;

		private UdonMachine machine;
		private PropertiesBlock properties;
		private int executionOrder;

		private SortedSet<IMethodBuilder> builders;
		private List<UBehMethodInfo> methods = new List<UBehMethodInfo>();

		public ProgramBlock(UdonMachine machine, PropertiesBlock properties, int executionOrder)
		{
			this.machine = machine;
			this.properties = properties;
			this.executionOrder = executionOrder;

			builders = new SortedSet<IMethodBuilder>(this);
		}

		public void AddMethodBuilder(IMethodBuilder builder)
		{
			builders.Add(builder);
		}

		public void BuildMethod(MethodInfo method, UBehMethodInfo info)
		{
			methods.Add(info);
			machine.ApplyLabel(info);
			foreach(var builder in builders)
			{
				if(builder.BuildMethod(method, info, machine, properties))
				{
					break;
				}
			}
		}

		public void BuildMachine()
		{
			machine.Build();
			machine.ApplyProperties(properties);
		}

		void IUAssemblyBlock.AppendCode(StringBuilder builder)
		{
			if(methods.Count < 1) return;
			builder.Append(".code_start\n");
			if(executionOrder != 0)
			{
				builder.AppendFormat(".update_order {0}\n", executionOrder);
			}
			machine.Append(builder, methods);
			builder.Append("\n.code_end\n");
		}

		void IUAssemblyBlock.InitProgram(VRC.Udon.Common.Interfaces.IUdonProgram program) { }

		int IComparer<IMethodBuilder>.Compare(IMethodBuilder x, IMethodBuilder y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}

		public interface IMethodBuilder
		{
			int order { get; }

			bool BuildMethod(MethodInfo method, UBehMethodInfo uBehMethod, UdonMachine udonMachine, PropertiesBlock properties);
		}
	}

	public class UBehMethodInfo : IAddressLabel, IEmbedAddressLabel
	{
		public readonly string name;
		public readonly bool export;

		public readonly IVariable[] arguments;
		public readonly IVariable ret;

		public uint address { get; private set; }

		private Func<uint> pointerGetter;

		public UBehMethodInfo(string name, bool export, IVariable[] parameters, IVariable ret)
		{
			this.name = name;
			this.export = export;
			this.arguments = parameters;
			this.ret = ret;
		}

		void IEmbedAddressLabel.Init(Func<uint> pointerGetter)
		{
			this.pointerGetter = pointerGetter;
		}

		void IEmbedAddressLabel.Apply()
		{
			address = pointerGetter();
		}
	}
}