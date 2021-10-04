using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Katsudon.Builder.Externs;
using UnityEngine;
using VRC.Udon.Common.Interfaces;

namespace Katsudon.Builder.Extensions.UnityExtensions
{
	[OperationBuilder]
	public class CallMonoBehaviour : IOperationBuider
	{
		public int order => 19;

		private Dictionary<MethodInfo, System.Func<IMethodDescriptor, IUdonMachine, bool>> methods;

		public CallMonoBehaviour()
		{
			methods = new Dictionary<MethodInfo, System.Func<IMethodDescriptor, IUdonMachine, bool>>() {
				{Utils.GetPropertyMethod<Component>(nameof(Component.transform)), GetTransform},
				{Utils.GetPropertyMethod<Component>(nameof(Component.gameObject)), GetGameObject},
				{Utils.GetPropertyMethod<MonoBehaviour>(nameof(MonoBehaviour.enabled)), GetEnabled},
				{Utils.GetPropertyMethod<MonoBehaviour>(nameof(MonoBehaviour.enabled), false), SetEnabled},
				{Utils.GetMethod<MonoBehaviour>(nameof(MonoBehaviour.print), typeof(object)), PrintMessage},
				// TODO: In future
				// {Utils.GetMethod<Component>(nameof(Component.SendMessage), typeof(string)), null},
				// {Utils.GetMethod<Component>(nameof(Component.SendMessageUpwards), typeof(string)), null},
				// {Utils.GetMethod<Component>(nameof(Component.SendMessage), typeof(string), typeof(SendMessageOptions)), null},
				// {Utils.GetMethod<Component>(nameof(Component.SendMessageUpwards), typeof(string), typeof(SendMessageOptions)), null},
			};
		}

		bool IOperationBuider.Process(IMethodDescriptor method)
		{
			var methodInfo = method.currentOp.argument as MethodInfo;
			if(methods.TryGetValue(methodInfo, out var callback))
			{
				return callback(method, method.machine);
			}
			return false;
		}

		private bool GetTransform(IMethodDescriptor method, IUdonMachine udonMachine)
		{
			if(!method.isStatic && !(method.PeekStack(0) is ThisVariable))
			{
				return false;
			}
			method.PopStack().Use();
			method.PushStack(udonMachine.GetThisVariable(UdonThisType.Transform));
			return true;
		}

		private bool GetGameObject(IMethodDescriptor method, IUdonMachine udonMachine)
		{
			if(!method.isStatic && !(method.PeekStack(0) is ThisVariable))
			{
				return false;
			}
			method.PopStack().Use();
			method.PushStack(udonMachine.GetThisVariable(UdonThisType.GameObject));
			return true;
		}

		private bool GetEnabled(IMethodDescriptor method, IUdonMachine udonMachine)
		{
			var target = method.PeekStack(0);
			if(target is ThisVariable || typeof(IUdonEventReceiver).IsAssignableFrom(target.type) || Utils.IsUdonAsm(target.type))
			{
				method.PopStack().Use();
				udonMachine.AddExtern(
					"VRCUdonCommonInterfacesIUdonEventReceiver.__get_enabled__SystemBoolean",
					() => method.GetOrPushOutVariable(typeof(bool)),
					target.OwnType()
				);
				return true;
			}
			return false;
		}

		private bool SetEnabled(IMethodDescriptor method, IUdonMachine udonMachine)
		{
			var target = method.PeekStack(1);
			if(target is ThisVariable || typeof(IUdonEventReceiver).IsAssignableFrom(target.type) || Utils.IsUdonAsm(target.type))
			{
				var value = method.PopStack();
				method.PopStack().Use();
				udonMachine.AddExtern(
					"VRCUdonCommonInterfacesIUdonEventReceiver.__set_enabled__SystemBoolean__SystemVoid",
					target.OwnType(), value.OwnType()
				);
				return true;
			}
			return false;
		}

		private bool PrintMessage(IMethodDescriptor method, IUdonMachine udonMachine)
		{
			var value = method.PopStack();
			udonMachine.DebugLog(value);
			return true;
		}

		public static void Register(IOperationBuildersRegistry container, IModulesContainer modules)
		{
			var instance = new CallMonoBehaviour();
			container.RegisterOpBuilder(OpCodes.Call, instance);
			container.RegisterOpBuilder(OpCodes.Callvirt, instance);
		}
	}
}