using System;

namespace KusakaFactory.Declavatar
{
    internal class DeclavatarException : Exception
    {
        internal DeclavatarException(string message) : base(message) { }
    }

    /// <summary>
    /// Internal logical error.
    /// </summary>
    internal class DeclavatarInternalException : Exception
    {
        internal DeclavatarInternalException(string message) : base(message) { }
    }

    /// <summary>
    /// GameObject search error.
    /// </summary>
    internal sealed class DeclavatarRuntimeException : DeclavatarException
    {
        internal DeclavatarRuntimeException(string message) : base(message) { }
    }
}
