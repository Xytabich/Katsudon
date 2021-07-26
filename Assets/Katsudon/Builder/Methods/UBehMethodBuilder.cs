using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Katsudon.Builder
{
	public class UBehMethodBuilder : ProgramBlock.IMethodBuilder, IComparer<IOperationBuider>, IOperationBuildersRegistry
	{
		public delegate void MethodHeaderCtor(IMethodDescriptor method, UdonMachine udonMachine);

		int ProgramBlock.IMethodBuilder.order => 1000;

		private SortedSet<IOperationBuider>[] builders = new SortedSet<IOperationBuider>[384];

		private NumericConvertersList convertersList;

		public UBehMethodBuilder(NumericConvertersList convertersList)
		{
			this.convertersList = convertersList;
		}

		public void RegisterOpBuilder(OpCode code, IOperationBuider builder)
		{
			SortedSet<IOperationBuider> list;
			int index = OpCodeToIndex(code);
			if((list = builders[index]) == null)
			{
				list = new SortedSet<IOperationBuider>(this);
				builders[index] = list;
			}
			list.Add(builder);
		}

		public void UnRegisterOpBuilder(OpCode code, IOperationBuider builder)
		{
			SortedSet<IOperationBuider> list;
			int index = OpCodeToIndex(code);
			if((list = builders[index]) != null)
			{
				list.Remove(builder);
			}
		}

		public bool BuildMethod(MethodInfo method, UBehMethodInfo uBehMethod, UdonMachine udonMachine, PropertiesBlock properties)
		{
			return BuildMethodBody(method, ApplyHeader, uBehMethod, udonMachine, properties);
		}

		public bool BuildMethodBody(MethodInfo method, MethodHeaderCtor headerCtor, UBehMethodInfo uBehMethod, UdonMachine udonMachine, PropertiesBlock properties)
		{
			var locals = method.GetMethodBody().LocalVariables;
			var localVars = new List<IVariable>(locals.Count);
			for(var i = 0; i < locals.Count; i++)
			{
				localVars.Add(new UnnamedVariable("loc", locals[i].LocalType));
			}

			List<Operation> operations = new List<Operation>();
			foreach(var op in new MethodReader(method, null))
			{
				operations.Add(op);
			}

			var methodDescriptor = new MethodDescriptor(uBehMethod, method.IsStatic, udonMachine.classType, operations, localVars, udonMachine, convertersList);
			methodDescriptor.PreBuild(udonMachine);

			if(headerCtor != null) headerCtor(methodDescriptor, udonMachine);

			SortedSet<IOperationBuider> list;
			while(methodDescriptor.Next())
			{
				bool isProcessed = false;
				int index = OpCodeToIndex(methodDescriptor.currentOp.opCode);
				try
				{
					if((list = builders[index]) != null)
					{
						foreach(var builder in list)
						{
							if(builder.Process(methodDescriptor))
							{
								isProcessed = true;
								break;
							}
						}
					}
					if(!isProcessed)
					{
						throw new System.Exception(string.Format("Builder not found for opcode {0}{1}", methodDescriptor.currentOp.opCode.Name, GetArgInfo(methodDescriptor.currentOp.argument)));
					}
				}
				catch
				{
					UnityEngine.Debug.LogErrorFormat("Exception during building opcode: {0} {1} 0x{2:X}", udonMachine.classType, method, methodDescriptor.currentOp.offset);
					throw;
				}
			}
			methodDescriptor.PostBuild();
			methodDescriptor.ApplyProperties(properties);
			return true;
		}

		int IComparer<IOperationBuider>.Compare(IOperationBuider x, IOperationBuider y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}

		private static void ApplyHeader(IMethodDescriptor method, UdonMachine udonMachine)
		{
			/*
			retaddr = __method_return;
			__method_return = 0xFFFFFFFF;
			*/
			var returnAddressVariable = method.GetReturnAddressVariable();
			method.machine.AddCopy(udonMachine.GetReturnAddressGlobal(), returnAddressVariable);
			method.machine.AddCopy(method.machine.GetConstVariable(UdonMachine.LAST_ALIGNED_ADDRESS), udonMachine.GetReturnAddressGlobal());
		}

		private static string GetArgInfo(object arg)
		{
			if(arg == null) return "";
			if(arg is MethodInfo method) return string.Format(", arg: {0}:{1}", method.DeclaringType, method);
			if(arg is FieldInfo field) return string.Format(", arg: {0}:{1}", field.DeclaringType, field);
			return ", arg: " + arg;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int OpCodeToIndex(OpCode opCode)
		{
			int value = (ushort)opCode.Value;
			return (value & 0xFF) + (value >> 8);
		}
	}
}