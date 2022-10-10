using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Messenger
{
    public class Program
    {
        public static string serverIP = "192.168.2.103";
        public static int serverPort = 2004;
        public static Socket userSocket;
        public static async Task Main()
        {
            Console.Write("Введите IP адрес сервера: ");
            serverIP = Console.ReadLine()!;
            Console.Write("Введите порт сервера (2004 по умолчанию): ");
            if (!int.TryParse(Console.ReadLine(), out serverPort))
            {
                serverPort = 2004;
            }

            string name;
            Console.Write("Введите своё имя: ");
            name = Console.ReadLine()!;
            userSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            Console.WriteLine("Попытка подключения к серверу...");
            try
            {
                userSocket.Connect(ipPoint);
                Console.WriteLine("Вы успешно подключились к серверу.");

                userSocket.Send(Encoding.Unicode.GetBytes(name));

                var reader = Task.Run(MessageReceiver);
                var writer = Task.Run(MessageSender);
                await Task.WhenAll(reader, writer);
            } catch (Exception e)
            {
                Console.WriteLine("Что-то пошло не так...");
                Console.WriteLine(e.Message);
            }
        }

        public static void MessageSender()
        {
            while (true)
            {
                var msg = Console.ReadLine();
                byte[] bytesMsg = Encoding.Unicode.GetBytes(msg);
                userSocket.Send(bytesMsg);
            }
        }

        public static void MessageReceiver()
        {
            while (true)
            {
                StringBuilder receivedMsg = new StringBuilder();
                byte[] data = new byte[256]; // буфер для получаемых данных

                do
                {
                    int bytes = userSocket.Receive(data);
                    receivedMsg.Append(Encoding.Unicode.GetString(data, 0, bytes));
                }
                while (userSocket.Available > 0);

                Console.WriteLine(receivedMsg.ToString());
            }
        }

        public static void Close()
        {
            userSocket.Shutdown(SocketShutdown.Both);
            userSocket.Close();
        }
    }
}