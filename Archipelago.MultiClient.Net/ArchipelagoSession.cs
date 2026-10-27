using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using System;
using System.Threading;

#if !NET35
using System.Threading.Tasks;
#endif

namespace Archipelago.MultiClient.Net
{
	/// <summary>
	/// `ArchipelagoSession` is the overarching class to access and modify and respond to the multiworld state
	/// </summary>
	public interface IArchipelagoSession : IArchipelagoSessionActions
	{
		/// <summary>
		/// Provides access to the underlying websocket, so send or receive messages
		/// </summary>
		IArchipelagoSocketHelper Socket { get; }

		/// <summary>
		/// Provides simplified methods to receive items
		/// </summary>
		IReceivedItemsHelper Items { get; }

		/// <summary>
		/// Provides way to mark locations as checked
		/// </summary>
		ILocationCheckHelper Locations { get; }

		/// <summary>
		/// Provides information about all players in the multiworld
		/// </summary>
		IPlayerHelper Players { get; }

		/// <summary>
		/// Provides access to a server side data storage to share and store values across sessions and players
		/// </summary>
		IDataStorageHelper DataStorage { get; }

		/// <summary>
		/// Provides information about your current connection
		/// </summary>
		IConnectionInfoProvider ConnectionInfo { get; }

		/// <summary>
		/// Provides information about the current state of the server
		/// </summary>
		IRoomStateHelper RoomState { get; }

		/// <summary>
		/// Provides simplified handlers to receive messages about events that happen in the multiworld
		/// </summary>
		IMessageLogHelper MessageLog { get; }

		/// <summary>
		/// Provides methods to interact with the hints system
		/// </summary>
		IHintsHelper Hints { get; }

#if !NET35
		/// <summary>
		/// Connect the websocket to the server
		/// Will wait a few seconds to retrieve the RoomInfoPacket, if the request timesout the task will be canceled
		/// </summary>
		/// <returns>The roominfo send from the server</returns>
		Task<RoomInfoPacket> ConnectAsync();

		/// <summary>
		///     Attempt to log in to the Archipelago server by opening a websocket connection and sending a Connect packet.
		///     Determining success for this attempt is done by attaching a listener to Socket.PacketReceived and listening for a Connected packet.
		/// </summary>
		/// <param name="game">The game this client is playing.</param>
		/// <param name="name">The slot name of this client.</param>
		/// <param name="version">The minimum AP protocol version this client supports.</param>
		/// <param name="itemsHandlingFlags">Informs the AP server how you want ReceivedItem packets to be sent to you.</param>
		/// <param name="tags">The tags this client supports.</param>
		/// <param name="uuid">The uuid of this client.</param>
		/// <param name="password">The password to connect to this AP room.</param>
		/// <param name="requestSlotData">Decides if the <see cref="LoginSuccessful"/> result will contain any slot data</param>
		/// <returns>
		///     <see cref="T:Archipelago.MultiClient.Net.LoginSuccessful"/> if the connection is succeeded and the server accepted the login attempt.
		///     <see cref="T:Archipelago.MultiClient.Net.LoginFailure"/> if the connection to the server failed in some way.
		/// </returns>
		/// <remarks>
		///     Most connections are instantaneous, however if the server doesn't respond for 4 seconds the result is a <see cref="T:Archipelago.MultiClient.Net.LoginFailure"/>.
		///     Time spent processing what the server already sent (e.g. loading a large data package from the cache) doesn't count towards those 4 seconds.
		/// </remarks>
		Task<LoginResult> LoginAsync(string game, string name, ItemsHandlingFlags itemsHandlingFlags,
			Version version = null, string[] tags = null, string uuid = null, string password = null, bool requestSlotData = true);
#endif

		/// <summary>
		///     Attempt to log in to the Archipelago server by opening a websocket connection and sending a Connect packet.
		///     Determining success for this attempt is done by attaching a listener to Socket.PacketReceived and listening for a Connected packet.
		/// </summary>
		/// <param name="game">The game this client is playing.</param>
		/// <param name="name">The slot name of this client.</param>
		/// <param name="version">The minimum AP protocol version this client supports.</param>
		/// <param name="itemsHandlingFlags">Informs the AP server how you want ReceivedItem packets to be sent to you.</param>
		/// <param name="tags">The tags this client supports.</param>
		/// <param name="uuid">The uuid of this client.</param>
		/// <param name="password">The password to connect to this AP room.</param>
		/// <param name="requestSlotData">Decides if the <see cref="LoginSuccessful"/> result will contain any slot data</param>
		/// <returns>
		///     <see cref="T:Archipelago.MultiClient.Net.LoginSuccessful"/> if the connection is succeeded and the server accepted the login attempt.
		///     <see cref="T:Archipelago.MultiClient.Net.LoginFailure"/> if the connection to the server failed in some way.
		/// </returns>
		/// <remarks>
		///     Most connections are instantaneous, however if the server doesn't respond for 4 seconds the result is a <see cref="T:Archipelago.MultiClient.Net.LoginFailure"/>.
		///     Time spent processing what the server already sent (e.g. loading a large data package from the cache) doesn't count towards those 4 seconds.
		/// </remarks>
		LoginResult TryConnectAndLogin(string game, string name, ItemsHandlingFlags itemsHandlingFlags, 
			Version version = null, string[] tags = null, string uuid = null, string password = null, bool requestSlotData = true);
	}

