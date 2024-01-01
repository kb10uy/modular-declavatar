using System;

namespace KusakaFactory.Declavatar
{
    public class DeclavatarException : Exception
    {
        internal DeclavatarException(string message) : base(message) { }
    }

    /// <summary>
    /// Internal logical error.
    /// </summary>
    public class DeclavatarInternalException : Exception
    {
        internal DeclavatarInternalException(string message) : base(message) { }
    }

    /// <summary>
    /// GameObject search error.
    /// </summary>
    public sealed class DeclavatarRuntimeException : DeclavatarException
    {
        internal DeclavatarRuntimeException(string message) : base(message) { }
    }
}
