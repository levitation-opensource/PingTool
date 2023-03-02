//
// Licensed to Roland Pihlakas under one or more agreements.
// Roland Pihlakas licenses this file to you under the GNU Lesser General Public License, ver 2.1.
// See the LICENSE and copyrights.txt files for more information.
//


//------------------------------------------------------------------------------
// <copyright file="Internal.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Threading;

namespace System.Net
{
    internal static class IntPtrHelperEx
    {
        internal static IntPtr Add(IntPtr a, int b)
        {
            return (IntPtr)((long)a + (long)b);
        }
    }

    internal static class NclUtilitiesEx
    {
        internal static bool IsFatal(Exception exception)
        {
            return exception != null && (exception is OutOfMemoryException || exception is StackOverflowException || exception is ThreadAbortException);
        }
    }

    //
    // support class for Validation related stuff.
    //
    internal static class ValidationHelperEx
    {
        public static bool ValidateTcpPort(int port)
        {
            // on false, API should throw new ArgumentOutOfRangeException("port");
            return port >= IPEndPoint.MinPort && port <= IPEndPoint.MaxPort;
        }
    }
}
