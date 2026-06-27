using System;
using System.IO;
using System.Security.Cryptography;
using DynLock.Core;

namespace DynLock.Encryptor
{
    /// <summary>
    /// Tool của team lead: mã hóa file .dyn thành .dynx.
    /// Cách dùng:
    ///   DynLockEncrypt.exe file.dyn                 -> tạo file.dynx cùng thư mục
    ///   DynLockEncrypt.exe file.dyn D:\Out          -> tao D:\Out\file.dynx
    ///   DynLockEncrypt.exe D:\Scripts               -> mã hóa mọi *.dyn trong thư mục
    ///   DynLockEncrypt.exe --genkey                 -> sinh key mới cho cấu hình nâng cao
    /// Cũng có thể kéo-thả file/thư mục .dyn vào file .exe này.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (args.Length == 0)
            {
                Console.WriteLine("Cách dùng: DynLockEncrypt.exe <file.dyn | thư-mục> [thư-mục-output]");
                Console.WriteLine("           DynLockEncrypt.exe --genkey");
                return 1;
            }

            if (args[0] == "--genkey")
            {
                var key = new byte[32];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(key);
                Console.WriteLine("Key mới cho cấu hình nâng cao (secrets.json hoặc env var DYNLOCK_MASTER_KEY_BASE64):");
                Console.WriteLine(Convert.ToBase64String(key));
                return 0;
            }

            string input = args[0];
            string outDir = args.Length > 1 ? args[1] : null;
            try
            {
                if (!Secrets.TryGetMasterKeyBytes(out byte[] masterKey, out string keyError))
                {
                    Console.Error.WriteLine(keyError);
                    return 1;
                }

                if (Directory.Exists(input))
                {
                    int count = 0;
                    foreach (var dyn in Directory.GetFiles(input, "*.dyn"))
                    {
                        EncryptOne(dyn, outDir, masterKey);
                        count++;
                    }
                    Console.WriteLine($"Xong: đã mã hóa {count} file.");
                    return 0;
                }

                if (File.Exists(input))
                {
                    EncryptOne(input, outDir, masterKey);
                    Console.WriteLine("Xong.");
                    return 0;
                }

                Console.Error.WriteLine("Không tìm thấy file/thư mục: " + input);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Lỗi: " + ex.Message);
                return 1;
            }
        }

        private static void EncryptOne(string dynPath, string outDir, byte[] masterKey)
        {
            string graphJson = File.ReadAllText(dynPath);
            byte[] plain = DynxPackage.Create(
                graphJson,
                "Utilities",
                Path.GetFileNameWithoutExtension(dynPath),
                null,
                Path.GetFileName(dynPath));
            byte[] blob = DynxCrypto.Encrypt(plain, masterKey);

            string dir = outDir ?? Path.GetDirectoryName(dynPath);
            Directory.CreateDirectory(dir);
            string outPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(dynPath) + ".dynx");
            File.WriteAllBytes(outPath, blob);
            Console.WriteLine($"  {Path.GetFileName(dynPath)} -> {outPath}");
        }
    }
}
