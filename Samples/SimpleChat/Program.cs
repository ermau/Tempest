using System;
using System.Collections.Generic;
using System.Net;
using Mono.Options;
using Tempest.Providers.Network;

namespace Tempest.Samples.SimpleChat
{
	public class Program
	{
		public static readonly Protocol ChatProtocol = new Protocol (2, 1);

		private static ChatServer server;
		private static ChatClient client;

		static void Main (string[] args)
		{
			ChatProtocol.Register (new []
			{
				new KeyValuePair<Type, Func<Message>> (typeof(ChatMessage), () => new ChatMessage()),
				new KeyValuePair<Type, Func<Message>> (typeof(SetNameMessage), () => new SetNameMessage()), 
			});

			bool runServer = false;
			string clientHost = null;

			var options = new OptionSet
			{
				{ "c=", v => clientHost = v },
				{ "s", v => runServer = true }
			};
			options.Parse (args);

			if (runServer)
			{
				server = new ChatServer (new NetworkConnectionProvider
					(ChatProtocol, new IPEndPoint (IPAddress.Any, 42900), 100));
			}

			if (clientHost != null)
			{
				client = new ChatClient (new NetworkClientConnection (ChatProtocol), Console.Out);
				client.Connected += (s, e) => Console.WriteLine ("Connected");
				client.Disconnected += (s, e) =>
				{
					switch (e.Reason)
					{
						case DisconnectedReason.ConnectionFailed:
							Console.WriteLine ("Failed to connect");
							break;

						default:
							Console.WriteLine ("Disconnected");
							break;
					}

					Environment.Exit (2);
				};

				client.Connect (new DnsEndPoint (clientHost, 42900));
			}

			string name = null;
			while (true)
			{
				string line = Console.ReadLine();
				if (line == null || line.StartsWith ("/quit") || line.StartsWith ("/exit"))
				{
					if (client != null)
						client.Disconnect (true);

					Environment.Exit (0);
				}

				if (client != null)
				{
					if (line.StartsWith ("/nick ") && line.Length > 6)
					{
						name = line.Substring (6);
						client.SetName (name);
					}
					else if (!line.StartsWith ("/"))
						client.SendMessage (line);
				}
			}
		}
	}
}