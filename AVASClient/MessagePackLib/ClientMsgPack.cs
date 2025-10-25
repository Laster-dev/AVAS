using MPLib.MP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AVASClient.MessagePackLib
{

    public class ClientMsgPack : MsgPack
    {
        internal static void Init()
        {
            //发送给服务端的消息包注册
            ClientMsgPack clientMsgPack = new ClientMsgPack();
            if (Settings.client == null) 
                throw  new ArgumentNullException("ClientMsgPack.Init:client是空的");
            clientMsgPack.ForcePathObject("Pac_ket").SetAsString("GetBeaconList");
            Settings.client.SendFramed(clientMsgPack.Encode2Bytes());
        }

        //自动加上“BC”字段，值为“C”
        public override byte[] Encode2Bytes()
        {
            this.ForcePathObject("BC").AsString = "C";
           // this.ForcePathObject("CID").AsString = Settings.CID;
            return base.Encode2Bytes();
        }

    }

}
