using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CustomAction
{
    public static class Security
    {

        private static RijndaelManaged BuildRigndaelCommon(out byte[] rgbIv, out byte[] key)
        {
            rgbIv = new byte[] {0x0, 0x2, 0x2, 0x3, 0x5, 0x1, 0x7, 0x8, 0xA, 0xB, 0xC, 0xE, 0xF, 0x10, 0x11, 0x12};
            key = new byte[] {0x0, 0x1, 0x2, 0x3, 0x5, 0x6, 0x7, 0x1, 0xD, 0xB, 0x3, 0xD, 0xF, 0x10, 0x11, 0x14};

            //Specify the algorithms key & IV
            var rijndael = new RijndaelManaged
            {
                BlockSize = 128,
                IV = rgbIv,
                KeySize = 128,
                Key = key,
                Padding = PaddingMode.PKCS7
            };
            return rijndael;
        }

        internal static string Encrypt(string plaintext)
        {
            byte[] rgbIV;
            byte[] key;

            RijndaelManaged rijndael = BuildRigndaelCommon(out rgbIV, out key);

            //convert plaintext into a byte array
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            byte[] cipherTextBytes = null;

            //create uninitialized Rijndael encryption obj
            using (RijndaelManaged symmetricKey = new RijndaelManaged())
            {
                //Call SymmetricAlgorithm.CreateEncryptor to create the Encryptor obj
                var transform = rijndael.CreateEncryptor();

                //Chaining mode
                symmetricKey.Mode = CipherMode.CFB;
                //create encryptor from the key and the IV value
                ICryptoTransform encryptor = symmetricKey.CreateEncryptor(key, rgbIV);

                //define memory stream to hold encrypted data
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    //encrypt contents of cryptostream
                    cs.Write(plaintextBytes, 0, plaintextBytes.Length);
                    cs.Flush();
                    cs.FlushFinalBlock();

                    //convert encrypted data from a memory stream into a byte array
                    ms.Position = 0;
                    cipherTextBytes = ms.ToArray();

                    ms.Close();
                    cs.Close();
                }
            }

            //store result as a hex value
            return BitConverter.ToString(cipherTextBytes).Replace("-", "");
        }
    }
}
