using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Messenger
{
    public class Client
    {
        public string ID { get; private set; }
        public string Name { get; private set; }

        public string Ip { get => (_socket.RemoteEndPoint as IPEndPoint).Address.ToString(); }
        // Сокет подключенного объекта
        private Socket _socket;
        private Server _server;

        // Проверка на то, есть ли в буфере какая-то непрочитанная инфа от объекта
        public bool HasMessage { get { return _socket.Available > 0; } }

        public Client(Socket socket, Server server)
        {
            // Задаём новый случайный ID для нового подключения к серверу
            ID = Guid.NewGuid().ToString();
            // Запоминаем сокет объекта, по которому он будет работать
            _socket = socket;
            _server = server;

            Name = GetMessage();
        }

        public string GetMessage()
        {
            StringBuilder receivedMsg = new StringBuilder();
            byte[] data = new byte[256]; // буфер для получаемых данных

            do
            {
                int bytes = _socket.Receive(data);
                receivedMsg.Append(Encoding.Unicode.GetString(data, 0, bytes));
            }
            while (_socket.Available > 0);

            return receivedMsg.ToString();
        }

        public void SendMessage(string msg)
        {
            byte[] bytesMsg = Encoding.Unicode.GetBytes(msg);
            _socket.Send(bytesMsg);
        }

        public void Close()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }

        public void Process()
        {
            try
            {
                while (_socket.Connected)
                {
                    var msg = GetMessage();
                    if (msg != "")
                    {
                        _server.AddMessageToBroadcast(msg, ID);
                    }
                }
            }
            catch (Exception e)
            {

            }
            finally
            {
                _server.RemoveClient(ID);
            }
        }
    }

    public class Server
    {
        public int Port { get; private set; }
        public IPAddress Ip { get; private set; }

        private Socket _socket;

        private List<Client> _clients = new();

        private ConcurrentQueue<Tuple<string, string>> _msgsQueue = new();

        public Server(int port = 2004, string ip = "127.0.0.25")
        {
            Port = port;
            Ip = IPAddress.Parse(ip);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipPoint = new(Ip, Port);
            _socket.Bind(ipPoint);
        }

        public void Listen()
        {
            _socket.Listen(100);
            Console.WriteLine("Сервер запущен. Ожидание подключений...");
            try
            {
                while (true)
                {
                    var client = _socket.Accept();
                    if (client != null)
                    {
                        ConnectClient(client);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Disconnect();
            }
        }

        public void AddMessageToBroadcast(string msg, string senderId)
        {
            _msgsQueue.Enqueue(Tuple.Create(msg, senderId));
            BroadcastMessages();
        }

        private void BroadcastMessages()
        {
            if (_msgsQueue.TryDequeue(out var elem))
            {
                var ID = elem.Item2;
                var msg = elem.Item1;

                var sender = _clients.FirstOrDefault(e => e.ID == ID);
                var str = $"{sender.Name}: {msg}";

                foreach (Client client in _clients)
                {
                    if (client.ID != ID)
                    {
                        client.SendMessage(str);
                    }
                }
                Console.WriteLine(str);
            }
        }
        //public void BroadcastMessage(string message, string clientId)
        //{
        //    var sender = _clients.FirstOrDefault(e => e.ID == clientId);
        //    var mgs = $"{sender.Name}: {message}";

        //    foreach (Client client in _clients)
        //    {
        //        if (client.ID != clientId)
        //        {
        //            client.SendMessage(mgs);
        //        }
        //    }
        //    Console.WriteLine(mgs);
        //}

        public void ConnectClient(Socket clientSocket)
        {
            var client = new Client(clientSocket, this);
            _clients.Add(client);
            AddMessageToBroadcast($"Пользователь {client.Name} ({client.Ip}) присоединяется к чату.", client.ID);

            new Thread(new ThreadStart(client.Process)).Start();
        }

        public void RemoveClient(string clientID)
        {
            var client = _clients.FirstOrDefault(e => e.ID == clientID);
            if (client != null)
            {
                AddMessageToBroadcast($"Пользователь {client.Name} ({client.Ip}) покидает чат.", client.ID);
                _clients.Remove(client);
                client.Close();
            }
        }

        public void Disconnect()
        {
            _socket.Close();
            foreach (var client in _clients)
            {
                RemoveClient(client.ID);
            }
        }
    }

    public class Program
    {
        public static async Task Main()
        {
            try
            {
                var server = new Server();
                await Task.Run(() => server.Listen());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}