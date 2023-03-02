//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


//------------------------------------------------------------------------------
// <copyright file="IPAddress.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Net.Sockets;
using System.Globalization;
using System.Text;

namespace System.Net
{
    /// <devdoc>
    ///    <para>Provides an internet protocol (IP) address.</para>
    /// </devdoc>
    [Serializable]
#pragma warning disable CS0659  //warning CS0659: 'IPAddressEx' overrides Object.Equals(object o) but does not override Object.GetHashCode()
    public class IPAddressEx
    {
        public static readonly IPAddressEx Any = new IPAddressEx(0x0000000000000000);

        //
        // IPv6 Changes: make this internal so other NCL classes that understand about
        //               IPv4 and IPv4 can still access it rather than the obsolete property.
        //
        internal long m_Address;

        public static readonly IPAddressEx IPv6Any = new IPAddressEx(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0);

        /// <devdoc>
        ///   <para>
        ///     Default to IPv4 address
        ///   </para>
        /// </devdoc>
        private AddressFamily m_Family = AddressFamily.InterNetwork;
        private ushort[] m_Numbers = new ushort[NumberOfLabels];
        private long m_ScopeId = 0;                             // really uint !

        internal const int IPv4AddressBytes = 4;
        internal const int IPv6AddressBytes = 16;

        internal const int NumberOfLabels = IPv6AddressBytes / 2;


        /// <devdoc>
        ///    <para>
        ///       Initializes a new instance of the <see cref='System.Net.IPAddressEx'/>
        ///       class with the specified
        ///       address.
        ///    </para>
        /// </devdoc>
        public IPAddressEx(long newAddress)
        {
            if (newAddress < 0 || newAddress > 0x00000000FFFFFFFF)
            {
                throw new ArgumentOutOfRangeException("newAddress");
            }
            m_Address = newAddress;
        }

        /// <devdoc>
        ///    <para>
        ///       Constructor for an IPv6 Address with a specified Scope.
        ///    </para>
        /// </devdoc>
        public IPAddressEx(byte[] address, long scopeid)
        {

            if (address == null)
            {
                throw new ArgumentNullException("address");
            }

            if (address.Length != IPv6AddressBytes)
            {
                throw new ArgumentException("Bad IP address", "address");
            }

            m_Family = AddressFamily.InterNetworkV6;

            for (int i = 0; i < NumberOfLabels; i++)
            {
                m_Numbers[i] = (ushort)(address[i * 2] * 256 + address[i * 2 + 1]);
            }

            //
            // Consider: Since scope is only valid for link-local and site-local
            //           addresses we could implement some more robust checking here
            //
            if (scopeid < 0 || scopeid > 0x00000000FFFFFFFF)
            {
                throw new ArgumentOutOfRangeException("scopeid");
            }

            m_ScopeId = scopeid;
        }

        /// <devdoc>
        ///    <para>
        ///       Constructor for IPv4 and IPv6 Address.
        ///    </para>
        /// </devdoc>
        public IPAddressEx(byte[] address)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
            if (address.Length != IPv4AddressBytes && address.Length != IPv6AddressBytes)
            {
                throw new ArgumentException("Bad IP address", "address");
            }

            if (address.Length == IPv4AddressBytes)
            {
                m_Family = AddressFamily.InterNetwork;
                m_Address = ((address[3] << 24 | address[2] << 16 | address[1] << 8 | address[0]) & 0x0FFFFFFFF);
            }
            else
            {
                m_Family = AddressFamily.InterNetworkV6;

                for (int i = 0; i < NumberOfLabels; i++)
                {
                    m_Numbers[i] = (ushort)(address[i * 2] * 256 + address[i * 2 + 1]);
                }
            }
        }

        //
        // we need this internally since we need to interface with winsock
        // and winsock only understands Int32
        //
        internal IPAddressEx(int newAddress)
        {
            m_Address = (long)newAddress & 0x00000000FFFFFFFF;
        }

