import sqlite3
import calendar

conn = sqlite3.connect('canteen.db')
c = conn.cursor()

# 测试：如果 daily_min_items 设置得比较高
# 肉类: 30天 × 8(每天最少8个) × 8元(最便宜) = 1920
# 如果 fixed_amount 是 1016.10，就会报错

# 临时修改肉类的 daily_min_items
c.execute('UPDATE categories SET daily_min_items=8, daily_max_items=10 WHERE code="meat"')
conn.commit()

c.execute('SELECT daily_min_items, daily_max_items FROM categories WHERE code="meat"')
r = c.fetchone()
print(f"肉类: min={r[0]}, max={r[1]}")

# 创建测试任务
c.execute('INSERT INTO procurement_tasks(year_month, total_budget, float_rate, status) VALUES(?,?,?,?)',
          ('202409', 100000.0, 0.0, 0))
task_id = c.lastrowid

# 为肉类设置固定金额 1016.10
c.execute('INSERT INTO task_category_budgets(task_id, category_code, ratio, budget, expected_count, actual_count, is_fixed_amount, fixed_amount) VALUES(?,?,?,?,?,?,?,?)',
          (task_id, 'meat', 0.3, 1016.1, 1, 0, 1, 1016.1))
conn.commit()

# 模拟
c.execute('SELECT year_month, total_budget FROM procurement_tasks WHERE id=?', (task_id,))
year_month, total_budget = c.fetchone()
print(f"\n任务: ID={task_id}, 年月={year_month}, 总预算={total_budget}")

c.execute('SELECT code, name, ratio, frequency_days, daily_min_items, daily_max_items FROM categories WHERE status=1 ORDER BY sort')
categories = c.fetchall()

c.execute('SELECT category_code, fixed_amount FROM task_category_budgets WHERE task_id=? AND is_fixed_amount=1', (task_id,))
fixed_amounts = {row[0]: row[1] for row in c.fetchall()}
print(f"固定金额: {fixed_amounts}")

year = int(year_month[:4])
month = int(year_month[4:])
days_in_month = calendar.monthrange(year, month)[1]

for cat in categories:
    cat_code = cat[0]
    frequency_days = cat[3]
    daily_min_items = cat[4]

    c.execute('SELECT id, name, price FROM products WHERE category_code=? AND is_active=1 AND price>0 ORDER BY price', (cat_code,))
    products = c.fetchall()
    if not products:
        continue

    dates_count = len(range(1, days_in_month + 1, max(1, frequency_days)))
    slots_count = dates_count * daily_min_items
    min_price = products[0][2]
    minimum_required = round(slots_count * min_price, 1)

    is_fixed = cat_code in fixed_amounts
    budget = round(fixed_amounts[cat_code], 1) if is_fixed else None

    status = "FIXED" if is_fixed else "RATIO"
    print(f"{cat_code}: 日期={dates_count}, slots={slots_count}, 最便宜={min_price}, 最低={minimum_required}, 预算={budget if is_fixed else 'N/A'} {status}")

    if is_fixed and minimum_required > budget:
        print(f"  *** 错误: 分类最低采购金额 {minimum_required} 已超过预算 {budget} ***")

# 清理
c.execute('DELETE FROM task_category_budgets WHERE task_id=?', (task_id,))
c.execute('DELETE FROM procurement_tasks WHERE id=?', (task_id,))
c.execute('UPDATE categories SET daily_min_items=1, daily_max_items=5 WHERE code="meat"')
conn.commit()
conn.close()
