-- ==========================================================
-- 食堂物料预采购管理系统数据库初始化脚本
-- 创建时间: 2026-04-01
-- ==========================================================

-- 创建数据库
CREATE DATABASE IF NOT EXISTS canteen_procurement 
CHARACTER SET utf8mb4 
COLLATE utf8mb4_unicode_ci;

USE canteen_procurement;

-- ==========================================================
-- 分类表 (categories)
-- ==========================================================
CREATE TABLE categories (
    id INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    name VARCHAR(50) NOT NULL COMMENT '分类名称',
    code VARCHAR(20) NOT NULL UNIQUE COMMENT '分类编码',
    ratio DECIMAL(5,4) NOT NULL COMMENT '预算占比（如0.45）',
    frequency_days INT NOT NULL DEFAULT 1 COMMENT '出现频率（几天一次）',
    daily_min_items INT NOT NULL DEFAULT 1 COMMENT '每日最小采购品类数',
    daily_max_items INT NOT NULL DEFAULT 1 COMMENT '每日最大采购品类数',
    sort INT NOT NULL DEFAULT 0 COMMENT '排序号',
    status TINYINT NOT NULL DEFAULT 1 COMMENT '状态（1启用，0禁用）',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间'
) COMMENT='商品分类表';


-- 分类表索引
CREATE INDEX idx_categories_code ON categories(code);
CREATE INDEX idx_categories_status ON categories(status);
CREATE INDEX idx_categories_sort ON categories(sort);

-- ==========================================================
-- 商品表 (products)
-- ==========================================================
CREATE TABLE products (
    id INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    name VARCHAR(100) NOT NULL COMMENT '商品名称',
    category_code VARCHAR(20) NOT NULL COMMENT '分类编码',
    price DECIMAL(10,2) NOT NULL COMMENT '单价',
    unit VARCHAR(20) NOT NULL COMMENT '单位',
    min_interval_days INT NOT NULL DEFAULT 2 COMMENT '最小间隔天数',
    is_active TINYINT NOT NULL DEFAULT 1 COMMENT '是否启用（1启用，0禁用）',
    remark TEXT COMMENT '备注信息',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    FOREIGN KEY (category_code) REFERENCES categories(code),
    CONSTRAINT chk_products_price_positive CHECK (price > 0)
) COMMENT='商品表';


-- 商品表索引
CREATE INDEX idx_products_category ON products(category_code);
CREATE INDEX idx_products_active ON products(is_active);
CREATE INDEX idx_products_name ON products(name);

-- ==========================================================
-- 采购任务表 (procurement_tasks)
-- ==========================================================
CREATE TABLE procurement_tasks (
    id INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    `year_month` CHAR(6) NOT NULL COMMENT '年月（如202604）',
    total_budget DECIMAL(12,2) NOT NULL COMMENT '月度总预算',
    float_rate DECIMAL(4,3) NOT NULL DEFAULT 0.100 COMMENT '随机波动率（±10%）',
    status TINYINT NOT NULL DEFAULT 0 COMMENT '状态（0待生成，1已完成，2已取消）',
    generated_at DATETIME COMMENT '计划生成时间',
    created_by VARCHAR(50) COMMENT '创建人',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    CONSTRAINT chk_tasks_budget_positive CHECK (total_budget > 0),
    CONSTRAINT chk_tasks_float_rate_valid CHECK (float_rate >= 0 AND float_rate <= 1)
) COMMENT='采购任务表';


-- 采购任务表索引
CREATE UNIQUE INDEX idx_tasks_year_month ON procurement_tasks(`year_month`);
CREATE INDEX idx_tasks_status ON procurement_tasks(status);
CREATE INDEX idx_tasks_created_at ON procurement_tasks(created_at);


-- ==========================================================
-- 分类预算表 (task_category_budgets)
-- ==========================================================
CREATE TABLE task_category_budgets (
    id INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    task_id INT NOT NULL COMMENT '任务ID',
    category_code VARCHAR(20) NOT NULL COMMENT '分类编码',
    ratio DECIMAL(5,4) NOT NULL COMMENT '预算占比',
    budget DECIMAL(12,2) NOT NULL COMMENT '分类预算金额',
    is_fixed_amount TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否为固定金额（0=按比例分配，1=固定金额）',
    fixed_amount DECIMAL(12,2) NULL COMMENT '固定金额值（is_fixed_amount=1时生效）',
    expected_count INT NOT NULL DEFAULT 1 COMMENT '预计商品种类数',
    actual_count INT DEFAULT 0 COMMENT '实际使用商品数',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (category_code) REFERENCES categories(code)
) COMMENT='任务分类预算表';

