//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


using System.Runtime.InteropServices;

namespace System.Net.NetworkInformation
{
    public class PingReplyEx
    {
        IPAddressEx address;
        PingOptionsEx options;
        IPStatus ipStatus;  // the status code returned by icmpsendecho, or the icmp status field on the raw socket
        long rtt;  // the round trip time.
        byte[] buffer; //buffer of the data


        internal PingReplyEx()
        {
        }

        internal PingReplyEx(IPStatus ipStatus)
        {
            this.ipStatus = ipStatus;
            buffer = new byte[0];
        }


        // the main constructor for the icmpsendecho apis
        internal PingReplyEx(IcmpEchoReplyEx reply)
        {
            address = new IPAddressEx(reply.address);
            ipStatus = (IPStatus)reply.status; //the icmpsendecho ip status codes

            //only copy the data if we succeed w/ the ping operation
            if (ipStatus == IPStatus.Success)
            {
                rtt = (long)reply.roundTripTime;
                buffer = new byte[reply.dataSize];
                Marshal.Copy(reply.data, buffer, 0, reply.dataSize);
                options = new PingOptionsEx(reply.options);
            }
            else
                buffer = new byte[0];

        }

        // the main constructor for the icmpsendecho apis
        internal PingReplyEx(Icmp6EchoReplyEx reply, IntPtr dataPtr, int sendSize)
        {

            address = new IPAddressEx(reply.Address.Address, reply.Address.ScopeID);
            ipStatus = (IPStatus)reply.Status; //the icmpsendecho ip status codes

            //only copy the data if we succeed w/ the ping operation
            if (ipStatus == IPStatus.Success)
            {
                rtt = (long)reply.RoundTripTime;
                buffer = new byte[sendSize];
                Marshal.Copy(IntPtrHelperEx.Add(dataPtr, 36), buffer, 0, sendSize);
                //options = new PingOptionsEx (reply.options);
            }
            else
                buffer = new byte[0];

        }

        //the basic properties
        public IPStatus Status { get { return ipStatus; } }
        public IPAddressEx Address { get { return address; } }
        public long RoundtripTime { get { return rtt; } }
        public PingOptionsEx Options
        {
            get
            {
                return options;
            }
        }
        public byte[] Buffer { get { return buffer; } }
    }
}
