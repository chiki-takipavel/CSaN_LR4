using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LR4_CSaN
{
    class Program
    {
        const string PROXY_ADDRESS = "127.0.0.1";
        const int PROXY_PORT = 8080;
        const string blackListPath = "Blaclist.conf";

        private static List<string> blackList;

        /// <summary>
        /// Загружаем чёрный список из файла
        /// </summary>
        /// <param name="path">Путь к файлу чёрного списка</param>
        /// <returns>Чёрный список</returns>
        private static List<string> LoadBlackList(string path)
        {
            if (File.Exists(path))
            {
                return new List<string>(File.ReadAllLines(path));
            }
            else
            {
                File.Create(path);
                return new List<string>();
            }
        }

        static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        static string GetString(byte[] bytes)
        {
            char[] chars = new char[bytes.Length / sizeof(char)];
            System.Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }

        /// <summary>
        /// Точка входа
        /// </summary>
        static void Main()
        {
            blackList = LoadBlackList(blackListPath);

            TcpListener listener = new TcpListener(IPAddress.Parse(PROXY_ADDRESS), PROXY_PORT);
            listener.Start();
            while (true)
            {
                if (listener.Pending())
                {
                    Socket socket = listener.AcceptSocket();
                    Task.Factory.StartNew(() => ListenRequest(socket));
                }
            }
        }

        /// <summary>
        /// Прослушивание запросов
        /// </summary>
        /// <param name="client">Клиент</param>
        private static void ListenRequest(Socket client)
        {
            try
            {
                using NetworkStream clientStream = new NetworkStream(client);
                while (client.Connected)
                {
                    string request = RecieveMessage(clientStream, true);

                    Regex patternHost = new Regex(@"Host: (?<hostline>(((?<host>.+?):(?<port>\d+?))|(?<host>.+?)))\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    Match matchHost = patternHost.Match(request);
                    string host = matchHost.Groups["host"].Value;
                    if (!int.TryParse(matchHost.Groups["port"].Value, out int port)) { port = 80; }

                    if ((blackList.Count != 0) && (blackList.FirstOrDefault(s => s.Contains(host)) != null))
                    {
                        byte[] proxyResponse = GetHTTPError(403, "Forbidden");
                        clientStream.Write(proxyResponse, 0, proxyResponse.Length);
                        throw new Exception("Сайт в чёрном списке");
                    }

                    request = AbsoluteToRelative(request, matchHost.Groups["hostline"].Value);

                    using Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    server.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
                    IPHostEntry hostIp = Dns.GetHostEntry(host);
                    EndPoint endPoint = new IPEndPoint(hostIp.AddressList.First(), port);
                    server.Connect(endPoint);

                    using NetworkStream serverStream = new NetworkStream(server);
                    byte[] bytesRequest = Encoding.ASCII.GetBytes(request);
                    serverStream.Write(bytesRequest, 0, bytesRequest.Length);

                    string response = RecieveMessage(serverStream, false);
                    byte[] bytesResponse = GetBytes(response);
                    clientStream.Write(bytesResponse, 0, bytesResponse.Length);
                    string[] head = Encoding.ASCII.GetString(bytesResponse).Split(new char[] { '\r', '\n' });

                    string responseCode = head[0].Substring(head[0].IndexOf(" ") + 1);
                    Console.WriteLine($"Запрос к {host}\nОтвет: {host} {responseCode}\n");
                    serverStream.CopyTo(clientStream);
                }
            }
            catch
            {
                return;
            }
        }

        /// <summary>
        /// Изменение абсолютного пути на относительный
        /// </summary>
        /// <param name="request">Запрос</param>
        /// <param name="host">Доменное имя</param>
        /// <returns>Запрос с изменёным путём</returns>
        private static string AbsoluteToRelative(string request, string host)
        {
            Regex pattern = new Regex(@".+ (?<url>\S+) .+", RegexOptions.IgnoreCase);
            Match match = pattern.Match(request);
            string oldRequestFirstLine = match.Value;
            string oldUrl = match.Groups["url"].Value;
            string newUrl = oldUrl.Remove(0, oldUrl.IndexOf(host));
            newUrl = newUrl.Replace(host, "");
            if (newUrl.IndexOf("/") >= 0)
            {
                newUrl = newUrl.Remove(0, newUrl.IndexOf("/"));
            }
            string newRequestFirstLine = oldRequestFirstLine.Replace(oldUrl, newUrl);
            return request.Replace(oldRequestFirstLine, newRequestFirstLine);
        }

        private static string RecieveMessage(NetworkStream networkStream, bool encoding)
        {
            byte[] data = new byte[256];
            StringBuilder message = new StringBuilder();
            do
            {
                int size = networkStream.Read(data, 0, data.Length);
                if (encoding)
                {
                    message.Append(Encoding.ASCII.GetString(data, 0, size));
                }
                else
                {
                    message.Append(GetString(data));
                }
            }
            while (networkStream.DataAvailable);
            return message.ToString();
        }

        private static byte[] GetHTTPError(int statusCode, string statusMessage)
        {
            FileInfo fileInfo = new FileInfo(string.Format("HTTP{0}.htm", statusCode));
            byte[] headers = Encoding.ASCII.GetBytes(string.Format("HTTP/1.1 {0} {1}\r\nContent-Type: text/html\r\nContent-Length: {2}\r\n\r\n",
                statusCode, statusMessage, fileInfo.Length));
            byte[] result = null;

            using (FileStream fileStream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read))
            {
                using BinaryReader binaryReader = new BinaryReader(fileStream, Encoding.UTF8);
                result = new byte[headers.Length + fileStream.Length];
                Buffer.BlockCopy(headers, 0, result, 0, headers.Length);
                Buffer.BlockCopy(binaryReader.ReadBytes(Convert.ToInt32(fileStream.Length)), 0, result, headers.Length, Convert.ToInt32(fileStream.Length));
            }

            return result;
        }
    }
}
