    /// <summary>
    /// https://github.com/KiwifruitDev/KCOM/blob/main/LICENSE.md
    /// Based heavily upon https://github.com/KiwifruitDev/KCOM/blob/main/KiwisCoOpMod/VConsole.cs
    /// Took the liberty of improving upon the code, 
    /// however credit for the research and reverse engineering of VConsole protocol
    /// (presumably) belongs to KiwifruitDev.
    /// </summary>

    public class VConsole // Bareminimum interface for Half-Life Alyx's VConsole.
    {
        public int VConsolePort = 29000;
        private static int VConsoleProtocol = 211;

        private static byte[] VConsoleCommandHeader = Encoding.ASCII.GetBytes("CMND").Reverse().ToArray();
        private static byte[] VConsoleFocusWindowHeader = Encoding.ASCII.GetBytes("VFCS").Reverse().ToArray();

        private Stream stream;

        private StreamWatcher streamWatcher;

        private TcpClient client;

        public List<byte[]> commandQueue = new();

        public event VConsoleOnPrintEventHandler? VConsoleOnPrint;


        private Task commandWriter;
        public bool Connect()
        {
            Disconnect();

            if (!IsVConsoleListening())
                return false;

            client = new TcpClient("127.0.0.1", VConsolePort);
            stream = client.GetStream();

            streamWatcher = new StreamWatcher(stream);
            streamWatcher.MessageAvailable += MessageAvailable;
            streamWatcher.SetWorking(true);

            foreach (var cmd in commandQueue)
            {
                SendCommand(cmd,true);
            }
            commandQueue.Clear();

            commandWriter = Task.Run(CommandQueueProcessor);
            Console.WriteLine("Initialised!");
            return true;
        }
        public void Disconnect()
        {
            if (client == null || stream == null)
                return;

            SendCommand("disconnect");
            commandQueue.Clear();

            client.Close();
            client.Dispose();
            client = null;

            streamWatcher.SetWorking(false);

            stream.Close();
            stream.Dispose();
            stream = null;
        }

        public void SendCommand(string command, bool priority=false)
        {
            if (client == null || stream == null)
            {
                return;
            }
            byte[] cmdData = ProcessCommand(command);
            SendCommand(cmdData, priority);
        }
        private void SendCommand(byte[] cmdData, bool priority = false)
        {
            if (client == null || stream == null)
            {
                return;
            }
            if (priority && (client != null && stream !=null))
            {
                stream.Write(cmdData);
                return;
            }
            commandQueue.Add(cmdData);
        }
        public void SendSetWindowFocus(bool focused = true)
        {
            if (client == null || stream == null)
            {
                return;
            }
          
            byte protocol = Convert.ToByte(VConsoleProtocol);
            byte dataLength = Convert.ToByte(13);
            List<byte> dataList = new()
            {
                0,
                protocol,
                0,
                0,
                0,
                dataLength,
                0,
                0,
                (byte)(focused == true ? 1 : 0)
            };
            foreach (byte cmdByte in VConsoleFocusWindowHeader)
            {
                dataList = dataList.Prepend(cmdByte).ToList();
            }
            SendCommand(dataList.ToArray());
        }
        private byte[] ProcessCommand(string command)
        {

            byte[] data = Encoding.ASCII.GetBytes(command);
            byte dataLength = Convert.ToByte(data.Length + 13);
            byte protocol = Convert.ToByte(VConsoleProtocol);
            List<byte> dataList = new()
            {
                0, protocol, 0, 0, 0, dataLength, 0, 0
            };
            foreach (byte cmdByte in VConsoleCommandHeader)
            {
                dataList = dataList.Prepend(cmdByte).ToList();
            }
            foreach (byte cmdByte in data)
            {
                dataList = dataList.Append(cmdByte).ToList();
            }
            dataList.Add(0x00);
            return dataList.ToArray();
        }

        private bool IsVConsoleListening()
        {
            System.Net.IPEndPoint[] endPoints = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (System.Net.IPEndPoint endPoint in endPoints)
            {
                if (endPoint.Port == VConsolePort)
                {
                    return true;
                }
            }
            return false;
        }

        private void MessageAvailable(object sender, MessageAvailableEventArgs e)
        {
            string command = e.MessageType.ToUpper();

            switch (command)
            {
                case "PRNT":
                    byte[] message = e.Data.Skip(30).SkipLast(1).ToArray();
                    string newMessage = Encoding.ASCII.GetString(message);//.Replace("\n", "");

                    if (newMessage == "" && !newMessage.Contains("Command buffer full"))
                    {
                        break;
                    }
                    VConsoleOnPrint.Invoke(this, new(newMessage));
                    /*Response input = new("print", newMessage);
                    input.timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
                    ws.Send(JsonConvert.SerializeObject(input));*/
                    break;
            }
        }
        private void ProcessNextCommandInQueue()
        {
            if (commandQueue.Count < 1) return;
            SendCommand(commandQueue[0], true);
            commandQueue.RemoveAt(0);
        }

        private void CommandQueueProcessor()
        {
            while (!(client == null || stream == null))
            {
                ProcessNextCommandInQueue();
            }
        }

        public delegate void VConsoleOnPrintEventHandler(object sender, VConsolePrintEventArgs e);

        public class VConsolePrintEventArgs : EventArgs
        {
            public VConsolePrintEventArgs(string message) : base()
            {
                Message = message;
            }

            public string Message { get; private set; }
        }
    }
