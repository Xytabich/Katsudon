using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Builder.Helpers;
using Katsudon.Info;

namespace Katsudon.Members
{
	[MemberHandler]
	public class MagicMethods : IMemberHandler
	{
		private Dictionary<string, Info> methods = new Dictionary<string, Info>() {
			{"Awake", new Info("Event Awake is not supported in udon, use Start instead")},
			{"FixedUpdate", new Info("Event {0} is not supported in udon")},
			{"LateUpdate", new Info("Event {0} is not supported in udon")},
			{"OnAnimatorIK", new Info("Event {0} is not supported in udon")},
			{"OnAnimatorMove", new Info("Event {0} is not supported in udon")},
			{"OnApplicationFocus", new Info("Event {0} is not supported in udon")},
			{"OnApplicationPause", new Info("Event {0} is not supported in udon")},
			{"OnApplicationQuit", new Info("Event {0} is not supported in udon")},
			{"OnAudioFilterRead", new Info("Event {0} is not supported in udon")},
			{"OnBecameInvisible", new Info("Event {0} is not supported in udon")},
			{"OnBecameVisible", new Info("Event {0} is not supported in udon")},
			{"OnCollisionEnter", new Info("Event {0} is not supported in udon")},
			{"OnCollisionEnter2D", new Info("Event {0} is not supported in udon")},
			{"OnCollisionExit", new Info("Event {0} is not supported in udon")},
			{"OnCollisionExit2D", new Info("Event {0} is not supported in udon")},
			{"OnCollisionStay", new Info("Event {0} is not supported in udon")},
			{"OnCollisionStay2D", new Info("Event {0} is not supported in udon")},
			{"OnConnectedToServer", new Info("Event {0} is not supported in udon")},
			{"OnControllerColliderHit", new Info("Event {0} is not supported in udon")},
			{"OnDestroy", new Info("Event {0} is not supported in udon")},
			{"OnDisable", new Info("Event {0} is not supported in udon")},
			{"OnDisconnectedFromServer", new Info("Event {0} is not supported in udon")},
			{"OnDrawGizmos", default},
			{"OnDrawGizmosSelected", default},
			{"OnEnable", new Info("Event {0} is not supported in udon")},
			{"OnFailedToConnect", new Info("Event {0} is not supported in udon")},
			{"OnFailedToConnectToMasterServer", new Info("Event {0} is not supported in udon")},
			{"OnGUI", new Info("Event {0} is not supported in udon")},
			{"OnJointBreak", new Info("Event {0} is not supported in udon")},
			{"OnJointBreak2D", new Info("Event {0} is not supported in udon")},
			{"OnLevelWasLoaded", new Info("Event {0} is not supported in udon")},
			{"OnMasterServerEvent", new Info("Event {0} is not supported in udon")},
			{"OnMouseDown", new Info("Event {0} is not supported in udon")},
			{"OnMouseDrag", new Info("Event {0} is not supported in udon")},
			{"OnMouseEnter", new Info("Event {0} is not supported in udon")},
			{"OnMouseExit", new Info("Event {0} is not supported in udon")},
			{"OnMouseOver", new Info("Event {0} is not supported in udon")},
			{"OnMouseUp", new Info("Event {0} is not supported in udon")},
			{"OnMouseUpAsButton", new Info("Event {0} is not supported in udon")},
			{"OnNetworkInstantiate", new Info("Event {0} is not supported in udon")},
			{"OnParticleCollision", new Info("Event {0} is not supported in udon")},
			{"OnPlayerConnected", new Info("Event {0} is not supported in udon")},
			{"OnPlayerDisconnected", new Info("Event {0} is not supported in udon")},
			{"OnPostRender", new Info("Event {0} is not supported in udon")},
			{"OnPreCull", new Info("Event {0} is not supported in udon")},
			{"OnPreRender", new Info("Event {0} is not supported in udon")},
			{"OnRenderImage", new Info("Event {0} is not supported in udon")},
			{"OnRenderObject", new Info("Event {0} is not supported in udon")},
			{"OnSerializeNetworkView", new Info("Event {0} is not supported in udon")},
			{"OnServerInitialized", new Info("Event {0} is not supported in udon")},
			{"OnTransformChildrenChanged", new Info("Event {0} is not supported in udon")},
			{"OnTransformParentChanged", new Info("Event {0} is not supported in udon")},
			{"OnTriggerEnter", new Info("Event {0} is not supported in udon")},
			{"OnTriggerEnter2D", new Info("Event {0} is not supported in udon")},
			{"OnTriggerExit", new Info("Event {0} is not supported in udon")},
			{"OnTriggerExit2D", new Info("Event {0} is not supported in udon")},
			{"OnTriggerStay", new Info("Event {0} is not supported in udon")},
			{"OnTriggerStay2D", new Info("Event {0} is not supported in udon")},
			{"OnValidate", new Info("Event {0} is not supported in udon")},
			{"OnWillRenderObject", new Info("Event {0} is not supported in udon")},
			{"Reset", new Info("Event {0} is not supported in udon")},
			{"Start", new Info("Event {0} is not supported in udon")},
			{"Update", new Info("Event {0} is not supported in udon")}
		};

