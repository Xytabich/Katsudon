using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Katsudon.Editor.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.VM.Common;

namespace Katsudon.Editor
{
	public class UdonProgramDisassemblyWindow : EditorWindow
	{
		private static Action<VisualElement, bool> _setChecked = null;
		private static Action<VisualElement, bool> SetChecked
		{
			get
			{
				if(_setChecked == null)
				{
					var element = Expression.Parameter(typeof(VisualElement));
					var state = Expression.Parameter(typeof(bool));

					var stateType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.PseudoStates");
					var stateValue = Expression.Constant(Convert.ToInt32(Enum.Parse(stateType, "Checked")));
					var stateProp = Expression.Property(element, typeof(VisualElement).GetProperty("pseudoStates", BindingFlags.Instance | BindingFlags.NonPublic));
					_setChecked = Expression.Lambda<Action<VisualElement, bool>>(
						Expression.IfThenElse(state,
							Expression.Assign(stateProp, Expression.Convert(Expression.Or(Expression.Convert(stateProp, typeof(int)), stateValue), stateType)),
							Expression.Assign(stateProp, Expression.Convert(Expression.And(Expression.Convert(stateProp, typeof(int)), Expression.Not(stateValue)), stateType))
						),
						element, state
					).Compile();
				}
				return _setChecked;
			}
		}

		private VisualElement heapFieldsAddressRoot;
		private VisualElement heapFieldsTypeRoot;
		private VisualElement heapFieldsNameRoot;
		private VisualElement heapFieldsValueRoot;
		private VisualElement heapFieldInfoRoot;
		private ListView programRowsRoot;

		private StringBuilder cachedSb = new StringBuilder();
		private IUdonProgram program = null;
		private List<ProgramRowInfo> programRows = new List<ProgramRowInfo>();

		private List<VisualElement> selectedHeapElements = new List<VisualElement>(4);

		private void OnEnable()
		{
			var splitterType = typeof(PropertyField).Assembly.GetType("UnityEditor.UIElements.VisualSplitter");

			var root = (VisualElement)Activator.CreateInstance(splitterType);
			root.style.flexGrow = 1f;

			var heapRoot = new VisualElement();
			heapRoot.style.flexGrow = 1f;
			heapRoot.style.minHeight = 128f;
			heapRoot.style.flexBasis = 128f;

			AddToolbarLabel(heapRoot, "Heap:");

			var heapFieldsInfoRoot = (VisualElement)Activator.CreateInstance(splitterType);
			heapFieldsInfoRoot.style.flexGrow = 1f;
			heapFieldsInfoRoot.style.flexDirection = FlexDirection.Row;

			var heapFieldsScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
			heapFieldsScroll.AddToClassList(Box.ussClassName);
			heapFieldsScroll.style.flexGrow = 0.8f;
			heapFieldsScroll.style.flexBasis = 256f;
			heapFieldsScroll.style.minWidth = 256f;

			var heapFieldsRoot = new VisualSplitter();
			heapFieldsRoot.style.flexDirection = FlexDirection.Row;

			heapFieldsRoot.AddToClassList(ListView.ussClassName);
			heapFieldsAddressRoot = AddFixedColumn(heapFieldsRoot, SelectHeapField, 72f);
			heapFieldsNameRoot = AddFlexColumn(heapFieldsRoot, SelectHeapField);
			heapFieldsTypeRoot = AddFlexColumn(heapFieldsRoot, SelectHeapField);
			heapFieldsValueRoot = AddFlexColumn(heapFieldsRoot, SelectHeapField);

			heapFieldsScroll.Add(heapFieldsRoot);
			heapFieldsInfoRoot.Add(heapFieldsScroll);

			heapFieldInfoRoot = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
			heapFieldInfoRoot.AddToClassList(Box.ussClassName);
			heapFieldInfoRoot.style.flexGrow = 0.2f;
			heapFieldInfoRoot.style.flexBasis = 256f;
			heapFieldInfoRoot.style.minWidth = 256f;
			heapFieldsInfoRoot.Add(heapFieldInfoRoot);
			heapRoot.Add(heapFieldsInfoRoot);

			root.Add(heapRoot);

			var programRoot = new VisualElement();
			programRoot.style.flexGrow = 1f;
			programRoot.style.minHeight = 256f;
			programRoot.style.flexBasis = 256f;

			AddToolbarLabel(programRoot, "Program:").RegisterCallback<ContextClickEvent>(ShowProgramMenu);

			programRowsRoot = new ListView();
			programRowsRoot.AddToClassList(Box.ussClassName);
			programRowsRoot.itemHeight = 20;
			programRowsRoot.style.flexGrow = 1f;
			programRowsRoot.makeItem = CreateRow;
			programRowsRoot.bindItem = UpdateRow;
			programRoot.Add(programRowsRoot);

			root.Add(programRoot);

			this.rootVisualElement.Add(root);
		}

