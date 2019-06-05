using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Flwr.IO
{
	public class StreamDecorator : Stream
	{
		public StreamDecorator(Stream inner, object userState) {
			this.BaseStream = inner;
			this.UserState = userState;
		}

		public Stream BaseStream { get; }
		public object UserState { get; }

		public Action<StreamDecorator> OnClosed;

		public override bool CanRead => BaseStream.CanRead;
		public override bool CanSeek => BaseStream.CanSeek;
		public override bool CanWrite => BaseStream.CanWrite;
		public override long Length => BaseStream.Length;
		public override long Position {
			get => BaseStream.Position;
			set => BaseStream.Position = value;
		}

		public override void Flush() => BaseStream.Flush();
		public override int Read(byte[] buffer, int offset, int count) => BaseStream.Read(buffer, offset, count);
		public override long Seek(long offset, SeekOrigin origin) => BaseStream.Seek(offset, origin);
		public override void SetLength(long value) => BaseStream.SetLength(value);
		public override void Write(byte[] buffer, int offset, int count) => BaseStream.Write(buffer, offset, count);

		public override void Close() {
			BaseStream.Close();
			OnClosed?.Invoke(this);
		}
	}

	public interface IFileOutput
	{
		Stream Create(string path);
		IFileInput ToFileInput();
	}

	public interface IFileInput : IDisposable
	{
		IEnumerable<string> GetFiles();
		Stream OpenRead(string path);
	}

	public static class FileInputOutputExtensions
	{
		public static TextReader OpenText(this IFileInput self, string path) => new StreamReader(self.OpenRead(path));
		public static TextWriter CreateText(this IFileOutput self, string path) => new StreamWriter(self.Create(path));
	}

	class FileIO
	{
		readonly string rootPath;

		public FileIO(string rootPath)
		{
			this.rootPath = rootPath;
		}

		public IEnumerable<string> GetFiles() => Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories);

		public string GetFullPath(string path) {
			var fullPath = Path.GetFullPath(Path.Combine(rootPath, path));
			if (!fullPath.StartsWith(rootPath))
				throw new InvalidOperationException("Destination isn't below rootPath");
			return fullPath;
		}
	}

	public class FileSystemInput : IFileInput
	{
		readonly FileIO fs;

		internal FileSystemInput(FileIO fs) { this.fs = fs; }
		public FileSystemInput(string rootPath) : this(new FileIO(rootPath))
		{ }

		public IEnumerable<string> GetFiles() => fs.GetFiles();
		public Stream OpenRead(string path) => new FileStream(fs.GetFullPath(path),FileMode.Open, FileAccess.Read, FileShare.Read);

		void IDisposable.Dispose() { }
	}

	public class FileSystemOutput : IFileOutput
	{
		readonly FileIO fs;

		public FileSystemOutput(string rootPath) {
			this.fs = new FileIO(rootPath);
		}

		public Stream Create(string path) {
			var fullPath = fs.GetFullPath(path);
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
			return File.Create(fullPath);
		}

		public IFileInput ToFileInput() => new FileSystemInput(fs);
	}

	class MemoryFileInput : IFileInput
	{
		readonly Dictionary<string, byte[]> inputs = new Dictionary<string, byte[]>();

		internal void Add(string path, byte[] bytes) => inputs.Add(path, bytes);

		public IEnumerable<string> GetFiles() => inputs.Keys;
		public Stream OpenRead(string path) => new MemoryStream(inputs[path], writable: false);

		void IDisposable.Dispose() { inputs.Clear(); }
	}

	public class MemoryFileOutput : IFileOutput
	{
		readonly Dictionary<string, byte[]> outputs = new Dictionary<string, byte[]>();

		public IEnumerable<string> Outputs => outputs.Keys;
		public long TotalBytes => outputs.Sum(x => x.Value.LongLength);

		public Stream Create(string path) => new StreamDecorator(new MemoryStream(), path) {
			OnClosed = AddEntry,
		};

		void AddEntry(StreamDecorator self) =>
			outputs.Add(self.UserState.ToString(), ((MemoryStream)self.BaseStream).ToArray());

		public IFileInput ToFileInput() {
			var r = new MemoryFileInput();
			foreach(var item in outputs)
				r.Add(item.Key, item.Value);
			return r;
		}
	}

	public class ZipFileInput : IFileInput
	{
		readonly ZipArchive zip;

		public ZipFileInput(Stream stream) { this.zip = new ZipArchive(stream, ZipArchiveMode.Read); }

		public IEnumerable<string> GetFiles() => zip.Entries.Select(x => x.FullName);
		public Stream OpenRead(string path) => zip.GetEntry(path).Open();

		void IDisposable.Dispose() { zip.Dispose(); }
	}


	public class ZipFileOutput : IFileOutput
	{
		readonly Stream stream;
		ZipArchive zip;

		public ZipFileOutput() : this(new MemoryStream()) 
		{ }

		public ZipFileOutput(string path) : this(new FileStream(path, FileMode.Create, FileAccess.Write))
		{ }

		ZipFileOutput(Stream stream) {
			this.stream = stream;
			this.zip = new ZipArchive(stream, ZipArchiveMode.Create);
		}

		public CompressionLevel CompressionLevel;

		public Stream Create(string path) => new StreamDecorator(new MemoryStream(), path) {
			OnClosed = AddEntry,
		};

		void AddEntry(StreamDecorator self) {
			var src = (MemoryStream)self.BaseStream;
			var buffer = src.ToArray();
			var x = zip.CreateEntry(self.UserState.ToString(), CompressionLevel);
			using (var dst = x.Open())
				dst.Write(buffer, 0, buffer.Length);
		}

		public IFileInput ToFileInput() {
			if(zip != null) { 
				zip.Dispose();
				zip = null;
			}
			switch(stream) {
				default: throw new InvalidOperationException();
				case FileStream fs: return new ZipFileInput(new FileStream(fs.Name, FileMode.Open, FileAccess.Read, FileShare.Read));
				case MemoryStream ms: 
					var buffer = ms.ToArray();
					return new ZipFileInput(new MemoryStream(buffer, 0, buffer.Length, writable: false));
			}
		}
	}

	public class FileResult<T> : IFileInput
	{
		readonly IFileInput result;
		public FileResult(IFileInput result) {
			this.result = result;
		}

		public void Dispose() => result.Dispose();

		public IEnumerable<string> GetFiles() => result.GetFiles();
		public Stream OpenRead(string path) => result.OpenRead(path);
	}

	public static class FileResult
	{
		public static FileResult<T> Create<T>(IFileOutput output) => new FileResult<T>(output.ToFileInput());
		public static FileResult<T> Create<T>(IFileInput input) => new FileResult<T>(input);
	}
}
