using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace KusakaFactory.Declavatar
{
    internal sealed class DeclavatarCore : IDisposable
    {
        private NativeHandle _handle = null;
        private bool _disposed = false;
        private StatusCode _lastCompileResult = StatusCode.NotCompiled;

        public DeclavatarCore()
        {
            _handle = NativeHandle.Create();
            if (_handle.IsInvalid) throw new NullReferenceException("failed to create declavatar handle");
        }

        public void Reset()
        {
            Native.DeclavatarReset(_handle);
            _lastCompileResult = StatusCode.NotCompiled;
        }

        public void AddLibraryPath(string path)
        {
            var utf8bytes = Encoding.UTF8.GetBytes(path);
            Native.DeclavatarAddLibraryPath(_handle, ref utf8bytes[0], (uint)utf8bytes.Length);
        }

        public bool Compile(string source, FormatKind kind)
        {
            var utf8bytes = Encoding.UTF8.GetBytes(source);
            _lastCompileResult = Native.DeclavatarCompile(_handle, ref utf8bytes[0], (uint)utf8bytes.Length, (uint)kind);
            return _lastCompileResult == StatusCode.Success;
        }

        public string GetAvatarJson()
        {
            if (_lastCompileResult != StatusCode.Success) return null;

            IntPtr json = IntPtr.Zero;
            uint jsonLength = 0;
            if (Native.DeclavatarGetAvatarJson(_handle, ref json, ref jsonLength) != StatusCode.Success)
            {
                return null;
            }

            var buffer = new byte[jsonLength];
            Marshal.Copy(json, buffer, 0, (int)jsonLength);
            var jsonString = Encoding.UTF8.GetString(buffer);
            return jsonString;
        }

        public List<string> FetchLogJsons()
        {
            var logs = new List<string>();
            uint logsCount = 0;
            Native.DeclavatarGetLogsCount(_handle, ref logsCount);

            for (uint i = 0; i < logsCount; i++)
            {
                IntPtr logJson = IntPtr.Zero;
                uint logJsonLength = 0;
                Native.DeclavatarGetLogJson(_handle, i, ref logJson, ref logJsonLength);

                var buffer = new byte[logJsonLength];
                Marshal.Copy(logJson, buffer, 0, (int)logJsonLength);
                var logJsonString = Encoding.UTF8.GetString(buffer);

                logs.Add(logJsonString);
            }

            return logs;
        }

        public static Dictionary<string, string> GetLogLocalization(string locale)
        {
            var keyBytes = Encoding.UTF8.GetBytes($"log.{locale}");
            IntPtr json = IntPtr.Zero;
            uint jsonLength = 0;
            if (Native.DeclavatarGetI18n(ref keyBytes[0], (uint)keyBytes.Length, ref json, ref jsonLength) != StatusCode.Success)
            {
                return null;
            }
            var buffer = new byte[jsonLength];
            Marshal.Copy(json, buffer, 0, (int)jsonLength);
            var jsonString = Encoding.UTF8.GetString(buffer);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing) this._handle.Dispose();
            _disposed = true;
        }

        internal static class Native
        {
#if UNITY_EDITOR_WIN
            private const string LIBRARY_NAME = "declavatar.dll";
#elif UNITY_EDITOR_OSX
            private const string LIBRARY_NAME = "libdeclavatar.dylib";
#elif UNITY_EDITOR_LINUX
            private const string LIBRARY_NAME = "libdeclavatar.so";
#endif

            [DllImport(LIBRARY_NAME)]
            public static extern IntPtr DeclavatarInit();
            [DllImport(LIBRARY_NAME)]
            public static extern StatusCode DeclavatarFree(IntPtr da);
            [DllImport(LIBRARY_NAME)]
            public static extern StatusCode DeclavatarReset(NativeHandle da);
            [DllImport(LIBRARY_NAME)]
            public static extern StatusCode DeclavatarAddLibraryPath(NativeHandle da, ref byte path, uint pathLength);
            [DllImport(LIBRARY_NAME)]
            public static extern StatusCode DeclavatarCompile(NativeHandle da, ref byte source, uint sourceLength, uint formatKind);
            [DllImport(LIBRARY_NAME)]
            public static extern StatusCode DeclavatarGetAvatarJson(NativeHandle da, ref IntPtr json, ref uint jsonLength);
            [DllImport(LIBRARY_NAME)]
            public static extern StatusCode DeclavatarGetLogsCount(NativeHandle da, ref uint errors);
            [DllImport(LIBRARY_NAME)]
            public static extern StatusCode DeclavatarGetLogJson(NativeHandle da, uint index, ref IntPtr message, ref uint messageLength);
            [DllImport(LIBRARY_NAME)]
            public static extern StatusCode DeclavatarGetI18n(ref byte i18nKey, uint i18nKeyLength, ref IntPtr i18nJson, ref uint i18nJsonLength);
        }

        internal sealed class NativeHandle : SafeHandle
        {
            public override bool IsInvalid => handle == IntPtr.Zero;

            private NativeHandle(IntPtr newHandle) : base(IntPtr.Zero, true)
            {
                SetHandle(newHandle);
            }

            protected override bool ReleaseHandle()
            {
                return Native.DeclavatarFree(handle) == (uint)StatusCode.Success;
            }

            public static NativeHandle Create()
            {
                var newHandle = Native.DeclavatarInit();
                return new NativeHandle(newHandle);
            }
        }

        internal enum StatusCode : uint
        {
            Success = 0,
            Utf8Error = 1,
            CompileError = 2,
            AlreadyInUse = 3,
            NotCompiled = 4,
            InvalidPointer = 128,
        }
    }

    internal enum FormatKind : uint
    {
        SExpression = 1,
        Lua = 2,
    }
}