		private void Init(IUdonProgram program)
		{
			this.program = program;
			var heapDump = new List<(uint address, IStrongBox strongBoxedObject, Type objectType)>();
			program.Heap.DumpHeapObjects(heapDump);
			InitHeapFields(program.SymbolTable, heapDump);
			InitProgram(program);
		}

		private VisualElement CreateRow()
		{
			return new ProgramRowElement();
		}

		private void UpdateRow(VisualElement element, int index)
		{
			var row = (ProgramRowElement)element;
			row.Init(cachedSb, programRows[index], program);
		}

		private void InitProgram(IUdonProgram program)
		{
			programRows.Clear();
			var publicMethods = program.EntryPoints;
			var bytes = program.ByteCode;
			uint index = 0;
			while(index < bytes.Length)
			{
				ProgramRowInfo rowInfo = default;
				rowInfo.address = index;
				if(publicMethods.TryGetSymbolFromAddress(index, out string name))
				{
					rowInfo.opCode = OpCode.ANNOTATION;
					rowInfo.argument = index;
					programRows.Add(rowInfo);
				}
				OpCode op = (OpCode)UIntFromBytes(bytes, index);
				index += 4;
				switch(op)
				{
					case OpCode.PUSH:
					case OpCode.EXTERN:
					case OpCode.JUMP:
					case OpCode.JUMP_IF_FALSE:
					case OpCode.JUMP_INDIRECT:
						rowInfo.opCode = op;
						rowInfo.argument = UIntFromBytes(bytes, index);
						programRows.Add(rowInfo);
						index += 4;
						break;
					case OpCode.NOP:
					case OpCode.POP:
					case OpCode.COPY:
						rowInfo.opCode = op;
						programRows.Add(rowInfo);
						break;
					case OpCode.ANNOTATION:
						index += 4;
						break;
				}
			}
			programRowsRoot.itemsSource = programRows;
		}

		private void InitHeapFields(IUdonSymbolTable symbols, List<(uint address, IStrongBox strongBoxedObject, Type objectType)> heapDump)
		{
			InitTableColumn(heapFieldsAddressRoot, "Address");
			InitTableColumn(heapFieldsNameRoot, "Name");
			InitTableColumn(heapFieldsTypeRoot, "Type");
			InitTableColumn(heapFieldsValueRoot, "Value");

			for(var i = 0; i < heapDump.Count; i++)
			{
				var address = heapDump[i].address;
				AddTableCell(heapFieldsAddressRoot, address.ToString("X8"));
				if(symbols.HasSymbolForAddress(address))
				{
					var symbol = symbols.GetSymbolFromAddress(address);
					AddTableCell(heapFieldsNameRoot, symbol);
				}
				else
				{
					AddTableCell(heapFieldsNameRoot, "<unknown>");
				}

				AddTableCell(heapFieldsTypeRoot, heapDump[i].objectType.ToString());

				cachedSb.Clear();
				AppendObjectValue(cachedSb, heapDump[i].strongBoxedObject.Value);
				if(cachedSb.Length > 32)
				{
					cachedSb.Remove(29, cachedSb.Length - 29);
					cachedSb.Append("...");
				}
				AddTableCell(heapFieldsValueRoot, cachedSb.ToString());
			}
		}

