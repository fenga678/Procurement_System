<?php
/**
 * 食堂采购系统 - 注册码生成器
 * 
 * 使用方法：
 * 1. 将此文件部署到支持 PHP 的 Web 服务器
 * 2. 访问页面输入机器码和有效期
 * 3. 获取生成的注册码
 */

// 密钥（必须与 LicensingService.cs 中的一致）
const SECRET_KEY = 'CanteenProcurement2024SecretKey!@#';

/**
 * 生成签名
 */
function generateSignature(string $machineCode, ?string $expirationDate): string {
    $expiration = $expirationDate ?? 'PERMANENT';
    $dataToSign = "{$machineCode}|{$expiration}|" . SECRET_KEY;
    return strtoupper(substr(hash('sha256', $dataToSign), 0, 16));
}

/**
 * 生成注册码
 * 注册码格式：Base64编码的JSON数据，格式化为XXXX-XXXX-XXXX-XXXX形式
 */
function generateLicenseKey(string $machineCode, ?int $days): string {
    // 清理机器码格式
    $machineCode = strtoupper(str_replace('-', '', $machineCode));
    
    // 计算到期日期
    $expirationDate = null;
    if ($days !== null && $days > 0) {
        $expirationDate = date('Y-m-d', strtotime("+{$days} days"));
    }
    
    // 生成签名
    $signature = generateSignature($machineCode, $expirationDate);
    
    // 创建授权数据
    $licenseData = [
        'MachineCode' => $machineCode,
        'ExpirationDate' => $expirationDate,
        'Signature' => $signature
    ];
    
    // JSON 编码（与C#的JsonSerializer行为一致）
    $json = json_encode($licenseData, JSON_UNESCAPED_SLASHES);
    
    // Base64 编码
    $base64 = base64_encode($json);
    
    // 格式化注册码：每4个字符加一个短横线，不截断完整Base64
    return formatLicenseKey($base64);
}

/**
 * 格式化注册码 - 不截断，只添加分隔符方便阅读
 */
function formatLicenseKey(string $code): string {
    // 移除可能存在的等号填充（Base64填充）
    $clean = rtrim($code, '=');
    
    // 转大写（Base64标准是大写字母敏感，但这里为了美观转大写）
    // 注意：这会导致解码失败！应该保持原样
    // $clean = strtoupper($clean);
    
    // 每4个字符加一个短横线
    $result = '';
    $len = strlen($clean);
    for ($i = 0; $i < $len; $i++) {
        if ($i > 0 && $i % 4 === 0) {
            $result .= '-';
        }
        $result .= $clean[$i];
    }
    return $result;
}

/**
 * 解析注册码格式（用于显示）
 */
