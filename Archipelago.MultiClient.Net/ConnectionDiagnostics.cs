using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Archipelago.MultiClient.Net
{
	/// <summary>
	/// Temporary diagnostics: appends timestamped lines about the connection process to
	/// %LOCALAPPDATA%/Archipelago/mcnet-diagnostics.log (next to the data package cache)
	/// </summary>
	static class ConnectionDiagnostics
	{
		static readonly object padlock = new object();
		static readonly Stopwatch clock = Stopwatch.StartNew();
		static readonly string logFile = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			Path.Combine("Archipelago", "mcnet-diagnostics.log"));

		public static void Log(string message)
		{
			try
			{
				var line = $"{DateTime.Now:HH:mm:ss.fff} +{clock.ElapsedMilliseconds,7}ms [thread {Thread.CurrentThread.ManagedThreadId,3}] {message}{Environment.NewLine}";

				lock (padlock)
				{
					Directory.CreateDirectory(Path.GetDirectoryName(logFile));
					File.AppendAllText(logFile, line);
				}
			}
			catch
			{
				// diagnostics must never break the client
			}
		}

		public static long ElapsedMilliseconds => clock.ElapsedMilliseconds;

		public static string ThreadPoolState()
		{
			ThreadPool.GetMinThreads(out var minWorker, out var minIo);
			ThreadPool.GetMaxThreads(out var maxWorker, out var maxIo);
			ThreadPool.GetAvailableThreads(out var availableWorker, out var availableIo);

			return $"thread pool: worker {maxWorker - availableWorker} busy (min {minWorker}), io {maxIo - availableIo} busy (min {minIo})";
		}
	}
}