		private void ShowProgramMenu(ContextClickEvent evt)
		{
			if(program == null) return;
			var menu = new GenericMenu();
			menu.AddItem(new GUIContent("Copy disassembly"), false, () => {
				cachedSb.Clear();
				foreach(var row in programRows)
				{
					if(row.opCode == OpCode.ANNOTATION)
					{
						cachedSb.AppendLine();
						cachedSb.AppendLine();
						var symbol = program.EntryPoints.GetSymbolFromAddress(row.argument);
						if(program.EntryPoints.HasExportedSymbol(symbol))
						{
							cachedSb.Append(".export ");
						}
						cachedSb.Append(symbol);
						cachedSb.Append(':');
					}
					else
					{
						cachedSb.AppendFormat("0x{0:X8}: ", row.address);
						row.AppendInfo(cachedSb, program);
					}
					cachedSb.AppendLine();
				}
				GUIUtility.systemCopyBuffer = cachedSb.ToString();
			});
			menu.ShowAsContext();
		}

		private void SelectHeapField(PointerDownEvent evt)
		{
			var root = (VisualElement)evt.currentTarget;
			var element = (VisualElement)evt.target;
			if(root == element) return;

			while(element.parent != root)
			{
				element = element.parent;
			}
			int index = root.IndexOf(element);
			if(index < 1) return; // 0 is header

			foreach(var e in selectedHeapElements)
			{
				e.RemoveFromClassList(ListView.itemSelectedVariantUssClassName);
				SetChecked(e, false);
			}
			selectedHeapElements.Clear();

			selectedHeapElements.Add(heapFieldsAddressRoot.ElementAt(index));
			selectedHeapElements.Add(heapFieldsNameRoot.ElementAt(index));
			selectedHeapElements.Add(heapFieldsTypeRoot.ElementAt(index));
			selectedHeapElements.Add(heapFieldsValueRoot.ElementAt(index));
			foreach(var e in selectedHeapElements)
			{
				e.AddToClassList(ListView.itemSelectedVariantUssClassName);
				SetChecked(e, true);
			}

			evt.StopPropagation();
			InitHeapFieldInfo((uint)(index - 1));
		}

		private void InitHeapFieldInfo(uint address)
		{
			heapFieldInfoRoot.Clear();
			var symbols = program.SymbolTable;
			if(symbols.TryGetSymbolFromAddress(address, out var name))
			{
				heapFieldInfoRoot.Add(new Label("Name: " + name));
				if(symbols.HasExportedSymbol(name))
				{
					heapFieldInfoRoot.Add(new Label("Exported"));
				}
				var syncInfo = program.SyncMetadataTable.GetSyncMetadataFromSymbol(name);
				if(syncInfo != null) heapFieldInfoRoot.Add(new Label("Sync: " + syncInfo.Properties[0].InterpolationAlgorithm));
			}
			heapFieldInfoRoot.Add(new Label("Type: " + program.Heap.GetHeapVariableType(address)));

			cachedSb.Clear();
			AppendObjectFullValue(cachedSb, 0, program.Heap.GetHeapVariable(address));
			heapFieldInfoRoot.Add(new Label("Value: " + cachedSb.ToString()));
		}

		public static void Show(IUdonProgram program)
		{
			GetWindow<UdonProgramDisassemblyWindow>(true, "Udon Disassembly").Init(program);
		}

		private static uint UIntFromBytes(byte[] bytes, uint startIndex)
		{
			return (uint)((bytes[startIndex] << 24) + (bytes[startIndex + 1] << 16) + (bytes[startIndex + 2] << 8) + bytes[startIndex + 3]);
		}

		private static VisualElement AddFixedColumn(VisualElement root, EventCallback<PointerDownEvent> onEvent, float width)
		{
			var element = new VisualElement();
			element.style.width = width;
			element.style.minWidth = width;
			element.style.maxWidth = width;
			element.RegisterCallback<PointerDownEvent>(onEvent);
			root.Add(element);
			return element;
		}

		private static VisualElement AddFlexColumn(VisualElement root, EventCallback<PointerDownEvent> onEvent)
		{
			var element = new VisualElement();
			element.style.minWidth = 64f;
			element.style.flexBasis = 64f;
			element.style.flexGrow = 1f;
			element.style.flexShrink = 0f;
			element.RegisterCallback<PointerDownEvent>(onEvent);
			root.Add(element);
			return element;
		}

		private static void InitTableColumn(VisualElement root, string text)
		{
			root.Clear();
			var label = new Label(text);
			label.style.height = 24f;
			label.style.flexShrink = 1f;
			label.style.paddingLeft = 4f;
			label.style.unityTextAlign = TextAnchor.MiddleLeft;
			label.AddToClassList(Box.ussClassName);
			root.Add(label);
		}

