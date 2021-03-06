using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.AsmOpCodes;
using Katsudon.Builder.Externs;
using Katsudon.Info;
using Katsudon.Utility;
using UnityEngine;
using VRC.Udon;

namespace Katsudon.Builder.Extensions.UnityExtensions
{
	[OperationBuilder]
	public class BroadcastMessageCall : IOperationBuider
	{
		public int order => 15;

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = (MethodInfo)method.currentOp.argument;
			if(methodInfo.Name == nameof(GameObject.BroadcastMessage) && (methodInfo.DeclaringType == typeof(Component) ||
				methodInfo.DeclaringType == typeof(GameObject)))
			{
				/*
				var behaviours = GetComponentsInChildren(typeof(UdonBehaviour));
				for(int i = 0; i < behaviours.Length; i++)
				{
					if(!behaviours[i].enabled) continue;
					behaviours[i].SendCustomEvent(eventName);
				}
				*/
				var parameters = methodInfo.GetParameters();
				IVariable parameterValue = null;
				if(parameters.Length > 1)
				{
					if(parameters.Length == 3 || parameters[1].ParameterType != typeof(object))
					{
						method.PopStack().Use();
					}
					if(parameters[1].ParameterType == typeof(object))
					{
						parameterValue = method.PopStack();
					}
				}

				var methodName = method.PopStack();
				var target = method.PopStack();

				IVariable parameterName = null;
				if(parameterValue != null)
				{
					parameterName = method.GetTmpVariable(typeof(string));
					method.machine.AddExtern("SystemString.__Concat__SystemString_SystemString__SystemString",
						parameterName,
						methodName.OwnType(),
						method.machine.GetConstVariable(string.Format(AsmMethodInfo.PARAMETER_FORMAT, "", 0)).OwnType()
					);
				}

				var behaviours = method.GetTmpVariable(typeof(Component[])).Reserve();
				method.machine.AddExtern(
					CallGetComponents.GetExternName(methodInfo.DeclaringType == typeof(GameObject), true, nameof(GameObject.GetComponentsInChildren)),
					behaviours,
					target.OwnType(),
					method.machine.GetConstVariable(typeof(UdonBehaviour)).OwnType()
				);
				using(var loop = ForLoop.Array(method, behaviours, out var index))
				{
					var behaviour = method.GetTmpVariable(typeof(UdonBehaviour)).Reserve();
					method.machine.AddExtern("UnityEngineComponentArray.__Get__SystemInt32__UnityEngineComponent", behaviour, behaviours.OwnType(), index.OwnType());

					var condition = method.GetTmpVariable(typeof(bool));
					method.machine.AddExtern("VRCUdonCommonInterfacesIUdonEventReceiver.__get_enabled__SystemBoolean", condition, behaviour.OwnType());
					method.machine.AddBranch(condition, loop.continueLabel);

					if(parameterName != null) method.machine.SetVariableExtern(behaviour, parameterName, parameterValue);
					method.machine.SendEventExtern(behaviour, methodName);

					behaviour.Release();
				}
				behaviours.Release();
				return true;
			}
			return false;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var builder = new BroadcastMessageCall();
			container.RegisterOpBuilder(OpCodes.Call, builder);
			container.RegisterOpBuilder(OpCodes.Callvirt, builder);
		}
	}
}