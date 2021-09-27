using System.Collections.Generic;
using System.Text;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;

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

		int IComparer<IUAssemblyBlock>.Compare(IUAssemblyBlock x, IUAssemblyBlock y)
		{
			if(x.order == y.order) return x == y ? 0 : -1;
			return x.order.CompareTo(y.order);
		}
	}

	public interface IUAssemblyBlock
	{
		int order { get; }

		void AppendCode(StringBuilder builder);

		void InitProgram(IUdonProgram program);
	}
}