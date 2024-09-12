using Riptide;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public class SgServerTransport : SgTransport
    {
        public override bool IsServer => true;
        public override bool IsClient => false;

        public ushort Port { private set; get; }
        public ushort MaxClientCount { private set; get; }
        public Server Server { private set; get; }

        public SgServerTransport(SgNetConfigData configData) : base(configData)
        {
            this.Server = new Server();
            this.Server.MessageReceived += this.OnReceiveMessage;
        }

        public void StartServer(ushort port, ushort maxClientCount)
        {
            this.Port = port;
            this.MaxClientCount = maxClientCount;
            this.Server.Start(port, maxClientCount, useMessageHandlers:false);
            RiptideLogger.Log(LogType.Debug, "Server Start");
        }

        public override void NetworkUpdate()
        {
            this.Server.Update();
        }

        public override void SendMessage(string str)
        {
            Message message = Message.Create(MessageSendMode.Unreliable, (ushort)Protocol.ToClient);
            message.AddString(str);
            this.Server.SendToAll(message);
        }
        
        private void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            var msg = args.Message;
            RiptideLogger.Log(LogType.Debug, $"id:{args.MessageId}:" + msg.GetString());
        }
        
        public override void Disconnect()
        {
            this.Server.Stop();
        }
    }
}