-- 分类预算表索引
CREATE INDEX idx_budgets_task ON task_category_budgets(task_id);
CREATE INDEX idx_budgets_category ON task_category_budgets(category_code);
CREATE INDEX idx_task_category_budgets_fixed ON task_category_budgets(task_id, is_fixed_amount);

-- ==========================================================
-- 采购明细表 (procurement_details)
-- ==========================================================
CREATE TABLE procurement_details (
    id BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    task_id INT NOT NULL COMMENT '任务ID',
    category_code VARCHAR(20) NOT NULL COMMENT '分类编码',
    product_id INT NOT NULL COMMENT '商品ID',
    purchase_date DATE NOT NULL COMMENT '采购日期',
    price DECIMAL(10,2) NOT NULL COMMENT '单价',
    quantity DECIMAL(10,2) NOT NULL COMMENT '采购数量',
    amount DECIMAL(12,2) NOT NULL COMMENT '采购金额',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (category_code) REFERENCES categories(code),
    FOREIGN KEY (product_id) REFERENCES products(id),
    CONSTRAINT chk_details_price_positive CHECK (price > 0),
    CONSTRAINT chk_details_quantity_positive CHECK (quantity > 0),
    CONSTRAINT chk_details_amount_positive CHECK (amount > 0)
) COMMENT='采购明细表';


-- 采购明细表索引
CREATE INDEX idx_details_task_date ON procurement_details(task_id, purchase_date);
CREATE INDEX idx_details_category ON procurement_details(category_code);
CREATE INDEX idx_details_product ON procurement_details(product_id);
CREATE INDEX idx_details_date ON procurement_details(purchase_date);

-- ==========================================================
-- 商品使用历史表 (product_usage_history)
-- ==========================================================
CREATE TABLE product_usage_history (
    id BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    task_id INT NOT NULL COMMENT '任务ID',
    product_id INT NOT NULL COMMENT '商品ID',
    last_used_date DATE NOT NULL COMMENT '最后使用日期',
    usage_count INT NOT NULL DEFAULT 1 COMMENT '使用次数',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (product_id) REFERENCES products(id)
) COMMENT='商品使用历史表';

-- 商品使用历史表索引
CREATE UNIQUE INDEX idx_history_task_product ON product_usage_history(task_id, product_id);
CREATE INDEX idx_history_last_used ON product_usage_history(last_used_date);

-- ==========================================================
-- 系统配置表 (system_configs)
-- ==========================================================
CREATE TABLE system_configs (
    id INT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    config_key VARCHAR(100) NOT NULL UNIQUE COMMENT '配置键',
    config_value TEXT NOT NULL COMMENT '配置值',
    description VARCHAR(200) COMMENT '配置说明',
    is_system TINYINT NOT NULL DEFAULT 0 COMMENT '是否系统配置',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间'
) COMMENT='系统配置表';

-- 系统配置表索引
CREATE INDEX idx_configs_key ON system_configs(config_key);

-- ==========================================================
-- 操作日志表 (operation_logs)
-- ==========================================================
CREATE TABLE operation_logs (
    id BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    operation_type VARCHAR(50) NOT NULL COMMENT '操作类型',
    operation_desc VARCHAR(500) NOT NULL COMMENT '操作描述',
    user_id VARCHAR(50) COMMENT '操作用户',
    task_id INT COMMENT '关联任务ID',
    ip_address VARCHAR(50) COMMENT 'IP地址',
    user_agent TEXT COMMENT '用户代理',
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间'
) COMMENT='操作日志表';

-- 操作日志表索引
CREATE INDEX idx_logs_operation ON operation_logs(operation_type);
CREATE INDEX idx_logs_user ON operation_logs(user_id);
CREATE INDEX idx_logs_created_at ON operation_logs(created_at);

-- ==========================================================
-- 存储过程：清理历史数据（增强版）
-- ==========================================================
DELIMITER //

