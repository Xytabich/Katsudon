using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Ctor
{
	public class CtorDefaultsExtractor
	{
		private static MethodInfo storeFieldMethod = typeof(BuildHelper).GetMethod("StoreField");
		private static MethodInfo handleToField = typeof(FieldInfo).GetMethod("GetFieldFromHandle", new Type[] { typeof(RuntimeFieldHandle) });

		public void ExtractDefaults(ConstructorInfo ctor, FieldsCollection fields)
		{
			var type = ctor.DeclaringType;
			var helper = new BuildHelper(fields);
			var body = ctor.GetMethodBody();
			var tmpMethod = new DynamicMethod("VariablesInit", typeof(void), new Type[] { typeof(BuildHelper) });
			var builder = tmpMethod.GetILGenerator();

			var localsInfo = body.LocalVariables;
			for(int i = 0; i < localsInfo.Count; i++)
			{
				var info = localsInfo[i];
				builder.DeclareLocal(info.LocalType == type ? typeof(BuildHelper) : info.LocalType, info.IsPinned);
			}

			var reader = new MethodReader(ctor, helper);
			var labels = new Dictionary<int, LabelInfo>();
			var flushStack = new Stack<int>();
			int flushIndex = -1;
			bool partialBuild = false;
			var remainingOperations = new Dictionary<int, Operation>();
			int maxJump = 0;
			foreach(var operation in reader)
			{
				if(partialBuild)
				{
					remainingOperations.Add(operation.offset, operation);
				}
				else
				{
					ProcessResult result = ProcessOperation(operation, labels, builder, type, ref maxJump);
					if(result == ProcessResult.Stop)
					{
						if(maxJump > flushIndex)
						{
							partialBuild = true;
							remainingOperations.Add(operation.offset, operation);
						}
						else break;
					}
					else if(result == ProcessResult.Flush)
					{
						flushIndex = operation.offset;
						flushStack.Push(operation.offset);
					}
				}
			}
			if(flushStack.Count < 1) return;

			var labelsList = new List<int>(labels.Keys);
			labelsList.Sort();
			if(partialBuild)
			{
				flushIndex = flushStack.Peek();
				for(int i = labelsList.Count - 1; i >= 0; i--)
				{
					int index = labelsList[i];
					if(index <= flushIndex) continue;
					var labelInfo = labels[index];
					if(labelInfo.isReturn || labelInfo.offset > flushIndex) continue;
					if(IsReturnAtEnd(index, remainingOperations))
					{
						labels[index] = new LabelInfo(labelInfo, true);
					}
					else
					{
						do flushStack.Pop();
						while(flushStack.Count > 0 && flushStack.Peek() > labelInfo.offset);

						if(flushStack.Count < 1) break;

						flushIndex = flushStack.Peek();
						i = labelsList.Count;
					}
				}
			}
			if(flushStack.Count < 1) return;

			int labelIndex = 0;
			int labelValue = labelIndex < labelsList.Count ? labelsList[labelIndex] : -1;
			int lastIndex = flushStack.Peek();

			var flushOffsets = new HashSet<int>(flushStack);

			Label? retLabel = null;
			foreach(var operation in reader)
			{
				if(operation.offset > lastIndex) break;
				if(operation.offset == labelValue)
				{
					builder.MarkLabel(labels[labelsList[labelIndex]].label);
					labelIndex++;
					labelValue = labelIndex < labelsList.Count ? labelsList[labelIndex] : -1;
				}
				switch(operation.opCode.OperandType)
				{
					case OperandType.InlineSwitch:
						{
							var targets = (int[])operation.argument;
							var switchLabels = new Label[targets.Length];
							for(int i = 0; i < targets.Length; i++)
							{
								var info = labels[targets[i]];
								if(info.isReturn)
								{
									if(!retLabel.HasValue) retLabel = builder.DefineLabel();
									switchLabels[i] = retLabel.Value;
								}
								else
								{
									switchLabels[i] = info.label;
								}
							}
							builder.Emit(operation.opCode, switchLabels);
						}
						break;
					case OperandType.InlineBrTarget:
					case OperandType.ShortInlineBrTarget:
						{
							var info = labels[(int)operation.argument];
							Label target;
							if(info.isReturn)
							{
								if(!retLabel.HasValue) retLabel = builder.DefineLabel();
								target = retLabel.Value;
							}
							else target = info.label;
							var opcode = operation.opCode;
							if(opcode.OperandType == OperandType.ShortInlineBrTarget)
							{
								if(opcode == OpCodes.Br_S) opcode = OpCodes.Br;
								else if(opcode == OpCodes.Brtrue_S) opcode = OpCodes.Brtrue;
								else if(opcode == OpCodes.Brfalse_S) opcode = OpCodes.Brfalse;
							}
							builder.Emit(opcode, target);
						}
						break;
					case OperandType.ShortInlineI:
						if(operation.opCode == OpCodes.Ldc_I4_S)
						{
							builder.Emit(operation.opCode, (sbyte)operation.argument);
						}
						else
						{
							builder.Emit(operation.opCode, (byte)operation.argument);
						}
						break;
					case OperandType.InlineI:
						builder.Emit(operation.opCode, (int)operation.argument);
						break;
					case OperandType.ShortInlineR:
						builder.Emit(operation.opCode, (float)operation.argument);
						break;
					case OperandType.InlineR:
						builder.Emit(operation.opCode, (double)operation.argument);
						break;
					case OperandType.InlineI8:
						builder.Emit(operation.opCode, (long)operation.argument);
						break;
					case OperandType.InlineString:
						builder.Emit(operation.opCode, (string)operation.argument);
						break;
					case OperandType.InlineTok:
						if(operation.argument is Type) builder.Emit(operation.opCode, (Type)operation.argument);
						else if(operation.argument is FieldInfo)
						{
							if(flushStack.Contains(operation.offset))
							{
								StoreFieldOp((FieldInfo)operation.argument, builder);
							}
							else
							{
								builder.Emit(operation.opCode, (FieldInfo)operation.argument);
							}
						}
						else if(operation.argument is MethodInfo) builder.Emit(operation.opCode, (MethodInfo)operation.argument);
						else if(operation.argument is ConstructorInfo) builder.Emit(operation.opCode, (ConstructorInfo)operation.argument);
						break;
					case OperandType.InlineField:
						if(flushStack.Contains(operation.offset))
						{
							StoreFieldOp((FieldInfo)operation.argument, builder);
						}
						else
						{
							builder.Emit(operation.opCode, (FieldInfo)operation.argument);
						}
						break;
					case OperandType.InlineType:
						builder.Emit(operation.opCode, (Type)operation.argument);
						break;
					case OperandType.InlineMethod:
						if(operation.argument is MethodInfo) builder.Emit(operation.opCode, (MethodInfo)operation.argument);
						else if(operation.argument is ConstructorInfo) builder.Emit(operation.opCode, (ConstructorInfo)operation.argument);
						break;
					case OperandType.InlineVar:
						builder.Emit(operation.opCode, (short)operation.rawArgument);
						break;
					case OperandType.ShortInlineVar:
						builder.Emit(operation.opCode, (byte)operation.rawArgument);
						break;
					case OperandType.InlineNone:
						builder.Emit(operation.opCode);
						break;
					default:
						throw new NotSupportedException(operation.opCode.Name);
				}
			}
			if(retLabel.HasValue) builder.MarkLabel(retLabel.Value);
			builder.Emit(OpCodes.Ret);
			tmpMethod.Invoke(null, new object[] { helper });
		}

		private static void StoreFieldOp(FieldInfo field, ILGenerator builder)
		{
			builder.Emit(OpCodes.Ldtoken, field);
			builder.Emit(OpCodes.Call, handleToField);
			builder.EmitCall(OpCodes.Callvirt, storeFieldMethod.MakeGenericMethod(field.FieldType), null);//FIX: cache generic method
		}

		private static bool IsReturnAtEnd(int index, Dictionary<int, Operation> operations)
		{
			var operation = operations[index];
			if(operation.opCode == OpCodes.Ret) return true;
			while(operation.opCode == OpCodes.Br || operation.opCode == OpCodes.Br_S)
			{
				operation = operations[(int)operation.argument];
				if(operation.opCode == OpCodes.Ret) return true;
			}
			return false;
		}

		private static ProcessResult ProcessOperation(Operation operation, Dictionary<int, LabelInfo> labels, ILGenerator builder, Type type, ref int maxJump)
		{
			var result = ProcessResult.None;
			int position = operation.offset;
			switch(operation.opCode.OperandType)
			{
				case OperandType.InlineSwitch:
					foreach(int index in (int[])operation.argument)
					{
						if(index < position)
						{
							return ProcessResult.Stop;
						}
						maxJump = Math.Max(index, maxJump);
						SetLabelInfo(position, index, labels, builder);
					}
					break;
				case OperandType.InlineBrTarget:
				case OperandType.ShortInlineBrTarget:
					{
						int index = (int)operation.argument;
						if(index < position)
						{
							return ProcessResult.Stop;
						}
						maxJump = Math.Max(index, maxJump);
						SetLabelInfo(position, index, labels, builder);
					}
					break;
				case OperandType.InlineSig:
					return ProcessResult.Stop;
				case OperandType.InlineTok:
				case OperandType.InlineField:
				case OperandType.InlineMethod:
					if(operation.opCode == OpCodes.Stsfld ||
						operation.opCode == OpCodes.Ldfld ||
						operation.opCode == OpCodes.Ldflda)
					{
						return ProcessResult.Stop;
					}
					else if(operation.opCode == OpCodes.Call || operation.opCode == OpCodes.Callvirt || operation.opCode == OpCodes.Newobj)
					{
						var dtype = ((MethodBase)operation.argument).DeclaringType;
						if(dtype.IsAssignableFrom(type))
						{
							return ProcessResult.Stop;
						}
					}
					else if(operation.opCode == OpCodes.Stfld)
					{
						var dtype = ((FieldInfo)operation.argument).DeclaringType;
						if(dtype == type)
						{
							result = ProcessResult.Flush;
						}
						else if(dtype.IsAssignableFrom(type))
						{
							return ProcessResult.Stop;
						}
					}
					break;
			}
			return result;
		}

		private static void SetLabelInfo(int position, int target, Dictionary<int, LabelInfo> labels, ILGenerator builder)
		{
			if(labels.TryGetValue(target, out var info))
			{
				if(info.offset > position)
				{
					labels[target] = new LabelInfo(info, position);
				}
			}
			else
			{
				labels.Add(target, new LabelInfo(builder.DefineLabel(), position));
			}
		}

		private enum ProcessResult
		{
			None,
			Stop,
			Flush
		}

		private struct LabelInfo
		{
			public readonly Label label;
			public readonly int offset;
			public readonly bool isReturn;

			public LabelInfo(Label label, int offset)
			{
				this.label = label;
				this.offset = offset;
				this.isReturn = false;
			}

			public LabelInfo(LabelInfo info, int offset)
			{
				this.label = info.label;
				this.offset = offset;
				this.isReturn = info.isReturn;
			}

			public LabelInfo(LabelInfo info, bool isReturn)
			{
				this.label = info.label;
				this.offset = info.offset;
				this.isReturn = isReturn;
			}
		}

		private class BuildHelper
		{
			private FieldsCollection fields;

			public BuildHelper(FieldsCollection fields)
			{
				this.fields = fields;
			}

			public void StoreField<T>(T value, FieldInfo field)
			{
				fields.GetField(field).SetValue(value);
			}
		}
	}
}