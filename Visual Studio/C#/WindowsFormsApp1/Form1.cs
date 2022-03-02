using Eruru.Socket;
using System;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace WindowsFormsApp1 {

	public partial class Form1 : Form {

		readonly SocketServer SocketServer = new SocketServer (16100);
		readonly string[] FileUnitNames = { "B", "KB", "MB", "GB", "TB" };

		int TotalLength;

		public Form1 () {
			InitializeComponent ();
		}

		private void Form1_Load (object sender, EventArgs e) {
			SocketServer.OnStarted = () => {
				Console.WriteLine ($"服务器 已启动 {SocketServer.LocalEndPoint}");
			};
			SocketServer.OnAccepted = serverClient => {
				Console.WriteLine ($"服务器 客户端 {serverClient.RemoteEndPoint} 已连接 当前连接数 {SocketServer.ClientCount}");
			};
			SocketServer.OnReceived = (serverClient, bytes) => {
				serverClient.Send (bytes);
				/*
				Console.WriteLine ($"服务器 客户端 {serverClient.RemoteEndPoint} 发来长度 {bytes.Length} " +
					$"内容 {Encoding.UTF8.GetString (bytes)} 的消息 当前连接数 {SocketServer.ClientCount}"
				);
				*/
				TotalLength += bytes.Length;
			};
			SocketServer.OnDisconnected = serverClient => {
				Console.WriteLine ($"服务器 客户端 {serverClient.RemoteEndPoint} 断开连接 代码 {serverClient.DisconnectCode} 当前连接数 {SocketServer.ClientCount}");
			};
			SocketServer.OnClosed = () => {
				Console.WriteLine ($"服务器 已关闭 当前连接数 {SocketServer.ClientCount}");
			};
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
						label1.Text = $"当前连接数 {SocketServer.ClientCount} 总计接收 {length:F2} {FileUnitNames[unitIndex]}";
					}));
				} catch (ObjectDisposedException exception) {
					Console.WriteLine (exception);
				}
			};
			timer.Enabled = true;
		}

		private void StartButton_Click (object sender, EventArgs e) {
			SocketServer.Start (IPAddress.Any, 3510);
		}

		private void Form1_FormClosed (object sender, FormClosedEventArgs e) {
			button2.PerformClick ();
		}

		private void CloseButton_Click (object sender, EventArgs e) {
			SocketServer.Close ();
		}

		private void button3_Click (object sender, EventArgs e) {
			SocketServer.Broadcast (Encoding.UTF8.GetBytes ("Hello"));
		}
	}

}