using Eruru.Socket;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp2 {

	public partial class Form1 : Form {

		static Client[] Clients;
		static int Count;
		static int TotalLength;

		readonly string[] FileUnitNames = { "B", "KB", "MB", "GB", "TB" };

		public Form1 () {
			InitializeComponent ();
		}

		private void Form1_Load (object sender, EventArgs e) {
			Clients = new Client[10000];
			for (int i = 0; i < Clients.Length; i++) {
				Clients[i] = new Client {
					Name = i.ToString ()
				};
			}
			new Thread (() => {
				while (true) {
					Thread.Sleep (16);
					Parallel.ForEach (Clients, client => {
						client.SendTime = client.Stopwatch.ElapsedMilliseconds;
						client.SocketClient.Send (Encoding.UTF8.GetBytes ("Hello, World!"));
					});
				}
			}) {
				IsBackground = true
			}.Start ();
			System.Timers.Timer timer = new System.Timers.Timer (100);
			timer.Elapsed += (innerSender, innerE) => {
				float length = TotalLength;
				int unitIndex = 0;
				while (length >= 1024) {
					length /= 1024F;
					unitIndex++;
				}
				try {
					Invoke (new Action (() => {
						label1.Text = $"当前连接数 {Count} 总计接收 {length:F2} {FileUnitNames[unitIndex]}";
					}));
				} catch (ObjectDisposedException exception) {
					Console.WriteLine (exception);
				}
			};
			timer.Enabled = true;
		}

		class Client {

			public SocketClient SocketClient = new SocketClient ();
			public Stopwatch Stopwatch = new Stopwatch ();
			public long SendTime;
			public string Name;

			public Client () {
				Stopwatch.Start ();
				SocketClient.OnConnected += () => {
					lock (Clients) {
						Count++;
						Console.WriteLine ($"客户端 {Name} 连接服务器 {SocketClient.RemoteEndPoint} 成功 当前连接数 {Count}");
					}
				};
				SocketClient.OnReceved += bytes => {
					//Console.WriteLine ($"客户端 {Name} 收到来自服务器长度 {bytes.Length} 内容 {Encoding.UTF8.GetString (bytes)} 的消息 RTT {Stopwatch.ElapsedMilliseconds - SendTime}");
					TotalLength += bytes.Length;
				};
				SocketClient.OnDisconnected += () => {
					lock (Clients) {
						Count--;
						Console.WriteLine ($"客户端 {Name} 与服务器 {SocketClient.RemoteEndPoint} 断开连接 当前连接数 {Count}");
					}
				};
			}

		}

		private void ConnectButton_Click (object sender, EventArgs e) {
			Parallel.ForEach (Clients, client => {
				try {
					client.SocketClient.Connect ("127.0.0.1", 3510);
				} catch (Exception exception) {
					Console.WriteLine (exception);
				}
			});
		}

		private void DisconnectButton_Click (object sender, EventArgs e) {
			Parallel.ForEach (Clients, client => {
				client.SocketClient.Disconnect ();
			});
		}

		private void Form1_FormClosed (object sender, FormClosedEventArgs e) {
			button2.PerformClick ();
		}

	}

}