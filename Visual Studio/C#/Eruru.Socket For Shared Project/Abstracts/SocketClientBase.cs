using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Eruru.Socket {

	public abstract class SocketClientBase : IDisposable {

		public SocketClientState State {

			get {
				return _State;
			}

		}
		public EndPoint RemoteEndPoint {

			get {
				return _RemoteEndPoint;
			}

		}
		public EndPoint LocalEndPoint {

			get {
				return _LocalEndPoint;
			}

		}
		public int BufferSize {

			get {
				return _BufferSize;
			}

		}
		public int DisconnectCode {

			get {
				return _DisconnectCode;
			}

		}
		public DateTime LastReceiveTime {

			get {
				return _LastReceiveTime;
			}

		}
		public DateTime LastSendTime {

			get {
				return _LastSendTime;
			}

		}
		public SocketProtocolType ProtocolType {

			get {
				return _ProtocolType;
			}

		}
		public int MillisecondsTimeout {

			get {
				return _MillisecondsTimeout;
			}

			set {
				_MillisecondsTimeout = value;
				EventLock.MillisecondsTimeout = value;
			}

		}
		public object Tag { get; set; }

		protected readonly EventLock<string> EventLock = new EventLock<string> ();

		protected SocketClientState _State;
		protected EndPoint _RemoteEndPoint;
		protected System.Net.Sockets.Socket Socket;
		protected SocketAsyncEventArgs ReceiveSocketAsyncEventArgs = new SocketAsyncEventArgs ();
		protected SocketAsyncEventArgs SendSocketAsyncEventArgs = new SocketAsyncEventArgs ();
		protected SocketProtocolType _ProtocolType;

		readonly int _BufferSize;
		readonly Queue<byte> ReceiveBuffer = new Queue<byte> ();
		readonly Queue<byte> SendBuffer = new Queue<byte> ();

		int PacketBodyLength;
		int WebSocketPacketHeaderLength;
		int _DisconnectCode;
		bool WebSocketIsEndOfFrame;
		int WebSocketOperationCode;
		bool WebSocketHasMask;
		EndPoint _LocalEndPoint;
		DateTime _LastReceiveTime;
		DateTime _LastSendTime;
		int _MillisecondsTimeout;

		public SocketClientBase (int bufferSize = 1024) {
			_BufferSize = bufferSize;
			ReceiveSocketAsyncEventArgs.Completed += Completed;
			SendSocketAsyncEventArgs.Completed += Completed;
			MillisecondsTimeout = 60 * 1000;
		}

		public void Send (byte[] buffer, int offset, int length) {
			Send (buffer, offset, length, null, false);
		}
		public void Send (byte[] bytes) {
			Send (bytes, 0, bytes.Length, bytes, false);
		}

		public void SendAsync (byte[] buffer, int offset, int length) {
			Send (buffer, offset, length, null, true);
		}
		public void SendAsync (byte[] bytes) {
			Send (bytes, 0, bytes.Length, bytes, true);
		}

		public void Disconnect () {
			EventLock.Enter ();
			try {
				if (State != SocketClientState.Connected) {
					return;
				}
				_State = SocketClientState.Disconnecting;
				try {
					Socket.Shutdown (SocketShutdown.Both);
				} catch (SocketException socketException) {
					Console.WriteLine (socketException);
				}
				Socket.Close ();
				EventLock.WaitOne ("Disconnect");
				DisconnectCompleted ();
			} finally {
				EventLock.Exit ();
			}
		}

		protected virtual void PerformReceived (byte[] bytes) {

		}

		protected virtual void PerformSend (byte[] bytes) {

		}

		protected virtual void PerformDisconnected () {

		}

		void Send (byte[] buffer, int offset, int length, byte[] data, bool isAsync) {
			EventLock.Enter ();
			try {
				if (State != SocketClientState.Connected) {
					return;
				}
				EventLock.WaitOne ("Send");
				if (State != SocketClientState.Connected) {
					return;
				}
				byte[] bytes = ToPacket (buffer, offset, length);
				if (data == null && length > 0) {
					data = new byte[length];
					Array.Copy (buffer, offset, data, 0, length);
				}
				SendSocketAsyncEventArgs.UserToken = isAsync;
				for (int i = 0; i < bytes.Length; i++) {
					SendBuffer.Enqueue (bytes[i]);
				}
				if (length > 0) {
					PerformSend (data);
				}
				SendAsync (isAsync);
			} catch (TimeoutException exception) {
				Console.WriteLine ("{0} LocalEndPoint: {1} RemoteEndPoint : {2} Exception: {3}", this, LocalEndPoint, RemoteEndPoint, exception);
			} catch (Exception exception) {
				Console.WriteLine ("{0} LocalEndPoint: {1} RemoteEndPoint : {2} Exception: {3}", this, LocalEndPoint, RemoteEndPoint, exception);
				EventLock.Set ("Send");
			} finally {
				EventLock.Exit ();
			}
		}

		void Reset () {
			EventLock.Enter ();
			try {
				_State = SocketClientState.Disconnected;
				ReceiveBuffer.Clear ();
				SendBuffer.Clear ();
				PacketBodyLength = -1;
				WebSocketPacketHeaderLength = 2;
				_DisconnectCode = 0;
				EventLock.Set ("Send");
			} finally {
				EventLock.Exit ();
			}
		}

		protected void ReceiveAsync () {
			EventLock.Enter ();
			try {
				switch (State) {
					default:
						return;
					case SocketClientState.Connected:
						break;
					case SocketClientState.Disconnecting:
						EventLock.Set ("Disconnect");
						return;
				}
				if (!Socket.ReceiveAsync (ReceiveSocketAsyncEventArgs)) {
					ReceiveCompleted ();
				}
			} catch {
				throw;
			} finally {
				EventLock.Exit ();
			}
		}

		void SendAsync (bool useAsync) {
			EventLock.Enter ();
			try {
				_LastSendTime = DateTime.Now;
				if (State != SocketClientState.Connected) {
					return;
				}
				int length = Math.Min (BufferSize, SendBuffer.Count);
				SocketAPI.QueueDequeue (SendBuffer, SendSocketAsyncEventArgs.Buffer, SendSocketAsyncEventArgs.Offset, length);
				if (useAsync) {
					SendSocketAsyncEventArgs.SetBuffer (SendSocketAsyncEventArgs.Offset, length);
					if (!Socket.SendAsync (SendSocketAsyncEventArgs)) {
						SendCompleted ();
					}
					return;
				}
				Socket.Send (SendSocketAsyncEventArgs.Buffer, SendSocketAsyncEventArgs.Offset, length, SocketFlags.None);
				SendCompleted ();
			} catch (SocketException socketException) {
				if (socketException.SocketErrorCode != SocketError.ConnectionAborted) {
					Console.WriteLine ("{0} LocalEndPoint: {1} RemoteEndPoint : {2} Exception: {3}", this, LocalEndPoint, RemoteEndPoint, socketException);
					throw;
				}
			} finally {
				EventLock.Exit ();
			}
		}

		void Completed (object sender, SocketAsyncEventArgs e) {
			switch (e.LastOperation) {
				case SocketAsyncOperation.Receive:
					ReceiveCompleted ();
					break;
				case SocketAsyncOperation.Send:
					SendCompleted ();
					break;
				default:
					throw new NotImplementedException (e.LastOperation.ToString ());
			}
		}

		protected virtual void ConnectCompleted () {
			EventLock.Enter ();
			try {
				Reset ();
				_State = SocketClientState.Connected;
				_RemoteEndPoint = Socket.RemoteEndPoint;
				_LocalEndPoint = Socket.LocalEndPoint;
				_LastReceiveTime = DateTime.Now;
				_LastSendTime = DateTime.Now;
				EventLock.Tag = string.Format ("{0} LocalEndPoint: {1} RemoteEndPoint: {2}", this, LocalEndPoint, RemoteEndPoint);
			} finally {
				EventLock.Exit ();
			}
		}

		void ReceiveCompleted () {
			_LastReceiveTime = DateTime.Now;
			EventLock.Enter ();
			try {
				if (ProtocolType == SocketProtocolType.Unknown) {
					try {
						if (!DetermineProtocolType ()) {
							return;
						}
					} finally {
						EventLock.Set ("Protocol");
					}
				}
				if (ReceiveSocketAsyncEventArgs.BytesTransferred <= 0) {
					switch (State) {
						default:
							throw new NotImplementedException (State.ToString ());
						case SocketClientState.Connected:
							DisconnectCompleted ();
							break;
						case SocketClientState.Disconnecting:
							EventLock.Set ("Disconnect");
							break;
					}
					return;
				}
				for (int i = 0; i < ReceiveSocketAsyncEventArgs.BytesTransferred; i++) {
					ReceiveBuffer.Enqueue (ReceiveSocketAsyncEventArgs.Buffer[ReceiveSocketAsyncEventArgs.Offset + i]);
				}
				while (true) {
					if (PacketBodyLength < 0) {
						if (!ParsePacketHeader ()) {
							break;
						}
					} else {
						if (!ParsePacketBody ()) {
							break;
						}
					}
				}
				ReceiveAsync ();
			} catch (Exception exception) {
				Console.WriteLine ("{0} LocalEndPoint: {1} RemoteEndPoint : {2} Exception: {3}", this, LocalEndPoint, RemoteEndPoint, exception);
				switch (State) {
					default:
						break;
					case SocketClientState.Connected:
						Disconnect ();
						break;
					case SocketClientState.Disconnecting:
						EventLock.Set ("Disconnect");
						break;
				}
			} finally {
				EventLock.Exit ();
			}
		}

		void SendCompleted () {
			EventLock.Enter ();
			try {
				if (SendBuffer.Count == 0) {
					EventLock.Set ("Send");
					return;
				}
				SendAsync ((bool)SendSocketAsyncEventArgs.UserToken);
			} finally {
				EventLock.Exit ();
			}
		}

		protected virtual void DisconnectCompleted () {
			EventLock.Enter ();
			try {
				_State = SocketClientState.Disconnecting;
				EventLock.Reset ();
				_State = SocketClientState.Disconnected;
				PerformDisconnected ();
			} finally {
				EventLock.Exit ();
			}
		}

		byte[] ToPacket (byte[] buffer, int offset, int length) {
			switch (ProtocolType) {
				default:
					return SocketAPI.EmptyBytes;
				case SocketProtocolType.Socket:
					return SocketAPI.ToPacket (buffer, offset, length);
				case SocketProtocolType.WebSocket: {
					byte[] bytes;
					int dataOffset;
					if (length < 126) {
						dataOffset = 2;
						bytes = new byte[dataOffset + length];
						bytes[1] = (byte)length;
					} else if (length < ushort.MaxValue) {
						dataOffset = 4;
						bytes = new byte[dataOffset + length];
						bytes[1] = 126;
					} else {
						throw new NotImplementedException ("暂不支持WebSocket ulong大小的包");
					}
					bytes[0] = 129;
					Array.Copy (buffer, offset, bytes, dataOffset, length);
					return bytes;
				}
			}
		}

		bool DetermineProtocolType () {
			string text = Encoding.UTF8.GetString (ReceiveSocketAsyncEventArgs.Buffer, ReceiveSocketAsyncEventArgs.Offset, ReceiveSocketAsyncEventArgs.BytesTransferred);
			if (text.StartsWith ("GET")) {
				_ProtocolType = SocketProtocolType.WebSocket;
				string key = Regex.Match (text, @"Sec-WebSocket-Key: (\S+)").Groups[1].Value;
				byte[] sha1 = SHA1.Create ().ComputeHash (Encoding.ASCII.GetBytes (key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
				StringBuilder stringBuilder = new StringBuilder ();
				stringBuilder.AppendLine ("HTTP/1.1 101 Switching Protocols");
				stringBuilder.AppendLine ("Upgrade: websocket");
				stringBuilder.AppendLine ("Connection: Upgrade");
				stringBuilder.Append ("Sec-WebSocket-Accept: ").AppendLine (Convert.ToBase64String (sha1));
				stringBuilder.AppendLine ();
				Socket.Send (Encoding.UTF8.GetBytes (stringBuilder.ToString ()));
				return false;
			}
			_ProtocolType = SocketProtocolType.Socket;
			return true;
		}

		bool ParsePacketHeader () {
			switch (ProtocolType) {
				default:
					throw new NotImplementedException (ProtocolType.ToString ());
				case SocketProtocolType.Socket: {
					if (ReceiveBuffer.Count < SocketAPI.PacketHeaderLength) {
						return false;
					}
					PacketBodyLength = BitConverter.ToInt32 (SocketAPI.QueueDequeue (ReceiveBuffer, SocketAPI.PacketHeaderLength), 0);
					return true;
				}
				case SocketProtocolType.WebSocket:
					return ParseWebSocketPacketHeader ();
			}
		}

		bool ParseWebSocketPacketHeader () {
			if (ReceiveBuffer.Count < WebSocketPacketHeaderLength) {
				return false;
			}
			if (WebSocketPacketHeaderLength == 2) {
				byte data = ReceiveBuffer.Dequeue ();
				WebSocketIsEndOfFrame = data >> 7 > 0;
				if (!WebSocketIsEndOfFrame) {
					throw new NotImplementedException ("暂不支持WebSocket分帧");
				}
				WebSocketOperationCode = data & 15;
				data = ReceiveBuffer.Dequeue ();
				WebSocketHasMask = data >> 7 > 0;
				WebSocketPacketHeaderLength = data & 127;
				switch (WebSocketPacketHeaderLength) {
					default:
						PacketBodyLength = WebSocketPacketHeaderLength;
						WebSocketPacketHeaderLength = 2;
						break;
					case 126:
						WebSocketPacketHeaderLength = 4;
						break;
					case 127:
						WebSocketPacketHeaderLength = 10;
						break;
				}
				if (ReceiveBuffer.Count < WebSocketPacketHeaderLength) {
					return false;
				}
			}
			if (WebSocketPacketHeaderLength > 2) {
				switch (WebSocketPacketHeaderLength) {
					default:
						throw new NotImplementedException ("PacketBodyLength < 0 WebSocketPacketHeaderLength > 2 WebSocketPacketHeaderLength " + WebSocketPacketHeaderLength);
					case 4: {
						byte[] bytes = SocketAPI.QueueDequeue (ReceiveBuffer, WebSocketPacketHeaderLength - 2);
						Array.Reverse (bytes);
						PacketBodyLength = BitConverter.ToUInt16 (bytes, 0);
						break;
					}
					case 10:
						throw new NotImplementedException ("暂不支持WebSocket ulong大小的包");
				}
			}
			if (WebSocketHasMask) {
				PacketBodyLength += 4;
			}
			return true;
		}

		bool ParsePacketBody () {
			if (ReceiveBuffer.Count < PacketBodyLength) {
				return false;
			}
			byte[] bytes = null;
			switch (ProtocolType) {
				default:
					throw new NotImplementedException (ProtocolType.ToString ());
				case SocketProtocolType.Socket:
					if (PacketBodyLength > 0) {
						bytes = SocketAPI.QueueDequeue (ReceiveBuffer, PacketBodyLength);
					}
					break;
				case SocketProtocolType.WebSocket:
					ParseWebSocketPacketBody (ref bytes);
					break;
			}
			if (PacketBodyLength > 0) {
				PerformReceived (bytes);
			}
			PacketBodyLength = -1;
			WebSocketPacketHeaderLength = 2;
			return true;
		}

		void ParseWebSocketPacketBody (ref byte[] bytes) {
			if (WebSocketHasMask) {
				byte[] mask = SocketAPI.QueueDequeue (ReceiveBuffer, 4);
				PacketBodyLength -= mask.Length;
				if (PacketBodyLength > 0) {
					bytes = SocketAPI.QueueDequeue (ReceiveBuffer, PacketBodyLength);
					for (int i = 0; i < bytes.Length; i++) {
						bytes[i] ^= mask[i % 4];
					}
				}
			}
			if (bytes == null) {
				bytes = SocketAPI.QueueDequeue (ReceiveBuffer, PacketBodyLength);
			}
			switch (WebSocketOperationCode) {
				default:
					throw new NotImplementedException ("WebSocket操作码 " + WebSocketOperationCode);
				case 1:
				case 2:
					break;
				case 8:
					PacketBodyLength = 0;
					Array.Reverse (bytes);
					_DisconnectCode = BitConverter.ToInt16 (bytes, 0);
					break;
			}
		}

		public virtual void Dispose () {
			Disconnect ();
			ReceiveSocketAsyncEventArgs.Dispose ();
			SendSocketAsyncEventArgs.Dispose ();
			EventLock.Dispose ();
#if NET5_0
			GC.SuppressFinalize (this);
#endif
		}

	}

}