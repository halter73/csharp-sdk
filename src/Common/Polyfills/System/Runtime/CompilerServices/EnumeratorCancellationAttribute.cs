// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETSTANDARD2_0 && !HAVE_ENUMERATOR_CANCELLATION_ATTRIBUTE

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill of EnumeratorCancellationAttribute for netstandard2.0
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class EnumeratorCancellationAttribute : Attribute
    {
    }
}

#endif