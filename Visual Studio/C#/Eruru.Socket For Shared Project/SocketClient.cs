using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Eruru.Socket {

	public class SocketClient : SocketClientBase {

		public event SocketClientConnectedEventHandler OnConnected;
		public event SocketClientReceivedEventHandler OnReceved;
		public event SocketClientSendEventHandler OnSend;
		public event SocketClientDisconnectedEventHandler OnDisconnected;
		public int HeartbeatTimeout {

			get {
				return _HeartbeatTimeout;
			}

			set {
				_HeartbeatTimeout = value;
			}

		}
		public int HeartbeatInterval {

			get {
				return _HeartbeatInterval;
			}

			set {
				_HeartbeatInterval = value;
			}

		}

		readonly SocketAsyncEventArgs ConnectSocketAsyncEventArgs = new SocketAsyncEventArgs ();
		readonly byte[] Buffer;
		readonly Timer HeartbeatTimer;

		int _HeartbeatTimeout = 120;
		int _HeartbeatInterval = 60;

		public SocketClient (int bufferSize = 1024) : base (bufferSize) {
			Buffer = new byte[BufferSize * 2];
			ConnectSocketAsyncEventArgs.Completed += Completed;
			ReceiveSocketAsyncEventArgs.SetBuffer (Buffer, 0, BufferSize);
			SendSocketAsyncEventArgs.SetBuffer (Buffer, BufferSize, BufferSize);
			_ProtocolType = SocketProtocolType.Socket;
			HeartbeatTimer = new Timer (Heartbeat);
		}

		public void Connect (EndPoint remoteEndPoint) {
			Connect (remoteEndPoint, false);
		}
		public void Connect (IPAddress ip, int port) {
			Connect (new IPEndPoint (ip, port), false);
		}
		public void Connect (long ip, int port) {
			Connect (new IPEndPoint (ip, port), false);
		}
		public void Connect (string ip, int port) {
			Connect (new IPEndPoint (IPAddress.Parse (ip), port), false);
		}

		public void ConnectAsync (EndPoint remoteEndPoint) {
			Connect (remoteEndPoint, true);
		}
		public void ConnectAsync (IPAddress ip, int port) {
			Connect (new IPEndPoint (ip, port), true);
		}
		public void ConnectAsync (long ip, int port) {
			Connect (new IPEndPoint (ip, port), true);
		}
		public void ConnectAsync (string ip, int port) {
			Connect (new IPEndPoint (IPAddress.Parse (ip), port), true);
		}

		void Connect (EndPoint remoteEndPoint, bool isAsync) {
			EventLock.Enter ();
			try {
				if (State != SocketClientState.Disconnected) {
					return;
				}
				_State = SocketClientState.Connecting;
				try {
					_RemoteEndPoint = remoteEndPoint;
					Socket = new System.Net.Sockets.Socket (RemoteEndPoint.AddressFamily, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
					if (isAsync) {
						ConnectSocketAsyncEventArgs.RemoteEndPoint = RemoteEndPoint;
						if (!Socket.ConnectAsync (ConnectSocketAsyncEventArgs)) {
							ConnectCompleted ();
						}
						return;
					}
					Socket.Connect (remoteEndPoint);
					ConnectCompleted ();
				} catch {
					_State = SocketClientState.Disconnected;
					throw;
				}
			} finally {
				EventLock.Exit ();
			}
		}

		void Heartbeat (object state) {
			EventLock.Enter ();
			try {
				if (LastReceiveTime.AddSeconds (HeartbeatTimeout) < DateTime.Now) {
					Disconnect ();
					return;
				}
				if (LastSendTime.AddSeconds (HeartbeatInterval) < DateTime.Now) {
					Send (SocketAPI.EmptyBytes);
				}
			} finally {
				EventLock.Exit ();
			}
		}

		void PerformConnected () {
			if (OnConnected != null) {
				OnConnected ();
			}
		}

		protected override void PerformReceived (byte[] bytes) {
			if (OnReceved != null) {
				OnReceved (bytes);
			}
		}

		protected override void PerformSend (byte[] bytes) {
			if (OnSend != null) {
				OnSend (bytes);
			}
		}

		protected override void PerformDisconnected () {
			if (OnDisconnected != null) {
				OnDisconnected ();
			}
		}

		void Completed (object sender, SocketAsyncEventArgs e) {
			switch (e.LastOperation) {
				case SocketAsyncOperation.Connect:
					EventLock.Enter ();
					try {
						if (ConnectSocketAsyncEventArgs.SocketError != SocketError.Success) {
							_State = SocketClientState.Disconnected;
							return;
						}
						ConnectCompleted ();
					} finally {
						EventLock.Exit ();
					}
					break;
				default:
					throw new NotImplementedException (e.LastOperation.ToString ());
			}
		}

		protected override void ConnectCompleted () {
			EventLock.Enter ();
			try {
				base.ConnectCompleted ();
				SendProtocolType ();
				HeartbeatTimer.Change (1000, 1000);
				PerformConnected ();
				ReceiveAsync ();
			} finally {
				EventLock.Exit ();
			}
		}

		protected override void DisconnectCompleted () {
			EventLock.Enter ();
			try {
				_State = SocketClientState.Disconnecting;
				HeartbeatTimer.Change (Timeout.Infinite, Timeout.Infinite);
				base.DisconnectCompleted ();
			} finally {
				EventLock.Exit ();
			}
		}

		void SendProtocolType () {
			Send (SocketAPI.EmptyBytes);
		}

		public override void Dispose () {
			base.Dispose ();
			ConnectSocketAsyncEventArgs.Dispose ();
			HeartbeatTimer.Dispose ();
#if NET5_0
			GC.SuppressFinalize (this);
#endif
		}

	}

}