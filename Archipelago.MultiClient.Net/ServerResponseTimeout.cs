using Archipelago.MultiClient.Net.Helpers;
using System;
using System.Threading;

namespace Archipelago.MultiClient.Net
{
	/// <summary>
	/// Times out a wait on the server, counting only the time the client spends waiting on the server.
	/// The clock is paused while a received packet is being handled (e.g. while the data package is loaded from the cache),
	/// and restarts from zero once the client is done with it, so slow local processing of large rooms isn't
	/// mistaken for an unresponsive server.
	/// </summary>
	class ServerResponseTimeout
	{
		readonly TimeSpan timeout;
		readonly object padlock = new object();

		Action onTimeout;
		Timer timer;
		// invalidates the callback of a timer that was paused or stopped after it already started firing
		int generation;
		// tracked even when nothing is waiting, so a wait started while a packet is being processed only starts counting afterwards
		bool processingPacket;

		/// <summary>
		/// Create this before any other subscriber to <see cref="IArchipelagoSocketHelper.PacketReceived"/>,
		/// so the clock is paused before any of them starts processing a packet.
		/// Whoever subscribes last should call <see cref="Resume"/> once it's done with a packet.
		/// </summary>
		internal ServerResponseTimeout(IArchipelagoSocketHelper socket, int timeoutInSeconds)
		{
			timeout = TimeSpan.FromSeconds(timeoutInSeconds);

			socket.PacketReceived += _ => Pause();
		}

		/// <summary>
		/// Starts waiting on the server, replacing any previous wait. <paramref name="timeoutAction"/> is invoked if the server takes too long.
		/// </summary>
		public void Start(Action timeoutAction)
		{
			lock (padlock)
			{
				onTimeout = timeoutAction;

				if (processingPacket)
					Disarm();
				else
					Arm();
			}
		}

		public void Stop()
		{
			lock (padlock)
			{
				onTimeout = null;
				Disarm();
			}
		}

		public void Pause()
		{
			lock (padlock)
			{
				processingPacket = true;
				Disarm();
			}
		}

		public void Resume()
		{
			lock (padlock)
			{
				processingPacket = false;

				if (onTimeout != null && timer == null)
					Arm();
			}
		}

		void Arm()
		{
			Disarm();

			var armedGeneration = generation;
			timer = new Timer(_ => OnTimerElapsed(armedGeneration), null, timeout, TimeSpan.FromMilliseconds(-1));
		}

		void Disarm()
		{
			generation++;

			if (timer == null)
				return;

			timer.Dispose();
			timer = null;
		}

		void OnTimerElapsed(int armedGeneration)
		{
			Action timeoutAction;

			lock (padlock)
			{
				if (armedGeneration != generation || onTimeout == null)
					return;

				timeoutAction = onTimeout;
				onTimeout = null;
				Disarm();
			}

			timeoutAction();
		}
	}
}
