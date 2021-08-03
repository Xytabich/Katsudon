using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Katsudon.Editor.Udon;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;
using VRC.Udon.VM.Common;

#if UDON_DEBUG
using VRC.Udon.Serialization.OdinSerializer;
#endif

namespace Katsudon.Editor
{
	[CustomEditor(typeof(SerializedUdonProgramAsset))]
	public class ProgramAssetInspector : EditorProxyDrawer
	{
		private bool hasScript = false;
		private MonoScript script = null;

		private bool showProgram = false;
		private IUdonProgram program = null;
		private string variablesAddresses;
		private string variablesNames;
		private string variablesValues;
		private string disassemblyAddresses;
		private List<GUIContent> disassembly = new List<GUIContent>();
		private string fullText;

		protected override void OnInit()
		{
			var fallback = EditorReplacer.GetFallbackEditor(typeof(SerializedUdonProgramAsset), targets.Length > 1);
			base.CreateEditor(fallback);

			var programImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(target));
			if(programImporter.GetExternalObjectMap().TryGetValue(ProgramUtils.GetScriptIdentifier(), out var obj))
			{
				hasScript = true;
				script = (MonoScript)obj;
			}
		}

		public override void OnInspectorGUI()
		{
			if(hasScript)
			{
				if(script == null)
				{
					script = ReplaceMissingClassGUI(target as SerializedUdonProgramAsset);
				}
				else
				{
					using(new EditorGUI.DisabledScope(true))
					{
						EditorGUILayout.ObjectField("Class", script, typeof(MonoScript), false);
					}
				}
			}

			if(showProgram != EditorGUILayout.Foldout(showProgram, "Show program"))
			{
				showProgram = !showProgram;
				if(showProgram)
				{
					program = (target as SerializedUdonProgramAsset).RetrieveProgram();
					var symbols = program.SymbolTable;

					var cachedSb = new StringBuilder();
					var cachedSb2 = new StringBuilder();
					var cachedSb3 = new StringBuilder();
					var cachedSb4 = new StringBuilder();
					var fullTextSb = new StringBuilder();

					var heapDump = new List<(uint address, IStrongBox strongBoxedObject, Type objectType)>();
					var heap = program.Heap;
					heap.DumpHeapObjects(heapDump);
					heapDump.Sort((a, b) => a.address.CompareTo(b.address));

					fullTextSb.AppendLine("VARIABLES:");
					var exportedVariables = new HashSet<string>(symbols.GetExportedSymbols());
					for(var i = 0; i < heapDump.Count; i++)
					{
						cachedSb4.Clear();
						var address = heapDump[i].address;
						cachedSb.AppendFormat("0x{0:X8}\n", address);
						//cachedSb4.AppendFormat("0x{0:X8} ", address);
						if(symbols.HasSymbolForAddress(address))
						{
							var symbol = symbols.GetSymbolFromAddress(address);
							if(exportedVariables.Contains(symbol))
							{
								cachedSb2.Append("[export] ");
								cachedSb4.Append("[export] ");
							}
							cachedSb2.AppendFormat("{0} ", heapDump[i].objectType);
							cachedSb2.AppendLine(symbol);
							cachedSb4.Append(symbol);
						}
						else
						{
							cachedSb2.AppendFormat("{0} ", heapDump[i].objectType);
							cachedSb2.AppendLine("[Unknown]");
							cachedSb4.Append("[Unknown]");
						}
						cachedSb4.Append(' ', 64 - cachedSb4.Length);
						AppendObjectValue(cachedSb3, heapDump[i].strongBoxedObject.Value);
						AppendObjectValue(cachedSb4, heapDump[i].strongBoxedObject.Value);
						cachedSb3.AppendLine();
						fullTextSb.AppendLine(cachedSb4.ToString());
					}

					variablesAddresses = cachedSb.ToString();
					variablesNames = cachedSb2.ToString();
					variablesValues = cachedSb3.ToString();

					fullTextSb.AppendLine("PROGRAM:");
					cachedSb2.Clear();
					disassembly.Clear();
					var publicMethods = program.EntryPoints;
					var bytes = program.ByteCode;
					uint index = 0;
					while(index < bytes.Length)
					{
						cachedSb4.Clear();
						if(publicMethods.TryGetSymbolFromAddress(index, out string name))
						{
							cachedSb2.AppendLine();
							disassembly.Add(new GUIContent(string.Format("{0}:", name)));
							fullTextSb.AppendLine(string.Format("{0}:", name));
						}
						uint address = index;
						uint variableAddress;
						OpCode op = (OpCode)UIntFromBytes(bytes, index);
						index += 4;
						cachedSb.Clear();
						cachedSb.Append(op);
						GUIContent content;
						switch(op)
						{
							case OpCode.PUSH:
								cachedSb.Append(", ");
								variableAddress = UIntFromBytes(bytes, index);
								cachedSb.AppendFormat("<i>{0}</i>", symbols.HasSymbolForAddress(variableAddress) ? symbols.GetSymbolFromAddress(variableAddress) : "[Unknown]");
								index += 4;
								cachedSb3.Clear();
								AppendObjectValue(cachedSb3, heap.GetHeapVariable(variableAddress));
								content = new GUIContent(cachedSb.ToString(), string.Format("0x{0:X8}: ({1}) {2}", variableAddress, heap.GetHeapVariableType(variableAddress), cachedSb3.ToString()));
								break;
							case OpCode.EXTERN:
								cachedSb.Append(", \"");
								cachedSb.Append(heap.GetHeapVariable(UIntFromBytes(bytes, index)));
								cachedSb.Append("\"");
								index += 4;
								content = new GUIContent(cachedSb.ToString());
								break;
							case OpCode.JUMP:
							case OpCode.JUMP_IF_FALSE:
								cachedSb.Append(", ");
								cachedSb.AppendFormat("0x{0:X8}", UIntFromBytes(bytes, index));
								index += 4;
								content = new GUIContent(cachedSb.ToString());
								break;
							case OpCode.JUMP_INDIRECT:
								cachedSb.Append(", ");
								variableAddress = UIntFromBytes(bytes, index);
								cachedSb.AppendFormat("<i>{0}</i>", symbols.HasSymbolForAddress(variableAddress) ? symbols.GetSymbolFromAddress(variableAddress) : "[Unknown]");
								index += 4;
								content = new GUIContent(cachedSb.ToString(), string.Format("0x{0:X8}", variableAddress));
								break;
							default:
								content = new GUIContent(cachedSb.ToString());
								break;
						}
						cachedSb2.AppendFormat("0x{0:X8}\n", address);
						disassembly.Add(content);

						//cachedSb4.AppendFormat("0x{0:X8}\n", address);
						//cachedSb4.Append(' ', 64 - cachedSb4.Length);
						cachedSb4.Append(content.text);
						//cachedSb4.Append(' ');
						//cachedSb4.Append('(');
						//cachedSb4.Append(content.tooltip);
						//cachedSb4.Append(')');
						fullTextSb.AppendLine(cachedSb4.ToString());
					}
					disassemblyAddresses = cachedSb2.ToString();
					fullText = fullTextSb.ToString();
				}
				else
				{
					disassembly.Clear();
					fullText = null;
					variablesAddresses = null;
					variablesNames = null;
					variablesValues = null;
					disassemblyAddresses = null;
				}
			}
			if(showProgram)
			{
				if(program != null)
				{
					EditorGUILayout.BeginHorizontal();
					if(GUILayout.Button("Copy program"))
					{
						GUIUtility.systemCopyBuffer = fullText;
					}
					EditorGUILayout.EndHorizontal();
					GUILayout.Label("Variables:", EditorStyles.boldLabel);
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label(variablesAddresses);
					GUILayout.Label(variablesNames);
					GUILayout.Label(variablesValues);
					GUILayout.FlexibleSpace();
					EditorGUILayout.EndHorizontal();

					var style = new GUIStyle(EditorStyles.label);
					style.border = RemoveVertical(style.border);
					style.margin = RemoveVertical(style.margin);
					style.padding = RemoveVertical(style.padding);
					style.richText = true;
					var height = style.lineHeight;
					var heightProp = GUILayout.Height(height);

					GUILayout.Label("Program:", EditorStyles.boldLabel);
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label(disassemblyAddresses, style);
					EditorGUILayout.BeginVertical();
					for(var i = 0; i < disassembly.Count; i++)
					{
						GUILayout.Label(disassembly[i], style, heightProp);
					}
					EditorGUILayout.EndVertical();
					GUILayout.FlexibleSpace();
					EditorGUILayout.EndHorizontal();
				}
				else showProgram = false;
			}

			base.OnInspectorGUI();
		}

