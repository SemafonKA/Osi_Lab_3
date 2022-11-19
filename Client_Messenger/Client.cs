using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Messenger
{
    public class Program
    {
        public static string serverIP = "192.168.2.103";
        public static int serverPort = 2004;
        public static Socket? userSocket;
        public static async Task Main()
        {
            Console.Write("Введите IP адрес сервера: ");
            serverIP = Console.ReadLine()!;
            IPAddress serverIPStruct;
            if (!IPAddress.TryParse(serverIP, out serverIPStruct))
            {
                Console.WriteLine($"IP-адресс введён некорректно. Повторите попытку.");
                return;
            }

            Console.Write("Введите порт сервера (2004 по умолчанию): ");
            if (!int.TryParse(Console.ReadLine(), out serverPort))
            {
                serverPort = 2004;
                Console.WriteLine($"Номер порта введён некорректно. Выбран порт по умолчанию ({serverPort})");
            }

            string name;
            Console.Write("Введите своё имя: ");
            name = Console.ReadLine()!;
            // Создаём сокет для юзера
            userSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            if (userSocket == null)
            {
                Console.WriteLine("Произошла ошибка создания пользовательского сокета.");
                return;
            }

            // Задаём конечный адрес сокета как адрес:порт сервера
            IPEndPoint ipPoint = new IPEndPoint(serverIPStruct, serverPort);

            Console.WriteLine("Попытка подключения к серверу...");
            try
            {
                // Подключаемся к серверу по сокету
                userSocket.Connect(ipPoint);
                Console.WriteLine("Вы успешно подключились к серверу.");
                Console.WriteLine("Для выхода с сервера напишите в чат /exit");
                // Отправляем на сервер своё имя
                userSocket.Send(Encoding.Unicode.GetBytes(name));

                // Запускаем таски ресивера и сендера сообщений
                var reader = Task.Run(MessageReceiver);
                var writer = Task.Run(MessageSender);
                // Дожидаемся окончания хотя бы одного таска
                await Task.WhenAny(reader, writer);

            } catch (Exception e)
            {
                Console.WriteLine("Что-то пошло не так...");
                Console.WriteLine(e.Message);
            }
            finally
            {
                Close();
                Console.WriteLine("Соединение завершено.");
                Console.ReadLine();
            }
        }

        public static void MessageSender()
        {
            try
            {
                while (userSocket != null && IsSocketConnected(userSocket))
                {
                    // Читаем с консоли сообщение
                    var msg = Console.ReadLine();
                    if (msg == "/exit")
                    {
                        break;
                    }
                    byte[] bytesMsg = Encoding.Unicode.GetBytes(msg);
                    // Отправляем его в виде байтов
                    userSocket.Send(bytesMsg);
                }
            }
            catch (Exception e)
            {
                return;
            }
        }

        public static void MessageReceiver()
        {
            try
            {
                while (userSocket != null && IsSocketConnected(userSocket))
                {
                    // Ресивим сообщения, если ресивятся
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
            catch (Exception e)
            {
                return;
            }
        }

        public static bool IsSocketConnected (Socket s)
        {
            return !(s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0) || !s.Connected);
        }

        public static void Close()
        {
            // Закрываем подключение по сокету по двум сторонам
            userSocket?.Shutdown(SocketShutdown.Both);
            // Закрываем сокет
            userSocket?.Close();
            userSocket = null;
        }
    }
}