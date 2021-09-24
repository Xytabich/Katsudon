using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Katsudon.Builder.Extensions.PropertiesShortcuts
{
	[StaticBuilderModule]
	public class FieldShortcuts
	{
		private Dictionary<MethodInfo, FieldInfo> getters = new Dictionary<MethodInfo, FieldInfo>();
		private Dictionary<MethodInfo, FieldInfo> setters = new Dictionary<MethodInfo, FieldInfo>();

		public FieldInfo GetGetter(MethodInfo method)
		{
			FieldInfo field;
			if(getters.TryGetValue(method, out field)) return field;

			field = TryGetGetterField(method);
			getters[method] = field;
			return field;
		}

		public FieldInfo GetSetter(MethodInfo method)
		{
			FieldInfo field;
			if(setters.TryGetValue(method, out field)) return field;

			field = TryGetSetterField(method);
			setters[method] = field;
			return field;
		}

		private FieldInfo TryGetSetterField(MethodInfo method)
		{
			var parameters = method.GetParameters();
			if(parameters.Length != 1 || method.GetMethodBody().LocalVariables.Count > 0) return null;
			if(parameters[0].ParameterType.IsByRef) return null;

			FieldInfo field = null;
			var reader = new MethodReader(method, null);
			int index;
			bool waitThis = true;
			foreach(var op in reader)
			{
				if(op.opCode == OpCodes.Nop) continue;
				if(ILUtils.TryGetLdarg(op, out index, true))
				{
					if(index != (waitThis ? -1 : 0)) break;
					waitThis = false;
					continue;
				}
				if(op.opCode == OpCodes.Ret) return field;
				if(ILUtils.TryGetStfld(op, out FieldInfo f))
				{
					if(f.IsStatic) break;
					field = f;
					continue;
				}
				break;
			}
			return null;
		}

		private FieldInfo TryGetGetterField(MethodInfo method)
		{
			var parameters = method.GetParameters();
			if(parameters.Length > 0 || method.GetMethodBody().LocalVariables.Count > 1) return null;

			FieldInfo field = null;
			var reader = new MethodReader(method, null);
			bool waitLdLoc = false;
			int branch = -1;
			int index;
			foreach(var op in reader)
			{
				if(branch >= 0)
				{
					if(op.offset != branch) break;
					branch = -1;
				}
				if(op.opCode == OpCodes.Nop) continue;
				if((op.opCode == OpCodes.Br || op.opCode == OpCodes.Br_S))
				{
					branch = Convert.ToInt32(op.argument);
					continue;
				}
				if(ILUtils.TryGetLdarg(op, out index, true)) continue;
				if(waitLdLoc)
				{
					if(!ILUtils.TryGetLdloc(op, out index)) break;
					waitLdLoc = false;
				}
				else
				{
					if(op.opCode == OpCodes.Ret) return field;
					if(ILUtils.TryGetLdfld(op, out FieldInfo f))
					{
						if(f.IsStatic) break;
						field = f;
						continue;
					}
					if(ILUtils.TryGetStloc(op, out index))
					{
						waitLdLoc = true;
						continue;
					}
					break;
				}
			}
			return null;
		}

		public static void Register(IModulesContainer modules)
		{
			modules.AddModule(new FieldShortcuts());
		}
	}
}