//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//

//------------------------------------------------------------------------------
// <copyright file="_OSSOCK.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace System.Net
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LingerEx
    {
        internal ushort OnOff; // option on/off
        internal ushort Time; // linger time
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WSADataEx
    {
        internal short wVersion;
        internal short wHighVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        internal string szDescription;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        internal string szSystemStatus;
        internal short iMaxSockets;
        internal short iMaxUdpDg;
        internal IntPtr lpVendorInfo;
    }

    //
    // used as last parameter to WSASocket call
    //
    [Flags]
    internal enum SocketConstructorFlagsEx
    {
        WSA_FLAG_OVERLAPPED = 0x01,
        WSA_FLAG_MULTIPOINT_C_ROOT = 0x02,
        WSA_FLAG_MULTIPOINT_C_LEAF = 0x04,
        WSA_FLAG_MULTIPOINT_D_ROOT = 0x08,
        WSA_FLAG_MULTIPOINT_D_LEAF = 0x10,
    }
} // namespace System.Net