-- 清理历史采购明细
CREATE PROCEDURE CleanupProcurementDetails()
BEGIN
    DECLARE deleted_count INT DEFAULT 0;
    
    START TRANSACTION;
    
    -- 统计将要删除的记录数
    SELECT COUNT(*) INTO deleted_count 
    FROM procurement_details 
    WHERE purchase_date < DATE_SUB(CURDATE(), INTERVAL 1 YEAR);
    
    -- 删除一年前的采购明细
    DELETE FROM procurement_details 
    WHERE purchase_date < DATE_SUB(CURDATE(), INTERVAL 1 YEAR);
    
    -- 记录操作日志
    INSERT INTO operation_logs (operation_type, operation_desc, user_id)
    VALUES ('DATA_CLEANUP', CONCAT('清理采购明细: ', deleted_count, ' 条记录'), 'system');
    
    COMMIT;
    
    SELECT CONCAT('成功清理 ', deleted_count, ' 条采购明细记录') as result;
END //

-- 清理历史任务
CREATE PROCEDURE CleanupOldTasks()
BEGIN
    DECLARE deleted_count INT DEFAULT 0;
    
    START TRANSACTION;
    
    -- 统计将要删除的记录数
    SELECT COUNT(*) INTO deleted_count 
    FROM procurement_tasks 
    WHERE created_at < DATE_SUB(CURDATE(), INTERVAL 2 YEAR);
    
    -- 删除两年以前的任务（相关明细会自动级联删除）
    DELETE FROM procurement_tasks 
    WHERE created_at < DATE_SUB(CURDATE(), INTERVAL 2 YEAR);
    
    -- 记录操作日志
    INSERT INTO operation_logs (operation_type, operation_desc, user_id)
    VALUES ('DATA_CLEANUP', CONCAT('清理历史任务: ', deleted_count, ' 条记录'), 'system');
    
    COMMIT;
    
    SELECT CONCAT('成功清理 ', deleted_count, ' 条历史任务记录') as result;
END //

-- 优化表统计信息
CREATE PROCEDURE OptimizeTableStats()
BEGIN
    -- 更新表的统计信息
    ANALYZE TABLE categories, products, procurement_tasks, 
                  task_category_budgets, procurement_details,
                  product_usage_history, system_configs;
    
    -- 记录操作日志
    INSERT INTO operation_logs (operation_type, operation_desc, user_id)
    VALUES ('OPTIMIZATION', '优化表统计信息', 'system');
    
    SELECT '表统计信息优化完成' as result;
END //

-- 重建索引
CREATE PROCEDURE RebuildIndexes()
BEGIN
    -- 重建主要索引（注意：大数据表可能需要较长时间）
    ALTER TABLE procurement_details DROP INDEX idx_details_task_date;
    ALTER TABLE procurement_details ADD INDEX idx_details_task_date (task_id, purchase_date);
    
    ALTER TABLE products DROP INDEX idx_products_category;
    ALTER TABLE products ADD INDEX idx_products_category (category_code);
    
    -- 记录操作日志
    INSERT INTO operation_logs (operation_type, operation_desc, user_id)
    VALUES ('OPTIMIZATION', '重建主要表索引', 'system');
    
    SELECT '索引重建完成' as result;
END //

