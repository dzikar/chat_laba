using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Data.Sqlite;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Threading.Tasks;



namespace chat
{
    public partial class Form1 : Form
    {
        private Socket sListener;
        private EndPoint localEndPoint;
        private Thread receiveThread;
        private IPEndPoint broadcastEndPoint;
        private string NameNode;
        private List<string> onlineUsers = new List<string>();
        private System.Timers.Timer userUpdateTimer;
        private CancellationTokenSource cancellationTokenSource;

        public Button Button1
        {
            get { return button1; }
        }

        public Form1()
        {
            InitializeComponent();
            InitializeSocket();
            StartUserUpdateTimer();
            button1.Text = "Произведите первичную настройку";
            button2.Text = "Настройка";
            button3.Text = "Отправить";
            label2.Text = "Чат";
            label1.Text = "Пользователи в сети";
            textBox1.KeyDown += textBox1_KeyDown;
            richTextBox1.KeyPress += new KeyPressEventHandler(richTextBox1_KeyPress);

        }

        private async void InitializeSocket()
        {
            string pathFile = Application.StartupPath + "\\settings.xml";

            //блокируем кнопку отправки
            button3.Enabled = false;
            // Ожидаем  сеть
            await WaitForNetworkAvailability();

            if (System.IO.File.Exists(pathFile))
            {
                button1.Visible = false;

                // Читаем и парсим XML файл
                ParseXmlSettings(pathFile);

                if (localEndPoint != null)
                {
                    sListener = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    sListener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                    sListener.Bind(localEndPoint); // Привязываем сокет к локальной конечной точке
                    broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, ((IPEndPoint)localEndPoint).Port);

                    // Начинаем прослушивание входящих соединений в отдельном потоке
                    receiveThread = new Thread(ReceiveMessages)
                    {
                        IsBackground = true
                    };
                    receiveThread.Start();
                }
            }
            else
            {
                this.Visible = false;
                button2.Visible = false;
                textBox1.Visible = false;
                button3.Visible = false;

            }

                    /*button2.Text = "Настройка сети";
            using (var connection = new SqliteConnection("Data Source=./mydb.sqlite"))
           {
                connection.Open();
           }
           Console.Read();*/
        }


        //чтение и парсинг xml файла. 
        private void ParseXmlSettings(string pathFile)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(pathFile);
            XmlNode portNode = xmlDoc.SelectSingleNode("//IdentAndSettings/port");
            NameNode = xmlDoc.SelectSingleNode("//IdentAndSettings/nic")?.InnerText ?? "Неизвестный";
            XmlNode IPNode = xmlDoc.SelectSingleNode("//IdentAndSettings/ip");
            //дополнительно на месте заносим данные для сокета
            if (portNode != null)
            {
                int port = int.Parse(portNode.InnerText); // Преобразуем порт в целое число
                IPAddress ipAddr = Dns.GetHostEntry(Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
                localEndPoint = new IPEndPoint(ipAddr, port);
            }
        }

