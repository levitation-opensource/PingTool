//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


using System.Runtime.InteropServices;
using System.Security;
using System.Runtime.ConstrainedExecution;
using System.Net.Sockets;
using System.Text;

namespace System.Net
{
    [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
    internal static class UnsafeNclNativeMethodsEx
    {
        public const string Kernel32 = "kernel32.dll";

#if !FEATURE_PAL
        private const string WS2_32 = "ws2_32.dll";
#else
        private const string WS2_32 = Kernel32; // Resolves to rotor_pal
#endif // !FEATURE_PAL

        [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
        internal static class SafeNetHandlesEx
        {
            [DllImport(Kernel32, ExactSpelling = true, SetLastError = true)]
            internal static extern SafeLocalFreeEx LocalAlloc(int uFlags, UIntPtr sizetdwBytes);

            [DllImport(Kernel32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern IntPtr LocalFree(IntPtr handle);

            [DllImport(WS2_32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern SocketError closesocket(
                                                  [In] IntPtr socketHandle
                                                  );

            [DllImport(WS2_32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern SocketError setsockopt(
                                               [In] IntPtr handle,
                                               [In] SocketOptionLevel optionLevel,
                                               [In] SocketOptionName optionName,
                                               [In] ref LingerEx linger,
                                               [In] int optionLength
                                               );
        }

        //
        // UnsafeNclNativeMethods.OSSOCK class contains all Unsafe() calls and should all be protected
        // by the appropriate SocketPermission() to connect/accept to/from remote
        // peers over the network and to perform name resolution.
        // te following calls deal mainly with:
        // 1) socket calls
        // 2) DNS calls
        //

        //
        // here's a brief explanation of all possible decorations we use for PInvoke.
        // these are used in such a way that we hope to gain maximum performance from the
        // unmanaged/managed/unmanaged transition we need to undergo when calling into winsock:
        //
        // [In] (Note: this is similar to what msdn will show)
        // the managed data will be marshalled so that the unmanaged function can read it but even
        // if it is changed in unmanaged world, the changes won't be propagated to the managed data
        //
        // [Out] (Note: this is similar to what msdn will show)
        // the managed data will not be marshalled so that the unmanaged function will not see the
        // managed data, if the data changes in unmanaged world, these changes will be propagated by
        // the marshaller to the managed data
        //
        // objects are marshalled differently if they're:
        //
        // 1) structs
        // for structs, by default, the whole layout is pushed on the stack as it is.
        // in order to pass a pointer to the managed layout, we need to specify either the ref or out keyword.
        //
        //      a) for IN and OUT:
        //      [In, Out] ref Struct ([In, Out] is optional here)
        //
        //      b) for IN only (the managed data will be marshalled so that the unmanaged
        //      function can read it but even if it changes it the change won't be propagated
        //      to the managed struct)
        //      [In] ref Struct
        //
        //      c) for OUT only (the managed data will not be marshalled so that the
        //      unmanaged function cannot read, the changes done in unmanaged code will be
        //      propagated to the managed struct)
        //      [Out] out Struct ([Out] is optional here)
        //
        // 2) array or classes
        // for array or classes, by default, a pointer to the managed layout is passed.
        // we don't need to specify neither the ref nor the out keyword.
        //
        //      a) for IN and OUT:
        //      [In, Out] byte[]
        //
        //      b) for IN only (the managed data will be marshalled so that the unmanaged
        //      function can read it but even if it changes it the change won't be propagated
        //      to the managed struct)
        //      [In] byte[] ([In] is optional here)
        //
        //      c) for OUT only (the managed data will not be marshalled so that the
        //      unmanaged function cannot read, the changes done in unmanaged code will be
        //      propagated to the managed struct)
        //      [Out] byte[]
        //
        [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
        internal static class OSSOCK
        {

#if FEATURE_PAL
            private const string WS2_32 = ROTOR_PAL;
#else
            private const string WS2_32 = "ws2_32.dll";
#endif

            // CharSet=Auto here since WSASocket has A and W versions. We can use Auto cause the method is not used under constrained execution region
            [DllImport(WS2_32, CharSet = CharSet.Auto, SetLastError = true)]
            internal static extern SafeCloseSocketEx.InnerSafeCloseSocket WSASocket(
                                                    [In] AddressFamily addressFamily,
                                                    [In] SocketType socketType,
                                                    [In] ProtocolType protocolType,
                                                    [In] IntPtr protocolInfo, // will be WSAProtcolInfo protocolInfo once we include QOS APIs
                                                    [In] uint group,
                                                    [In] SocketConstructorFlagsEx flags
                                                    );

            [DllImport(WS2_32, CharSet = CharSet.Auto, SetLastError = true)]
            internal unsafe static extern SafeCloseSocketEx.InnerSafeCloseSocket WSASocket(
                                        [In] AddressFamily addressFamily,
                                        [In] SocketType socketType,
                                        [In] ProtocolType protocolType,
                                        [In] byte* pinnedBuffer, // will be WSAProtcolInfo protocolInfo once we include QOS APIs
                                        [In] uint group,
                                        [In] SocketConstructorFlagsEx flags
                                        );

            [DllImport(WS2_32, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
            internal static extern SocketError WSAStartup(
                                               [In] short wVersionRequested,
                                               [Out] out WSADataEx lpWSAData
                                               );

#if !FEATURE_PAL
            [DllImport(WS2_32, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
            internal static extern SocketError WSAAddressToString(
                [In] byte[] socketAddress,
                [In] int socketAddressSize,
                [In] IntPtr lpProtocolInfo,// always passing in a 0
                [Out] StringBuilder addressString,
                [In, Out] ref int addressStringLength);
#endif
        }
    }
}

