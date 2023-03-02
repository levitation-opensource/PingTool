//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace System.Net
{

#if DEBUG
    //
    // This is a helper class for debugging GC-ed handles that we define.
    // As a general rule normal code path should always destroy handles explicitly
    //
    internal abstract class DebugSafeHandleEx : SafeHandleZeroOrMinusOneIsInvalid
    {
        string m_Trace;

        protected DebugSafeHandleEx(bool ownsHandle) : base(ownsHandle)
        {
            Trace();
        }

        protected DebugSafeHandleEx(IntPtr invalidValue, bool ownsHandle) : base(ownsHandle)
        {
            SetHandle(invalidValue);
            Trace();
        }

        [EnvironmentPermission(SecurityAction.Assert, Unrestricted = true)]
        private void Trace()
        {
            m_Trace = "WARNING! GC-ed  >>" + this.GetType().FullName + "<< (should be excplicitly closed) \r\n";
#if TRAVE
            (new FileIOPermission(PermissionState.Unrestricted)).Assert();
            string stacktrace = Environment.StackTrace;
            m_Trace += stacktrace;
            FileIOPermission.RevertAssert();
#endif //TRAVE
        }

        [ReliabilityContract(Consistency.MayCorruptAppDomain, Cer.None)]
        ~DebugSafeHandleEx()
        {
            //GlobalLog.SetThreadSource(ThreadKinds.Finalization);
            //GlobalLog.Print(m_Trace);
        }
    }
#endif

    //
    // This is a helper class for debugging GC-ed handles that we define.
    // As a general rule normal code path should always destroy handles explicitly
    //
    internal abstract class DebugSafeHandleMinusOneIsInvalidEx : SafeHandleMinusOneIsInvalid
    {
        string m_Trace;

        protected DebugSafeHandleMinusOneIsInvalidEx(bool ownsHandle) : base(ownsHandle)
        {
            Trace();
        }

        [EnvironmentPermission(SecurityAction.Assert, Unrestricted = true)]
        private void Trace()
        {
            m_Trace = "WARNING! GC-ed  >>" + this.GetType().FullName + "<< (should be excplicitly closed) \r\n";
            //GlobalLog.Print("Creating SafeHandle, type = " + this.GetType().FullName);
#if TRACE
            (new FileIOPermission(PermissionState.Unrestricted)).Assert();
            string stacktrace = Environment.StackTrace;
            m_Trace += stacktrace;
            FileIOPermission.RevertAssert();
#endif //TRACE
        }

        ~DebugSafeHandleMinusOneIsInvalidEx()
        {
            //GlobalLog.SetThreadSource(ThreadKinds.Finalization);
            //GlobalLog.Print(m_Trace);
        }
    }

    //
    // SafeHandle to wrap handles created by IcmpCreateFile or Icmp6CreateFile
    // from either icmp.dll or iphlpapi.dll. These handles must be closed by
    // IcmpCloseHandle.
    //
    // Code creating handles will use ComNetOS.IsPostWin2K to determine
    // which DLL being used. This code uses same construct to determine
    // which DLL being used but stashes the OS query results away at ctor
    // time so it is always available at critical finalizer time.
    //
    [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
    internal sealed class SafeCloseIcmpHandle : SafeHandleZeroOrMinusOneIsInvalid
    {

        private SafeCloseIcmpHandle() : base(true)
        {
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        override protected bool ReleaseHandle()
        {
            return UnsafeNetInfoNativeMethodsEx.IcmpCloseHandle(handle);
        }
    }

    ///////////////////////////////////////////////////////////////
    //
    // This is implementaion of Safe AllocHGlobal which is turned out
    // to be LocalAlloc down in CLR
    //
    ///////////////////////////////////////////////////////////////
    [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
#if DEBUG
    internal sealed class SafeLocalFreeEx : DebugSafeHandleEx
    {
#else
    internal sealed class SafeLocalFreeEx : SafeHandleZeroOrMinusOneIsInvalid {
#endif
        private const int LMEM_FIXED = 0;

        // This returned handle cannot be modified by the application.
        public static SafeLocalFreeEx Zero = new SafeLocalFreeEx(false);

        private SafeLocalFreeEx() : base(true) { }

        private SafeLocalFreeEx(bool ownsHandle) : base(ownsHandle) { }

        public static SafeLocalFreeEx LocalAlloc(int cb)
        {
            SafeLocalFreeEx result = UnsafeNclNativeMethodsEx.SafeNetHandlesEx.LocalAlloc(LMEM_FIXED, (UIntPtr)cb);
            if (result.IsInvalid)
            {
                result.SetHandleAsInvalid();
                throw new OutOfMemoryException();
            }
            return result;
        }

        override protected bool ReleaseHandle()
        {
            return UnsafeNclNativeMethodsEx.SafeNetHandlesEx.LocalFree(handle) == IntPtr.Zero;
        }
    }

    ///////////////////////////////////////////////////////////////
    //
    // This class implements a safe socket handle.
    // It uses an inner and outer SafeHandle to do so.  The inner
    // SafeHandle holds the actual socket, but only ever has one
    // reference to it.  The outer SafeHandle guards the inner
    // SafeHandle with real ref counting.  When the outer SafeHandle
    // is cleaned up, it releases the inner SafeHandle - since
    // its ref is the only ref to the inner SafeHandle, it deterministically
    // gets closed at that point - no ----s with concurrent IO calls.
    // This allows Close() on the outer SafeHandle to deterministically
    // close the inner SafeHandle, in turn allowing the inner SafeHandle
    // to block the user thread in case a g----ful close has been
    // requested.  (It's not legal to block any other thread - such closes
    // are always abortive.)
    //
    ///////////////////////////////////////////////////////////////
    [SuppressUnmanagedCodeSecurity]     //SuppressUnmanagedCodeSecurity - For methods in this particular class, execution time is often critical. Security can be traded for additional speed by applying the SuppressUnmanagedCodeSecurity attribute to the method declaration. This will prevent the runtime from doing a security stack walk at runtime. - MSDN: Generally, whenever managed code calls into unmanaged code (by PInvoke or COM interop into native code), there is a demand for the UnmanagedCode permission to ensure all callers have the necessary permission to allow this. By applying this explicit attribute, developers can suppress the demand at run time. The developer must take responsibility for assuring that the transition into unmanaged code is sufficiently protected by other means. The demand for the UnmanagedCode permission will still occur at link time. For example, if function A calls function B and function B is marked with SuppressUnmanagedCodeSecurityAttribute, function A will be checked for unmanaged code permission during just-in-time compilation, but not subsequently during run time.
#if DEBUG
    internal class SafeCloseSocketEx : DebugSafeHandleMinusOneIsInvalidEx
#else
    internal class SafeCloseSocketEx : SafeHandleMinusOneIsInvalid
#endif
    {
        protected SafeCloseSocketEx() : base(true) { }

        private InnerSafeCloseSocket m_InnerSocket;

        public override bool IsInvalid
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get
            {
                return IsClosed || base.IsInvalid;
            }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        private void SetInnerSocket(InnerSafeCloseSocket socket)
        {
            m_InnerSocket = socket;
            SetHandle(socket.DangerousGetHandle());
        }

        private static SafeCloseSocketEx CreateSocket(InnerSafeCloseSocket socket)
        {
            SafeCloseSocketEx ret = new SafeCloseSocketEx();
            CreateSocket(socket, ret);
            return ret;
        }

        protected static void CreateSocket(InnerSafeCloseSocket socket, SafeCloseSocketEx target)
        {
            if (socket != null && socket.IsInvalid)
            {
                target.SetHandleAsInvalid();
                return;
            }

            bool b = false;
            RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                socket.DangerousAddRef(ref b);
            }
            catch
            {
                if (b)
                {
                    socket.DangerousRelease();
                    b = false;
                }
            }
            finally
            {
                if (b)
                {
                    target.SetInnerSocket(socket);
                    socket.Close();
                }
                else
                {
                    target.SetHandleAsInvalid();
                }
            }
        }

        protected override bool ReleaseHandle()
        {
            InnerSafeCloseSocket innerSocket = m_InnerSocket == null ? null : Interlocked.Exchange<InnerSafeCloseSocket>(ref m_InnerSocket, null);
            if (innerSocket != null)
            {
                innerSocket.DangerousRelease();
            }
            return true;
        }

        internal class InnerSafeCloseSocket : SafeHandleMinusOneIsInvalid
        {
            protected InnerSafeCloseSocket() : base(true) { }

            private static readonly byte[] tempBuffer = new byte[1];

            public override bool IsInvalid
            {
                [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
                get
                {
                    return IsClosed || base.IsInvalid;
                }
            }

            // This method is implicitly reliable and called from a CER.
            protected override bool ReleaseHandle()
            {
                bool ret = false;

#if DEBUG
                try
                {
#endif
                    //GlobalLog.Print("SafeCloseSocketEx::ReleaseHandle(handle:" + handle.ToString("x") + ")");

                    SocketError errorCode;

                    // By default or if CloseAsIs() path failed, set linger timeout to zero to get an abortive close (RST).
                    LingerEx lingerStruct;
                    lingerStruct.OnOff = 1;
                    lingerStruct.Time = 0;

                    errorCode = UnsafeNclNativeMethodsEx.SafeNetHandlesEx.setsockopt(
                        handle,
                        SocketOptionLevel.Socket,
                        SocketOptionName.Linger,
                        ref lingerStruct,
                        4);
#if DEBUG
                    m_CloseSocketLinger = errorCode;
#endif
                    if (errorCode == SocketError.SocketError) errorCode = (SocketError)Marshal.GetLastWin32Error();
                    //GlobalLog.Print("SafeCloseSocketEx::ReleaseHandle(handle:" + handle.ToString("x") + ") setsockopt():" + errorCode.ToString());

                    if (errorCode != SocketError.Success && errorCode != SocketError.InvalidArgument && errorCode != SocketError.ProtocolOption)
                    {
                        // Too dangerous to try closesocket() - it might block!
                        return ret = false;
                    }

                    errorCode = UnsafeNclNativeMethodsEx.SafeNetHandlesEx.closesocket(handle);
#if DEBUG
                    m_CloseSocketHandle = handle;
                    m_CloseSocketResult = errorCode;
#endif
                    //GlobalLog.Print("SafeCloseSocketEx::ReleaseHandle(handle:" + handle.ToString("x") + ") closesocket#3():" + (errorCode == SocketError.SocketError ? (SocketError)Marshal.GetLastWin32Error() : errorCode).ToString());

                    return ret = errorCode == SocketError.Success;
#if DEBUG
                }
                catch (Exception exception)
                {
                    if (!NclUtilitiesEx.IsFatal(exception))
                    {
                        //GlobalLog.Assert("SafeCloseSocketEx::ReleaseHandle(handle:" + handle.ToString("x") + ")", exception.Message);
                    }
                    ret = true;  // Avoid a second assert.
                    throw;
                }
                finally
                {
                    m_CloseSocketThread = Thread.CurrentThread.ManagedThreadId;
                    m_CloseSocketTick = Environment.TickCount;
                    //GlobalLog.Assert(ret, "SafeCloseSocketEx::ReleaseHandle(handle:{0:x})|ReleaseHandle failed.", handle);
                }
#endif
            }

#if DEBUG
            private IntPtr m_CloseSocketHandle;
            private SocketError m_CloseSocketResult = unchecked((SocketError)0xdeadbeef);
            private SocketError m_CloseSocketLinger = unchecked((SocketError)0xdeadbeef);
            private int m_CloseSocketThread;
            private int m_CloseSocketTick;
#endif
        }
    }
}
