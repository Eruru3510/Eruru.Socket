using Eruru.Socket;
using System;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace ConsoleApp1 {

	class Program {

		static void Main (string[] args) {
			Console.Title = string.Empty;
			SocketServer socketServer = new SocketServer (10);
			socketServer.OnAccepted += client => {
				Console.WriteLine ($"服务端 {socketServer.LocalEndPoint} 客户端 {client.RemoteEndPoint} 连入 当前连接数 {socketServer.ClientCount}");
				client.Send (Encoding.UTF8.GetBytes ("Hello, World!"));
			};
			socketServer.OnReceived += (client, bytes) => {
				Console.WriteLine ($"服务端 {socketServer.LocalEndPoint} 客户端 {client.RemoteEndPoint} 发来 {Encoding.UTF8.GetString (bytes)} 当前连接数 {socketServer.ClientCount}");
			};
			socketServer.OnDisconnected += client => {
				Console.WriteLine ($"服务端 {socketServer.LocalEndPoint} 客户端 {client.RemoteEndPoint} 断开 当前连接数 {socketServer.ClientCount}");
			};
			socketServer.Start (IPAddress.Any, 3510);
			SocketClient socketClient = new SocketClient ();
			socketClient.OnConnected += () => Console.WriteLine ($"客户端 {socketClient.LocalEndPoint} 已连接到 {socketClient.RemoteEndPoint}");
			socketClient.OnReceved += bytes => {
				Console.WriteLine ($"客户端 {socketClient.LocalEndPoint} 收到 {Encoding.UTF8.GetString (bytes)} 来自 {socketClient.RemoteEndPoint}");
				socketClient.Send (Encoding.UTF8.GetBytes ($"你发送的是：{Encoding.UTF8.GetString (bytes)}"));
			};
			socketClient.OnDisconnected += () => Console.WriteLine ($"客户端 {socketClient.LocalEndPoint} 与 {socketClient.RemoteEndPoint} 断开");
			Stopwatch stopwatch = new Stopwatch ();
			stopwatch.Start ();
			for (int i = 0; i >= 0; i++) {
				socketClient.Connect ("127.0.0.1", 3510);
				socketClient.Disconnect ();
				Console.WriteLine ($"执行次数 {i + 1}");
			}
			Console.ReadLine ();
		}

	}

}