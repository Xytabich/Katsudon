using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using UnityEditor;
using System.Reflection.Emit;
using UnityEngine;
using UnityEditor.Compilation;
using Katsudon.Builder;

using AssemblyBuilder = UnityEditor.Compilation.AssemblyBuilder;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Katsudon.Editor
{
	public class ReadClassWindow : EditorWindow
	{
		private List<Type> types;
		private TextElement text;

		[MenuItem("Tools/BuildTemp")]
		private static void BuildTemp()
		{
			var path = EditorUtility.OpenFolderPanel("Select folder", "", "");
			if(!string.IsNullOrEmpty(path))
			{
				path = "Assets/" + path.Replace(Application.dataPath, "").Trim('/');
				var assets = AssetDatabase.FindAssets("t:monoscript", new string[] { path });
				List<string> files = new List<string>(assets.Length);
				for(int i = assets.Length - 1; i >= 0; i--)
				{
					assets[i] = AssetDatabase.GUIDToAssetPath(assets[i]);
					if(!string.IsNullOrEmpty(assets[i])) files.Add(assets[i]);
				}
				AssemblyBuilder builder = new AssemblyBuilder("Temp/TmpBuild.dll", files.ToArray());
				builder.buildTarget = BuildTarget.StandaloneWindows64;
				builder.buildTargetGroup = BuildTargetGroup.Standalone;
				builder.flags = AssemblyBuilderFlags.None;
				builder.buildStarted += (s) => Debug.LogFormat("Assembly build started for {0}", s);
				builder.buildFinished += (s, compilerMessages) => {
					Debug.LogFormat("Assembly build finished for {0}", s);
				};
				if(!builder.Build())
				{
					Debug.LogErrorFormat("Failed to start build of assembly {0}!", builder.assemblyPath);
				}
			}
		}

		[MenuItem("Tools/ReadClass")]
		private static void ShowWindow()
		{
			GetWindow<ReadClassWindow>("ReadClass");
		}

		void OnEnable()
		{
			var root = this.rootVisualElement;
			root.Clear();

			var asmAttrib = typeof(UdonAsmAttribute);
			types = new List<Type>(AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.IsDefined(asmAttrib))
				.SelectMany(a => a.GetTypes()).Where(t => t.IsClass));
			types.Insert(0, typeof(void));
			types.TrimExcess();

			var list = new PopupField<Type>(types, typeof(void));
			list.RegisterValueChangedCallback(OnListChanged);
			root.Add(list);

			text = new TextElement();
			var scroll = new ScrollView();
			scroll.style.flexGrow = 1f;
			scroll.Add(text);
			root.Add(scroll);
		}

		private void OnListChanged(ChangeEvent<Type> evt)
		{
			BuildTypeStruct(evt.newValue);
		}

		private void BuildTypeStruct(Type type)
		{
			var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

			StringBuilder sb = new StringBuilder();
			var fields = type.GetFields(flags | BindingFlags.Static);

			HashSet<FieldInfo> ignoreFields = new HashSet<FieldInfo>();
			HashSet<MethodInfo> ignoreMethods = new HashSet<MethodInfo>();

			/*var replaces = BuildProperties(type, flags);
			foreach(var replace in replaces)
			{
				ignoreMethods.Add(replace.Key);
			}*/

			FieldInfo field;
			for(int i = 0; i < fields.Length; i++)
			{
				field = fields[i];
				if(ignoreFields.Contains(field)) continue;

				if(AppendAttribs(field, sb)) sb.Append('\n');

				if(field.IsPublic) sb.Append("public ");
				else sb.Append("private ");

				if(field.IsLiteral) sb.Append("const ");
				else
				{
					if(field.IsStatic) sb.Append("static ");
					if(field.IsInitOnly) sb.Append("readonly ");
					else if(field.IsStatic) sb.Append("(not supported) ");
				}

				sb.Append(field.FieldType);
				sb.Append(' ');

				sb.Append(field.Name);
				sb.Append(';');
				sb.AppendLine();
				sb.AppendLine();
			}

			var methods = ((MethodBase[])type.GetConstructors(flags)).Union(type.GetMethods(flags)).ToArray();
			for(int i = 0; i < methods.Length; i++)
			{
				var method = methods[i];
				if(ignoreMethods.Contains(method) || method.IsAbstract) continue;

				AppendMethod(method, sb);

				sb.AppendLine();
				sb.AppendLine();
			}

			text.text = sb.ToString();
		}

		public static void AppendMethod(MethodBase method, StringBuilder sb)
		{
			if(AppendAttribs(method, sb)) sb.AppendLine();

			sb.Append('[');
			sb.Append(method.Attributes);
			sb.Append(']');
			sb.AppendLine();

			if(method.IsPublic) sb.Append("public ");
			else sb.Append("private ");

			if(method is MethodInfo mi)
			{
				sb.Append(mi.ReturnType);
				sb.Append(' ');
			}

			sb.Append(method.DeclaringType);
			sb.Append(':');

			sb.Append(method.Name);
			sb.Append('(');
			bool isNext = false;
			foreach(var par in method.GetParameters())
			{
				if(isNext)
				{
					sb.Append(',');
					sb.Append(' ');
				}
				if(AppendAttribs(par, sb)) sb.Append(' ');
				sb.Append(par.ParameterType);
				sb.Append(' ');
				sb.Append(par.Name);
				isNext = true;
			}
			sb.Append(')');

			if(method.IsAbstract)
			{
				sb.Append(';');
			}
			else
			{
				sb.Append("\n{");
				var body = method.GetMethodBody();
				var locals = body.LocalVariables;
				if(locals.Count > 0)
				{
					foreach(var variable in body.LocalVariables)
					{
						sb.AppendLine();
						sb.Append('\t');
						sb.Append(variable.LocalType);
						sb.Append(" var_");
						sb.Append(variable.LocalIndex);
					}
					sb.AppendLine();
				}

				foreach(var op in new MethodReader(method, method.DeclaringType))
				{
					sb.AppendLine();
					sb.Append('\t');
					OpToString(op.offset, op.opCode, op.argument, sb);
				}

				sb.Append("\n}");
			}
		}

		private static bool AppendAttribs(ICustomAttributeProvider provider, StringBuilder sb)
		{
			var firstAttrib = true;
			foreach(var attr in provider.GetCustomAttributes(true))
			{
				if(firstAttrib)
				{
					sb.Append('[');
					firstAttrib = false;
				}
				else
				{
					sb.Append(',');
					sb.Append(' ');
				}
				sb.Append(attr.GetType());
			}
			if(!firstAttrib) sb.Append(']');
			return !firstAttrib;
		}

		private static void OpToString(int offset, OpCode opCode, object operand, StringBuilder sb)
		{
			AppendLabel(offset, sb);
			sb.Append(':');
			sb.Append(' ');
			sb.Append(opCode.Name);

			if(operand == null) return;

			sb.Append(' ');
			switch(opCode.OperandType)
			{
				case OperandType.ShortInlineBrTarget:
				case OperandType.InlineBrTarget:
					AppendLabel(Convert.ToInt32(operand), sb);
					break;
				case OperandType.InlineSwitch:
					var offsets = (int[])operand;
					for(int i = 0; i < offsets.Length; i++)
					{
						if(i > 0) sb.Append(',');
						AppendLabel(offsets[i], sb);
					}
					break;
				case OperandType.InlineString:
					sb.Append('\"');
					sb.Append(operand);
					sb.Append('\"');
					break;
				case OperandType.InlineTok:
					sb.Append(operand.GetType());
					sb.Append(':');
					sb.Append(operand);
					break;
				case OperandType.InlineMethod:
					{
						var info = operand as MethodBase;

						sb.Append(info.DeclaringType);
						sb.Append(':');

						sb.Append(info);
					}
					break;
				default:
					sb.Append('{');
					sb.Append(operand.GetType().Name);
					sb.Append('}');
					sb.Append(operand);
					break;
			}
		}

		private static void AppendLabel(int offset, StringBuilder sb)
		{
			sb.Append("IL_");
			sb.Append(offset.ToString("x4"));
		}
	}
}