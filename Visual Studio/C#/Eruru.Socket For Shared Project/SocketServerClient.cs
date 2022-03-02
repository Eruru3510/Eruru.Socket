namespace Eruru.Socket {

	public class SocketServerClient : SocketClientBase {

		public SocketServer Server {

			get {
				return _Server;
			}

		}
		public SocketServerClientReceivedEventHandler OnReceived { get; set; }
		public SocketServerClientSendEventHandler OnSend { get; set; }
		public SocketServerClientDisconnectedEventHandler OnDisconnected { get; set; }

		readonly SocketServer _Server;

		internal SocketServerClient (SocketServer server) : base (server.BufferSize) {
			_Server = server;
			server.BufferManager.SetBuffer (ReceiveSocketAsyncEventArgs);
			server.BufferManager.SetBuffer (SendSocketAsyncEventArgs);
		}

		internal void Initialize (System.Net.Sockets.Socket socket) {
			EventLock.Enter ();
			try {
				Socket = socket;
				_ProtocolType = SocketProtocolType.Unknown;
				ConnectCompleted ();
			} finally {
				EventLock.Exit ();
			}
		}

		internal void DetermineProtocolType () {
			EventLock.Enter ();
			try {
				ReceiveAsync ();
				EventLock.WaitOne ("Protocol");
			} finally {
				EventLock.Exit ();
			}
		}

		protected override void PerformReceived (byte[] bytes) {
			if (OnReceived != null) {
				OnReceived (this, bytes);
			}
		}

		protected override void PerformSend (byte[] bytes) {
			if (OnSend != null) {
				OnSend (this, bytes);
			}
		}

		protected override void PerformDisconnected () {
			if (OnDisconnected != null) {
				OnDisconnected (this);
			}
		}

	}

}