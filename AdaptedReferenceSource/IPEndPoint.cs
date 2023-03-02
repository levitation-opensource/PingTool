//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


//------------------------------------------------------------------------------
// <copyright file="IPEndPoint.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Net.Sockets;
using System.Globalization;

namespace System.Net
{
    /// <devdoc>
    ///    <para>
    ///       Provides an IP address.
    ///    </para>
    /// </devdoc>
    [Serializable]
    public class IPEndPointEx : EndPoint
    {
        /// <devdoc>
        ///    <para>
        ///       Specifies the minimum acceptable value for the <see cref='System.Net.IPEndPointEx.Port'/>
        ///       property.
        ///    </para>
        /// </devdoc>
        public const int MinPort = 0x00000000;
        /// <devdoc>
        ///    <para>
        ///       Specifies the maximum acceptable value for the <see cref='System.Net.IPEndPointEx.Port'/>
        ///       property.
        ///    </para>
        /// </devdoc>
        public const int MaxPort = 0x0000FFFF;

        private IPAddressEx m_Address;
        private int m_Port;

        internal const int AnyPort = MinPort;

        internal static IPEndPointEx Any = new IPEndPointEx(IPAddressEx.Any, AnyPort);
        internal static IPEndPointEx IPv6Any = new IPEndPointEx(IPAddressEx.IPv6Any, AnyPort);


        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override AddressFamily AddressFamily
        {
            get
            {
                //
                // IPv6 Changes: Always delegate this to the address we are
                //               wrapping.
                //
                return m_Address.AddressFamily;
            }
        }

        /// <devdoc>
        ///    <para>Creates a new instance of the IPEndPointEx class with the specified address and
        ///       port.</para>
        /// </devdoc>
        public IPEndPointEx(long address, int port)
        {
            if (!ValidationHelperEx.ValidateTcpPort(port))
            {
                throw new ArgumentOutOfRangeException("port");
            }
            m_Port = port;
            m_Address = new IPAddressEx(address);
        }

        /// <devdoc>
        ///    <para>Creates a new instance of the IPEndPointEx class with the specified address and port.</para>
        /// </devdoc>
        public IPEndPointEx(IPAddressEx address, int port)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
            if (!ValidationHelperEx.ValidateTcpPort(port))
            {
                throw new ArgumentOutOfRangeException("port");
            }
            m_Port = port;
            m_Address = address;
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets the IP address.
        ///    </para>
        /// </devdoc>
        public IPAddressEx Address
        {
            get
            {
                return m_Address;
            }
            set
            {
                m_Address = value;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets the port.
        ///    </para>
        /// </devdoc>
        public int Port
        {
            get
            {
                return m_Port;
            }
            set
            {
                if (!ValidationHelperEx.ValidateTcpPort(value))
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                m_Port = value;
            }
        }


        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override string ToString()
        {
            string format;
            if (m_Address.AddressFamily == AddressFamily.InterNetworkV6)
                format = "[{0}]:{1}";
            else
                format = "{0}:{1}";
            return String.Format(format, m_Address.ToString(), Port.ToString(NumberFormatInfo.InvariantInfo));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public /*override*/new SocketAddressEx Serialize()
        {
            // Let SocketAddressEx do the bulk of the work
            return new SocketAddressEx(Address, Port);
        }
    } // class IPEndPointEx
} // namespace System.Net
