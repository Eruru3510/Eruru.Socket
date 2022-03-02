namespace Eruru.Socket {

	struct EventLockEvent<TKey> {

		public TKey Key;
		public int Count;
		public bool Signal;

		public EventLockEvent (TKey key, int count, bool signal) {
			Key = key;
			Count = count;
			Signal = signal;
		}

	}

}