		private HashSet<string> magicMethodNames = new HashSet<string>();

		private bool initComplete = false;

		public void SetMethod(string name, string udonName, Type retType, KeyValuePair<string, Type>[] parameters)
		{
			methods[name] = new Info(udonName, retType, parameters);
		}

		int IMemberHandler.order => 15;

		bool IMemberHandler.Process(MemberInfo member, AssembliesInfo assemblies, AsmTypeInfo typeInfo)
		{
			var method = member as MethodInfo;
			if(method.IsStatic) return false;

			if(!initComplete) InitMethods();

			if(methods.TryGetValue(method.Name, out var info))
			{
				switch(info.mode)
				{
					case Info.Mode.Supported:
						if(method.ReturnType == info.retType)
						{
							var parameters = method.GetParameters();
							if(parameters.Length == info.parameters.Length)
							{
								for(var i = 0; i < parameters.Length; i++)
								{
									if(info.parameters[i].Value != parameters[i].ParameterType)
									{
										throw new System.Exception(string.Format("Event {0} has invalid parameters type", method.Name));
									}
								}

								var args = new string[parameters.Length];
								for(var i = 0; i < args.Length; i++)
								{
									args[i] = info.parameters[i].Key;
								}
								typeInfo.AddMethod(new AsmMethodInfo(AsmMethodInfo.Flags.Export | AsmMethodInfo.Flags.Unique, info.info, args, null, method));
								return true;
							}
						}
						throw new System.Exception(string.Format("Event {0} has invalid structure", method.Name));

					case Info.Mode.NotSupported: throw new System.Exception(string.Format(info.info, method.Name));

					default: return true;
				}
			}
			else if(magicMethodNames.Contains(method.Name))
			{
				UnityEngine.Debug.LogWarningFormat("Method {0} declared in {1} uses a reserved name which may cause incorrect behavior", method, method.DeclaringType);
			}
			return false;
		}

		private void InitMethods()
		{
			foreach(var pair in UdonCacheHelper.cache.GetMagicMethods())
			{
				methods[pair.Key] = new Info(pair.Value.udonName, pair.Value.returnType, pair.Value.parameters);
				magicMethodNames.Add(pair.Value.udonName);
			}
			initComplete = true;
		}

		public static void Register(IMemberHandlersRegistry registry)
		{
			registry.RegisterHandler(MemberTypes.Method, new MagicMethods());
		}

		private struct Info
		{
			public Mode mode;
			public string info;
			public Type retType;
			public KeyValuePair<string, Type>[] parameters;

			public Info(string udonName, Type retType, KeyValuePair<string, Type>[] parameters)
			{
				this.info = udonName;
				this.retType = retType;
				this.parameters = parameters;
				this.mode = Mode.Supported;
			}

			public Info(string message)
			{
				this.info = message;
				this.mode = Mode.NotSupported;
				this.retType = null;
				this.parameters = null;
			}

			public enum Mode
			{
				Ignore = 0,
				Supported,
				NotSupported
			}
		}
	}
}