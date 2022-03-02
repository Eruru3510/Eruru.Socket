using System;
using System.Collections.Generic;

namespace Eruru.Socket {

	public delegate void SocketServerStartedEventHandler ();
	public delegate void SocketServerAcceptedEventHandler (SocketServerClient client);
	public delegate void SocketServerReceivedEventHandler (SocketServerClient client, byte[] bytes);
	public delegate void SocketServerSendEventHandler (SocketServerClient client, byte[] bytes);
	public delegate void SocketServerDisconnectedEventHandler (SocketServerClient client);
	public delegate void SocketServerClosedEventHandler ();

	public delegate void SocketServerClientReceivedEventHandler (SocketServerClient client, byte[] bytes);
	public delegate void SocketServerClientSendEventHandler (SocketServerClient client, byte[] bytes);
	public delegate void SocketServerClientDisconnectedEventHandler (SocketServerClient client);

	public delegate void SocketClientConnectedEventHandler ();
	public delegate void SocketClientReceivedEventHandler (byte[] bytes);
	public delegate void SocketClientSendEventHandler (byte[] bytes);
	public delegate void SocketClientDisconnectedEventHandler ();

	public delegate TResult SocketFunc<in T, out TResult> (T arg);

	delegate void SocketHeartbeatEventHandler ();

	class SocketAPI {

		internal const int PacketHeaderLength = sizeof (int);

#if NET5_0_OR_GREATER
		internal static readonly byte[] EmptyBytes = Array.Empty<byte> ();
#else
		internal static readonly byte[] EmptyBytes = new byte[0];
#endif

		internal static byte[] ToPacket (byte[] buffer, int offset, int length) {
			byte[] bytes = new byte[PacketHeaderLength + length];
			Array.Copy (BitConverter.GetBytes (length), bytes, PacketHeaderLength);
			Array.Copy (buffer, offset, bytes, PacketHeaderLength, length);
			return bytes;
		}

		internal static void QueueDequeue<T> (Queue<T> source, T[] destination, int offset, int length) {
			for (int i = 0; i < length; i++) {
				destination[offset + i] = source.Dequeue ();
			}
		}
		internal static void QueueDequeue<T> (Queue<T> source, T[] destination, int length) {
			QueueDequeue (source, destination, 0, length);
		}
		internal static T[] QueueDequeue<T> (Queue<T> source, int length) {
			T[] results = new T[length];
			QueueDequeue (source, results, length);
			return results;
		}

	}

}