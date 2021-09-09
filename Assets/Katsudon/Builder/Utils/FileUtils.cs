using System.IO;

namespace Katsudon.Utility
{
	public static class FileUtils
	{
		public static BinaryReader TryGetFileReader(string filename)
		{
			var stream = GetReadStream(filename);
			if(stream == null) return null;
			return new BinaryReader(stream);
		}

		public static FileStream GetReadStream(string filename)
		{
			var path = GetFilePath(filename, true);
			if(!File.Exists(path)) return null;

			var stream = File.Open(path, FileMode.Open, FileAccess.Read);
			if(stream == null) return null;

			return stream;
		}

		public static BinaryWriter GetFileWriter(string filename)
		{
			return new BinaryWriter(GetWriteStream(filename));
		}

		public static FileStream GetWriteStream(string filename)
		{
			var stream = File.Open(GetFilePath(filename, true), FileMode.OpenOrCreate, FileAccess.ReadWrite);
			stream.SetLength(0);
			return stream;
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