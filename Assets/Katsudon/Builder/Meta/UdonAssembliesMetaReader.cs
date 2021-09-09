using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Katsudon.Meta
{
	public class UdonAssembliesMetaReader : IDisposable
	{
		private const byte META_FILE_VERSION = 0;

		private const byte ADDRESS_BLOCK = 0;
		private const byte METHOD_BLOCK = 1;

		private FileStream stream;
		private BinaryReader reader;

		private List<string> stringsList = null;
		private Dictionary<Guid, long> typePointers = null;

		public UdonAssembliesMetaReader(FileStream stream)
		{
			this.stream = stream;
			this.reader = new BinaryReader(stream);
		}

		public void SearchTrace(Guid typeGuid, uint udonPosition, IList<TraceInfo> outList)
		{
			Init();
			if(typePointers.TryGetValue(typeGuid, out var pos))
			{
				stream.Position = pos;
				int methodsCount = reader.ReadInt32();
				for(int i = 0; i < methodsCount; i++)
				{
					uint startAddress = reader.ReadUInt32();
					if(startAddress <= udonPosition)
					{
						uint endAddress = reader.ReadUInt32();
						uint size = reader.ReadUInt32();
						if(udonPosition < endAddress)
						{
							int assembly = reader.ReadInt32();
							int module = reader.ReadInt32();
							int token = reader.ReadInt32();
							ReadMethodBlocks(assembly, module, token, udonPosition, outList);
							break;
						}
						else stream.Seek(size, SeekOrigin.Current);
					}
					else break;
				}
			}
		}

		public void Dispose()
		{
			stream.Dispose();
		}

		private void ReadMethodBlocks(int assemblyIndex, int moduleIndex, int methodToken, uint udonPosition, IList<TraceInfo> outList)
		{
			int lastIlOffset = 0;
			int blocksCount = reader.ReadInt32();
			for(int i = 0; i < blocksCount; i++)
			{
				uint startAddress = reader.ReadUInt32();
				if(udonPosition >= startAddress)
				{
					uint endAddress = reader.ReadUInt32();
					var blockType = reader.ReadByte();
					if(udonPosition < endAddress)
					{
						if(blockType == ADDRESS_BLOCK)
						{
							stream.Seek(sizeof(int), SeekOrigin.Current);
							uint size = reader.ReadUInt32();
							stream.Seek(sizeof(uint), SeekOrigin.Current);
							int il = reader.ReadInt32();
							for(uint j = 8; j < size; j += 8)
							{
								uint address = reader.ReadUInt32();
								if(udonPosition < address)
								{
									outList.Add(new TraceInfo(stringsList[assemblyIndex], stringsList[moduleIndex], methodToken, il));
									return;
								}
								il = reader.ReadInt32();
							}
						}
						else
						{
							outList.Add(new TraceInfo(stringsList[assemblyIndex], stringsList[moduleIndex], methodToken, lastIlOffset));
							stream.Seek(sizeof(uint), SeekOrigin.Current);
							int assembly = reader.ReadInt32();
							int module = reader.ReadInt32();
							int token = reader.ReadInt32();
							ReadMethodBlocks(assembly, module, token, udonPosition, outList);
							return;
						}
					}
					else
					{
						if(blockType == ADDRESS_BLOCK)
						{
							lastIlOffset = reader.ReadInt32();
						}
						uint size = reader.ReadUInt32();
						stream.Seek(size, SeekOrigin.Current);
					}
				}
				else break;
			}
			outList.Add(new TraceInfo(stringsList[assemblyIndex], stringsList[moduleIndex], methodToken, lastIlOffset));
		}

		private void Init()
		{
			if(stringsList != null) return;
			if(reader.ReadByte() != META_FILE_VERSION)
			{
				stringsList = new List<string>();
				typePointers = new Dictionary<Guid, long>();
				Debug.LogError("Invalid meta version");
				return;
			}
			long namesOffset = reader.ReadInt64();
			int typesCount = reader.ReadInt32();
			typePointers = new Dictionary<Guid, long>(typesCount);
			var guidBuffer = new byte[16];
			for(int i = 0; i < typesCount; i++)
			{
				stream.Read(guidBuffer, 0, 16);
				uint size = reader.ReadUInt32();
				var pos = stream.Position;
				typePointers.Add(new Guid(guidBuffer), pos);
				stream.Seek(size, SeekOrigin.Current);
			}

			stream.Seek(namesOffset, SeekOrigin.Begin);
			int stringsCount = reader.ReadInt32();
			stringsList = new List<string>(stringsCount);
			for(int i = 0; i < stringsCount; i++)
			{
				stringsList.Add(reader.ReadString());
			}
		}

		public struct TraceInfo
		{
			public string assemblyName;
			public string module;
			public int methodToken;
			public int ilOffset;

			public TraceInfo(string assemblyName, string module, int methodToken, int ilOffset)
			{
				this.assemblyName = assemblyName;
				this.module = module;
				this.methodToken = methodToken;
				this.ilOffset = ilOffset;
			}
		}
	}
}