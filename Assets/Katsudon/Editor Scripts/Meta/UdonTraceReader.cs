using System;
using System.Collections.Generic;
using System.Reflection;
using Katsudon.Builder;
using Katsudon.Meta;
using Katsudon.Utility;

namespace Katsudon.Editor.Meta
{
	public class UdonTraceReader : IDisposable
	{
		private FileMetaReader metaReader;
		private UdonAssembliesMetaReader assembliesReader;
		private Dictionary<string, Assembly> assemblies = null;

		private bool disposed = false;

		public UdonTraceReader()
		{
			metaReader = new FileMetaReader();
			assembliesReader = new UdonAssembliesMetaReader(FileUtils.GetReadStream(AssembliesBuilder.ASSEMBLIES_META_FILE));
		}

		~UdonTraceReader()
		{
			if(!disposed) Dispose();
		}

		public void FillTraceInfo(Guid typeGuid, uint programOffset, IList<TraceFrame> outList)
		{
			if(assemblies == null)
			{
				assemblies = new Dictionary<string, Assembly>();
				var arr = AppDomain.CurrentDomain.GetAssemblies();
				for(int i = 0; i < arr.Length; i++)
				{
					if(!arr[i].IsDynamic) assemblies.Add(arr[i].FullName, arr[i]);
				}
			}
			var traceList = new List<UdonAssembliesMetaReader.TraceInfo>();//TODO: cache
			assembliesReader.SearchTrace(typeGuid, programOffset, traceList);
			for(int i = traceList.Count - 1; i >= 0; i--)
			{
				var trace = traceList[i];
				if(assemblies.TryGetValue(trace.assemblyName, out var assembly))
				{
					metaReader.GetSourceLineInfo(assembly.Location, trace.methodToken, trace.ilOffset, out var source, out var line, out var column);
					outList.Add(new TraceFrame(string.IsNullOrEmpty(source) ? "<unknown>" : source, string.IsNullOrEmpty(source) ? null :
						assembly.GetModule(trace.module).ResolveMethod(trace.methodToken), trace.ilOffset, line, column));
				}
			}
		}

		public void Dispose()
		{
			metaReader.Dispose();
			assembliesReader.Dispose();
			disposed = true;
		}

		public struct TraceFrame
		{
			public string fileName;
			public MethodBase method;
			public int ilOffset;
			public int line;
			public int column;

			public TraceFrame(string fileName, MethodBase method, int ilOffset, int line, int column)
			{
				this.fileName = fileName;
				this.method = method;
				this.ilOffset = ilOffset;
				this.line = line;
				this.column = column;
			}
		}
	}
}