	/// <summary>
	/// `ArchipelagoSession` is the overarching class to access and modify and respond to the multiworld state
	/// </summary>
	public partial class ArchipelagoSession : IArchipelagoSession
    {
        internal const int ArchipelagoConnectionTimeoutInSeconds = 4;

		/// <inheritdoc/>
		public IArchipelagoSocketHelper Socket { get; }

		/// <inheritdoc/>
		public IReceivedItemsHelper Items { get; }

		/// <inheritdoc/>
		public ILocationCheckHelper Locations { get; }

		/// <inheritdoc/>
		public IPlayerHelper Players { get; }

		/// <inheritdoc/>
		public IDataStorageHelper DataStorage { get; }

		/// <inheritdoc/>
		public IConnectionInfoProvider ConnectionInfo => connectionInfo;
		ConnectionInfoHelper connectionInfo;

		/// <inheritdoc/>
		public IRoomStateHelper RoomState { get; }

		/// <inheritdoc/>
		public IMessageLogHelper MessageLog { get; }

		/// <inheritdoc/>
		public IHintsHelper Hints { get; }

#if NET35
	    volatile bool awaitingRoomInfo;
		volatile bool expectingLoginResult;
        LoginResult loginResult;
#else
        TaskCompletionSource<LoginResult> loginResultTask = new TaskCompletionSource<LoginResult>();
        TaskCompletionSource<RoomInfoPacket> roomInfoPacketTask = new TaskCompletionSource<RoomInfoPacket>();
#endif
		readonly ServerResponseTimeout serverResponseTimeout;

		internal ArchipelagoSession(IArchipelagoSocketHelper socket,
									IReceivedItemsHelper items,
                                    ILocationCheckHelper locations,
                                    IPlayerHelper players,
                                    IRoomStateHelper roomState,
                                    ConnectionInfoHelper connectionInfoHelper,
                                    IDataStorageHelper dataStorage,
                                    IMessageLogHelper messageLog,
                                    IHintsHelper createHints,
                                    ServerResponseTimeout serverResponseTimeout = null)
        {
			// Only excludes the processing time of the other helpers if it was created before them, see ArchipelagoSessionFactory
			this.serverResponseTimeout = serverResponseTimeout
				?? new ServerResponseTimeout(socket, ArchipelagoConnectionTimeoutInSeconds);

            Socket = socket;
            Items = items;
            Locations = locations;
            Players = players;
            RoomState = roomState;
            connectionInfo = connectionInfoHelper;
            DataStorage = dataStorage;
            MessageLog = messageLog;
            Hints = createHints;
            
            socket.PacketReceived += Socket_PacketReceived;
			// if a packet handler throws, Socket_PacketReceived won't run for that packet, so resume waiting on the server here instead
			socket.ErrorReceived += (e, message) => this.serverResponseTimeout.Resume();
        }
		
        void Socket_PacketReceived(ArchipelagoPacketBase packet)
        {
            switch (packet)
            {
                case ConnectedPacket _:
	            case ConnectionRefusedPacket _:
					serverResponseTimeout.Stop();
#if NET35
					if (expectingLoginResult)
                    {
                        expectingLoginResult = false;
                        loginResult = LoginResult.FromPacket(packet);
                    }
                    break;
#else
                    loginResultTask.TrySetResult(LoginResult.FromPacket(packet));
                    break;
#endif
                // ReSharper disable once UnusedVariable
                case RoomInfoPacket roomInfoPacket:
					serverResponseTimeout.Stop();
#if NET35
					awaitingRoomInfo = false;
#else
					roomInfoPacketTask.TrySetResult(roomInfoPacket);
#endif
					break;
            }

			// this handler is subscribed last, so the client is now done processing this packet and is waiting on the server again
			serverResponseTimeout.Resume();
        }