		private static void AddTableCell(VisualElement root, string text)
		{
			var label = new Label(text);
			label.AddToClassList(ListView.itemUssClassName);
			label.style.unityTextAlign = TextAnchor.MiddleLeft;
			label.style.overflow = Overflow.Hidden;
			label.style.flexWrap = Wrap.NoWrap;
			label.style.flexShrink = 1f;
			label.style.height = 20f;
			root.Add(label);
		}

		private static VisualElement AddToolbarLabel(VisualElement root, string text)
		{
			var label = new Label(text);
			label.AddToClassList(ProgressBar.ussClassName);
			label.style.height = 24f;
			label.style.flexShrink = 1f;
			label.style.paddingLeft = 4f;
			label.style.unityTextAlign = TextAnchor.MiddleLeft;
			label.style.unityFontStyleAndWeight = FontStyle.Bold;
			root.Add(label);
			return label;
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
			if(value is string)
			{
				sb.Append('"');
				sb.Append(value);
				sb.Append('"');
				return;
			}
			sb.Append(value ?? "<null>");
		}

		public static void AppendObjectFullValue(StringBuilder sb, int indent, object value)
		{
			sb.Append(' ', indent * 2);
			if(value is Array array)
			{
				if(array.Rank <= 1)
				{
					sb.Append(array.GetType());
					sb.Append(' ');
					sb.Append('{');
					sb.AppendLine();
					indent++;
					for(var i = 0; i < array.Length; i++)
					{
						if(i > 0)
						{
							sb.Append(',');
							sb.AppendLine();
						}
						AppendObjectFullValue(sb, indent, array.GetValue(i));
					}
					indent--;
					sb.AppendLine();
					sb.Append('}');
					return;
				}
			}
			if(value is string)
			{
				sb.Append('"');
				sb.Append(value);
				sb.Append('"');
				return;
			}
			sb.Append(value ?? "null");
		}

		private struct ProgramRowInfo
		{
			public uint address;
			public OpCode opCode;
			public uint argument;

			public void AppendInfo(StringBuilder sb, IUdonProgram program)
			{
				switch(opCode)
				{
					case OpCode.JUMP:
					case OpCode.JUMP_IF_FALSE:
						sb.AppendFormat("{0}, 0x{1:X8}", opCode, argument);
						break;
					case OpCode.PUSH:
					case OpCode.JUMP_INDIRECT:
						sb.AppendFormat("{0}, {1}", opCode, GetSymbol(program.SymbolTable, argument));
						break;
					case OpCode.EXTERN:
						sb.AppendFormat("{0}, \"{1}\"", opCode, program.Heap.GetHeapVariable(argument));
						break;
					default:
						sb.Append(opCode);
						break;
				}
			}

			private static string GetSymbol(IUdonSymbolTable symbols, uint address)
			{
				return symbols.TryGetSymbolFromAddress(address, out var symbol) ? symbol : string.Format("<0x{0:X8}>", address);
			}
		}

		private class ProgramRowElement : VisualElement
		{
			private Label addressLabel;
			private Label infoLabel;

			public ProgramRowElement()
			{
				style.flexDirection = FlexDirection.Row;
				Add(addressLabel = new Label());
				addressLabel.style.width = 72f;
				addressLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
				Add(infoLabel = new Label());
				infoLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
			}

			public void Init(StringBuilder sb, ProgramRowInfo info, IUdonProgram program)
			{
				addressLabel.style.display = info.opCode != OpCode.ANNOTATION ? DisplayStyle.Flex : DisplayStyle.None;
				addressLabel.text = info.address.ToString("X8");
				infoLabel.style.unityFontStyleAndWeight = info.opCode != OpCode.ANNOTATION ? FontStyle.Normal : FontStyle.Bold;
				if(info.opCode == OpCode.ANNOTATION)
				{
					var symbol = program.EntryPoints.GetSymbolFromAddress(info.argument);
					if(program.EntryPoints.HasExportedSymbol(symbol))
					{
						infoLabel.text = ".export " + symbol + ":";
					}
					else infoLabel.text = symbol + ":";
				}
				else
				{
					sb.Clear();
					info.AppendInfo(sb, program);
					infoLabel.text = sb.ToString();
				}
			}
		}
	}
}