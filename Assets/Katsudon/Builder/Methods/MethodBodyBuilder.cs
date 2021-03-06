using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using Katsudon.Builder.Helpers;
using Katsudon.Builder.Methods;
using Katsudon.Meta;

namespace Katsudon.Builder
{
	public class MethodBodyBuilder : IComparer<IOperationBuider>, IOperationBuildersRegistry
	{
		private SortedSet<IOperationBuider>[] builders = new SortedSet<IOperationBuider>[384];
		private HashSet<MethodIdentifier> inBuild = new HashSet<MethodIdentifier>();

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

		public void BuildBehaviour(MethodInfo method, IList<IVariable> arguments, IVariable returnVariable,
			IAddressLabel returnAddress, IUdonProgramBlock machineBlock, PropertiesBlock properties)
		{
			var locals = method.GetMethodBody().LocalVariables;
			var localVars = CollectionCache.GetList<IVariable>();
			for(var i = 0; i < locals.Count; i++)
			{
				localVars.Add(new UnnamedVariable("loc", locals[i].LocalType));
			}

			Build(method, true, arguments, localVars, returnVariable, returnAddress, machineBlock);

			foreach(var variable in localVars)
			{
				properties.AddVariable(variable);
			}
			CollectionCache.Release(localVars);
		}

		public void Build(MethodInfo method, bool isBehaviour, IList<IVariable> arguments, IList<IVariable> locals,
			IVariable returnVariable, IAddressLabel returnAddress, IUdonProgramBlock machineBlock)
		{
			var methodDescriptor = new MethodDescriptor(method.IsStatic, isBehaviour, arguments, locals, returnVariable, returnAddress);
			Build(method, methodDescriptor, machineBlock);
		}

		public void Build(MethodBase method, MethodDescriptor methodDescriptor, IUdonProgramBlock machineBlock)
		{
			var id = new MethodIdentifier(UdonCacheHelper.cache, method);
			if(!inBuild.Add(id)) throw new Exception(string.Format("Recursion detected: method {0}:{1} is already in the build process", method.DeclaringType, method));

			var operations = CollectionCache.GetList<Operation>();
			foreach(var op in new MethodReader(method, null))
			{
				operations.Add(op);
			}

			var addressPointers = new List<UdonAddressPointer>();
			addressPointers.Add(new UdonAddressPointer(machineBlock.machine.GetAddressCounter(), 0));
			methodDescriptor.Init(operations, machineBlock, addressPointers);
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
					catch(Exception e)
					{
						throw new IlOffsetInfoException(methodDescriptor.currentOp.offset, e);
					}
				}
#if KATSUDON_DEBUG
				methodDescriptor.CheckState();
#endif
				methodDescriptor.ApplyProperties();

				if(BuildMeta(method, addressPointers, machineBlock.machine.GetAddressCounter(), out var meta))
				{
					machineBlock.machine.AddMethodMeta(meta);
				}
			}
			catch(Exception e)
			{
				if(!(e is IlOffsetInfoException ilOffset))
				{
					throw new Exception(string.Format("Exception during building method {0}:{1}", method.DeclaringType, method), e);
				}

				KatsudonBuildException exception = null;
				var assembly = method.DeclaringType.Assembly;
				if(!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
				{
					using(var reader = new FileMetaReader())
					{
						if(reader.GetSourceLineInfo(assembly.Location, method.MetadataToken, methodDescriptor.currentOp.offset, out var source, out var line, out _))
						{
							if(!string.IsNullOrEmpty(source) && !source.StartsWith("<"))
							{
								exception = new KatsudonBuildException(method, source, line, ilOffset.ilOffset, e.InnerException);
							}
						}
					}
				}
				if(exception == null) exception = new KatsudonBuildException(method, ilOffset.ilOffset, e.InnerException);
#if KATSUDON_DEBUG
				UnityEngine.Debug.LogException(e.InnerException);
#endif
				throw exception;
			}
			finally
			{
				methodDescriptor.Dispose();
				CollectionCache.Release(operations);
				inBuild.Remove(id);
			}
		}

		int IComparer<IOperationBuider>.Compare(IOperationBuider x, IOperationBuider y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}

		private static bool BuildMeta(MethodBase method, List<UdonAddressPointer> addressPointers, uint endAddress, out UdonMethodMeta meta)
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

		[System.Serializable]
		private class KatsudonBuildException : Exception
		{
			public KatsudonBuildException(MethodBase method, int ilOffset, Exception innerException) :
				base(BuildMessage(method, ilOffset, innerException))
			{ }

			public KatsudonBuildException(MethodBase method, string source, int line, int ilOffset, Exception innerException) :
				base(BuildMessage(method, source, line, ilOffset, innerException))
			{ }

			protected KatsudonBuildException(SerializationInfo info, StreamingContext context) : base(info, context) { }

			private static string BuildMessage(MethodBase method, int ilOffset, Exception innerException)
			{
				var sb = new StringBuilder();
				sb.AppendLine(innerException.Message);
				sb.AppendFormat("(at {0}:{1}) IL_{2:x8}", method.DeclaringType, method, ilOffset);
				sb.AppendLine();
				return sb.ToString();
			}

			private static string BuildMessage(MethodBase method, string source, int line, int ilOffset, Exception innerException)
			{
				var sb = new StringBuilder();
				sb.AppendLine(innerException.Message);
				sb.AppendFormat("{0} (at {1}:{2}) IL_{3:x8}", method, source, line, ilOffset);
				sb.AppendLine();
				return sb.ToString();
			}
		}

		private class IlOffsetInfoException : Exception
		{
			public readonly int ilOffset;

			public IlOffsetInfoException(int ilOffset, Exception innerException) : base(null, innerException)
			{
				this.ilOffset = ilOffset;
			}
		}
	}

	public interface IMethodDescriptor : IMethodProgram, IMethodStack, IMethodVariables, IUdonProgramBlock
	{
		bool isStatic { get; }

		bool isBehaviour { get; }

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

		/// <summary>
		/// Pushes the variables in the current stack to the udon stack, and then merges the results of the branches at the given IL position
		/// </summary>
		IDisposable StoreBranchingStack(int loadIlOffset, bool clearStack = false);

		/// <param name="isVolatile">Set to true if the variable can be changed outside of the method context</param>
		void PushStack(IVariable value, bool isVolatile = false);

		IVariable PopStack();

		IVariable PeekStack(int offset);

		IEnumerator<IVariable> PopMultiple(int count);
	}
}