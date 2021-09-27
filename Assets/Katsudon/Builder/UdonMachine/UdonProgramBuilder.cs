using System.Collections.Generic;
using System.Reflection;
using System.Text;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.UAssembly.Assembler;

namespace Katsudon.Builder
{
	public class UdonProgramBuilder : IComparer<IUAssemblyBlock>
	{
		private SortedSet<IUAssemblyBlock> blocks;

		public UdonProgramBuilder()
		{
			blocks = new SortedSet<IUAssemblyBlock>(this);
		}

		public void AddBlock<T>(T block) where T : class, IUAssemblyBlock
		{
			blocks.Add(block);
		}

		public IUdonProgram Build(StringBuilder cachedSb)
		{
			cachedSb.Clear();
			foreach(var block in blocks)
			{
				block.AppendCode(cachedSb);
				cachedSb.Append('\n');
			}
			var program = UdonEditorManager.Instance.Assemble(cachedSb.ToString());
			foreach(var block in blocks)
			{
				block.InitProgram(program);
			}
			return program;
		}

#if KATSUDON_ENABLE_DPC
		public IUdonProgram DirectProgramBuild()
		{
			var container = new UdonProgramContainer();
			foreach(var block in blocks)
			{
				block.DirectProgramBuild(container);
			}
			return container.ToProgram();
		}
#endif

		int IComparer<IUAssemblyBlock>.Compare(IUAssemblyBlock x, IUAssemblyBlock y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}

#if KATSUDON_ENABLE_DPC
		public class UdonProgramContainer
		{
			private static string identifier = (string)typeof(UAssemblyAssembler).GetField("INSTRUCTION_SET_IDENTIFIER",
				BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
			private static int version = (int)typeof(UAssemblyAssembler).GetField("INSTRUCTION_SET_VERSION",
				BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);

			public byte[] byteCode;
			public IUdonHeap heap;
			public IUdonSymbolTable entryPoints;
			public IUdonSymbolTable symbolTable;
			public IUdonSyncMetadataTable syncMetadataTable;
			public int updateOrder;

			public IUdonProgram ToProgram()
			{
				return new UdonProgram(identifier, version, byteCode, heap, entryPoints, symbolTable, syncMetadataTable, updateOrder);
			}
		}
#endif
	}

	public interface IUAssemblyBlock
	{
		int order { get; }

		void AppendCode(StringBuilder builder);

		void InitProgram(IUdonProgram program);

#if KATSUDON_ENABLE_DPC
		void DirectProgramBuild(UdonProgramBuilder.UdonProgramContainer container);
#endif
	}
}