using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Timers;

namespace Org.Reddragonit.Net.TFTP
{
    internal class TFTPStream : IDisposable 
    {
        private const int _dataSize = 512;
        
        private BinaryReader _dataReader;
        private int _curBlock=1;
        private int _lastBlockLength = -1;
        private bool _reTrans = false;
        private Timer _checkThread;
        private Server _ser;

        private IPEndPoint _endPoint;
        public IPEndPoint EndPoint
        {
            get { return _endPoint; }
        }

        private int _timeout = 255;
        public int Timeout
        {
            get { return _timeout; }
        }

        public bool IsComplete
        {
            get { return _lastBlockLength==0; }
        }

        public void Dispose(){
            _dataReader.Close();
        }

        public TFTPStream(IPEndPoint endpoint, Stream stream,int timeout,Server ser)
        {
            _dataReader = new BinaryReader(stream);
            _endPoint = endpoint;
            _timeout = timeout;
            _ser = ser;
        }

        public int ReadBlock(int blocknum,ref byte[] buffer,int start)
        {
            if ((_checkThread != null) && !_reTrans)
            {
                _ser.Log(LogLevels.Trace,"Closing resend thread");
                _reTrans = false;
                _checkThread.Stop();
                _checkThread.Enabled = false;
            }
            if (_checkThread == null)
            {
                _checkThread = new Timer();
                _checkThread.Interval = _timeout * 1000;
                _checkThread.AutoReset = false;
                _checkThread.Elapsed += new ElapsedEventHandler(_checkThread_Elapsed);
                _checkThread.Enabled = false;
            }
            int ret = 0;
            _ser.Log(LogLevels.Trace,"Checking to see if more data available to send for stream for endpoint: " + _endPoint.Address.ToString());
            if (_lastBlockLength != 0) // still more to send
            {
                _ser.Log(LogLevels.Trace,"Moving stream for endpoint: " + _endPoint.Address.ToString()+" to position: "+((blocknum-1)*512).ToString());
                _dataReader.BaseStream.Seek((blocknum - 1) * 512, SeekOrigin.Begin);
                _ser.Log(LogLevels.Trace,"Reading in next chunk for stream for endpoint: " + _endPoint.Address.ToString());
                ret = _dataReader.Read(buffer, start, _dataSize);
                _ser.Log(LogLevels.Trace,"Data block of size " + ret.ToString() + " was read for stream for endpoint: " + _endPoint.Address.ToString());
                _curBlock++;
                _ser.Log(LogLevels.Trace,"Starting timeout thread for stream for endpoint: " + _endPoint.Address.ToString());
                _checkThread.Start();
            }
            _lastBlockLength = ret;
            return ret;
        }

        private void _checkThread_Elapsed(object sender, ElapsedEventArgs e)
        {
            _ser.Log(LogLevels.Trace,"Retransmitting block " + _curBlock.ToString() + " to endpoint " + _endPoint.Address.ToString());
            _reTrans = true;
            if (!_ser.Done)
                _ser.SendStream(EndPoint, _curBlock - 1);
        }
    }
}
