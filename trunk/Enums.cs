using System;
using System.Collections.Generic;
using System.Text;

namespace Org.Reddragonit.Net.TFTP
{
    public enum LogLevels
    {
        Debug,
        Error,
        Trace
    }

    #region Opcode
    internal enum Opcodes
    {
        TFTP_RRQ = 01,		// TFTP read request packet.
        TFTP_WRQ = 02,		// TFTP write request packet. 
        TFTP_DATA = 03,		// TFTP data packet. 
        TFTP_ACK = 04,		// TFTP acknowledgement packet.
        TFTP_ERROR = 05,		// TFTP error packet. 
        TFTP_OACK = 06		// TFTP option acknowledgement packet. 
    }
    #endregion

    #region ErrorCodes
    internal enum ErrorCodes
    {
        TFTP_ERROR_UNDEFINED = 0,	// Not defined, see error message (if any).
        TFTP_ERROR_FILE_NOT_FOUND = 1,	// File not found.
        TFTP_ERROR_ACCESS_VIOLATION = 2,    // Access violation.
        TFTP_ERROR_ALLOC_ERROR = 3,	// Disk full or allocation exceeded.
        TFTP_ERROR_ILLEGAL_OP = 4,    // Illegal TFTP operation.
        TFTP_ERROR_UNKNOWN_TID = 5,	// Unknown transfer ID.
        TFTP_ERROR_FILE_EXISTS = 6,    // File already exists.
        TFTP_ERROR_INVALID_USER = 7     //   No such user.
    }
    #endregion
}
