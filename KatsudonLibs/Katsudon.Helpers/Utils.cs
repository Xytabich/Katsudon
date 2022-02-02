using System;
using System.Text;

namespace Katsudon.Builder.Helpers
{
	internal static class Utils
	{
		public static void AppendCtor(this MagicMethodInfo self, StringBuilder sb)
		{
			sb.Append("new MagicMethodInfo(\"");
			sb.Append(self.udonName);
			sb.Append("\", ");
			AppendTypeof(sb, self.returnType);
			sb.Append(", new KeyValuePair<string, Type>[] {");
			var parameters = self.parameters;
			for(var i = 0; i < parameters.Length; i++)
			{
				if(i > 0) sb.Append(',');
				sb.Append(" new KeyValuePair<string, Type>(\"");
				sb.Append(parameters[i].Key);
				sb.Append("\", ");
				AppendTypeof(sb, parameters[i].Value);
				sb.Append(")");
			}
			sb.Append(" })");
		}

		public static void AppendCtor(this MethodIdentifier self, StringBuilder sb)
		{
			sb.Append("new MethodIdentifier(");
			sb.Append(self.declaringType);
			sb.Append(", \"");
			sb.Append(self.name);
			sb.Append("\", new int[] {");
			var arguments = self.arguments;
			for(var i = 0; i < arguments.Length; i++)
			{
				if(i > 0) sb.Append(',');
				sb.Append(' ');
				sb.Append(arguments[i]);
			}
			sb.Append(" })");
		}

		public static void AppendCtor(this FieldIdentifier self, StringBuilder sb)
		{
			sb.Append("new FieldIdentifier(");
			sb.Append(self.declaringType);
			sb.Append(", \"");
			sb.Append(self.name);
			sb.Append("\")");
		}

		public static void AppendCtor(this FieldNameInfo self, StringBuilder sb)
		{
			sb.Append("new FieldNameInfo(");
			if(string.IsNullOrEmpty(self.getterName)) sb.Append("null");
			else
			{
				sb.Append('"');
				sb.Append(self.getterName);
				sb.Append('"');
			}
			sb.Append(", ");
			if(string.IsNullOrEmpty(self.setterName)) sb.Append("null");
			else
			{
				sb.Append('"');
				sb.Append(self.setterName);
				sb.Append('"');
			}
			sb.Append(")");
		}

		public static void AppendTypeof(StringBuilder sb, Type type)
		{
			sb.Append("typeof(");
			AppendTypeName(sb, type);
			sb.Append(')');
			if(type.IsByRef) sb.Append(".MakeByRefType()");
		}

		public static void AppendTypeName(StringBuilder sb, Type type)
		{
			if(type == typeof(void))
			{
				sb.Append("void");
				return;
			}

			if(type.IsByRef) type = type.GetElementType();
			if(type.IsArray)
			{
				AppendTypeName(sb, type.GetElementType());
				sb.Append('[');
				int rank = type.GetArrayRank();
				if(rank > 1) sb.Append(',', rank - 1);
				sb.Append(']');
				return;
			}
			if(type.IsNested)
			{
				sb.Append(type.DeclaringType);
				sb.Append('.');
			}
			else if(!string.IsNullOrEmpty(type.Namespace))
			{
				sb.Append(type.Namespace);
				sb.Append('.');
			}
			if(type.IsGenericType)
			{
				var definition = type.GetGenericTypeDefinition();
				var args = type.GetGenericArguments();

				var name = definition.Name;
				sb.Append(name.Remove(name.LastIndexOf('`')));
				sb.Append('<');
				for(var i = 0; i < args.Length; i++)
				{
					if(i > 0) sb.Append(", ");
					AppendTypeName(sb, args[i]);
				}
				sb.Append('>');
			}
			else
			{
				sb.Append(type.Name);
			}
		}
	}
}