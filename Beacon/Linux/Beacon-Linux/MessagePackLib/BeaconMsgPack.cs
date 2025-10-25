using MPLib.MP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Beacon.MessagePackLib
{
    public class BeaconMsgPack : MsgPack
    {
        //自动加上“BC”字段，值为“C”
        public override byte[] Encode2Bytes()
        {
            this.ForcePathObject("BC").AsString = "B";
            return base.Encode2Bytes();
        }

    }
}