		internal static MonoScript ReplaceMissingClassGUI(SerializedUdonProgramAsset program)
		{
			MonoScript script = null;
			var programImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(program));
			if(programImporter.GetExternalObjectMap().TryGetValue(ProgramUtils.GetScriptIdentifier(), out var scriptAsset))
			{
				script = (MonoScript)scriptAsset;
			}

			var oldColor = GUI.color;
			GUI.color = new Color(1f, 0.5f, 0f);
			EditorGUI.BeginChangeCheck();
			script = (MonoScript)EditorGUILayout.ObjectField("Missing class", script, typeof(MonoScript), false);
			if(EditorGUI.EndChangeCheck() && script != null)
			{
				if(BuildTracker.IsValidScript(script))
				{
					AssetImporter scriptImporter;
					AssetImporter.SourceAssetIdentifier identifier;
					if(AssetDatabase.IsMainAsset(script))
					{
						scriptImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(script));
						identifier = ProgramUtils.GetMainProgramIdentifier();
					}
					else
					{
						AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileId);
						scriptImporter = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
						identifier = ProgramUtils.GetSubProgramIdentifier(script.GetClass());
					}
					if(scriptImporter.GetExternalObjectMap().TryGetValue(identifier, out var programAsset))
					{
						if(programAsset != null)
						{
							if(programAsset != program)
							{
								if(EditorUtility.DisplayDialog("Script replacement", "The target script already contains a link to the program. Do you want to destroy the old program and replace the link with this one?", "Yes", "No"))
								{
									programImporter.AddRemap(ProgramUtils.GetScriptIdentifier(), script);
									programImporter.SaveAndReimport();
									scriptImporter.AddRemap(identifier, program);
									scriptImporter.SaveAndReimport();

									AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(programAsset));
								}
								else
								{
									script = null;
								}
							}
							else
							{
								programImporter.AddRemap(ProgramUtils.GetScriptIdentifier(), script);
								programImporter.SaveAndReimport();
							}
						}
						else
						{
							programImporter.AddRemap(ProgramUtils.GetScriptIdentifier(), script);
							programImporter.SaveAndReimport();
							scriptImporter.AddRemap(identifier, program);
							scriptImporter.SaveAndReimport();
						}
					}
				}
				else
				{
					script = null;
					Debug.LogError("Invalid class. The class must inherit from MonoBehaviour, located in an assembly marked with UdonAsm, and must not be abstract.");
				}
			}
			GUI.color = oldColor;
			return script;
		}

		public static void AppendObjectValue(StringBuilder sb, object value)
		{
			if(value is Array array)
			{
				if(array.Rank <= 1)
				{
					sb.Append(array.GetType());
					sb.Append(' ');
					sb.Append('{');
					for(var i = 0; i < array.Length; i++)
					{
						if(i > 0) sb.Append(',');
						sb.Append(' ');
						sb.Append(array.GetValue(i));
					}
					sb.Append(' ');
					sb.Append('}');
					return;
				}
			}
			sb.Append(value ?? "<null>");
		}

		private static uint UIntFromBytes(byte[] bytes, uint startIndex)
		{
			return (uint)((bytes[startIndex] << 24) + (bytes[startIndex + 1] << 16) + (bytes[startIndex + 2] << 8) + bytes[startIndex + 3]);
		}

		private static RectOffset RemoveVertical(RectOffset ro)
		{
			ro.top = 0;
			ro.bottom = 0;
			return ro;
		}
	}
}