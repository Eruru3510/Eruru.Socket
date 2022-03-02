using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Eruru.Socket {

	public class SocketServer : IDisposable {

		public SocketServerState State {

			get {
				return _State;
			}

		}
		public IPEndPoint LocalEndPoint {

			get {
				return _LocalEndPoint;
			}

		}
		public int MaximumClientCount {

			get {
				return _MaximumClientCount;
			}

		}
		public int BufferSize {

			get {
				return _BufferSize;
			}

		}
		public int ClientCount {

			get {
				return _ClientCount;
			}

		}
		public SocketServerStartedEventHandler OnStarted { get; set; }
		public SocketServerAcceptedEventHandler OnAccepted { get; set; }
		public SocketServerReceivedEventHandler OnReceived { get; set; }
		public SocketServerSendEventHandler OnSend { get; set; }
		public SocketServerDisconnectedEventHandler OnDisconnected { get; set; }
		public SocketServerClosedEventHandler OnClosed { get; set; }
		public bool EnableWebSocket { get; set; }
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
		public int MillisecondsTimeout {

			get {
				return _MillisecondsTimeout;
			}

			set {
				_MillisecondsTimeout = value;
				EventLock.MillisecondsTimeout = value;
				ReaderWriterLock.MillisecondsTimeout = value;
			}

		}

		internal readonly BufferManager BufferManager;

		readonly Semaphore MaximumClientCountSemaphore;
		readonly SocketAsyncEventArgs AcceptSocketAsyncEventArgs = new SocketAsyncEventArgs ();
		readonly int _MaximumClientCount;
		readonly int _BufferSize;
		readonly Queue<SocketServerClient> ClientPool;
		readonly List<SocketServerClient> Clients;
		readonly ReaderWriterLockHelper.ReaderWriterLockHelper ReaderWriterLock = new ReaderWriterLockHelper.ReaderWriterLockHelper ();
		readonly EventLock<string> EventLock = new EventLock<string> ();
		readonly Timer HeartbeatTimer;

		System.Net.Sockets.Socket Socket;
		SocketServerState _State = SocketServerState.NotStarted;
		IPEndPoint _LocalEndPoint;
		int _ClientCount;
		int _HeartbeatTimeout = 120;
		int _HeartbeatInterval = 60;
		int _MillisecondsTimeout;

		public SocketServer (int maximumClientCount = 32, int bufferSize = 1024) {
			_MaximumClientCount = maximumClientCount;
			_BufferSize = bufferSize;
			BufferManager = new BufferManager (BufferSize * MaximumClientCount * 2, BufferSize);
			ClientPool = new Queue<SocketServerClient> (MaximumClientCount);
			Clients = new List<SocketServerClient> (MaximumClientCount);
			MaximumClientCountSemaphore = new Semaphore (MaximumClientCount, MaximumClientCount);
			BufferManager.InitBuffer ();
			MillisecondsTimeout = 60 * 1000;
			SocketServerClient client;
			for (int i = 0; i < MaximumClientCount; i++) {
				client = new SocketServerClient (this) {
					MillisecondsTimeout = MillisecondsTimeout
				};
				client.OnReceived += PerformReceived;
				client.OnSend += PerformSend;
				client.OnDisconnected += DisconnectCompleted;
				ClientPool.Enqueue (client);
			}
			AcceptSocketAsyncEventArgs.Completed += Completed;
			HeartbeatTimer = new Timer (Heartbeat);
		}

		public void Start (IPEndPoint localEndPoint) {
			EventLock.Enter ();
			try {
				switch (State) {
					default:
						return;
					case SocketServerState.NotStarted:
						break;
					case SocketServerState.Started:
						Close ();
						break;
				}
				_State = SocketServerState.Starting;
				_LocalEndPoint = localEndPoint;
				Socket = new System.Net.Sockets.Socket (LocalEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				try {
					if (LocalEndPoint.AddressFamily == AddressFamily.InterNetworkV6) {
						Socket.SetSocketOption (SocketOptionLevel.IPv6, (SocketOptionName)27, false);
						Socket.Bind (new IPEndPoint (IPAddress.IPv6Any, LocalEndPoint.Port));
					} else {
						Socket.Bind (LocalEndPoint);
					}
					Socket.Listen (MaximumClientCount);
				} catch {
					_State = SocketServerState.NotStarted;
					throw;
				}
				_State = SocketServerState.Started;
				HeartbeatTimer.Change (1000, 1000);
				PerformStarted ();
				AcceptAsync ();
			} finally {
				EventLock.Exit ();
			}
		}
		public void Start (IPAddress ip, int port) {
			Start (new IPEndPoint (ip, port));
		}
		public void Start (long ip, int port) {
			Start (new IPEndPoint (ip, port));
		}
		public void Start (string ip, int port) {
			Start (new IPEndPoint (IPAddress.Parse (ip), port));
		}

		public void ForEach (SocketFunc<SocketServerClient, bool> func) {
			ReaderWriterLock.Read (() => {
				List<SocketServerClient> clients = new List<SocketServerClient> (Clients);
				foreach (SocketServerClient client in clients) {
					if (!func (client)) {
						break;
					}
				}
			});
		}

		public void Broadcast (byte[] buffer, int offset, int length) {
			ForEach (client => {
				client.Send (buffer, offset, length);
				return true;
			});
		}
		public void Broadcast (byte[] bytes) {
			ForEach (client => {
				client.Send (bytes);
				return true;
			});
		}

		public void BroadcastAsync (byte[] buffer, int offset, int length) {
			ForEach (client => {
				client.SendAsync (buffer, offset, length);
				return true;
			});
		}
		public void BroadcastAsync (byte[] bytes) {
			ForEach (client => {
				client.SendAsync (bytes);
				return true;
			});
		}

		public void Close () {
			EventLock.Enter ();
			try {
				if (State != SocketServerState.Started) {
					return;
				}
				_State = SocketServerState.Closing;
				try {
					HeartbeatTimer.Change (Timeout.Infinite, Timeout.Infinite);
					Socket.Close ();
					ReaderWriterLock.Write (() => {
						ForEach (client => {
							client.Disconnect ();
							return true;
						});
					});
					PerformClosed ();
				} finally {
					_State = SocketServerState.NotStarted;
				}
			} finally {
				EventLock.Exit ();
			}
		}

		void Heartbeat (object state) {
			ForEach (client => {
				if (client.LastReceiveTime.AddSeconds (HeartbeatTimeout) < DateTime.Now) {
					client.Disconnect ();
					return true;
				}
				if (client.LastSendTime.AddSeconds (HeartbeatInterval) < DateTime.Now) {
					client.Send (SocketAPI.EmptyBytes);
				}
				return true;
			});
		}

		void PerformStarted () {
			if (OnStarted != null) {
				OnStarted ();
			}
		}

		void PerformAccepted (SocketServerClient client) {
			if (OnAccepted != null) {
				OnAccepted (client);
			}
		}

		void PerformReceived (SocketServerClient client, byte[] bytes) {
			if (OnReceived != null) {
				OnReceived (client, bytes);
			}
		}

		void PerformSend (SocketServerClient client, byte[] bytes) {
			if (OnSend != null) {
				OnSend (client, bytes);
			}
		}

		void PerformDisconnected (SocketServerClient client) {
			if (OnDisconnected != null) {
				OnDisconnected (client);
			}
		}

		void PerformClosed () {
			if (OnClosed != null) {
				OnClosed ();
			}
		}

		void AcceptAsync () {
			MaximumClientCountSemaphore.WaitOne ();
			AcceptSocketAsyncEventArgs.AcceptSocket = null;
			try {
				if (!Socket.AcceptAsync (AcceptSocketAsyncEventArgs)) {
					AcceptCompleted ();
				}
			} catch (ObjectDisposedException) {

			}
		}

		void Completed (object sender, SocketAsyncEventArgs e) {
			try {
				switch (e.LastOperation) {
					case SocketAsyncOperation.Accept:
						if (AcceptSocketAsyncEventArgs.SocketError != SocketError.Success) {
							break;
						}
						AcceptCompleted ();
						break;
					default:
						throw new NotImplementedException (e.LastOperation.ToString ());
				}
			} catch (Exception exception) {
				Console.WriteLine (exception);
			}
		}

		void AcceptCompleted () {
			SocketServerClient client = null;
			ReaderWriterLock.Write (() => {
				_ClientCount++;
				client = ClientPool.Dequeue ();
				Clients.Add (client);
				client.Initialize (AcceptSocketAsyncEventArgs.AcceptSocket);
			});
			AcceptAsync ();
			try {
				client.DetermineProtocolType ();
				PerformAccepted (client);
			} catch (Exception exception) {
				Console.WriteLine (exception);
				PerformAccepted (client);
				client.Disconnect ();
			}
		}

		void DisconnectCompleted (SocketServerClient client) {
			ReaderWriterLock.Write (() => {
				_ClientCount--;
				ClientPool.Enqueue (client);
				Clients.Remove (client);
				MaximumClientCountSemaphore.Release ();
				PerformDisconnected (client);
			});
		}

		public void Dispose () {
			Close ();
			MaximumClientCountSemaphore.Close ();
			AcceptSocketAsyncEventArgs.Dispose ();
			ReaderWriterLock.Dispose ();
			HeartbeatTimer.Dispose ();
			EventLock.Dispose ();
#if NET5_0
			GC.SuppressFinalize (this);
#endif
		}

	}

}