using InfiniteTool.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Credentials;

namespace InfiniteTool.Credentials
{
    internal unsafe class CredentialStore
    {
        const string infiniteCredentialId = "2043073184";

        public static void ClearInfiniteCredentials()
        {
            var creds = GetInfiniteCredentials();

            foreach(var cred in creds)
            {
                DeleteCredential(cred);
            }
        }

        public static void SaveInfiniteCredentials()
        {
            var creds = GetInfiniteCredentials();

            var output = Path.Combine(Environment.CurrentDirectory, $"DONOTSHARE_{DateTime.UtcNow.ToFileTime()}.infcred");
            using var file = File.OpenWrite(output);
            using var writer = new StreamWriter(file);
            writer.WriteLine(Json.Serialize(creds));
        }

        public static void LoadInfiniteCredentials(string path)
        {
            using var file = File.OpenRead(path);
            var input = Json.DeserializeCredentials(file);

            if (input.Count == 0)
            {
                throw new Exception("No creds found in file!");
            }

            var creds = GetInfiniteCredentials();

            foreach (var cred in creds)
            {
                DeleteCredential(cred);
            }

            foreach(var cred in input)
            {
                StoreCredential(cred);
            }
        }

        public static void StoreCredential(Credential credential)
        {
            var nameHandle = GCHandle.Alloc(credential.TargetName, GCHandleType.Pinned);
            var commentHandle = credential.Comment != null ? GCHandle.Alloc(credential.Comment, GCHandleType.Pinned) : default;
            var attrHandles = new List<GCHandle>(credential.Attributes.Length * 2);

            try
            {
                var pwByteCount = Encoding.UTF8.GetByteCount(credential.Secret);
                var pwBytes = stackalloc byte[pwByteCount];
                Encoding.UTF8.GetBytes(credential.Secret, new Span<byte>(pwBytes, pwByteCount));

                var attrs = stackalloc CREDENTIAL_ATTRIBUTEW[credential.Attributes.Length];

                for(var i = 0; i < credential.Attributes.Length; i++)
                {
                    var attr = credential.Attributes[i];

                    var keywordHandle = GCHandle.Alloc(attr.Keyword, GCHandleType.Pinned);
                    attrHandles.Add(keywordHandle);

                    var valueBytes = Convert.FromBase64String(attr.ValueBase64);
                    var valueHandle = GCHandle.Alloc(valueBytes, GCHandleType.Pinned);
                    attrHandles.Add(valueHandle);

                    attrs[i] = new CREDENTIAL_ATTRIBUTEW()
                    {
                        Keyword = new PWSTR((char*)keywordHandle.AddrOfPinnedObject()),
                        Value = (byte*)valueHandle.AddrOfPinnedObject(),
                        ValueSize = (uint)valueBytes.Length
                    };
                }

                CREDENTIALW newcred = new()
                {
                    TargetName = new PWSTR((char*)nameHandle.AddrOfPinnedObject()),
                    CredentialBlob = pwBytes,
                    CredentialBlobSize = (uint)pwByteCount,
                    Type = (CRED_TYPE)credential.Type,
                    Persist = (CRED_PERSIST)credential.Persist,
                    Comment = new PWSTR((char*)commentHandle.AddrOfPinnedObject()),
                    AttributeCount = (uint)credential.Attributes.Length,
                    Attributes = attrs
                };

                PInvoke.CredWrite(in newcred, 0);

            }
            finally
            {
                nameHandle.Free();

                if (commentHandle.IsAllocated)
                    commentHandle.Free();

                foreach (var h in attrHandles)
                    h.Free();
            }
        }

        public static bool DeleteCredential(Credential credential)
        {
            return PInvoke.CredDelete(credential.TargetName, (CRED_TYPE)credential.Type);
        }

        public static List<Credential> GetInfiniteCredentials()
        {
            CREDENTIALW** credBufferPtr = default;

            var results = new List<Credential>();

            try
            {
                uint count;
                var result = PInvoke.CredEnumerate(default, CRED_ENUMERATE_FLAGS.CRED_ENUMERATE_ALL_CREDENTIALS, &count, &credBufferPtr);

                for (var i = 0; i < count; i++)
                {
                    var credPtr = credBufferPtr[i];
                    var cred = ReadCredential(credPtr);

                    if (cred.TargetName.Contains(infiniteCredentialId))
                    {
                        results.Add(cred);
                    }
                }
            }
            finally
            {
                if (credBufferPtr != default)
                    PInvoke.CredFree(credBufferPtr);
            }

            return results;
        }

        private static Credential ReadCredential(CREDENTIALW* nativeCred)
        {
            var cred = Unsafe.Read<CREDENTIALW>(nativeCred);
            var name = Marshal.PtrToStringUni((nint)cred.TargetName.Value);
            string? secret = null;
            string? comment = null;
            CredentialAttribute[] attributes = new CredentialAttribute[cred.AttributeCount];

            if(cred.CredentialBlobSize > 0 && cred.CredentialBlob != default)
            {
                secret = Marshal.PtrToStringUTF8((nint)cred.CredentialBlob, (int)cred.CredentialBlobSize);
            }

            if(cred.Comment.Value != default)
            {
                comment = Marshal.PtrToStringUni((nint)cred.Comment.Value, cred.Comment.Length);
            }

            for(var i = 0; i < cred.AttributeCount; i++)
            {
                var a = cred.Attributes[i];

                attributes[i] = new CredentialAttribute()
                {
                    Keyword = Marshal.PtrToStringUni((nint)a.Keyword.Value, cred.Comment.Length),
                    ValueBase64 = Convert.ToBase64String(new Span<byte>(a.Value, (int)a.ValueSize))
                };
            }

            return new Credential()
            {
                TargetName = name,
                Type = (uint)cred.Type,
                Secret = secret,
                Comment = comment,
                Attributes = attributes,
                Persist = (uint)cred.Persist
            };
        }
    }

    public class Credential
    {
        public string TargetName { get; set; }
        public string? Secret { get; set; }
        public string? Comment { get; set; }

        public CredentialAttribute[] Attributes { get; set; }
        public uint Persist { get; set; }

        public uint Type { get; set; }
    }

    public class CredentialAttribute
    {
        public string Keyword { get; set; }
        public string ValueBase64 { get; set; }
    }
}
