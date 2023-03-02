//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace System.Net.NetworkInformation
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct IPOptionsEx
    {
        internal byte ttl;
        internal byte tos;
        internal byte flags;
        internal byte optionsSize;
        internal IntPtr optionsData;

        internal IPOptionsEx(PingOptionsEx options)
        {
            ttl = 128;
            tos = 0;
            flags = 0;
            optionsSize = 0;
            optionsData = IntPtr.Zero;

            if (options != null)
            {
                this.ttl = (byte)options.Ttl;

                if (options.DontFragment)
                {
                    flags = 2;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IcmpEchoReplyEx
    {
        internal uint address;
        internal uint status;
        internal uint roundTripTime;
        internal ushort dataSize;
        internal ushort reserved;
        internal IntPtr data;
        internal IPOptionsEx options;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct Ipv6AddressEx
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        internal byte[] Goo;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal byte[] Address;    // Replying address.
        internal uint ScopeID;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Icmp6EchoReplyEx
    {
        internal Ipv6AddressEx Address;
        internal uint Status;               // Reply IP_STATUS.
        internal uint RoundTripTime; // RTT in milliseconds.
        internal IntPtr data;
        // internal IPOptionsEx options;
        // internal IntPtr data; data os after tjos
    }

    /// <summary>
    ///   Wrapper for API's in iphlpapi.dll
    /// </summary>

    [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
    internal static class UnsafeNetInfoNativeMethodsEx
    {
        private const string IPHLPAPI = "iphlpapi.dll";

        [DllImport(IPHLPAPI, SetLastError = true)]
        internal extern static SafeCloseIcmpHandle IcmpCreateFile();

        [DllImport(IPHLPAPI, SetLastError = true)]
        internal extern static SafeCloseIcmpHandle Icmp6CreateFile();

        [DllImport(IPHLPAPI, SetLastError = true)]
        internal extern static bool IcmpCloseHandle(IntPtr handle);

        [DllImport(IPHLPAPI, SetLastError = true)]
        internal extern static uint IcmpSendEcho2(SafeCloseIcmpHandle icmpHandle, IntPtr Event, IntPtr apcRoutine, IntPtr apcContext,
            uint ipAddress, [In] SafeLocalFreeEx data, ushort dataSize, ref IPOptionsEx options, SafeLocalFreeEx replyBuffer, uint replySize, uint timeout);

        [DllImport(IPHLPAPI, SetLastError = true)]
        internal extern static uint Icmp6SendEcho2(SafeCloseIcmpHandle icmpHandle, IntPtr Event, IntPtr apcRoutine, IntPtr apcContext,
            byte[] sourceSocketAddress, byte[] destSocketAddress, [In] SafeLocalFreeEx data, ushort dataSize, ref IPOptionsEx options, SafeLocalFreeEx replyBuffer, uint replySize, uint timeout);

        /// <summary>
        /// The IcmpSendEcho2Ex function is an enhanced version of the IcmpSendEcho2 function that allows the user to specify the IPv4 source address on which to issue the ICMP request. The IcmpSendEcho2Ex function is useful in cases where a computer has multiple network interfaces.
        /// </summary>
        //The IcmpSendEcho2Ex function is available on Windows Server 2008 and later.
        [DllImport(IPHLPAPI, SetLastError = true)]
        internal static extern uint IcmpSendEcho2Ex         //roland
        (
            SafeCloseIcmpHandle icmpHandle,
            IntPtr Event,
            IntPtr apcRoutine,
            IntPtr apcContext,
            uint SourceAddress,
            uint DestinationAddress,
            [In] SafeLocalFreeEx data,
            ushort dataSize,
            ref IPOptionsEx options,
            SafeLocalFreeEx replyBuffer,
            uint replySize,
            uint timeout
        );

    }
}
