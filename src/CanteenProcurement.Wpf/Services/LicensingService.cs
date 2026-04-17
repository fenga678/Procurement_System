using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanteenProcurement.Wpf.Services
{
    /// <summary>
    /// 授权服务 - 管理软件注册和限制
    /// </summary>
    public class LicensingService
    {
        private static LicensingService? _instance;
        private static readonly object _lock = new();

        // 密钥（实际生产中应混淆或加密存储）
        private const string SecretKey = "CanteenProcurement2024SecretKey!@#";

        // 授权数据文件路径
        private readonly string _licenseFilePath;

        // 授权数据
        private LicenseData _licenseData;

        // 缓存的机器码
        private string? _cachedMachineCode;

        // 试用版限制
        public const int TrialMaxProducts = 20;
        public const int TrialMaxCategories = 5;
        public const int TrialMaxTasks = 3;
        public const int TrialMaxExportRows = 20;
        public const int TrialMaxImportRows = 10;

        /// <summary>
        /// 单例实例（向后兼容，新代码使用 DI）
        /// </summary>
        public static LicensingService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LicensingService();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 是否已注册
        /// </summary>
        public bool IsRegistered => _licenseData.IsRegistered && ValidateRegistration();

        /// <summary>
        /// 是否为试用版
        /// </summary>
        public bool IsTrial => !IsRegistered;

        /// <summary>
        /// 到期日期
        /// </summary>
        public DateTime? ExpirationDate => _licenseData.ExpirationDate;

        /// <summary>
        /// 机器码
        /// </summary>
        public string MachineCode => _cachedMachineCode ??= GenerateMachineCode();

        /// <summary>
        /// 注册码（已注册时显示）
        /// </summary>
        public string? LicenseKey => _licenseData.LicenseKey;

        private LicensingService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var companyPath = Path.Combine(appDataPath, "CanteenProcurement");
            Directory.CreateDirectory(companyPath);
            _licenseFilePath = Path.Combine(companyPath, "license.dat");

            _licenseData = LoadLicenseData();

            // 检查并记录首次安装时间
            EnsureFirstInstallTimestamp();
        }

        #region 机器码生成

        /// <summary>
        /// 生成机器码（基于硬件信息）
        /// </summary>
        private string GenerateMachineCode()
        {
            try
            {
                var cpuId = GetCpuId();
                var motherboardSerial = GetMotherboardSerial();
                var diskSerial = GetDiskSerial();

                var combined = $"{cpuId}|{motherboardSerial}|{diskSerial}|{SecretKey}";
                var hash = ComputeSha256Hash(combined);

                // 格式化为 16 位机器码（4组，每组4位）
                return $"{hash.Substring(0, 4)}-{hash.Substring(4, 4)}-{hash.Substring(8, 4)}-{hash.Substring(12, 4)}".ToUpper();
            }
            catch
            {
                // 如果无法获取硬件信息，使用备用方案
                var fallback = Environment.MachineName + Environment.UserName + SecretKey;
                var hash = ComputeSha256Hash(fallback);
                return $"{hash.Substring(0, 4)}-{hash.Substring(4, 4)}-{hash.Substring(8, 4)}-{hash.Substring(12, 4)}".ToUpper();
            }
        }

        private string GetCpuId()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    return obj["ProcessorId"]?.ToString() ?? "CPU-UNKNOWN";
                }
            }
            catch { }
            return "CPU-UNKNOWN";
        }

        private string GetMotherboardSerial()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString() ?? "MB-UNKNOWN";
                }
            }
            catch { }
            return "MB-UNKNOWN";
        }

        private string GetDiskSerial()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE MediaType='Fixed hard disk media'");
                foreach (var obj in searcher.Get())
                {
                    return obj["SerialNumber"]?.ToString()?.Trim() ?? "DISK-UNKNOWN";
                }
            }
            catch { }
            return "DISK-UNKNOWN";
        }

        private static string ComputeSha256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
        }

        #endregion

        #region 注册码验证

        /// <summary>
        /// 验证注册码
        /// </summary>
        public (bool Success, string Message) ValidateLicenseKey(string licenseKey)
        {
            try
            {
                // 清理输入（移除短横线和空格）
                licenseKey = licenseKey.Trim().Replace("-", "").Replace(" ", "");

                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    return (false, "请输入注册码。");
                }

                // 解析注册码
                var decoded = DecodeLicenseKey(licenseKey);
                if (decoded == null)
                {
                    return (false, "注册码无效，无法解析。");
                }

                // 验证机器码
                if (string.IsNullOrEmpty(decoded.MachineCode) || 
                    !decoded.MachineCode.Equals(MachineCode.Replace("-", ""), StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "注册码与本机器不匹配。");
                }

                // 验证签名
                if (!VerifySignature(decoded))
                {
                    return (false, "注册码签名验证失败。");
                }

                // 验证到期日期
                if (decoded.ExpirationDate.HasValue && decoded.ExpirationDate.Value < DateTime.Today)
                {
                    return (false, $"注册码已过期，到期日期：{decoded.ExpirationDate.Value:yyyy-MM-dd}");
                }

                // 注册成功，保存数据
                _licenseData.IsRegistered = true;
                _licenseData.LicenseKey = FormatLicenseKey(licenseKey);
                _licenseData.ExpirationDate = decoded.ExpirationDate;
                _licenseData.RegistrationDate = DateTime.Now;
                SaveLicenseData();

                return (true, $"注册成功！{(decoded.ExpirationDate.HasValue ? $"到期日期：{decoded.ExpirationDate.Value:yyyy-MM-dd}" : "永久授权")}");
            }
            catch (Exception ex)
            {
                return (false, $"注册失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 解码注册码
        /// </summary>
        private DecodedLicense? DecodeLicenseKey(string licenseKey)
        {
            try
            {
                // 移除短横线和空格（兼容格式化后的注册码）
                licenseKey = licenseKey.Replace("-", "").Replace(" ", "");
                var jsonBytes = Convert.FromBase64String(licenseKey);
                var json = Encoding.UTF8.GetString(jsonBytes);
                return JsonSerializer.Deserialize<DecodedLicense>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 验证签名
        /// </summary>
        private bool VerifySignature(DecodedLicense decoded)
        {
            var dataToSign = $"{decoded.MachineCode}|{decoded.ExpirationDate?.ToString("yyyy-MM-dd") ?? "PERMANENT"}|{SecretKey}";
            var expectedSignature = ComputeSha256Hash(dataToSign).Substring(0, 16);
            return decoded.Signature?.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// 格式化注册码显示（每4位加短横线）
        /// </summary>
        private static string FormatLicenseKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            
            // 移除已有的分隔符
            key = key.Replace("-", "").Replace(" ", "");
            
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    result.Append('-');
                }
                result.Append(key[i]);
            }
            return result.ToString();
        }

        /// <summary>
        /// 验证当前注册状态（纯读操作，不触发保存）
        /// </summary>
        private bool ValidateRegistration()
        {
            if (!_licenseData.IsRegistered || string.IsNullOrEmpty(_licenseData.LicenseKey))
            {
                return false;
            }

            // 检查时间篡改
            if (IsTimeTampered())
            {
                return false;
            }

            // 检查到期日期
            if (_licenseData.ExpirationDate.HasValue && _licenseData.ExpirationDate.Value < DateTime.Today)
            {
                return false;
            }

            // 仅验证注册码，不保存（分离验证与持久化）
            var decoded = DecodeLicenseKey(_licenseData.LicenseKey);
            if (decoded == null) return false;
            if (!VerifySignature(decoded)) return false;
            if (!string.IsNullOrEmpty(decoded.MachineCode) &&
                !decoded.MachineCode.Equals(MachineCode.Replace("-", ""), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        #endregion

        #region 时间篡改检测

        /// <summary>
        /// 确保首次安装时间戳存在
        /// </summary>
        private void EnsureFirstInstallTimestamp()
        {
            if (_licenseData.FirstInstallTimestamp == null)
            {
                _licenseData.FirstInstallTimestamp = DateTime.UtcNow;
                _licenseData.LastRunTimestamp = DateTime.UtcNow;
                SaveLicenseData();
            }
            else
            {
                // 更新最后运行时间
                _licenseData.LastRunTimestamp = DateTime.UtcNow;
                SaveLicenseData();
            }
        }

        /// <summary>
        /// 检测时间是否被篡改
        /// </summary>
        private bool IsTimeTampered()
        {
            if (_licenseData.FirstInstallTimestamp == null)
            {
                return false;
            }

            // 如果当前时间早于首次安装时间，说明时间被回退
            if (DateTime.UtcNow < _licenseData.FirstInstallTimestamp.Value)
            {
                return true;
            }

            // 如果当前时间早于上次运行时间（允许一定误差），说明时间被回退
            if (_licenseData.LastRunTimestamp.HasValue)
            {
                var diff = DateTime.UtcNow - _licenseData.LastRunTimestamp.Value;
                if (diff.TotalMinutes < -5) // 允许5分钟误差
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region 数据持久化

        /// <summary>
        /// 加载授权数据
        /// </summary>
        private LicenseData LoadLicenseData()
        {
            try
            {
                if (File.Exists(_licenseFilePath))
                {
                    var encryptedBytes = File.ReadAllBytes(_licenseFilePath);
                    var json = DecryptData(encryptedBytes);
                    return JsonSerializer.Deserialize<LicenseData>(json) ?? new LicenseData();
                }
            }
            catch { }

            return new LicenseData();
        }

        /// <summary>
        /// 保存授权数据
        /// </summary>
        private void SaveLicenseData()
        {
            try
            {
                var json = JsonSerializer.Serialize(_licenseData);
                var encryptedBytes = EncryptData(json);
                File.WriteAllBytes(_licenseFilePath, encryptedBytes);
            }
            catch { }
        }

        /// <summary>
        /// 加密数据
        /// </summary>
        private byte[] EncryptData(string data)
        {
            using var aes = Aes.Create();
            aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(SecretKey));
            aes.IV = new byte[16]; // 简化处理

            using var encryptor = aes.CreateEncryptor();
            var dataBytes = Encoding.UTF8.GetBytes(data);
            return encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
        }

        /// <summary>
        /// 解密数据
        /// </summary>
        private string DecryptData(byte[] encryptedData)
        {
            using var aes = Aes.Create();
            aes.Key = SHA256.HashData(Encoding.UTF8.GetBytes(SecretKey));
            aes.IV = new byte[16];

            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        #endregion

        #region 限制检查方法

        /// <summary>
        /// 检查商品数量限制
        /// </summary>
        public (bool Allowed, int Max) CheckProductLimit(int currentCount)
        {
            if (IsRegistered) return (true, int.MaxValue);
            return (currentCount < TrialMaxProducts, TrialMaxProducts);
        }

        /// <summary>
        /// 检查分类数量限制
        /// </summary>
        public (bool Allowed, int Max) CheckCategoryLimit(int currentCount)
        {
            if (IsRegistered) return (true, int.MaxValue);
            return (currentCount < TrialMaxCategories, TrialMaxCategories);
        }

        /// <summary>
        /// 检查任务数量限制
        /// </summary>
        public (bool Allowed, int Max) CheckTaskLimit(int currentCount)
        {
            if (IsRegistered) return (true, int.MaxValue);
            return (currentCount < TrialMaxTasks, TrialMaxTasks);
        }

        /// <summary>
        /// 获取导出行数限制
        /// </summary>
        public int GetExportRowLimit()
        {
            return IsRegistered ? int.MaxValue : TrialMaxExportRows;
        }

        /// <summary>
        /// 获取导入行数限制
        /// </summary>
        public int GetImportRowLimit()
        {
            return IsRegistered ? int.MaxValue : TrialMaxImportRows;
        }

        /// <summary>
        /// 获取授权状态描述
        /// </summary>
        public string GetStatusDescription()
        {
            if (IsTimeTampered())
            {
                return "系统时间异常，请校准时间后重试";
            }

            if (IsRegistered)
            {
                if (ExpirationDate.HasValue)
                {
                    return $"正式版 (到期: {ExpirationDate.Value:yyyy-MM-dd})";
                }
                return "正式版 (永久授权)";
            }

            return "试用版";
        }

        /// <summary>
        /// 获取试用限制描述
        /// </summary>
        public string GetTrialLimitsDescription()
        {
            return $"试用版限制：\n" +
                   $"• 商品最多 {TrialMaxProducts} 个\n" +
                   $"• 分类最多 {TrialMaxCategories} 个\n" +
                   $"• 任务最多 {TrialMaxTasks} 个\n" +
                   $"• 导出最多 {TrialMaxExportRows} 条\n" +
                   $"• Web导入每次最多 {TrialMaxImportRows} 条";
        }

        #endregion
    }

    #region 数据类

    /// <summary>
    /// 授权数据
    /// </summary>
    internal class LicenseData
    {
        public bool IsRegistered { get; set; }
        public string? LicenseKey { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public DateTime? FirstInstallTimestamp { get; set; }
        public DateTime? LastRunTimestamp { get; set; }
    }

    /// <summary>
    /// 解码后的注册码信息
    /// </summary>
    internal class DecodedLicense
    {
        public string? MachineCode { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? Signature { get; set; }
    }

    #endregion
}
