//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


//------------------------------------------------------------------------------
// <copyright file="Socket.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace System.Net.Sockets
{
    public class SocketEx //: IDisposable
    {
        //
        // Overlapped constants.
        //
#if !FEATURE_PAL || CORIOLIS
        internal static volatile bool UseOverlappedIO;
#else
        // Disable the I/O completion port for Rotor
        internal static volatile bool UseOverlappedIO = true;
#endif // !FEATURE_PAL || CORIOLIS

        private static object s_InternalSyncObject;

        internal static volatile bool s_SupportsIPv4;
        //internal static volatile bool s_SupportsIPv6;
        internal static volatile bool s_OSSupportsIPv6;
        internal static volatile bool s_Initialized;

        // Renamed to be consistent with OSSupportsIPv6
        public static bool OSSupportsIPv4
        {
            get
            {
                InitializeSockets();
                return s_SupportsIPv4;
            }
        }

        public static bool OSSupportsIPv6
        {
            get
            {
                InitializeSockets();
                return s_OSSupportsIPv6;
            }
        }

        private static object InternalSyncObject
        {
            get
            {
                if (s_InternalSyncObject == null)
                {
                    object o = new object();
                    Interlocked.CompareExchange(ref s_InternalSyncObject, o, null);
                }
                return s_InternalSyncObject;
            }
        }

        internal static void InitializeSockets()
        {
            if (!s_Initialized)
            {
                lock (InternalSyncObject)
                {
                    if (!s_Initialized)
                    {

                        WSADataEx wsaData = new WSADataEx();

                        SocketError errorCode =
                            UnsafeNclNativeMethodsEx.OSSOCK.WSAStartup(
                                (short)0x0202, // we need 2.2
                                out wsaData);

                        if (errorCode != SocketError.Success)
                        {
                            //
                            // failed to initialize, throw
                            //
                            // WSAStartup does not set LastWin32Error
                            throw new SocketException((int)errorCode);
                        }

#if !FEATURE_PAL
                        //
                        // we're on WinNT4 or greater, we could use CompletionPort if we
                        // wanted. check if the user has disabled this functionality in
                        // the registry, otherwise use CompletionPort.
                        //

#if DEBUG
                        BooleanSwitch disableCompletionPortSwitch = new BooleanSwitch("DisableNetCompletionPort", "System.Net disabling of Completion Port");

                        //
                        // the following will be true if they've disabled the completionPort
                        //
                        UseOverlappedIO = disableCompletionPortSwitch.Enabled;
#endif

                        bool ipv4 = true;
                        bool ipv6 = true;

                        SafeCloseSocketEx.InnerSafeCloseSocket socketV4 =
                                                             UnsafeNclNativeMethodsEx.OSSOCK.WSASocket(
                                                                    AddressFamily.InterNetwork,
                                                                    SocketType.Dgram,
                                                                    ProtocolType.IP,
                                                                    IntPtr.Zero,
                                                                    0,
                                                                    (SocketConstructorFlagsEx)0);
                        if (socketV4.IsInvalid)
                        {
                            errorCode = (SocketError)Marshal.GetLastWin32Error();
                            if (errorCode == SocketError.AddressFamilyNotSupported)
                                ipv4 = false;
                        }

                        socketV4.Close();

                        SafeCloseSocketEx.InnerSafeCloseSocket socketV6 =
                                                             UnsafeNclNativeMethodsEx.OSSOCK.WSASocket(
                                                                    AddressFamily.InterNetworkV6,
                                                                    SocketType.Dgram,
                                                                    ProtocolType.IP,
                                                                    IntPtr.Zero,
                                                                    0,
                                                                    (SocketConstructorFlagsEx)0);
                        if (socketV6.IsInvalid)
                        {
                            errorCode = (SocketError)Marshal.GetLastWin32Error();
                            if (errorCode == SocketError.AddressFamilyNotSupported)
                                ipv6 = false;
                        }

                        socketV6.Close();


#if COMNET_DISABLEIPV6
                        //
                        // Turn off IPv6 support
                        //
                        ipv6 = false;
#else
                        //
                        // Now read the switch as the final check: by checking the current value for IPv6
                        // support we may be able to avoid a painful configuration file read.
                        //
                        if (ipv6)
                        {
                            s_OSSupportsIPv6 = true;
                            //ipv6 = SettingsSectionInternal.Section.Ipv6Enabled;
                        }
#endif

                        //
                        // Update final state
                        //
                        s_SupportsIPv4 = ipv4;
                        //s_SupportsIPv6 = ipv6;

#else //!FEATURE_PAL

                        s_SupportsIPv4 = true;
                        //s_SupportsIPv6 = false;

#endif //!FEATURE_PAL

                        // Cache some settings locally.

                        s_Initialized = true;
                    }
                }
            }
        }
    }
}