function parseLicenseKeyForDisplay(string $licenseKey): string {
    // 移除短横线
    $clean = str_replace('-', '', $licenseKey);
    
    // 尝试解码
    $json = base64_decode($clean);
    if ($json === false) {
        return '无法解析';
    }
    
    $data = json_decode($json, true);
    if ($data === null) {
        return '无法解析';
    }
    
    return json_encode($data, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
}

// 处理表单提交
$result = null;
$error = null;

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $machineCode = trim($_POST['machine_code'] ?? '');
    $days = intval($_POST['days'] ?? 0);
    
    if (empty($machineCode)) {
        $error = '请输入机器码';
    } elseif (strlen(str_replace('-', '', $machineCode)) !== 16) {
        $error = '机器码格式错误，应为16位字符';
    } else {
        $licenseKey = generateLicenseKey($machineCode, $days > 0 ? $days : null);
        
        // 格式化机器码显示
        $formattedMachineCode = strtoupper(str_replace('-', '', $machineCode));
        $formattedMachineCode = substr($formattedMachineCode, 0, 4) . '-' . 
                                substr($formattedMachineCode, 4, 4) . '-' .
                                substr($formattedMachineCode, 8, 4) . '-' .
                                substr($formattedMachineCode, 12, 4);
        
        $result = [
            'machine_code' => $formattedMachineCode,
            'license_key' => $licenseKey,
            'days' => $days > 0 ? $days : '永久',
            'expiration' => $days > 0 ? date('Y-m-d', strtotime("+{$days} days")) : '永久授权',
            'debug_info' => parseLicenseKeyForDisplay($licenseKey)
        ];
    }
}
?>
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>食堂采购系统 - 注册码生成器</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }
        
        .container {
            background: white;
            border-radius: 16px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            max-width: 600px;
            width: 100%;
            overflow: hidden;
        }
        
        .header {
            background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%);
            color: white;
            padding: 24px;
            text-align: center;
        }
        
        .header h1 {
            font-size: 24px;
            margin-bottom: 8px;
        }
        
        .header p {
            font-size: 14px;
            opacity: 0.9;
        }
        
        .form-container {
            padding: 24px;
        }
        
        .form-group {
            margin-bottom: 20px;
        }
        
        .form-group label {
            display: block;
            font-weight: 600;
            color: #374151;
            margin-bottom: 8px;
        }
        
        .form-group input {
            width: 100%;
            padding: 12px 16px;
            border: 2px solid #e5e7eb;
            border-radius: 8px;
            font-size: 16px;
            font-family: 'Consolas', 'Monaco', monospace;
            transition: border-color 0.2s;
        }
        
        .form-group input:focus {
            outline: none;
            border-color: #3b82f6;
        }
        
        .form-group .hint {
            font-size: 12px;
            color: #6b7280;
            margin-top: 6px;
        }
        
        .btn {
            width: 100%;
            padding: 14px 24px;
            background: linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%);
            color: white;
            border: none;
            border-radius: 8px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: transform 0.2s, box-shadow 0.2s;
        }
        
        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(59, 130, 246, 0.4);
        }
        
        .btn:active {
            transform: translateY(0);
        }
        
        .result {
            margin-top: 24px;
            padding: 20px;
            background: #f0fdf4;
            border: 2px solid #86efac;
            border-radius: 8px;
        }
        
        .result h3 {
            color: #166534;
            margin-bottom: 16px;
            font-size: 16px;
        }
        
        .result-item {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 8px 0;
            border-bottom: 1px solid #dcfce7;
        }
        
        .result-item:last-child {
            border-bottom: none;
        }
        
        .result-item .label {
            color: #4b5563;
            font-size: 14px;
        }
        
        .result-item .value {
            font-family: 'Consolas', 'Monaco', monospace;
            font-weight: 600;
            color: #1e3a8a;
            font-size: 14px;
            word-break: break-all;
            text-align: right;
            max-width: 70%;
        }
        
        .result-item .value.highlight {
            background: #fef3c7;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
            letter-spacing: 0.5px;
        }
        
        .copy-btn {
            background: #3b82f6;
            color: white;
            border: none;
            padding: 4px 12px;
            border-radius: 4px;
            font-size: 12px;
            cursor: pointer;
            margin-left: 8px;
            flex-shrink: 0;
        }
        
        .copy-btn:hover {
            background: #2563eb;
        }
        
        .error {
            margin-top: 24px;
            padding: 16px;
            background: #fef2f2;
            border: 2px solid #fca5a5;
            border-radius: 8px;
            color: #dc2626;
            text-align: center;
        }
        
        .debug-section {
            margin-top: 16px;
            padding: 12px;
            background: #f8fafc;
            border: 1px solid #e2e8f0;
            border-radius: 6px;
        }
        
        .debug-section h4 {
            font-size: 12px;
            color: #64748b;
            margin-bottom: 8px;
        }
        
        .debug-section pre {
            font-size: 11px;
            color: #475569;
            white-space: pre-wrap;
            word-break: break-all;
        }
        
        .footer {
            text-align: center;
            padding: 16px;
            background: #f9fafb;
            color: #6b7280;
            font-size: 12px;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>🔐 注册码生成器</h1>
            <p>食堂物料预采购管理系统</p>
        </div>
        
        <div class="form-container">
            <form method="POST">
                <div class="form-group">
                    <label for="machine_code">机器码</label>
                    <input type="text" id="machine_code" name="machine_code" 
                           placeholder="XXXX-XXXX-XXXX-XXXX"
                           value="<?php echo htmlspecialchars($_POST['machine_code'] ?? ''); ?>"
                           maxlength="19" required>
                    <div class="hint">请从系统设置页面获取机器码</div>
                </div>
                
                <div class="form-group">
                    <label for="days">有效期（天数）</label>
                    <input type="number" id="days" name="days" 
                           placeholder="输入 0 表示永久授权"
                           value="<?php echo htmlspecialchars($_POST['days'] ?? ''); ?>"
                           min="0">
                    <div class="hint">输入 0 或留空表示永久授权</div>
                </div>
                
                <button type="submit" class="btn">生成注册码</button>
            </form>
            
            <?php if ($error): ?>
                <div class="error">
                    ⚠️ <?php echo htmlspecialchars($error); ?>
                </div>
            <?php endif; ?>
            
            <?php if ($result): ?>
                <div class="result">
                    <h3>✅ 注册码生成成功</h3>
                    <div class="result-item">
                        <span class="label">机器码</span>
                        <span class="value"><?php echo htmlspecialchars($result['machine_code']); ?></span>
                    </div>
                    <div class="result-item">
                        <span class="label">注册码</span>
                        <div style="display:flex;align-items:center;">
                            <span class="value highlight" id="license_key" style="max-width:350px;"><?php echo htmlspecialchars($result['license_key']); ?></span>
                            <button class="copy-btn" onclick="copyToClipboard()">复制</button>
                        </div>
                    </div>
                    <div class="result-item">
                        <span class="label">有效期</span>
                        <span class="value"><?php echo htmlspecialchars($result['days']); ?> 天</span>
                    </div>
                    <div class="result-item">
                        <span class="label">到期日期</span>
                        <span class="value"><?php echo htmlspecialchars($result['expiration']); ?></span>
                    </div>
                    
                    <?php if (isset($result['debug_info'])): ?>
                    <div class="debug-section">
                        <h4>📋 注册码解析内容（调试信息）</h4>
                        <pre><?php echo htmlspecialchars($result['debug_info']); ?></pre>
                    </div>
                    <?php endif; ?>
                </div>
            <?php endif; ?>
        </div>
        
        <div class="footer">
            仅限授权管理员使用 · 请妥善保管注册码
        </div>
    </div>
    
    <script>
        function copyToClipboard() {
            const text = document.getElementById('license_key').innerText;
            navigator.clipboard.writeText(text).then(() => {
                const btn = event.target;
                const originalText = btn.innerText;
                btn.innerText = '已复制';
                btn.style.background = '#22c55e';
                setTimeout(() => {
                    btn.innerText = originalText;
                    btn.style.background = '#3b82f6';
                }, 2000);
            });
        }
        
        // 自动格式化机器码输入
        document.getElementById('machine_code').addEventListener('input', function(e) {
            let value = e.target.value.replace(/[^A-Za-z0-9]/g, '').toUpperCase();
            let formatted = '';
            for (let i = 0; i < value.length && i < 16; i++) {
                if (i > 0 && i % 4 === 0) {
                    formatted += '-';
                }
                formatted += value[i];
            }
            e.target.value = formatted;
        });
    </script>
</body>
</html>
