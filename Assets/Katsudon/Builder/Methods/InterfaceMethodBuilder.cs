﻿using System.Collections.Generic;
using System.Reflection;
using Katsudon.Builder.Helpers;
using VRC.Udon.VM.Common;

namespace Katsudon.Builder.Methods
{
	public class InterfaceMethodBuilder : ProgramBlock.IMethodBuilder
	{
		int ProgramBlock.IMethodBuilder.order => 100;

		private IReadOnlyDictionary<MethodInfo, MethodInfo> methodsMap;
		private UBehMethodBuilder uBehMethodBuilder;
		private MethodsInstance methodsContainer;

		public InterfaceMethodBuilder(IReadOnlyDictionary<MethodInfo, MethodInfo> methodsMap,
			UBehMethodBuilder uBehMethodBuilder, MethodsInstance methodsContainer)
		{
			this.methodsMap = methodsMap;
			this.uBehMethodBuilder = uBehMethodBuilder;
			this.methodsContainer = methodsContainer;
		}

		public bool BuildMethod(MethodInfo method, UBehMethodInfo uBehMethod, UdonMachine udonMachine, PropertiesBlock properties)
		{
			MethodInfo classMethod;
			if(methodsMap.TryGetValue(method, out classMethod))
			{
				if(classMethod.IsPublic)
				{
					var targetMethod = methodsContainer.GetFamily(classMethod);
					var args = uBehMethod.arguments;
					var classArgs = targetMethod.arguments;
					for(var i = 0; i < args.Length; i++)
					{
						properties.AddVariable(args[i]);
						udonMachine.AddOpcode(OpCode.PUSH, args[i]);
						udonMachine.AddOpcode(OpCode.PUSH, classArgs[i]);
						udonMachine.AddOpcode(OpCode.COPY);
					}

					var retLabel = udonMachine.CreateLabelVariable();
					udonMachine.AddOpcode(OpCode.PUSH, retLabel);
					udonMachine.AddOpcode(OpCode.PUSH, udonMachine.GetReturnAddressGlobal());
					udonMachine.AddOpcode(OpCode.COPY);

					udonMachine.AddOpcode(OpCode.JUMP, targetMethod);

					(retLabel as IEmbedAddressLabel).Apply();
					if(method.ReturnType != typeof(void))
					{
						properties.AddVariable(uBehMethod.ret);
						udonMachine.AddOpcode(OpCode.PUSH, targetMethod.ret);
						udonMachine.AddOpcode(OpCode.PUSH, uBehMethod.ret);
						udonMachine.AddOpcode(OpCode.COPY);
					}

					udonMachine.AddOpcode(OpCode.JUMP, UdonMachine.endProgramAddress);
					return true;
				}
				else
				{
					return uBehMethodBuilder.BuildMethodBody(classMethod, null, uBehMethod, udonMachine, properties);
				}
			}
			return false;
		}
	}
}