using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Katsudon.Builder.Methods;

namespace Katsudon.Builder
{
	public class MethodBodyBuilder : IComparer<IOperationBuider>, IOperationBuildersRegistry
	{
		private SortedSet<IOperationBuider>[] builders = new SortedSet<IOperationBuider>[384];

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

		public void Build(MethodInfo method, IList<IVariable> arguments, IVariable returnVariable,
			IAddressLabel returnAddress, IUdonProgramBlock machineBlock, PropertiesBlock properties)
		{
			var locals = method.GetMethodBody().LocalVariables;
			var localVars = new List<IVariable>(locals.Count);//FIX: cache
			for(var i = 0; i < locals.Count; i++)
			{
				localVars.Add(new UnnamedVariable("loc", locals[i].LocalType));
			}

			Build(method, arguments, localVars, returnVariable, returnAddress, machineBlock);

			foreach(var variable in localVars)
			{
				properties.AddVariable(variable);
			}
		}

		public void Build(MethodInfo method, IList<IVariable> arguments, IList<IVariable> locals,
			IVariable returnVariable, IAddressLabel returnAddress, IUdonProgramBlock machineBlock)
		{
			List<Operation> operations = new List<Operation>();//FIX: cache
			foreach(var op in new MethodReader(method, null))
			{
				operations.Add(op);
			}

			var addressPointers = new List<UdonAddressPointer>();//FIX: cache
			addressPointers.Add(new UdonAddressPointer(machineBlock.machine.GetAddressCounter(), 0));
			var methodDescriptor = new MethodDescriptor(method.IsStatic, arguments, returnVariable, returnAddress, operations, locals, machineBlock, addressPointers);
			try
			{
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
						UnityEngine.Debug.LogErrorFormat("Exception during building opcode on position 0x{0:X}", methodDescriptor.currentOp.offset);
						throw;
					}
				}
				methodDescriptor.CheckState();
				methodDescriptor.ApplyProperties();

				if(BuildMeta(method, addressPointers, machineBlock.machine.GetAddressCounter(), out var meta))
				{
					machineBlock.machine.AddMethodMeta(meta);
				}
			}
			catch
			{
				UnityEngine.Debug.LogErrorFormat("Exception during building method {1} from {0}", method.DeclaringType, method);
				throw;
			}
		}

		int IComparer<IOperationBuider>.Compare(IOperationBuider x, IOperationBuider y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}

		private static bool BuildMeta(MethodInfo method, List<UdonAddressPointer> addressPointers, uint endAddress, out UdonMethodMeta meta)
		{
			meta = default;
			var assembly = method.DeclaringType.Assembly;
			if(assembly.IsDynamic) return false;
			meta = new UdonMethodMeta(assembly.FullName, method.Module.Name, method.MetadataToken, addressPointers[0].udonAddress, endAddress, addressPointers);
			return true;
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

	public interface IMethodDescriptor : IMethodProgram, IMethodStack, IMethodVariables, IUdonProgramBlock
	{
		bool isStatic { get; }

		IAddressLabel GetMachineAddressLabel(int methodAddress);

		IAddressLabel GetReturnAddress();
	}

	public interface IExternBuilder
	{
		string BuildExtern(IMethodDescriptor method, IUdonMachine machine, Action<VariableMeta> pushCallback);
	}

	public interface IMethodVariables
	{
		IVariable GetReturnVariable();

		IVariable GetArgumentVariable(int index);

		IVariable GetLocalVariable(int index);
	}

	public interface IMethodStack
	{
		bool stackIsEmpty { get; }

		void PushStack(IVariable value);

		IVariable PopStack();

		IVariable PeekStack(int offset);

		IEnumerator<IVariable> PopMultiple(int count);
	}

	public interface IMethodProgram
	{
		Operation currentOp { get; }

		bool Next(bool skipNop = true);

		void PushState();

		void PopState();

		void DropState();
	}
}