# Tempest
Tempest is a simple library for sending and receiving messages across
any number of transports and dealing with them in a uniform manner. For
example, you could receive the same message from a HTTP POST and from a
TCP socket and from your perspective there would be no difference in
handling it.

**Features:**

 - Simple protocol definition
 - Encryption
 - Message signing
 - Multiple message transports (raw tcp, http, etc.) feeding to the same
   message handlers
 - Supports multiple Tempest protocols over a single connection

## Getting Started

### Protocol

The first thing we'll want to do is to define our protocol. We'll define
a simple protocol that will simply broadcast a message to everyone else
that's connected. We're only going to want reliable messages here,
which we'll keep in mind for later.

As Tempest supports multiple protocols over a single connection, we'll
give our protocol a unique identifier. `1` is reserved for Tempest, so
we'll use `2`.

```csharp
static class SimpleChatProtocol
{
	public static Protocol Instance = new Protocol (2);

	static SimpleChatProtocol()
	{
		// We need to tell our protocol about all the message
		// types belonging to it. Discover() does this automatically.
		Instance.Discover();
	}
}
```

Each message type needs a unique identifier. Any `ushort` value will do, but
personally I like to put the values in an enum to make things easier:

```csharp
public enum SimpleChatMessageType
	: ushort
{
	ChatMessage = 1
}
```

We define messages by subclassing from `Message` and we'll need to tell it
which `Protocol` it belongs to and its message identifier. To take care of
this, I like to provide a small subclass of `Message` for the protocol:

```csharp
public abstract class SimpleChatMessage
	: Message
{
	protected SimpleChatMessage (SimpleChatMessageType type)
		: base (SimpleChatProtocol.Instance, (ushort)type)
	{
	}
}
```

Now to define our actual chat message. To define a message type, you'll
override `WritePayload` and `ReadPayload`. These methods pass in a
`IValueWriter` and `IValueReader` respectively, which you'll use to
serialize and deserialize your message.

```csharp
public sealed class ChatMessage
	: SimpleChatMessage
{
	public string Message
	{
		get;
		set;
	}

	public override void WritePayload (ISerializationContext context, IValueWriter writer)
	{
		writer.WriteString (Message);
	}

	public override void ReadPayload (ISerializationContext context, IValueReader reader)
	{
		Message = reader.ReadString();
	}
}
```

And that's it for defining the protocol. **Note:** When using a platform where
`System.Reflection.Emit` is unavailable, such as iOS or Windows Phone, you
will need to use `Protocol.Register` and pass in your messages manually
as `Protocol.Discover` is unavailable.

### Client

For most applications, Tempest has a built in `TempestClient` class you can use
to handle the fundamentals. We'll subclass to provide an easy method to send
a chat message and add a listener for other's chat messages:

```csharp
public sealed class ChatClient
	: TempestClient
{
	// We need to tell TempestClient what kinds of messages we're dealing
	// with. Earlier we decided that we only need Reliable messages here.
	public ChatClient (IClientConnection connection)
		: base (connection, MessageTypes.Reliable)	
	{
		// Here we setup a handler for any `ChatMessage`s that come through.
		this.RegisterMessageHandler<ChatMessage> (OnChatMessage);
	}

	// A simple event for brevity
	public event Action<string> ChatReceived;

	public Task SendChatAsync (string message)
	{
		var msg = new ChatMessage { Message = message };
		return Connection.SendAsync (msg);
	}

	private void OnChatMessage (MessageEventArgs<ChatMessage> e)
	{
		ChatMessage msg = e.Message;
		
		var received = ChatReceived;
		if (received != null)
			received (msg.Message);
	}
}
```

To connect to a server, we'll need to pick a transport. We'll use the built in network
transport.

```csharp
var connection = new Tempest.Providers.Network.NetworkClientConnection (SimpleChatProtocol.Instance);

var client = new ChatClient (connection);
await client.ConnectAsync (new Target ("hostname", port));
```

### Server

For the server side of things, we also have a `TempestServer` class you can use
to handle the fundamentals. We'll need to keep track of connections ourselves,
where later on we may want to associate them with user data. So we'll subclass
and add our connection list and a handler for `ChatMessage`:

```csharp
public sealed class ChatServer
	: TempestServer
{
	public ChatServer (IConnectionProvider provider)
		: base (provider, MessageTypes.Reliable)
	{
		this.RegisterMessageHandler<ChatMessage> (OnChatMessage);
	}

	private readonly List<IConnection> connections = new List<IConnection>();
	private void OnChatMessage (MessageEventArgs<ChatMessage> e)
	{
		ChatMessage msg = e.Message;

		// Messages come in on various threads, we'll need to make
		// sure we stay thread safe.
		lock (this.connections) {
			foreach (IConnection connection in this.connections)
				connection.SendAsync (e.Message);
		}
	}

	protected override void OnConnectionMade (object sender, ConnectionMadeEventArgs e)
	{
		lock (this.connections)
			this.connections.Add (e.Connection);

		base.OnConnectionMade (sender, e);
	}

	protected override void OnConnectionDisconnected (object sender, DisconnectedEventArgs e)
	{
		lock (this.connections)
			this.connections.Remove (e.Connection);

		base.OnConnectionDisconnected (sender, e);
	}
}
```

To start the server, we'll need to provide a transport mechanism. You can add multiple
transports to listen to, but for now we'll just add a network connection provider as we've
already told the client to connect with it:

```
// NetworkConnectionProvider requires that you tell it what local target to listen
// to and the maximum number of connections you'll allow.
var provider = new Tempest.Providers.Network.NetworkConnectionProvider (SimpleChatProtocol.Instance, Target.AnyIP, 100);

var server = new ChatServer (provider);
server.Start();
```

### Transports
There are currently two available transports:

 - TCP
   - Supports reliable messaging
   - Supports encryption and signing
   - `NetworkClientConnection` for client connections
   - `NetworkConnectionProvider` for connection listeners
 - UDP
   - _Experimental_
   - Supports reliable and unreliable messaging
   - Supports encryption and signing
   - `UdpClientConnection` for client connections
   - `UdpConnectionProvider` for connection listeners

