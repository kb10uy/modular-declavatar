using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace KusakaFactory.Declavatar
{
    internal static class DeclavatarCore
    {
        internal static unsafe Dictionary<string, string> GetLogLocalization(string locale)
        {
            byte* json = null;
            uint jsonLength = 0;

            var localeBytes = Encoding.UTF8.GetBytes(locale);
            fixed (byte* localeBytesPtr = localeBytes)
            {
                if (NativeMethods.declavatar_log_localization(localeBytesPtr, (uint)localeBytes.Length, &json, &jsonLength) != DeclavatarStatus.Success)
                {
                    return null;
                }
            }

            var buffer = new byte[jsonLength];
            Marshal.Copy((IntPtr)json, buffer, 0, (int)jsonLength);
            var jsonString = Encoding.UTF8.GetString(buffer);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
        }

        internal static unsafe void* Create()
        {
            var state = NativeMethods.declavatar_init();
            if (state == null) throw new NullReferenceException("failed to create declavatar handle");
            return state;
        }

        internal static unsafe void Destroy(void* declavatarState)
        {
            NativeMethods.declavatar_free(declavatarState);
        }

        internal static unsafe void AddLibraryPath(void* declavatarState, string path)
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(path);
            fixed (byte* utf8BytesPtr = utf8Bytes)
            {
                NativeMethods.declavatar_add_library_path(declavatarState, utf8BytesPtr, (uint)utf8Bytes.Length);
            }
        }

        internal static unsafe void RegisterArbittach(void* declavatarState, string schemaJson)
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(schemaJson);
            fixed (byte* utf8BytesPtr = utf8Bytes)
            {
                NativeMethods.declavatar_register_arbittach(declavatarState, utf8BytesPtr, (uint)utf8Bytes.Length);
            }
        }

        internal static unsafe void DefineSymbol(void* declavatarState, string symbol)
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(symbol);
            fixed (byte* utf8BytesPtr = utf8Bytes)
            {
                NativeMethods.declavatar_define_symbol(declavatarState, utf8BytesPtr, (uint)utf8Bytes.Length);
            }
        }

        internal static unsafe void DefineLocalization(void* declavatarState, string key, string value)
        {
            var utf8Key = Encoding.UTF8.GetBytes(key);
            var utf8Value = Encoding.UTF8.GetBytes(value);
            fixed (byte* utf8KeyPtr = utf8Key, utf8ValuePtr = utf8Value)
            {
                NativeMethods.declavatar_define_localization(declavatarState, utf8KeyPtr, (uint)utf8Key.Length, utf8ValuePtr, (uint)utf8Value.Length);
            }
        }

        internal static unsafe void* Compile(void* declavatarState, string source, DeclavatarFormat formatKind)
        {
            void* compiledState = null;
            DeclavatarStatus status;

            var utf8Bytes = Encoding.UTF8.GetBytes(source);
            fixed (byte* utf8BytesPtr = utf8Bytes)
            {
                status = NativeMethods.declavatar_compile(declavatarState, &compiledState, utf8BytesPtr, (uint)utf8Bytes.Length, formatKind);
            }

            switch (status)
            {
                case DeclavatarStatus.Success: return compiledState;
                case DeclavatarStatus.JsonError: throw new InvalidOperationException("internal JSON error");
                case DeclavatarStatus.InvalidValue: throw new InvalidOperationException("invalid format specified");
                default: throw new InvalidOperationException("invalid pointer");
            }
        }

        internal static unsafe void DestroyCompiledState(void* compiledState)
        {
            NativeMethods.declavatar_compiled_free(compiledState);
        }

        internal static unsafe string GetAvatarJson(void* compiledState)
        {
            byte* json = null;
            uint jsonLength = 0;
            if (NativeMethods.declavatar_compiled_avatar_json(compiledState, &json, &jsonLength) != DeclavatarStatus.Success)
            {
                return null;
            }
            if (json == null) return null;

            var buffer = new byte[jsonLength];
            Marshal.Copy((IntPtr)json, buffer, 0, (int)jsonLength);
            var jsonString = Encoding.UTF8.GetString(buffer);
            return jsonString;
        }

        internal static unsafe List<string> GetLogJsons(void* compiledState)
        {
            var logs = new List<string>();
            uint logsCount = 0;
            NativeMethods.declavatar_compiled_logs_count(compiledState, &logsCount);

            for (uint i = 0; i < logsCount; i++)
            {
                byte* logJson = null;
                uint logJsonLength = 0;
                NativeMethods.declavatar_compiled_log(compiledState, i, &logJson, &logJsonLength);

                var buffer = new byte[logJsonLength];
                Marshal.Copy((IntPtr)logJson, buffer, 0, (int)logJsonLength);
                var logJsonString = Encoding.UTF8.GetString(buffer);

                logs.Add(logJsonString);
            }

            return logs;
        }
    }
}
