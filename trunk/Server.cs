using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace Org.Reddragonit.Net.TFTP
{
	#region TFTP Formats
	//	TFTP Formats
	//	Type   Op #     Format without header
	//         2 bytes    string   1 byte     string   1 byte
	//         -----------------------------------------------
	//	RRQ/  | 01/02 |  Filename  |   0  |    Mode    |   0  |
	//	WRQ    -----------------------------------------------
	//         2 bytes    2 bytes       n bytes
	//		   ---------------------------------
	//	DATA  | 03    |   Block #  |    Data    |
	//	       ---------------------------------
	//	       2 bytes    2 bytes
	//	       -------------------
	//	ACK   | 04    |   Block #  |
	//	       --------------------
	//	       2 bytes  2 bytes        string    1 byte
	//	       ----------------------------------------
	//	ERROR | 05    |  ErrorCode |   ErrMsg   |   0  |
	//	       ----------------------------------------
	#endregion

	/// <summary>
	/// Simple read-only TFTP Server	
	/// </summary>		
	public class Server
	{
        private static readonly char[] _delimStr = new char[] { '\0' };

        public static Server StartServer(IPAddress addy, int port,delGetStream getStream,delLogLine log)
        {
            return new Server(addy,port,getStream,log);
        }

        #region fields
		private UdpClient _conn = null;
        private List<TFTPStream> _streams = null;
        private bool _done;
        public bool Done
        {
            get { return _done; }
        }
        private IPAddress _ip;
        private int _port;
        private delLogLine _log;
        private delGetStream _getStream;
		#endregion

        internal void Log(LogLevels level, string line)
        {
            if (_log != null)
                _log(level, line);
        }

        internal void Log(Exception e)
        {
            Log(LogLevels.Error, e.Message);
            Log(LogLevels.Error, e.StackTrace);
            if (e.InnerException != null)
                Log(e.InnerException);
        }
				
		#region ctor
		private Server(IPAddress addy,int port,delGetStream getStream,delLogLine log)
		{
            _ip = addy;
            _port = port;
            _log = log;
            _getStream = getStream;
		}
		#endregion

		#region Start / Stop
		/// <summary>
		/// Start server
		/// </summary>
		public void Start()
		{
            try
            {
                _done = false;
                if (_conn == null)
                {
                    Log(LogLevels.Debug,"Starting tftp server for " + _ip.ToString() + " by binding on port " + _port.ToString());
                    _conn = new UdpClient(new IPEndPoint(_ip, _port));
                }
                _streams = new List<TFTPStream>();
                _conn.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            }
            catch (Exception e)
            {
                Log(e);
            }
		}

		/// <summary>
		/// Stop server
		/// </summary>
		public void Stop()
		{
			_done = true;
            _conn.Close();
            while (_streams.Count > 0)
            {
                TFTPStream tmp = _streams[0];
                _streams.RemoveAt(0);
                tmp.Dispose();
            }
		}
		#endregion

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                IPEndPoint endpoint=null;
                byte[] data = _conn.EndReceive(ar,ref endpoint);
                if (!_done)
                {
                    Log(LogLevels.Trace,"Message received for TFTP Server restarting async recieve to obtain next message.");
                    _conn.BeginReceive(new AsyncCallback(ReceiveCallback), null);
                }
                if ((data!=null)&&(data.Length>0)&&(!_done)&&(endpoint!=null))
                {
                    Opcodes opcode = (Opcodes)(short)((((short)data[0]) * 256) + (short)data[1]);

                    switch (opcode)
                    {
                        case Opcodes.TFTP_RRQ:
                            DoReadRequest(data, endpoint);
                            break;
                        case Opcodes.TFTP_ERROR:
                            DoError(data, endpoint);
                            break;
                        case Opcodes.TFTP_ACK:
                            DoAck(data, endpoint);
                            break;
                        case Opcodes.TFTP_WRQ:
                        case Opcodes.TFTP_DATA:
                        case Opcodes.TFTP_OACK:
                        default:
                            break;

                    }	
                }
            }
            catch (Exception err)
            {
                Log(err);
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            _conn.EndSend(ar);
        }

		#region DoAck
		/// <summary>
		/// Handle ACK response and send next block.
		/// </summary>
		/// <param name="data">data from packet</param>
		/// <param name="endpoint">client</param>
		private void DoAck(Byte[] data, IPEndPoint endpoint)
		{
            Log(LogLevels.Trace,"ACK Recieved from endpoint: " + endpoint.Address.ToString());
			short blocknum = (short)((((short)data[2]) * 256) + (short)data[3]);
            SendStream(endpoint, blocknum+1);
		}
		#endregion

		#region DoError
		/// <summary>
		/// Parse an error response
		/// </summary>
		/// <param name="data">data from packet</param>
		/// <param name="endpoint">client</param>
		private void DoError(Byte[] data, IPEndPoint endpoint)
		{
	
			Encoding    ASCII = Encoding.ASCII;

			string delimStr = "\0";
			char [] delimiter = delimStr.ToCharArray();

			short errorcode = (short)((((short)data[2]) * 256) + (short)data[3]);

			string[] strData = ASCII.GetString(data,2,data.Length-2).Split(delimiter,3);

			string message = strData[0];

		}

        internal void SendError(IPEndPoint endpoint, ErrorCodes code,string msg)
        {
            byte[] buffer = new byte[5+msg.Length];
            buffer[0]=0;
            buffer[1]=(byte)Opcodes.TFTP_ERROR;
            buffer[2] = (byte)(((short)code & 0xFF00) / 256);
			buffer[3] = (byte)((short)code & 0x00FF);
            if (msg.Length == 0)
            {
                buffer[4] = (byte)'\0';
                _conn.Send(buffer, 5, endpoint);
            }
            else
            {
                MemoryStream ms = new MemoryStream(buffer);
                ms.Position = 4;
                byte[] bmsg = ASCIIEncoding.ASCII.GetBytes(msg);
                ms.Write(bmsg, 0, bmsg.Length);
                ms.WriteByte((byte)'\0');
                _conn.Send(ms.ToArray(), (int)ms.Length, endpoint);
            }
        }
		#endregion

		#region DoReadRequest
		/// <summary>
		/// Handle Read request
		/// </summary>
		/// <param name="data">data from packet</param>
		/// <param name="endpoint">client</param>
		private void DoReadRequest(Byte[] data, IPEndPoint endpoint)
		{
			Encoding    ASCII = Encoding.ASCII;
			string[] strData = ASCII.GetString(data,2,data.Length-2).Split(_delimStr);

			string filename = strData[0];
			string mode = strData[1].ToLower();
            int timeout = 255;
            for (int x = 0; x < strData.Length; x++)
            {
                if (strData[x].ToLower() == "timeout")
                {
                    timeout = int.Parse(strData[x + 1]);
                    break;
                }
            }
            Log(LogLevels.Trace,"Resolving file " + filename + " for endpoint: " + endpoint.Address.ToString());
            Stream str = _getStream(filename,endpoint);
            if (str==null){
                SendError(endpoint,ErrorCodes.TFTP_ERROR_FILE_NOT_FOUND,"Unable to locate requested file in system.");
            }else{
                _streams.Add(new TFTPStream(endpoint,str,timeout,this));
                SendStream(endpoint,1);
            }
		}
		#endregion

		#region SendStream
		/// <summary>
		/// Send part of a stream
		/// </summary>
		/// <param name="endpoint">location to send stream to</param>
		/// <param name="BlockNumber">512 byte block to send</param>
		internal void SendStream(IPEndPoint endpoint, int BlockNumber)
		{
											
			Byte [] buffer = new Byte[516];			
			buffer[0] = 0;
			buffer[1] = (byte)Opcodes.TFTP_DATA;	
			buffer[2] = (byte)((BlockNumber & 0xFF00) / 256);
			buffer[3] = (byte)(BlockNumber & 0x00FF);

            Log(LogLevels.Trace,"Searching for stream attached to endpoint: " + endpoint.Address.ToString());

            TFTPStream str = null;
            foreach (TFTPStream tftps in _streams){
                Log(LogLevels.Trace,"Checking against stream with endpoint: " + endpoint.Address.ToString());
                if (tftps.EndPoint.Address.ToString()==endpoint.Address.ToString()){
                    Log(LogLevels.Trace,"Stream located for endpoint: " + endpoint.Address.ToString());
                    str = tftps;
                    break;
                }
            }

            if(str==null){
                SendError(endpoint,ErrorCodes.TFTP_ERROR_ILLEGAL_OP,"Unable to locate existing stream for request end point.");
            }else if (!_done){
                Log(LogLevels.Trace,"Reading data chunk BLOCK: " + BlockNumber + " for endpoint: " + endpoint.Address.ToString()+" from stream.");
                int len = str.ReadBlock(BlockNumber,ref buffer,4);
                Log(LogLevels.Trace,"Sending data chunk BLOCK: " + BlockNumber + " to endpoint: " + endpoint.Address.ToString()+" of length "+len.ToString());
                _conn.BeginSend(buffer,len+4,endpoint,new AsyncCallback(SendCallback),null);
                Log(LogLevels.Trace,"Sending of chunk BLOCK: " + BlockNumber.ToString() + " to endpoint: " + endpoint.Address.ToString() + " complete, awaiting next ACK");
                if (str.IsComplete){
                    Log(LogLevels.Trace,"Stream completed, closing TFTP Stream for endpoint: " + endpoint.Address.ToString());
                    _streams.Remove(str);
                    str.Dispose();
                }
            }
		}
		#endregion
	}
}
