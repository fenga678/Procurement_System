using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LicenseGenerator
{
    class Program
    {
        // 密钥（必须与 LicensingService 中的一致）
        private const string SecretKey = "CanteenProcurement2024SecretKey!@#";

        static void Main(string[] args)
        {
            Console.WriteLine("====================================");
            Console.WriteLine("  食堂采购系统 - 注册码生成工具");
            Console.WriteLine("====================================");
            Console.WriteLine();

            while (true)
            {
                Console.Write("请输入机器码（输入 Q 退出）: ");
                var machineCode = Console.ReadLine()?.Trim().ToUpper().Replace("-", "");

                if (string.IsNullOrEmpty(machineCode))
                {
                    Console.WriteLine("机器码不能为空！");
                    continue;
                }

                if (machineCode.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (machineCode.Length != 16)
                {
                    Console.WriteLine("机器码格式错误，应为16位字符！");
                    continue;
                }

                Console.Write("请输入有效期（天数，输入 0 表示永久）: ");
                var daysInput = Console.ReadLine()?.Trim();
                int? days = null;

                if (int.TryParse(daysInput, out var parsedDays))
                {
                    days = parsedDays > 0 ? parsedDays : 0;
                }
                else
                {
                    Console.WriteLine("输入无效，将设置为永久授权。");
                    days = 0;
                }

                // 生成注册码
                var licenseKey = GenerateLicenseKey(machineCode, days);

                Console.WriteLine();
                Console.WriteLine("------------------------------------");
                Console.WriteLine($"机器码: {FormatCode(machineCode)}");
                Console.WriteLine($"注册码: {licenseKey}");
                
                if (days > 0)
                {
                    var expirationDate = DateTime.Today.AddDays(days.Value);
                    Console.WriteLine($"有效期: {days} 天 (到期: {expirationDate:yyyy-MM-dd})");
                }
                else
                {
                    Console.WriteLine("有效期: 永久授权");
                }
                Console.WriteLine("------------------------------------");
                Console.WriteLine();

                Console.Write("是否继续生成？(Y/N): ");
                var continueInput = Console.ReadLine()?.Trim().ToUpper();
                if (continueInput != "Y")
                {
                    break;
                }
                Console.WriteLine();
            }

            Console.WriteLine("感谢使用！");
        }

        /// <summary>
        /// 生成注册码
        /// </summary>
        static string GenerateLicenseKey(string machineCode, int? days)
        {
            DateTime? expirationDate = days > 0 ? DateTime.Today.AddDays(days.Value) : null;

            // 创建授权数据
            var licenseData = new
            {
                MachineCode = machineCode,
                ExpirationDate = expirationDate?.ToString("yyyy-MM-dd"),
                Signature = GenerateSignature(machineCode, expirationDate)
            };

            // JSON 序列化
            var json = JsonSerializer.Serialize(licenseData);

            // Base64 编码
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            // 格式化注册码
            return FormatCode(base64.Replace("=", "").ToUpper());
        }

        /// <summary>
        /// 生成签名
        /// </summary>
        static string GenerateSignature(string machineCode, DateTime? expirationDate)
        {
            var dataToSign = $"{machineCode}|{expirationDate?.ToString("yyyy-MM-dd") ?? "PERMANENT"}|{SecretKey}";
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
            return BitConverter.ToString(bytes).Replace("-", "").ToUpper().Substring(0, 16);
        }

        /// <summary>
        /// 格式化代码（每4位一组）
        /// </summary>
        static string FormatCode(string code)
        {
            var cleanCode = code.Replace("-", "").Replace("=", "");
            var result = new StringBuilder();
            for (int i = 0; i < cleanCode.Length && i < 16; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    result.Append('-');
                }
                result.Append(cleanCode[i]);
            }
            return result.ToString();
        }
    }
}
