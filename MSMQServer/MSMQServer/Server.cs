using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Messaging;
using MSMQServer;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
using System.IO;

namespace MSMQ
{
    public partial class frmMain : Form
    {
        private MessageQueue q = null;          // очередь сообщений
        private Thread t = null;                // поток, отвечающий за работу с очередью сообщений
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом

        private Dictionary<string, MessageQueue> _allClients = new Dictionary<string, MessageQueue>();

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
            string path = Dns.GetHostName() + "\\private$\\ServerQueue";    // путь к очереди сообщений, Dns.GetHostName() - метод, возвращающий имя текущей машины

            // если очередь сообщений с указанным путем существует, то открываем ее, иначе создаем новую
            if (MessageQueue.Exists(path))
                q = new MessageQueue(path);
            else
                q = MessageQueue.Create(path);

            // задаем форматтер сообщений в очереди
            q.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });

            // вывод пути к очереди сообщений в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + q.Path;

            // создание потока, отвечающего за работу с очередью сообщений
            Thread t = new Thread(ReceiveMessage);
            t.Start();
        }

        // проверка клиента
        private void CreateClient(string clientId)
        {
            if (!_allClients.ContainsKey(clientId))
            {
                if (MessageQueue.Exists(".\\private$\\" + clientId))
                    _allClients.Add(clientId, new MessageQueue(".\\private$\\" + clientId));
            }
        }

        // удаление клиента из списка
        private bool ClientDelete(System.Messaging.Message message)
        {
            if ("Delete" + message.Label.ToString() == message.Body.ToString())
            {
                _allClients.Remove(message.Label.ToString());
                return false;    
            }
            else
                return true;
        }

        private void SendMessagesToClients(string message)
        {
            foreach (var client in _allClients)
            {
                client.Value.Send(message, "Server");
            }
        }

        // получение сообщения
        private void ReceiveMessage()
        {
            if (q == null)
                return;

            System.Messaging.Message msg = null;

            // входим в бесконечный цикл работы с очередью сообщений
            while (_continue)
            {
                if (q.Peek() != null)   // если в очереди есть сообщение, выполняем его чтение, интервал до следующей попытки чтения равен 10 секундам
                    msg = q.Receive(TimeSpan.FromSeconds(10.0));

                if ("Create" + msg.Label.ToString() == msg.Body.ToString())
                    CreateClient(msg.Label.ToString());

                else
                {
                    // Check if message sent by client was a stoping message
                    bool isDeleted = ClientDelete(msg);

                    if (isDeleted)
                    {
                        rtbMessages.Invoke((MethodInvoker)delegate
                        {
                            if (msg != null)
                                rtbMessages.Text += "\n >> " + msg.Body.ToString();     // выводим полученное сообщение на форму
                        });

                        SendMessagesToClients(msg.Body.ToString());
                    }
                }

                Thread.Sleep(500);          // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с очередью сообщений завершена

            if (t != null)
            {
                t.Abort();          // завершаем поток
            }

            if (q != null)
            {
                q.Close();
                q.Dispose();
                // MessageQueue.Delete(q.Path);      // в случае необходимости удаляем очередь сообщений
            }
        }
    }
}