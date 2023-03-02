//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


//------------------------------------------------------------------------------
// <copyright file="SocketAddress.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Net.Sockets;
using System.Text;
using System.Globalization;
using System.Diagnostics;

namespace System.Net
{

    // a little perf app measured these times when comparing the internal
    // buffer implemented as a managed byte[] or unmanaged memory IntPtr
    // that's why we use byte[]
    // byte[] total ms:19656
    // IntPtr total ms:25671

    /// <devdoc>
    ///    <para>
    ///       This class is used when subclassing EndPoint, and provides indication
    ///       on how to format the memeory buffers that winsock uses for network addresses.
    ///    </para>
    /// </devdoc>
    public class SocketAddressEx
    {

        internal const int IPv6AddressSize = 28;
        internal const int IPv4AddressSize = 16;

        internal int m_Size;
        internal byte[] m_Buffer;

        private const int WriteableOffset = 2;

        //
        // Address Family
        //
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public AddressFamily Family
        {
            get
            {
                int family;
#if BIGENDIAN
                family = ((int)m_Buffer[0]<<8) | m_Buffer[1];
#else
                family = m_Buffer[0] | ((int)m_Buffer[1] << 8);
#endif
                return (AddressFamily)family;
            }
        }
        //
        // Size of this SocketAddressEx
        //
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public int Size
        {
            get
            {
                return m_Size;
            }
        }

        //
        // access to unmanaged serialized data. this doesn't
        // allow access to the first 2 bytes of unmanaged memory
        // that are supposed to contain the address family which
        // is readonly.
        //
        // <SECREVIEW> you can still use negative offsets as a back door in case
        // winsock changes the way it uses SOCKADDR. maybe we want to prohibit it?
        // maybe we should make the class sealed to avoid potentially dangerous calls
        // into winsock with unproperly formatted data? </SECREVIEW>
        //
        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public byte this[int offset]
        {
            get
            {
                //
                // access
                //
                if (offset < 0 || offset >= Size)
                {
                    throw new IndexOutOfRangeException();
                }
                return m_Buffer[offset];
            }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public SocketAddressEx(AddressFamily family, int size)
        {
            if (size < WriteableOffset)
            {
                //
                // it doesn't make sense to create a socket address with less than
                // 2 bytes, that's where we store the address family.
                //
                throw new ArgumentOutOfRangeException("size");
            }
            m_Size = size;
            m_Buffer = new byte[(size / IntPtr.Size + 2) * IntPtr.Size];//sizeof DWORD

#if BIGENDIAN
            m_Buffer[0] = unchecked((byte)((int)family>>8));
            m_Buffer[1] = unchecked((byte)((int)family   ));
#else
            m_Buffer[0] = unchecked((byte)((int)family));
            m_Buffer[1] = unchecked((byte)((int)family >> 8));
#endif
        }

        internal SocketAddressEx(IPAddressEx ipAddress)
            : this(ipAddress.AddressFamily,
                ((ipAddress.AddressFamily == AddressFamily.InterNetwork) ? IPv4AddressSize : IPv6AddressSize))
        {

            // No Port
            m_Buffer[2] = (byte)0;
            m_Buffer[3] = (byte)0;

            if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // No handling for Flow Information
                m_Buffer[4] = (byte)0;
                m_Buffer[5] = (byte)0;
                m_Buffer[6] = (byte)0;
                m_Buffer[7] = (byte)0;

                // Scope serialization
                long scope = ipAddress.ScopeId;
                m_Buffer[24] = (byte)scope;
                m_Buffer[25] = (byte)(scope >> 8);
                m_Buffer[26] = (byte)(scope >> 16);
                m_Buffer[27] = (byte)(scope >> 24);

                // Address serialization
                byte[] addressBytes = ipAddress.GetAddressBytes();
                for (int i = 0; i < addressBytes.Length; i++)
                {
                    m_Buffer[8 + i] = addressBytes[i];
                }
            }
            else
            {
                // IPv4 Address serialization
                m_Buffer[4] = unchecked((byte)(ipAddress.m_Address));
                m_Buffer[5] = unchecked((byte)(ipAddress.m_Address >> 8));
                m_Buffer[6] = unchecked((byte)(ipAddress.m_Address >> 16));
                m_Buffer[7] = unchecked((byte)(ipAddress.m_Address >> 24));
            }
        }

        internal SocketAddressEx(IPAddressEx ipaddress, int port)
            : this(ipaddress)
        {
            m_Buffer[2] = (byte)(port >> 8);
            m_Buffer[3] = (byte)port;
        }

        public override string ToString()
        {
            StringBuilder bytes = new StringBuilder();
            for (int i = WriteableOffset; i < this.Size; i++)
            {
                if (i > WriteableOffset)
                {
                    bytes.Append(",");
                }
                bytes.Append(this[i].ToString(NumberFormatInfo.InvariantInfo));
            }
            return Family.ToString() + ":" + Size.ToString(NumberFormatInfo.InvariantInfo) + ":{" + bytes.ToString() + "}";
        }

    } // class SocketAddressEx


} // namespace System.Net
