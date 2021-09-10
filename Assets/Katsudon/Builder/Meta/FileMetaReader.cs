using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Permissions;
using System.Reflection.Metadata.Ecma335;

namespace Katsudon.Meta
{
	public class FileMetaReader : IDisposable
	{
		private readonly ConcurrentDictionary<string, MetadataReaderProvider> metadataCache = new ConcurrentDictionary<string, MetadataReaderProvider>();

		public void Dispose()
		{
			foreach(MetadataReaderProvider value in metadataCache.Values)
			{
				value?.Dispose();
			}
			metadataCache.Clear();
		}

		public bool GetSourceLineInfo(string assemblyPath, int methodToken, int ilOffset, out string sourceFile, out int sourceLine, out int sourceColumn)
		{
			new SecurityPermission(SecurityPermissionFlag.UnmanagedCode).Assert();
			return GetSourceLineInfoWithoutCasAssert(assemblyPath, methodToken, ilOffset, out sourceFile, out sourceLine, out sourceColumn);
		}

		public bool GetSequencePoints(string assemblyPath, int methodToken, out FileInfoReader fileReader, out SequencePointCollection sequencePoints)
		{
			fileReader = default;
			sequencePoints = default;
			try
			{
				MetadataReader reader = TryGetReader(assemblyPath);
				if(reader != null)
				{
					Handle tokenHandle = MetadataTokens.Handle(methodToken);
					if(tokenHandle.Kind == HandleKind.MethodDefinition)
					{
						MethodDebugInformationHandle infoHandle = ((MethodDefinitionHandle)tokenHandle).ToDebugInformationHandle();
						MethodDebugInformation debugInfo = reader.GetMethodDebugInformation(infoHandle);
						if(!debugInfo.SequencePointsBlob.IsNil)
						{
							sequencePoints = debugInfo.GetSequencePoints();
							fileReader = new FileInfoReader(reader);
							return true;
						}
					}
				}
			}
			catch(BadImageFormatException) { }
			catch(IOException) { }
			return false;
		}

		private bool GetSourceLineInfoWithoutCasAssert(string assemblyPath, int methodToken, int ilOffset, out string sourceFile, out int sourceLine, out int sourceColumn)
		{
			sourceFile = null;
			sourceLine = 0;
			sourceColumn = 0;
			if(GetSequencePoints(assemblyPath, methodToken, out FileInfoReader fileReader, out SequencePointCollection sequencePoints))
			{
				SequencePoint? sequencePoint = null;
				foreach(SequencePoint item in sequencePoints)
				{
					if(item.Offset > ilOffset) break;
					if(item.StartLine != SequencePoint.HiddenLine) sequencePoint = item;
				}
				if(sequencePoint.HasValue)
				{
					sourceLine = sequencePoint.Value.StartLine;
					sourceColumn = sequencePoint.Value.StartColumn;
					sourceFile = fileReader.GetFileName(sequencePoint.Value.Document);
					return true;
				}
			}
			return false;
		}

		private MetadataReader TryGetReader(string assemblyPath)
		{
			if(string.IsNullOrEmpty(assemblyPath)) return null;
			if(metadataCache.TryGetValue(assemblyPath, out MetadataReaderProvider value))
			{
				return value?.GetMetadataReader();
			}
			value = TryOpenReaderFromAssemblyFile(assemblyPath);
			metadataCache.TryAdd(assemblyPath, value);
			return value?.GetMetadataReader();
		}

		private static PEReader TryGetPEReader(string assemblyPath)
		{
			Stream stream = TryOpenFile(assemblyPath);
			if(stream != null) return new PEReader(stream);
			return null;
		}

		private static MetadataReaderProvider TryOpenReaderFromAssemblyFile(string assemblyPath)
		{
			using(PEReader pEReader = TryGetPEReader(assemblyPath))
			{
				if(pEReader == null) return null;
				if(pEReader.TryOpenAssociatedPortablePdb(assemblyPath, TryOpenFile, out MetadataReaderProvider pdbReaderProvider, out string _))
				{
					return pdbReaderProvider;
				}
			}
			return null;
		}

		private static Stream TryOpenFile(string path)
		{
			if(!File.Exists(path)) return null;
			try
			{
				return File.OpenRead(path);
			}
			catch
			{
				return null;
			}
		}

		public struct FileInfoReader
		{
			private MetadataReader reader;

			public FileInfoReader(MetadataReader reader)
			{
				this.reader = reader;
			}

			public string GetFileName(DocumentHandle document)
			{
				return reader.GetString(reader.GetDocument(document).Name);
			}
		}
	}
}