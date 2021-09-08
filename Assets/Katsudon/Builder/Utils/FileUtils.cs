using System.IO;

namespace Katsudon.Utility
{
	public static class FileUtils
	{
		public static BinaryReader TryGetFileReader(string filename)
		{
			var path = GetFilePath(filename, true);
			if(!File.Exists(path)) return null;

			var file = File.Open(path, FileMode.Open, FileAccess.Read);
			if(file == null) return null;

			return new BinaryReader(file);
		}

		public static BinaryWriter GetFileWriter(string filename)
		{
			var stream = File.Open(GetFilePath(filename, true), FileMode.OpenOrCreate, FileAccess.Write);
			stream.SetLength(0);
			return new BinaryWriter(stream);
		}

		public static void DeleteFile(string filename)
		{
			string path = Path.Combine(LibraryDirectory(), filename);
			if(File.Exists(path)) File.Delete(path);
		}

		private static string GetFilePath(string filename, bool makedir)
		{
			string dir = LibraryDirectory();
			string path = Path.Combine(dir, filename);
			if(!File.Exists(path))
			{
				if(!makedir) return path;
				if(!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			}
			return path;
		}

		private static string LibraryDirectory()
		{
			return Path.Combine(Directory.GetCurrentDirectory(), "Library/Katsudon");
		}
	}
}