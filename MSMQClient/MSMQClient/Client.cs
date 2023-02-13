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
using System.IO;
using System.Messaging;
using System.Threading;

namespace MSMQ
{
    public partial class frmMain : Form
    {
        private MessageQueue q = null;      // очередь сообщений, в которую будет производиться запись сообщенийDNS
        private readonly string pathMQ = ".\\private$\\ServerQueue";

        private Guid clientId;

        private MessageQueue currentQueue = null;          // очередь сообщений
        private Thread t = null;                // поток, отвечающий за работу с очередью сообщений
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
            btnSend.Enabled = false;
            
            clientId = Guid.NewGuid();
        }

        private void CreateClientQueue()
        {
            string path = Dns.GetHostName() + "\\private$\\" + clientId.ToString();

            if (MessageQueue.Exists(path))
                currentQueue = new MessageQueue(path);
            else
                currentQueue = MessageQueue.Create(path);

            currentQueue.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });
        }

        // получение сообщения
        private void ReceiveMessage()
        {
            if (currentQueue == null)
                return;

            System.Messaging.Message msg = null;

            // входим в бесконечный цикл работы с очередью сообщений
            while (_continue)
            {
                if (currentQueue.Peek() != null)   // если в очереди есть сообщение, выполняем его чтение, интервал до следующей попытки чтения равен 10 секундам
                    msg = currentQueue.Receive(TimeSpan.FromSeconds(10.0));

                rtbMessages.Invoke((MethodInvoker)delegate
                {
                    if (msg != null)
                        rtbMessages.Text += "\n >> " + msg.Body;     // выводим полученное сообщение на форму
                });

                Thread.Sleep(500);          // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (MessageQueue.Exists(pathMQ))
            {
                // если очередь, путь к которой указан в поле tbPath существует, то открываем ее
                q = new MessageQueue(pathMQ);
                btnSend.Enabled = true;
                btnConnect.Enabled = false;
                tbName.Enabled = false;

                CreateClientQueue();
                q.Send("Create" + clientId.ToString(), clientId.ToString());

                Thread t = new Thread(ReceiveMessage);
                t.Start();
            }
            else
                MessageBox.Show("Указан неверный путь к очереди, либо очередь не существует");
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string message = tbName.Text + " >> " + tbMessage.Text;

            // выполняем отправку сообщения в очередь
            q.Send(message, clientId.ToString());
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с очередью сообщений завершена

            if (MessageQueue.Exists(pathMQ))
            {
                q.Send("Delete" + clientId.ToString(), clientId.ToString());
            }

            if (t != null)
            {
                t.Abort();          // завершаем поток
            }

            if (currentQueue != null)
            {
                currentQueue.Close();
                currentQueue.Dispose(); 

                MessageQueue.Delete(currentQueue.Path);      // в случае необходимости удаляем очередь сообщений
            }
        }
    }
}