-- 检查数据完整性
CREATE PROCEDURE CheckDataIntegrity()
BEGIN
    DECLARE error_count INT DEFAULT 0;
    DECLARE total_checks INT DEFAULT 0;
    
    CREATE TEMPORARY TABLE IF NOT EXISTS integrity_results (
        check_name VARCHAR(100),
        check_result VARCHAR(50),
        error_message TEXT
    );
    
    -- 检查分类预算比例总和
    SET total_checks = total_checks + 1;
    IF (SELECT SUM(ratio) FROM categories WHERE status = 1) <> 1.0 THEN
        INSERT INTO integrity_results VALUES ('预算比例检查', '失败', '分类预算比例总和不等于1.0');
        SET error_count = error_count + 1;
    ELSE
        INSERT INTO integrity_results VALUES ('预算比例检查', '通过', NULL);
    END IF;
    
    -- 检查商品价格有效性
    SET total_checks = total_checks + 1;
    IF EXISTS (SELECT 1 FROM products WHERE price <= 0 AND is_active = 1) THEN
        INSERT INTO integrity_results VALUES ('商品价格检查', '失败', '存在价格无效的商品');
        SET error_count = error_count + 1;
    ELSE
        INSERT INTO integrity_results VALUES ('商品价格检查', '通过', NULL);
    END IF;
    
    -- 检查任务年月格式
    SET total_checks = total_checks + 1;
    IF EXISTS (SELECT 1 FROM procurement_tasks WHERE `year_month` NOT REGEXP '^[0-9]{6}$') THEN
        INSERT INTO integrity_results VALUES ('任务年月格式检查', '失败', '存在年月格式错误的任务');
        SET error_count = error_count + 1;
    ELSE
        INSERT INTO integrity_results VALUES ('任务年月格式检查', '通过', NULL);
    END IF;

    -- 检查任务预算有效性
    SET total_checks = total_checks + 1;
    IF EXISTS (SELECT 1 FROM procurement_tasks WHERE total_budget <= 0) THEN
        INSERT INTO integrity_results VALUES ('任务预算检查', '失败', '存在总预算小于等于0的任务');
        SET error_count = error_count + 1;
    ELSE
        INSERT INTO integrity_results VALUES ('任务预算检查', '通过', NULL);
    END IF;

    -- 检查采购明细数值有效性
    SET total_checks = total_checks + 1;
    IF EXISTS (SELECT 1 FROM procurement_details WHERE price <= 0 OR quantity <= 0 OR amount <= 0) THEN
        INSERT INTO integrity_results VALUES ('采购明细数值检查', '失败', '存在价格、数量或金额小于等于0的明细');
        SET error_count = error_count + 1;
    ELSE
        INSERT INTO integrity_results VALUES ('采购明细数值检查', '通过', NULL);
    END IF;
    
    -- 检查外键约束
    SET total_checks = total_checks + 1;
    IF EXISTS (SELECT 1 FROM products p LEFT JOIN categories c ON p.category_code = c.code WHERE c.code IS NULL) THEN
        INSERT INTO integrity_results VALUES ('外键约束检查', '失败', '存在无效分类编码的商品');
        SET error_count = error_count + 1;
    ELSE
        INSERT INTO integrity_results VALUES ('外键约束检查', '通过', NULL);
    END IF;
    
    -- 返回检查结果
    SELECT * FROM integrity_results;
    SELECT CONCAT('数据完整性检查完成: ', total_checks - error_count, '/', total_checks, ' 项通过') as summary;
    
    DROP TEMPORARY TABLE integrity_results;
END //

-- 获取指定时间范围内的采购统计
CREATE PROCEDURE GetProcurementStats(IN start_date DATE, IN end_date DATE)
BEGIN
    SELECT 
        c.name as category_name,
        COUNT(pd.id) as item_count,
        SUM(pd.quantity) as total_quantity,
        SUM(pd.amount) as total_amount,
        AVG(pd.amount) as avg_amount
    FROM procurement_details pd
    JOIN products p ON pd.product_id = p.id
    JOIN categories c ON pd.category_code = c.code
    WHERE pd.purchase_date BETWEEN start_date AND end_date
    GROUP BY c.name
    ORDER BY total_amount DESC;
END //

-- 获取商品采购排行
CREATE PROCEDURE GetProductRanking(IN limit_count INT)
BEGIN
    SELECT 
        p.name as product_name,
        c.name as category_name,
        COUNT(pd.id) as purchase_count,
        SUM(pd.amount) as total_amount,
        AVG(pd.amount) as avg_amount
    FROM procurement_details pd
    JOIN products p ON pd.product_id = p.id
    JOIN categories c ON pd.category_code = c.code
    GROUP BY p.id, p.name, c.name
    ORDER BY total_amount DESC
    LIMIT limit_count;
END //

DELIMITER ;

-- ==========================================================
-- 视图：数据备份与统计
-- ==========================================================

CREATE OR REPLACE VIEW v_backup_status AS
SELECT 
    'procurement_tasks' as table_name,
    COUNT(*) as record_count,
    MAX(created_at) as last_created,
    MIN(created_at) as first_created
FROM procurement_tasks
UNION ALL
SELECT 
    'procurement_details' as table_name,
    COUNT(*) as record_count,
    MAX(created_at) as last_created,
    MIN(created_at) as first_created
FROM procurement_details
UNION ALL
SELECT 
    'products' as table_name,
    COUNT(*) as record_count,
    MAX(updated_at) as last_updated,
    MIN(created_at) as first_created
FROM products;

