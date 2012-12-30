using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;

namespace Org.Reddragonit.Net.TFTP
{
    public delegate void delLogLine(LogLevels level, string line);
    public delegate Stream delGetStream(string filename,IPEndPoint endpoint);
}