        /// <devdoc>
        /// <para>Converts an IP address string to an <see cref='System.Net.IPAddress'/>
        /// instance.</para>
        /// </devdoc>
        public static bool TryParse(string ipString, out IPAddressEx addressEx)
        {
            IPAddress address = null;
            IPAddress.TryParse(ipString, out address);    //roland

            if (address != null)
                addressEx = new IPAddressEx(address.GetAddressBytes());    //roland
            else
                addressEx = null;

            //address = InternalParse(ipString, true);
            return (address != null);
        }

        public static IPAddressEx Parse(string ipString)
        {
            return new IPAddressEx(IPAddress.Parse(ipString).GetAddressBytes());    //roland

            //return InternalParse(ipString, false);
        }

        /// <devdoc>
        /// <para>
        /// Provides a copy of the IPAddressEx internals as an array of bytes.
        /// </para>
        /// </devdoc>
        public byte[] GetAddressBytes()
        {
            byte[] bytes;
            if (m_Family == AddressFamily.InterNetworkV6)
            {
                bytes = new byte[NumberOfLabels * 2];

                int j = 0;
                for (int i = 0; i < NumberOfLabels; i++)
                {
                    bytes[j++] = (byte)((this.m_Numbers[i] >> 8) & 0xFF);
                    bytes[j++] = (byte)((this.m_Numbers[i]) & 0xFF);
                }
            }
            else
            {
                bytes = new byte[IPv4AddressBytes];
                bytes[0] = (byte)(m_Address);
                bytes[1] = (byte)(m_Address >> 8);
                bytes[2] = (byte)(m_Address >> 16);
                bytes[3] = (byte)(m_Address >> 24);
            }
            return bytes;
        }

        public AddressFamily AddressFamily
        {
            get
            {
                return m_Family;
            }
        }

        /// <devdoc>
        ///    <para>
        ///        IPv6 Scope identifier. This is really a uint32, but that isn't CLS compliant
        ///    </para>
        /// </devdoc>
        public long ScopeId
        {
            get
            {
                //
                // Not valid for IPv4 addresses
                //
                if (m_Family == AddressFamily.InterNetwork)
                {
                    throw new SocketException((int)SocketError.OperationNotSupported);
                }

                return m_ScopeId;
            }
            set
            {
                //
                // Not valid for IPv4 addresses
                //
                if (m_Family == AddressFamily.InterNetwork)
                {
                    throw new SocketException((int)SocketError.OperationNotSupported);
                }

                //
                // Consider: Since scope is only valid for link-local and site-local
                //           addresses we could implement some more robust checking here
                //
                if (value < 0 || value > 0x00000000FFFFFFFF)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                if (m_ScopeId != value)
                {
                    m_Address = value;
                    m_ScopeId = value;
                }
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Converts the Internet address to either standard dotted quad format
        ///       or standard IPv6 representation.
        ///    </para>
        /// </devdoc>
        public override string ToString()
        {
            return new IPAddress(GetAddressBytes()).ToString();
        }

        internal bool Equals(object comparandObj, bool compareScopeId)
        {
            IPAddressEx comparand = comparandObj as IPAddressEx;

            if (comparand == null)
            {
                return false;
            }
            //
            // Compare families before address representations
            //
            if (m_Family != comparand.m_Family)
            {
                return false;
            }
            if (m_Family == AddressFamily.InterNetworkV6)
            {
                //
                // For IPv6 addresses, we must compare the full 128bit
                // representation.
                //
                for (int i = 0; i < NumberOfLabels; i++)
                {
                    if (comparand.m_Numbers[i] != this.m_Numbers[i])
                        return false;
                }
                //
                // In addition, the scope id's must match as well
                //
                if (comparand.m_ScopeId == this.m_ScopeId)
                    return true;
                else
                    return (compareScopeId ? false : true);
            }
            else
            {
                //
                // For IPv4 addresses, compare the integer representation.
                //
                return comparand.m_Address == this.m_Address;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Compares two IP addresses.
        ///    </para>
        /// </devdoc>
        public override bool Equals(object comparand)
        {
            return Equals(comparand, true);
        }
    } // class IPAddressEx
#pragma warning restore CS0659
} // namespace System.Net
