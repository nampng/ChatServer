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

        public static List<StateObject> states = new List<StateObject>();

        static void Main(string[] args)
        {
            IPAddress ipAddress = IPAddress.Parse("192.168.0.191");
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
            catch
            {

            }
        }

        private static void AcceptCallback(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            StateObject state = new StateObject();
            state.handler = socket.EndAccept(ar);

            states.Add(state);

            Thread thr = new Thread(() => StartListen(state));
            thr.SetApartmentState(ApartmentState.MTA);
            thr.Start();
            acceptDone.Set();
        }

        private static void StartListen(StateObject state)
        {
            while(true)
            {
                try
                {
                    receiveDone.Reset();

                    state.handler.BeginReceive(state.receivedMessage, 0, state.receivedMessage.Length, 0, new AsyncCallback(ReceiveCallback), state);

                    receiveDone.WaitOne();
                }
                catch
                {
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
                state.handler.EndReceive(ar);

                string nonparsedMessage = Encoding.UTF8.GetString(state.receivedMessage);

                StartEcho(nonparsedMessage);

                //state.handler.Send(EchoMessage(nonparsedMessage));

                state.receivedMessage = new byte[1024];
                receiveDone.Set();
            }
            catch
            {
                state.handler.Close();
            }
        }

        private static void StartEcho(string nonparsedMessage)
        {
            byte[] message = EchoMessage(nonparsedMessage);
            List<StateObject> aliveStates = new List<StateObject>();

            foreach(StateObject state in states)
            {
                try
                {
                    state.handler.Send(message);
                    aliveStates.Add(state);
                }
                catch
                {
                    state.handler.Close();
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
            return Encoding.UTF8.GetBytes("<EOF>");
        }
    }
}