        //---------  блок повторной отправки команды /users даёт знать всем в сети, что пользователь онлайн и производит запрос кто в сети.
        private void StartUserUpdateTimer()
        {
            userUpdateTimer = new System.Timers.Timer(6000); // 60000 миллисекунд = 1 минута
            userUpdateTimer.Elapsed += OnTimedEvent;
            userUpdateTimer.AutoReset = true; // Повторять
            userUpdateTimer.Enabled = true; // Включить таймер
        }
        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            RequestUserList();
        }
        private void RequestUserList()
        {
            byte[] usersRequestData = Encoding.UTF8.GetBytes($"{NameNode}: /users"); // Сообщение приходит в формате "Имя: команда "(В данном случае "Имя: /users")
            sListener.SendTo(usersRequestData, broadcastEndPoint);
        }
        //------ конец блока ---------------------------

        //------ Блок приёма сообщений, все баги где-то тут --------------
        private void ReceiveMessages()
        {
            while (true)
            {
                try
                {
                    byte[] data = new byte[256];
                    EndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    int bytes = sListener.ReceiveFrom(data, ref senderEndPoint);
                    string message = Encoding.UTF8.GetString(data, 0, bytes);

                    // Сообщение приходит в формате "Имя: сообщение" и разбивается на две части с разделителем ": "
                    var (senderName, chatMessage) = ExtractSenderAndMessage(message);

                    // Обработка сервисного сообщения для запроса пользователей
                    if (HandleUserRequest(chatMessage, senderName, senderEndPoint))
            {
                        continue; // Пропускаем оставшуюся логику, если запрос был обработан
                    }

                    // Проверяем, не является ли это сообщение от текущего пользователя
                    if (senderName != NameNode)
                    {
                        // Отображаем сообщения других пользователей
                        DisplayIncomingMessage(senderName, chatMessage); 
                    }
                    else
                    {
                        // Выделяем цветом сообщение текущего пользователя
                        DisplayOwnMessage(chatMessage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при получении сообщения: {ex.Message}");
                }
            }
        }

        //отвечает за обработку запросов на получение списка пользователей
        private bool HandleUserRequest(string chatMessage, string senderName, EndPoint senderEndPoint)
        {
            if (chatMessage == "/users" || senderName == "Неизвестный")
            {
                SendUserList(senderEndPoint);
                AddUser(senderName);
                return true; // Указывает, что запрос был обработан
            }
            return false; // Запрос не был обработан
        }

        // Сообщение приходит в формате "Имя: сообщение" и разбивается на две части с разделителем ": " Можно доробатывать. 
        private (string senderName, string chatMessage) ExtractSenderAndMessage(string message)
        {
            string[] parts = message.Split(new[] { ": " }, 2, StringSplitOptions.None);
            string senderName = parts.Length > 1 ? parts[0] : "Неизвестный"; // Если имя необработано, пишет "Неизвестный"
            string chatMessage = parts.Length > 1 ? parts[1] : message;
            return (senderName, chatMessage);
        }

        //отображение чужих сообщений
        private void DisplayIncomingMessage(string senderName, string chatMessage)
        {
            Invoke(new Action(() =>
            {
                richTextBox1.AppendText($"{senderName}: {chatMessage}\n");
                AddUser(senderName); // Добавляем пользователя, если он ещё не в списке
            }));
        }

        //Отображение своих сообщений
        private void DisplayOwnMessage(string chatMessage)
        {
            Invoke(new Action(() =>
            {
                richTextBox1.SelectionStart = richTextBox1.TextLength;
                richTextBox1.SelectionLength = 0;
                richTextBox1.SelectionColor = Color.Blue;
                richTextBox1.AppendText($"Вы: {chatMessage}\n");
                richTextBox1.SelectionColor = richTextBox1.ForeColor;
            }));
        }
        private void richTextBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Запрещаем ввод с клавиатуры
            e.Handled = true;
        }

        public class User
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Age { get; set; }
        }

        // Класс для хранения информации о пользователе
        public class UserInfo
        {
            public string Name { get; set; }
            public int RequestCount { get; set; }

            public UserInfo(string name)
            {
                Name = name;
                RequestCount = 0;
            }
        }

        // Список пользователей, хранящий информацию о каждом
        private List<UserInfo> userInfos = new List<UserInfo>();

        // Отправляет список пользователей обратно запрашивающему
        private void SendUserList(EndPoint requester)
        {
            UpdateUserRequestCount();

            string userList = string.Join(", ", userInfos.Select(u => u.Name));
            byte[] data = Encoding.UTF8.GetBytes(userList);
            sListener.SendTo(data, requester);
        }

        // Добавляет пользователя в список и обновляет richTextBox2
        private void AddUser(string userName)
        {
            if (userName != "Неизвестный" && userName != NameNode)
            {
                if (!userInfos.Any(u => u.Name == userName))
                {
                    userInfos.Add(new UserInfo(userName));
                    UpdateUserList();
                }
            }
        }

        // Обновляет список
        private void UpdateUserList()
        {
            Invoke(new Action(() =>
            {
                richTextBox2.Clear();
                foreach (var user in userInfos)
                {
                    richTextBox2.AppendText($"{user.Name}\n");
                }
            }));
        }

        // Обновляет счетчик запросов для пользователей
        private void UpdateUserRequestCount()
        {
            foreach (var user in userInfos)
            {
                user.RequestCount++;
            }

            // Удаляет пользователей, не ответивших на 4 запроса
            userInfos.RemoveAll(user => user.RequestCount >= 4);

            // Обновляет список после удаления
            UpdateUserList();
        }

        // Обрабатывает ответ от пользователя
        private void OnUserResponse(string userName)
        {
            var userInfo = userInfos.FirstOrDefault(u => u.Name == userName);
            if (userInfo != null)
            {
                userInfo.RequestCount = 0; // Сбрасываем счетчик, если был получен ответ
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Form2 form2 = new Form2(this);
            form2.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Form2 form2 = new Form2(this);
            form2.Show();

        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Отменяем стандартное поведение, чтобы текст не добавлялся в TextBox
                e.SuppressKeyPress = true;

                // Вызываем обработчик кнопки
                button3_Click(sender, e);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {

            string message = $"{NameNode}: {textBox1.Text}";
            byte[] data = Encoding.UTF8.GetBytes(message);
            sListener.SendTo(data, broadcastEndPoint);
            //richTextBox1.AppendText($"Вы: {message}\n"); // Отображаем только отправленное сообщение
            //richTextBox1.AppendText($"{NameNode}: {textBox1.Text}\n");
            textBox1.Clear();

            if (textBox1.Text.Trim() == "/users")
            {
                byte[] usersRequestData = Encoding.UTF8.GetBytes($"{NameNode}: /users");
                sListener.SendTo(usersRequestData, broadcastEndPoint);
            }
        }



        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            sListener.Close();
            receiveThread.Abort();
            userUpdateTimer.Stop();
            userUpdateTimer.Dispose();
            base.OnFormClosing(e);
        }

        private bool IsNetworkAvailable()
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }

        // Асинхронный метод для ожидания доступности сети
        private async Task WaitForNetworkAvailability()
        {
            cancellationTokenSource = new CancellationTokenSource();

            while (true)
            {
                if (IsNetworkAvailable())
                {
                    button3.Enabled = true; // Разблокируем кнопку
                    break; // Выходим из цикла, если сеть доступна
                }

                button3.Enabled = false; // Заблокируем кнопку
                await Task.Delay(1000); // Ждем 1 секунду перед следующей проверкой
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
