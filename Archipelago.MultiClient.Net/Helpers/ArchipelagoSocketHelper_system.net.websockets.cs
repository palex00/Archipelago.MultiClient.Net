#if NET45 || NETSTANDARD2_0 || NET6_0
using Archipelago.MultiClient.Net.Extensions;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

#if NET45
using System.Net;
#endif

namespace Archipelago.MultiClient.Net.Helpers
{
    public class ArchipelagoSocketHelper : BaseArchipelagoSocketHelper<ClientWebSocket>, IArchipelagoSocketHelper
    {
	    /// <summary>
	    ///     The URL of the host that the socket is connected to.
	    /// </summary>
	    public Uri Uri { get; }

		internal ArchipelagoSocketHelper(Uri hostUri) : base(CreateWebSocket())
        {
            Uri = hostUri;

#if NET45
			//this is done on constructor, rather than static constructor override any value set anywhere else in the process
	        var Tls13 = (SecurityProtocolType)12288;
	        System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | Tls13;
#endif
        }

        static ClientWebSocket CreateWebSocket()
        {
	        var clientWebSocket = new ClientWebSocket();

#if NET6_0
			clientWebSocket.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
#endif

	        return clientWebSocket;
        }

		/// <summary>
		///     Initiates a connection to the host asynchronously.
		///     Handle the <see cref="SocketOpened"/> event to add a callback.
		/// </summary>
		public async Task ConnectAsync()
        {
			await ConnectToProvidedUri(Uri);

            StartPolling();
        }

        async Task ConnectToProvidedUri(Uri uri)
        {
	        if (uri.Scheme != "unspecified")
	        {
		        try
		        {
			        await Socket.ConnectAsync(uri, CancellationToken.None);
		        }
		        catch (Exception e)
		        {
			        OnError(e);
			        throw;
		        }
			}
			else
			{
				var errors = new List<Exception>(0);
				try
				{
					ConnectionDiagnostics.Log($"socket: trying {uri.AsWss()}");
					await Socket.ConnectAsync(uri.AsWss(), CancellationToken.None);
					ConnectionDiagnostics.Log($"socket: wss state {Socket.State}");

					if (Socket.State == WebSocketState.Open)
						return;
				}
				catch(Exception e)
				{
					ConnectionDiagnostics.Log($"socket: wss failed: {e.GetBaseException().Message}");
					errors.Add(e);
					Socket = CreateWebSocket();
				}

				try
				{
					ConnectionDiagnostics.Log($"socket: trying {uri.AsWs()}");
					await Socket.ConnectAsync(uri.AsWs(), CancellationToken.None);
					ConnectionDiagnostics.Log($"socket: ws state {Socket.State}");
				}
				catch (Exception e)
				{
					ConnectionDiagnostics.Log($"socket: ws failed: {e.GetBaseException().Message}");
					errors.Add(e);

					OnError(new AggregateException(errors));

					throw;
				}
			}
		}
    }
}
#endif