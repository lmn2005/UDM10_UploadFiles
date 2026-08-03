using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UDM10.Server
{
    public class UploadServer
    {
        private readonly int _port;
        private TcpListener? _listener;
        private bool _isRunning;

        public UploadServer(int port)
        {
            _port = port;
        }

        public async Task StartAsync()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                Console.WriteLine("Server started successfully!");
                Console.WriteLine("Server is listening on port {0}", _port);

                while (_isRunning)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine("\nNew client connected: {0}", client.Client.RemoteEndPoint);

                    ClientConnectionHandler handler = new ClientConnectionHandler();

                    _ = handler.HandleClientAsync(client);
                }
            }
            catch (Exception ex)
            {
                if (_isRunning) Console.WriteLine("[Server Startup Error]: {0}", ex.Message);
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            Console.WriteLine("Server stopped.");
        }
    }
}
