using System;
using System.Collections.Generic;
using System.IO;
using Katsudon.Builder.Methods;

namespace Katsudon.Meta
{
	public class UdonAssembliesMetaWriter : IDisposable
	{
		private const byte META_FILE_VERSION = 0;

		private const byte ADDRESS_BLOCK = 0;
		private const byte METHOD_BLOCK = 1;

		private FileStream stream;
		private BinaryWriter writer;

		private List<string> strings = new List<string>();
		private Dictionary<string, int> string2Index = new Dictionary<string, int>();
		private int typesCounter = 0;

		public UdonAssembliesMetaWriter(FileStream stream)
		{
			this.stream = stream;
			this.writer = new BinaryWriter(stream);
			writer.Write(META_FILE_VERSION);
			writer.Write((long)0);//strings list offset
			writer.Write((int)0);//types count
		}

		public void WriteType(Guid guid, IReadOnlyList<UdonMethodMeta> sortedMeta)
		{
			writer.Write(guid.ToByteArray());
			var typeSizePos = stream.Position;
			writer.Write((uint)0);
			writer.Write((int)0);

			long tmpPos;
			int methodsCount = 0;
			int index = 0;
			while(index < sortedMeta.Count)
			{
				var meta = sortedMeta[index];
				writer.Write(meta.startAddress);
				writer.Write(meta.endAddress);
				var methodSizePos = stream.Position;
				writer.Write((uint)0);
				writer.Write(GetStringIndex(meta.assemblyName));
				writer.Write(GetStringIndex(meta.moduleName));
				writer.Write(meta.methodToken);
				WriteBlocks(ref index, sortedMeta);

				tmpPos = stream.Position;
				stream.Position = methodSizePos;
				writer.Write((uint)((tmpPos - methodSizePos) - sizeof(uint)));
				stream.Position = tmpPos;

				index++;
				methodsCount++;
			}
			tmpPos = stream.Position;
			stream.Position = typeSizePos;
			writer.Write((uint)((tmpPos - typeSizePos) - sizeof(uint)));
			writer.Write(methodsCount);
			stream.Position = tmpPos;

			typesCounter++;
			stream.Flush();
		}

		public void Flush()
		{
			long pos = stream.Position;
			stream.Position = 1;//strings list pointer position
			writer.Write(pos);
			writer.Write(typesCounter);
			stream.Position = pos;

			writer.Write(strings.Count);
			for(int i = 0; i < strings.Count; i++)
			{
				writer.Write(strings[i]);
			}

			stream.Flush(true);
		}

		public void Dispose()
		{
			stream.Dispose();
		}

		private void WriteBlocks(ref int metaIndex, IReadOnlyList<UdonMethodMeta> sortedMeta)
		{
			var blocksCountPos = stream.Position;
			writer.Write((int)0);
			var meta = sortedMeta[metaIndex];

			uint nextMethod = (metaIndex + 1 < sortedMeta.Count) ? sortedMeta[metaIndex + 1].startAddress : uint.MaxValue;
			uint offset = meta.startAddress;

			long tmpPos;
			bool blockStarted = false;
			long blockInfoPos = 0;

			int blocksCount = 0;
			int lastIlOffset = 0;
			foreach(var pointer in meta.pointers)
			{
				if(pointer.udonAddress < nextMethod)
				{
					if(!blockStarted)
					{
						blocksCount++;
						blockStarted = true;
						writer.Write(pointer.udonAddress);
						blockInfoPos = stream.Position;
						stream.Seek(sizeof(uint) + sizeof(byte) + sizeof(int) + sizeof(uint), SeekOrigin.Current);
					}
					writer.Write(pointer.udonAddress);
					writer.Write(pointer.ilOffset);
					lastIlOffset = pointer.ilOffset;
				}
				else
				{
					metaIndex++;
					blocksCount++;
					var innerMeta = sortedMeta[metaIndex];
					if(blockStarted)
					{
						blockStarted = false;
						tmpPos = stream.Position;
						stream.Position = blockInfoPos;
						writer.Write(innerMeta.startAddress);
						writer.Write((byte)ADDRESS_BLOCK);
						writer.Write(lastIlOffset);
						writer.Write((uint)((tmpPos - blockInfoPos) - (sizeof(uint) + sizeof(byte) + sizeof(int) + sizeof(uint))));
						stream.Position = tmpPos;
					}
					writer.Write(innerMeta.startAddress);
					writer.Write(innerMeta.endAddress);
					writer.Write((byte)METHOD_BLOCK);
					var sizePos = stream.Position;
					writer.Write((uint)0);
					writer.Write(GetStringIndex(innerMeta.assemblyName));
					writer.Write(GetStringIndex(innerMeta.moduleName));
					writer.Write(innerMeta.methodToken);
					WriteBlocks(ref metaIndex, sortedMeta);

					tmpPos = stream.Position;
					stream.Position = sizePos;
					writer.Write((uint)((tmpPos - sizePos) - sizeof(uint)));
					stream.Position = tmpPos;

					nextMethod = (metaIndex + 1 < sortedMeta.Count) ? sortedMeta[metaIndex + 1].startAddress : uint.MaxValue;
					offset = innerMeta.endAddress;
				}
			}

			if(blockStarted)
			{
				tmpPos = stream.Position;
				stream.Position = blockInfoPos;
				writer.Write(meta.endAddress);
				writer.Write((byte)ADDRESS_BLOCK);
				writer.Write(lastIlOffset);
				writer.Write((uint)((tmpPos - blockInfoPos) - (sizeof(uint) + sizeof(byte) + sizeof(int) + sizeof(uint))));
				stream.Position = tmpPos;
			}

			tmpPos = stream.Position;
			stream.Position = blocksCountPos;
			writer.Write(blocksCount);
			stream.Position = tmpPos;
		}

		private int GetStringIndex(string name)
		{
			if(string2Index.TryGetValue(name, out var index)) return index;
			index = strings.Count;
			strings.Add(name);
			string2Index[name] = index;
			return index;
		}
	}
}