using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatServer
{
    class Program
    {
        public static ManualResetEvent receiveDone = new ManualResetEvent(false);
        public static ManualResetEvent acceptDone = new ManualResetEvent(false);

        public class StateObject
        {
            public byte[] receivedMessage;
            public Socket handler;

            public StateObject()
            {
                receivedMessage = new byte[1024];
            }
        }

        volatile public static List<StateObject> states = new List<StateObject>();

        static void Main(string[] args)
        {
            Console.WriteLine("What IP address would you like to bind to? (Remember to port forward port 60606)");
            string userInput = Console.ReadLine();

            IPAddress ipAddress;

            while(!IPAddress.TryParse(userInput, out ipAddress))
            {
                Console.WriteLine("Try another address.");
                userInput = Console.ReadLine();
            }

            //IPAddress ipAddress = IPAddress.Parse("192.168.0.191");
            IPEndPoint msgEndPoint = new IPEndPoint(ipAddress, 60606);

            Socket msgSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            Console.WriteLine("Creating socket...");

            msgSocket.Bind(msgEndPoint);
            msgSocket.Listen(100);

            Console.WriteLine("Binding and listening...");

            try
            {
                while (true)
                {
                    acceptDone.Reset();
                    Console.WriteLine("Awating connection...");
                    msgSocket.BeginAccept(new AsyncCallback(AcceptCallback), msgSocket);
                    acceptDone.WaitOne();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("BeginAccept method error.");
                Console.WriteLine(e.ToString());
            }
        }

        private static void AcceptCallback(IAsyncResult ar)
        {
            Console.WriteLine("Connection accepted.");
            Socket socket = (Socket)ar.AsyncState;
            StateObject state = new StateObject();
            state.handler = socket.EndAccept(ar);

            states.Add(state);

            Thread thr = new Thread(() => StartListen(state));
            thr.Start();
            acceptDone.Set();
        }

        private static void StartListen(StateObject state)
        {
            while(true)
            {
                try
                {
                    Console.WriteLine("Listening...");
                    receiveDone.Reset();
                    state.handler.BeginReceive(state.receivedMessage, 0, state.receivedMessage.Length, 0, new AsyncCallback(ReceiveCallback), state);
                    receiveDone.WaitOne();
                }
                catch(Exception e)
                {
                    Console.WriteLine("StartListen method error.");
                    Console.WriteLine(e.ToString());
                    state.handler.Close();
                    break;
                }
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            try
            {
                Console.WriteLine("Message received.");
                state.handler.EndReceive(ar);

                string nonparsedMessage = Encoding.UTF8.GetString(state.receivedMessage);
                Console.WriteLine("Nonparsed message: " + nonparsedMessage);
                Console.WriteLine("Starting echo thread.");
                Thread thr = new Thread(() => StartEcho(nonparsedMessage));
                thr.Start();
                //state.handler.Send(EchoMessage(nonparsedMessage));

                Array.Clear(state.receivedMessage, 0 ,state.receivedMessage.Length);
                receiveDone.Set();
            }
            catch(Exception e)
            {
                Console.WriteLine("ReceiveCallback method error.");
                Console.WriteLine(e.ToString());
                state.handler.Close();
            }
        }

        private static void StartEcho(string nonparsedMessage)
        {
            byte[] message = EchoMessage(nonparsedMessage);
            Console.WriteLine("Message to echo: " + Encoding.UTF8.GetString(message));
            Console.WriteLine("Echoing...");

            List<StateObject> aliveStates = new List<StateObject>();

            foreach (StateObject state in states)
            {
                try
                {
                    Console.WriteLine("Echo.");
                    state.handler.Send(message);
                    aliveStates.Add(state);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Echo message error.");
                    Console.WriteLine(e.ToString());
                }
            }

            states = aliveStates;
        }

        private static byte[] EchoMessage(string nonparsedMessage)
        {
            if(nonparsedMessage.Contains("<NAME>"))
            {
                byte[] msg = Encoding.UTF8.GetBytes(nonparsedMessage.Substring(0, nonparsedMessage.IndexOf("<NAME>")) + " has joined the chat.");
                byte[] sentinel = Encoding.UTF8.GetBytes("<EOF>");
                byte[] message = new byte[msg.Length + sentinel.Length];

                Buffer.BlockCopy(msg, 0, message, 0, msg.Length);
                Buffer.BlockCopy(sentinel, 0, message, msg.Length, sentinel.Length);

                Console.WriteLine(Encoding.UTF8.GetString(msg));

                return message;
            }
            else if(nonparsedMessage.Contains("<MSG>"))
            {
                Console.WriteLine(nonparsedMessage);

                byte[] from = Encoding.UTF8.GetBytes(nonparsedMessage.Substring(0, nonparsedMessage.IndexOf("<FROM>")) + ": ");

                nonparsedMessage = nonparsedMessage.Remove(0, nonparsedMessage.IndexOf("<FROM>") + 6);

                byte[] msg = Encoding.UTF8.GetBytes(nonparsedMessage.Substring(0, nonparsedMessage.IndexOf("<MSG>")));
                byte[] sentinel = Encoding.UTF8.GetBytes("<EOF>");
                byte[] message = new byte[from.Length + msg.Length + sentinel.Length];

                Buffer.BlockCopy(from, 0, message, 0, from.Length);
                Buffer.BlockCopy(msg, 0, message, from.Length, msg.Length);
                Buffer.BlockCopy(sentinel, 0, message, from.Length + msg.Length, sentinel.Length);

                Console.WriteLine(Encoding.UTF8.GetString(message));

                return message;
            }
            else if(nonparsedMessage.Contains("<BYE>"))
            {
                string name = nonparsedMessage.Substring(0, nonparsedMessage.IndexOf("<BYE>"));
                byte[] message = Encoding.UTF8.GetBytes(name + " has left the chat.<EOF>");
                return message;
            }
            return Encoding.UTF8.GetBytes("SERVER ERROR - COULD NOT PARSE SENT MESSAGE<EOF>");
        }
    }
}
