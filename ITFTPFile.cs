using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;

namespace Org.Reddragonit.Net.TFTP
{
    public interface ITFTPFile
    {
        bool CanProduceStream(IPEndPoint endPoint);
        Stream GetStream(IPEndPoint endPoint);
        string Path
        {
            get;
        }
    }
}
