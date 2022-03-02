using System;
using System.Collections.Generic;
using System.Threading;

namespace Eruru.Socket {

	public class EventLock<Key> : IDisposable {

		public object Tag;
		public int MillisecondsTimeout {

			get {
				return _MillisecondsTimeout;
			}

			set {
				_MillisecondsTimeout = value;
			}

		}

		readonly object Lock = new object ();
		readonly List<EventLockEvent<Key>> Events = new List<EventLockEvent<Key>> ();

		int _MillisecondsTimeout = Timeout.Infinite;
		int WaitCount;

		public void Enter (int millisecondsTimeout) {
			if (!Monitor.TryEnter (Lock, millisecondsTimeout)) {
				throw new TimeoutException ("获取锁超时");
			}
		}
		public void Enter (TimeSpan timeout) {
			Enter ((int)timeout.TotalMilliseconds);
		}
		public void Enter () {
			Enter (MillisecondsTimeout);
		}

		public bool TryEnter (int millisecondsTimeout) {
			try {
				Enter (millisecondsTimeout);
				return true;
			} catch (TimeoutException) {
				return false;
			}
		}
		public bool TryEnter (TimeSpan timeout) {
			return TryEnter ((int)timeout.TotalMilliseconds);
		}
		public bool TryEnter () {
			return TryEnter (MillisecondsTimeout);
		}

		public void WaitOne (Key key, int millisecondsTimeout) {
			Enter (millisecondsTimeout);
			int index = -1;
			try {
				WaitCount++;
				index = Events.FindIndex (item => item.Key.Equals (key));
				if (index < 0) {
					index = Events.Count;
					Events.Add (new EventLockEvent<Key> (key, 1, false));
				} else {
					Events[index] = new EventLockEvent<Key> (Events[index].Key, Events[index].Count + 1, Events[index].Signal);
				}
				//Console.WriteLine ("{0} 等待 {1} 计数: {2} 信号: {3} 等待数量：{4} 哈希码：{5}", Tag, key, Events[index].Count, Events[index].Signal, WaitCount, GetHashCode ());
				while (true) {
					if (Events[index].Signal) {
						Events[index] = new EventLockEvent<Key> (Events[index].Key, Events[index].Count - 1, false);
						break;
					}
					if (!Monitor.Wait (Lock, millisecondsTimeout)) {
						index = Events.FindIndex (item => item.Key.Equals (key));
						Events[index] = new EventLockEvent<Key> (Events[index].Key, Events[index].Count - 1, Events[index].Signal);
						throw new TimeoutException ("等待超时");
					}
					index = Events.FindIndex (item => item.Key.Equals (key));
				}
			} finally {
				if (Events[index].Count <= 0) {
					Events.RemoveAt (index);
				}
				WaitCount--;
				if (WaitCount <= 0) {
					Monitor.PulseAll (Lock);
				}
				//Console.WriteLine ("{0} 等待完毕 {1} 等待数量：{2} 哈希码：{3}", Tag, key, WaitCount, GetHashCode ());
				Exit ();
			}
		}
		public void WaitOne (Key key, TimeSpan timeout) {
			WaitOne (key, (int)timeout.TotalMilliseconds);
		}
		public void WaitOne (Key key) {
			WaitOne (key, MillisecondsTimeout);
		}

		public bool TryWaitOne (Key key, int millisecondsTimeout) {
			try {
				WaitOne (key, millisecondsTimeout);
				return true;
			} catch (TimeoutException) {
				return false;
			}
		}
		public bool TryWaitOne (Key key, TimeSpan timeout) {
			return TryWaitOne (key, (int)timeout.TotalMilliseconds);
		}
		public bool TryWaitOne (Key key) {
			return TryWaitOne (key, MillisecondsTimeout);
		}

		public void Set (Key key, int millisecondsTimeout) {
			Enter (millisecondsTimeout);
			try {
				//Console.WriteLine ("{0} Pulse {1} 等待数量：{2} 哈希码：{3}", Tag, key, WaitCount, GetHashCode ());
				int index = Events.FindIndex (item => item.Key.Equals (key));
				if (index < 0) {
					Events.Add (new EventLockEvent<Key> (key, 0, true));
				} else {
					Events[index] = new EventLockEvent<Key> (Events[index].Key, Events[index].Count, true);
				}
				Monitor.PulseAll (Lock);
			} finally {
				Exit ();
			}
		}
		public void Set (Key key, TimeSpan timeout) {
			Set (key, (int)timeout.TotalMilliseconds);
		}
		public void Set (Key key) {
			Set (key, MillisecondsTimeout);
		}

		public bool TrySet (Key key, int millisecondsTimeout) {
			try {
				Set (key, millisecondsTimeout);
				return true;
			} catch (TimeoutException) {
				return false;
			}
		}
		public bool TrySet (Key key, TimeSpan timeout) {
			return TrySet (key, (int)timeout.TotalMilliseconds);
		}
		public bool TrySet (Key key) {
			return TrySet (key, MillisecondsTimeout);
		}

		public void Reset (int millisecondsTimeout) {
			Enter (millisecondsTimeout);
			try {
				if (WaitCount > 0) {
					//Console.WriteLine ("{0} 清除 等待数量：{1} 哈希码：{2}", Tag, WaitCount, GetHashCode ());
					while (WaitCount > 0) {
						for (int i = 0; i < Events.Count; i++) {
							Events[i] = new EventLockEvent<Key> (Events[i].Key, Events[i].Count, true);
						}
						Monitor.PulseAll (Lock);
						if (!Monitor.Wait (Lock, millisecondsTimeout)) {
							throw new TimeoutException ("等待超时");
						}
					}
					//	Console.WriteLine ("{0} 清除完成 等待数量：{1} 哈希码：{2}", Tag, WaitCount, GetHashCode ());
				}
				Events.Clear ();
			} finally {
				Exit ();
			}
		}
		public void Reset (TimeSpan timeout) {
			Reset ((int)timeout.TotalMilliseconds);
		}
		public void Reset () {
			Reset (MillisecondsTimeout);
		}

		public bool TryReset (int millisecondsTimeout) {
			try {
				Reset (millisecondsTimeout);
				return true;
			} catch (TimeoutException) {
				return false;
			}
		}
		public bool TryReset (TimeSpan timeout) {
			return TryReset ((int)timeout.TotalMilliseconds);
		}
		public bool TryReset () {
			return TryReset (MillisecondsTimeout);
		}

		public void Exit () {
			Monitor.Exit (Lock);
		}

		public bool TryExit () {
			try {
				Exit ();
				return true;
			} catch (SynchronizationLockException) {
				return false;
			}
		}

		public void Dispose (int millisecondsTimeout) {
			Reset (millisecondsTimeout);
		}
		public void Dispose (TimeSpan timeout) {
			Dispose ((int)timeout.TotalMilliseconds);
		}
		public void Dispose () {
			Dispose (MillisecondsTimeout);
#if NET5_0
			GC.SuppressFinalize (this);
#endif
		}

		public bool TryDispose (int millisecondsTimeout) {
			try {
				Dispose (millisecondsTimeout);
				return true;
			} catch (TimeoutException) {
				return false;
			}
		}
		public bool TryDispose (TimeSpan timeout) {
			return TryDispose ((int)timeout.TotalMilliseconds);
		}
		public bool TryDispose () {
			try {
				Dispose ();
				return true;
			} catch (TimeoutException) {
				return false;
			}
		}

		~EventLock () {
			Dispose ();
		}

	}

}