using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Katsudon.Editor.Meta;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Katsudon.Editor
{
	public class UdonExceptionsParser : EditorWindow
	{
		private static Regex udonMessagePattern = new Regex(@"The VM encountered an error![\n\s]+Exception Message:[\n\s]+An exception occurred during EXTERN to '.*?'\.[\n\s]+Parameter Addresses:[0-9a-fA-Fx,\s]+[\s\n]+([^\s\n].*)[\n\s-]+Program Counter was at:\s*(\d+)[\n\s-]+[\n\s\S]+Heap Dump:[\n\s]+0x00000000:\s*([0-9a-fA-F\-]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		private static Regex katsudonMessagePattern = new Regex(@"^(.+?)[\s\n]+\{KatsudonExceptionInfo:([0-9a-fA-F\-]+):(\d+)\}", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private TextField fileField;
		private VisualElement messagesContainer;

		void OnEnable()
		{
			var root = this.rootVisualElement;
			root.style.paddingTop = 2f;

			var fileSelectRoot = new VisualElement();
			fileSelectRoot.style.flexDirection = FlexDirection.Row;
			fileSelectRoot.style.alignItems = Align.Stretch;
			fileField = new TextField("File");
			fileField.style.flexGrow = 1f;
			fileField.style.flexShrink = 1f;
			fileSelectRoot.Add(fileField);
			var fileBtn = new Button(SelectFileDialogue);
			fileBtn.style.width = 32f;
			var icon = new Image();
			icon.image = (Texture2D)EditorGUIUtility.LoadRequired("DefaultAsset Icon");
			icon.scaleMode = ScaleMode.ScaleToFit;
			icon.style.height = 16f;
			fileBtn.Add(icon);
			fileSelectRoot.Add(fileBtn);
			root.Add(fileSelectRoot);

			var parseBtn = new Button(ParseFile);
			parseBtn.text = "Parse";
			root.Add(parseBtn);

			messagesContainer = new ScrollView(ScrollViewMode.Vertical);
			root.Add(messagesContainer);
		}

		private void ParseFile()
		{
			messagesContainer.Clear();
			if(string.IsNullOrWhiteSpace(fileField.value)) return;

			var file = new FileInfo(fileField.value);
			if(file.Exists)
			{
				string text = null;
				using(var reader = new StreamReader(file.FullName, Encoding.UTF8))
				{
					text = reader.ReadToEnd();
					reader.Close();
				}
				if(!string.IsNullOrEmpty(text))
				{
					var list = new List<TraceMessage>();
					int offset = 0;
					while(MatchMessage(text, ref offset, out var guid, out var address, out var msg))
					{
						list.Add(new TraceMessage(msg, guid, address));
					}
					if(list.Count > 0)
					{
						var traceList = new List<UdonTraceReader.TraceFrame>();
						var sb = new StringBuilder();
						using(var reader = new UdonTraceReader())
						{
							for(int i = 0; i < list.Count; i++)
							{
								traceList.Clear();
								reader.FillTraceInfo(list[i].typeGuid, list[i].programOffset, traceList);

								var messageRoot = new Box();
								messageRoot.style.marginTop = 2f;
								messageRoot.style.marginLeft = 3f;
								messageRoot.style.marginRight = 3f;
								messageRoot.style.paddingTop = 4f;
								messageRoot.style.paddingLeft = 4f;
								messageRoot.style.paddingRight = 4f;
								messageRoot.style.paddingBottom = 4f;
								messageRoot.style.flexDirection = FlexDirection.Column;
								ContextMenu(messageRoot, list[i]);

								var msg = new TextElement() { text = list[i].message };
								msg.style.marginBottom = 4f;
								messageRoot.Add(msg);

								if(traceList.Count < 1)
								{
									var frame = new TextElement() { text = "(at <unknown>:0)" };
									frame.style.paddingLeft = 24f;
									frame.style.color = (Color)new Color32(147, 179, 248, 255);
									messageRoot.Add(frame);
								}
								else
								{
									for(int j = 0; j < traceList.Count; j++)
									{
										sb.Clear();
										AppendFrame(sb, traceList[j], true);
										var frame = new Button() { text = sb.ToString() };
										frame.ClearClassList();
										frame.style.marginLeft = 24f;
										frame.style.color = (Color)new Color32(147, 179, 248, 255);
										if(traceList[j].method != null)
										{
											GotoOnClick(frame, traceList[j]);
										}
										messageRoot.Add(frame);
									}
								}
								messagesContainer.Add(messageRoot);
							}
						}
					}
				}
			}
			else
			{
				Debug.Log("File does not exist: " + fileField.value);
			}
		}

		private void GotoOnClick(Button element, UdonTraceReader.TraceFrame frame)
		{
			element.clicked += () => {
				typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries")
					.GetMethod("OpenFileOnSpecificLineAndColumn").Invoke(null, new object[] { frame.fileName, frame.line, frame.column });
			};
		}

		private void ContextMenu(VisualElement element, TraceMessage message)
		{
			element.RegisterCallback<ContextClickEvent>((e) => {
				var menu = new GenericMenu();
				menu.AddItem(new GUIContent("Copy message"), false, () => {
					GUIUtility.systemCopyBuffer = message.message;
				});
				menu.ShowAsContext();
			}, TrickleDown.TrickleDown);
		}

		private void SelectFileDialogue()
		{
			var oldPath = "";
			try { oldPath = Path.GetDirectoryName(fileField.value); } catch { }
			var path = EditorUtility.OpenFilePanel("Select log file", oldPath, "");
			if(!string.IsNullOrEmpty(path)) fileField.value = path;
		}

		[MenuItem("Katsudon/Udon Exceptions Parser")]
		private static void ShowWindow()
		{
			GetWindow<UdonExceptionsParser>("Udon Exceptions Parser");
		}

		[InitializeOnLoadMethod]
		private static void TrackConsole()
		{
			Application.logMessageReceived += OnConsoleLog;
		}

		private static void OnConsoleLog(string condition, string stackTrace, LogType type)
		{
			if(type == LogType.Error)
			{
				int offset = 0;
				if(MatchMessage(condition, ref offset, out var guid, out var address, out var msg))
				{
					using(var reader = new UdonTraceReader())
					{
						Debug.LogFormat(LogType.Exception, LogOption.NoStacktrace, null, "[<color=green>KEP</color>] {0}", GenerateTrace(reader, guid, address, msg));
					}
				}
			}
		}

		private static bool MatchMessage(string condition, ref int offset, out Guid guid, out uint programOffset, out string msg)
		{
			var match = udonMessagePattern.Match(condition, offset);
			if(match.Success && uint.TryParse(match.Groups[2].Value, out programOffset) && Guid.TryParse(match.Groups[3].Value, out guid))
			{
				msg = match.Groups[1].Value.Trim();
				offset = match.Index + match.Length;
				return true;
			}
			match = katsudonMessagePattern.Match(condition, offset);
			if(match.Success && uint.TryParse(match.Groups[3].Value, out programOffset) && Guid.TryParse(match.Groups[2].Value, out guid))
			{
				msg = match.Groups[1].Value.Trim();
				offset = match.Index + match.Length;
				return true;
			}
			guid = default;
			programOffset = default;
			msg = default;
			return false;
		}

		private static string GenerateTrace(UdonTraceReader reader, Guid guid, uint programOffset, string message)
		{
			var traceList = new List<UdonTraceReader.TraceFrame>();
			reader.FillTraceInfo(guid, programOffset, traceList);
			var sb = new StringBuilder();
			sb.AppendLine(message);
			if(traceList.Count < 1)
			{
				sb.Append("(at <unknown>:0)");
			}
			else
			{
				for(int i = 0; i < traceList.Count; i++)
				{
					AppendFrame(sb, traceList[i]);
				}
			}
			return sb.ToString();
		}

		private static void AppendFrame(StringBuilder sb, UdonTraceReader.TraceFrame frame, bool includeColumn = false)
		{
			if(frame.method != null)
			{
				sb.Append(frame.method.DeclaringType);
				sb.Append(':');
				sb.Append(frame.method.Name);
				sb.Append(' ');
				sb.Append('(');
				var parameters = frame.method.GetParameters();
				for(int j = 0; j < parameters.Length; j++)
				{
					if(j > 0) sb.Append(',');
					sb.Append(parameters[j].ParameterType);
				}
				sb.Append(')');
				sb.Append(' ');
			}
			sb.Append("(at ");
			sb.Append(frame.fileName);
			sb.Append(':');
			sb.Append(frame.line);
			if(includeColumn)
			{
				sb.Append(':');
				sb.Append(frame.column);
			}
			sb.Append(')');
			sb.AppendLine();
		}

		private struct TraceMessage
		{
			public string message;
			public Guid typeGuid;
			public uint programOffset;

			public TraceMessage(string message, Guid typeGuid, uint programOffset)
			{
				this.message = message;
				this.typeGuid = typeGuid;
				this.programOffset = programOffset;
			}
		}
	}
}