 #if !NET35
	    /// <inheritdoc/>
		public Task<RoomInfoPacket> ConnectAsync()
        {
            roomInfoPacketTask = new TaskCompletionSource<RoomInfoPacket>();

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var task = Socket.ConnectAsync();
                    task.Wait(TimeSpan.FromSeconds(ArchipelagoConnectionTimeoutInSeconds));

					if (!task.IsCompleted)
                        roomInfoPacketTask.TrySetCanceled();
                }
                catch (AggregateException)
                {
                    roomInfoPacketTask.TrySetCanceled();
                }
            });

            return roomInfoPacketTask.Task;
        }

	    /// <inheritdoc/>
		public Task<LoginResult> LoginAsync(string game, string name, ItemsHandlingFlags itemsHandlingFlags,
            Version version = null, string[] tags = null, string uuid = null, string password = null, bool requestSlotData = true)
        {
	        loginResultTask = new TaskCompletionSource<LoginResult>();

			if (!roomInfoPacketTask.Task.IsCompleted)
            {
                loginResultTask.TrySetResult(new LoginFailure("You are not connected, run ConnectAsync() first"));
                return loginResultTask.Task;
            }

			connectionInfo.SetConnectionParameters(game, tags, itemsHandlingFlags, uuid);

			// started before sending, so a fast reply can't arrive before there's a wait for it to stop
			var loginResultSource = loginResultTask;
			serverResponseTimeout.Start(() => loginResultSource.TrySetResult(new LoginFailure("Connection timed out.")));

            try
            {
	            Socket.SendPacket(BuildConnectPacket(name, password, version, requestSlotData));
            }
            catch (ArchipelagoSocketClosedException)
            {
	            serverResponseTimeout.Stop();
                loginResultTask.TrySetResult(new LoginFailure("You are not connected, run ConnectAsync() first"));
                return loginResultTask.Task;
            }

            return loginResultTask.Task;
        }
#endif

	    /// <inheritdoc/>
		public LoginResult TryConnectAndLogin(string game, string name, ItemsHandlingFlags itemsHandlingFlags, 
	        Version version = null, string[] tags = null, string uuid = null, string password = null, bool requestSlotData = true)
        {
#if NET35
            connectionInfo.SetConnectionParameters(game, tags, itemsHandlingFlags, uuid);

            try
            {
				awaitingRoomInfo = true;
                expectingLoginResult = true;
                loginResult = null;

                var timedOut = false;
                serverResponseTimeout.Start(() => timedOut = true);

                Socket.Connect();

				while (awaitingRoomInfo)
				{
					if (timedOut)
					{
						Socket.Disconnect();

						return new LoginFailure("Connection timed out.");
					}

					Thread.Sleep(25);
				}

				serverResponseTimeout.Start(() => timedOut = true);
				Socket.SendPacket(BuildConnectPacket(name, password, version, requestSlotData));

                while (expectingLoginResult)
                {
                    if (timedOut)
                    {
                        Socket.Disconnect();

                        return new LoginFailure("Connection timed out.");
                    }

                    Thread.Sleep(25);
                }

                //give other handlers time to handle the ConnectedPacket so all values are available when this method returns
                Thread.Sleep(50);

                return loginResult;
            }
            catch (ArchipelagoSocketClosedException)
            {
	            serverResponseTimeout.Stop();
                return new LoginFailure("Socket closed unexpectedly.");
            }
#else
            var task = ConnectAsync();

            var roomInfoSource = roomInfoPacketTask;
            serverResponseTimeout.Start(() => roomInfoSource.TrySetCanceled());

            try
            {
	            // no fixed timeout here: serverResponseTimeout cancels the task, but only counts time spent waiting on the server
	            task.Wait();
            }
            catch (AggregateException e)
            {
	            if (e.GetBaseException() is OperationCanceledException)
		            return new LoginFailure("Connection timed out.");

	            return new LoginFailure(e.GetBaseException().Message);
            }
            finally
            {
	            serverResponseTimeout.Stop();
            }

			return LoginAsync(game, name, itemsHandlingFlags, version, tags, uuid, password, requestSlotData).Result;
#endif
        }

        ConnectPacket BuildConnectPacket(string name, string password, Version version, bool requestSlotData) =>
	        new ConnectPacket {
		        Game = ConnectionInfo.Game,
		        Name = name,
		        Password = password,
		        Tags = ConnectionInfo.Tags,
		        Uuid = ConnectionInfo.Uuid,
		        Version = version != null ? new NetworkVersion(version) : new NetworkVersion(0, 6, 0),
		        ItemsHandling = ConnectionInfo.ItemsHandlingFlags,
				RequestSlotData = requestSlotData
			};
    }
}
