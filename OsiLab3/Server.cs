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

            // Получаем от сервера имя
            Name = GetMessage();
        }

        public string GetMessage()
        {
            StringBuilder receivedMsg = new StringBuilder();
            byte[] data = new byte[256]; // буфер для получаемых данных

            do
            {
                // Считываем первую пачку данных до 256 символов
                int bytes = _socket.Receive(data);
                // Переводим пачку байт в юникод строку
                receivedMsg.Append(Encoding.Unicode.GetString(data, 0, bytes));
            }
            while (_socket.Available > 0);
            // Возвращаем полученную строку
            return receivedMsg.ToString();
        }

        public void SendMessage(string msg)
        {
            // Преобразуем сообщение в поток байтов и отправляем его клиенту
            byte[] bytesMsg = Encoding.Unicode.GetBytes(msg);
            _socket.Send(bytesMsg);
        }

        public void Close()
        {
            // Закрываем соединение сокета связи с клиентом для двух сторон
            _socket.Shutdown(SocketShutdown.Both);
            // Закрываем сокет
            _socket.Close();
        }

        public void Process()
        {
            try
            {
                // Пока сокет не закончил соединение (мягко, не аварийно)
                while (_socket.Connected)
                {
                    // Получаем сообщение с процесса
                    var msg = GetMessage();
                    // Если оно есть, то броадкастим его всем клиентам
                    if (msg != "")
                    {
                        _server.AddMessageToBroadcast(msg, ID);
                    }
                }
            }
            catch (Exception e)
            {
                // В случае возникновения ошибки чтения данных (например, клиент завершился аварийно),
                // просто делаем дисконнект клиента
            }
            finally
            {
                // Удаляем соединение клиента из сервера
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

        public Server(string ip = "192.168.2.103", int port = 2004)
        {
            Port = port;
            Ip = IPAddress.Parse(ip);

            // Инициируем сокет сервера, который будет заниматься прослушиванием входящих сообщений
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Указываем ip:port конечной точки сокета (сервера)
            IPEndPoint ipPoint = new(Ip, Port);
            // Биндим сокет с конечной точкой (привязываем сокет к серверу)
            _socket.Bind(ipPoint);
        }

        // Функция прослушивания канала и подключение новых соединений
        public void Listen()
        {
            // Открываем сокет на прослушивание, задаем размер стека очереди на подключение в 100 юзеров
            _socket.Listen(100);
            Console.WriteLine("Сервер запущен. Ожидание подключений...");
            try
            {
                // В вечном цикле ожидаем новые запросы на подключение и соответственно добавляем их в работу сервера
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
            // Добавляем сообщение в потокобезопасную очередь
            _msgsQueue.Enqueue(Tuple.Create(msg, senderId));
            // Вызываем вывод сообщений
            BroadcastMessages();
        }

        private void BroadcastMessages()
        {
            // Пока элементы извлекаются из очереди
            while (_msgsQueue.TryDequeue(out var elem))
            {
                var ID = elem.Item2;
                var msg = elem.Item1;

                // Находим отправителя, чтобы найти его имя
                var sender = _clients.FirstOrDefault(e => e.ID == ID);
                var str = $"{sender.Name}: {msg}";

                // Для всех клиентов, кроме отправителя, выводим месседж
                foreach (Client client in _clients)
                {
                    if (client.ID != ID)
                    {
                        client.SendMessage(str);
                    }
                }
                // Дублируем месседж на сервер
                Console.WriteLine(str);
            }
        }

        public void ConnectClient(Socket clientSocket)
        {
            // Создаём обёртку для подключаемого клиента
            var client = new Client(clientSocket, this);
            // Добавляем клиента в список клиентов сервера
            _clients.Add(client);
            // Выводим в чаты месседж о том, что пользователь подключен
            AddMessageToBroadcast($"Пользователь {client.Name} ({client.Ip}) присоединяется к чату.", client.ID);

            // Начинаем процесс работы с клиентом в отдельном потоке
            new Thread(new ThreadStart(client.Process)).Start();
        }

        public void RemoveClient(string clientID)
        {
            // Находим клиента по его айди
            var client = _clients.FirstOrDefault(e => e.ID == clientID);
            if (client != null)
            {   
                // Броадкастим месседж о выходе юзера
                AddMessageToBroadcast($"Пользователь {client.Name} ({client.Ip}) покидает чат.", client.ID);
                // Удаляем клиента с сервера
                _clients.Remove(client);
                // Закрываем сокет
                client.Close();
            }
        }

        public void Disconnect()
        {
            foreach (var client in _clients)
            {
                // Закрываем всех клиентов
                RemoveClient(client.ID);
            }
            // Закрываем сокет сервера
            _socket.Close();
        }
    }

    public class Program
    {
        public static async Task Main()
        {
            Console.Write("Введите IP адрес для сервера: ");
            var serverIp = Console.ReadLine()!;
            try
            {
                // Инициируем инстанс сервера
                var server = new Server(serverIp);
                // Запускаем в новом потоке службу прослушивания событий сервера
                await Task.Run(() => server.Listen());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}