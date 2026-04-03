-- ==========================================================
-- 食堂物料预采购管理系统 SQLite 数据库初始化脚本
-- 创建时间: 2026-04-03
-- 说明: 此脚本为 SQLite 版本，用于手动创建数据库结构
--       SQLiteProvider 会自动创建这些表，此脚本仅供参考
-- ==========================================================

-- ==========================================================
-- 分类表 (categories)
-- ==========================================================
CREATE TABLE IF NOT EXISTS categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    code TEXT NOT NULL UNIQUE,
    ratio REAL NOT NULL,
    frequency_days INTEGER NOT NULL DEFAULT 1,
    daily_min_items INTEGER NOT NULL DEFAULT 1,
    daily_max_items INTEGER NOT NULL DEFAULT 1,
    sort INTEGER NOT NULL DEFAULT 0,
    status INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- 分类表索引
CREATE INDEX IF NOT EXISTS idx_categories_code ON categories(code);
CREATE INDEX IF NOT EXISTS idx_categories_status ON categories(status);
CREATE INDEX IF NOT EXISTS idx_categories_sort ON categories(sort);

-- ==========================================================
-- 商品表 (products)
-- ==========================================================
CREATE TABLE IF NOT EXISTS products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    category_code TEXT NOT NULL,
    price REAL NOT NULL,
    unit TEXT NOT NULL,
    min_interval_days INTEGER NOT NULL DEFAULT 2,
    is_active INTEGER NOT NULL DEFAULT 1,
    remark TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (category_code) REFERENCES categories(code),
    CHECK (price > 0)
);

-- 商品表索引
CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_code);
CREATE INDEX IF NOT EXISTS idx_products_active ON products(is_active);
CREATE INDEX IF NOT EXISTS idx_products_name ON products(name);

-- ==========================================================
-- 采购任务表 (procurement_tasks)
-- ==========================================================
CREATE TABLE IF NOT EXISTS procurement_tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_month TEXT NOT NULL,
    total_budget REAL NOT NULL,
    float_rate REAL NOT NULL DEFAULT 0.100,
    status INTEGER NOT NULL DEFAULT 0,
    generated_at TEXT,
    created_by TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    CHECK (total_budget > 0),
    CHECK (float_rate >= 0 AND float_rate <= 1)
);

-- 采购任务表索引
CREATE UNIQUE INDEX IF NOT EXISTS idx_tasks_year_month ON procurement_tasks(year_month);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON procurement_tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON procurement_tasks(created_at);

-- ==========================================================
-- 任务分类预算表 (task_category_budgets)
-- ==========================================================
CREATE TABLE IF NOT EXISTS task_category_budgets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    category_code TEXT NOT NULL,
    ratio REAL NOT NULL,
    budget REAL NOT NULL,
    is_fixed_amount INTEGER NOT NULL DEFAULT 0,
    fixed_amount REAL,
    expected_count INTEGER NOT NULL DEFAULT 1,
    actual_count INTEGER DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (category_code) REFERENCES categories(code)
);

-- 分类预算表索引
CREATE INDEX IF NOT EXISTS idx_budgets_task ON task_category_budgets(task_id);
CREATE INDEX IF NOT EXISTS idx_budgets_category ON task_category_budgets(category_code);
CREATE INDEX IF NOT EXISTS idx_task_category_budgets_fixed ON task_category_budgets(task_id, is_fixed_amount);

-- ==========================================================
-- 采购明细表 (procurement_details)
-- ==========================================================
CREATE TABLE IF NOT EXISTS procurement_details (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    category_code TEXT NOT NULL,
    product_id INTEGER NOT NULL,
    purchase_date TEXT NOT NULL,
    price REAL NOT NULL,
    quantity REAL NOT NULL,
    amount REAL NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (category_code) REFERENCES categories(code),
    FOREIGN KEY (product_id) REFERENCES products(id),
    CHECK (price > 0),
    CHECK (quantity > 0),
    CHECK (amount > 0)
);

-- 采购明细表索引
CREATE INDEX IF NOT EXISTS idx_details_task_date ON procurement_details(task_id, purchase_date);
CREATE INDEX IF NOT EXISTS idx_details_category ON procurement_details(category_code);
CREATE INDEX IF NOT EXISTS idx_details_product ON procurement_details(product_id);
CREATE INDEX IF NOT EXISTS idx_details_date ON procurement_details(purchase_date);

-- ==========================================================
-- 商品使用历史表 (product_usage_history)
-- ==========================================================
CREATE TABLE IF NOT EXISTS product_usage_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    last_used_date TEXT NOT NULL,
    usage_count INTEGER NOT NULL DEFAULT 1,
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (product_id) REFERENCES products(id)
);

-- 商品使用历史表索引
CREATE UNIQUE INDEX IF NOT EXISTS idx_history_task_product ON product_usage_history(task_id, product_id);
CREATE INDEX IF NOT EXISTS idx_history_last_used ON product_usage_history(last_used_date);

-- ==========================================================
-- 系统配置表 (system_configs)
-- ==========================================================
CREATE TABLE IF NOT EXISTS system_configs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    config_key TEXT NOT NULL UNIQUE,
    config_value TEXT NOT NULL,
    description TEXT,
    is_system INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- 系统配置表索引
CREATE INDEX IF NOT EXISTS idx_configs_key ON system_configs(config_key);

-- ==========================================================
-- 操作日志表 (operation_logs)
-- ==========================================================
CREATE TABLE IF NOT EXISTS operation_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    operation_type TEXT NOT NULL,
    operation_desc TEXT NOT NULL,
    user_id TEXT,
    task_id INTEGER,
    ip_address TEXT,
    user_agent TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- 操作日志表索引
CREATE INDEX IF NOT EXISTS idx_logs_operation ON operation_logs(operation_type);
CREATE INDEX IF NOT EXISTS idx_logs_user ON operation_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_logs_created_at ON operation_logs(created_at);

-- ==========================================================
-- 自动更新时间戳触发器
-- SQLite 不支持 ON UPDATE CURRENT_TIMESTAMP，需要使用触发器实现
-- ==========================================================

-- categories 表自动更新时间触发器
CREATE TRIGGER IF NOT EXISTS trigger_categories_updated_at
AFTER UPDATE ON categories
BEGIN
    UPDATE categories SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

-- products 表自动更新时间触发器
CREATE TRIGGER IF NOT EXISTS trigger_products_updated_at
AFTER UPDATE ON products
BEGIN
    UPDATE products SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

-- procurement_tasks 表自动更新时间触发器
CREATE TRIGGER IF NOT EXISTS trigger_procurement_tasks_updated_at
AFTER UPDATE ON procurement_tasks
BEGIN
    UPDATE procurement_tasks SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

-- system_configs 表自动更新时间触发器
CREATE TRIGGER IF NOT EXISTS trigger_system_configs_updated_at
AFTER UPDATE ON system_configs
BEGIN
    UPDATE system_configs SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

-- ==========================================================
-- 结束
-- ==========================================================
