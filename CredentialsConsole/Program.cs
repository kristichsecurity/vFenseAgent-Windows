using System;

namespace CredentialsConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                string user = string.Empty;
                string pass = string.Empty;
               Console.Write("Enter Encrypt or Decrypt? ");
                var choice = Console.ReadLine();
                switch (choice)
                {
                    case "encrypt":
                    case "Encrypt":
                        Console.Write("User: ");
                        user = Console.ReadLine();
                        Console.Write("Pass: ");
                        pass = Console.ReadLine();

                        Console.WriteLine();
                        user = Encryption.Encrypt(user);
                        pass = Encryption.Encrypt(pass);

                        Console.WriteLine("Encrypted User: {0}", user);
                        Console.WriteLine("Encrypted Pass: {0}", pass);
                        Console.WriteLine();
                        Console.WriteLine("--------------------------------------------------");
         
                        break;

                    case "decrypt":
                    case "Decrypt":
                        Console.WriteLine("Enter Encrypted User/Pass: ");
                        var encrypted = Console.ReadLine();
                        var decrypted = Encryption.Decrypt(encrypted);
                        Console.WriteLine("Decryption: {0}", decrypted);       
                        break;
                }

            } 
        }
    }
}