-- 分类商品统计视图
CREATE OR REPLACE VIEW v_category_product_stats AS
SELECT 
    c.id as category_id,
    c.name as category_name,
    c.code as category_code,
    c.ratio as budget_ratio,
    COUNT(p.id) as active_product_count,
    AVG(p.price) as avg_price,
    MIN(p.price) as min_price,
    MAX(p.price) as max_price
FROM categories c
LEFT JOIN products p ON c.code = p.category_code AND p.is_active = 1
WHERE c.status = 1
GROUP BY c.id, c.name, c.code, c.ratio
ORDER BY c.sort;

-- 采购任务统计视图
CREATE OR REPLACE VIEW v_task_statistics AS
SELECT 
    YEAR(created_at) as year,
    MONTH(created_at) as month,
    COUNT(*) as task_count,
    SUM(total_budget) as total_budget,
    AVG(total_budget) as avg_budget,
    COUNT(CASE WHEN status = 1 THEN 1 END) as completed_tasks,
    COUNT(CASE WHEN status = 0 THEN 1 END) as pending_tasks
FROM procurement_tasks
GROUP BY YEAR(created_at), MONTH(created_at)
ORDER BY year DESC, month DESC;

-- 商品使用频率统计
CREATE OR REPLACE VIEW v_product_usage_stats AS
SELECT 
    p.id as product_id,
    p.name as product_name,
    c.name as category_name,
    COUNT(pd.id) as usage_count,
    SUM(pd.amount) as total_amount,
    AVG(pd.amount) as avg_amount,
    MIN(pd.purchase_date) as first_used,
    MAX(pd.purchase_date) as last_used
FROM products p
JOIN categories c ON p.category_code = c.code
LEFT JOIN procurement_details pd ON p.id = pd.product_id
GROUP BY p.id, p.name, c.name
ORDER BY usage_count DESC, total_amount DESC;

-- 任务分类预算详情视图（包含固定金额信息）
CREATE OR REPLACE VIEW v_task_category_budget_detail AS
SELECT
    tcb.id,
    tcb.task_id,
    pt.year_month,
    pt.total_budget AS task_total_budget,
    tcb.category_code,
    c.name AS category_name,
    c.ratio AS category_ratio,
    tcb.ratio AS actual_ratio,
    tcb.budget,
    tcb.is_fixed_amount,
    tcb.fixed_amount,
    CASE
        WHEN tcb.is_fixed_amount = 1 THEN '固定金额'
        ELSE '比例分配'
    END AS budget_mode,
    tcb.expected_count,
    tcb.actual_count,
    (tcb.budget / pt.total_budget * 100) AS budget_percentage,
    tcb.created_at
FROM task_category_budgets tcb
LEFT JOIN procurement_tasks pt ON tcb.task_id = pt.id
LEFT JOIN categories c ON tcb.category_code = c.code
ORDER BY tcb.task_id, c.sort;

-- ==========================================================
-- 定时事件：自动维护
-- ==========================================================

DELIMITER //

-- 创建月度维护事件
CREATE EVENT IF NOT EXISTS monthly_maintenance
ON SCHEDULE EVERY 1 MONTH
STARTS DATE_ADD(DATE_ADD(CURDATE(), INTERVAL 1 MONTH), INTERVAL 1 DAY)
DO
BEGIN
    -- 每月初执行数据清理
    CALL OptimizeTableStats();
    
    -- 如果是每年的第一个月，执行更彻底的清理
    IF MONTH(CURDATE()) = 1 THEN
        CALL CleanupProcurementDetails();
        CALL CleanupOldTasks();
    END IF;
END //

-- 创建每日统计事件
CREATE EVENT IF NOT EXISTS daily_stats_update
ON SCHEDULE EVERY 1 DAY
STARTS DATE_ADD(CURDATE(), INTERVAL 1 DAY)
DO
BEGIN
    -- 记录每日统计
    INSERT INTO operation_logs (operation_type, operation_desc, user_id)
    SELECT 
        'DAILY_STATS',
        CONCAT('采购明细: ', detail_count, '条, 活跃商品: ', product_count, '个'),
        'system'
    FROM (
        SELECT 
            COUNT(*) as detail_count,
            COUNT(DISTINCT product_id) as product_count
        FROM procurement_details 
        WHERE purchase_date = CURDATE()
    ) stats;
END //

DELIMITER ;

-- ==========================================================
-- 结束
-- ==========================================================