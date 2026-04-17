import sqlite3

conn = sqlite3.connect('canteen.db')
c = conn.cursor()

# 检查数据库中实际的所有分类数据
c.execute('SELECT id, code, name, ratio, frequency_days, daily_min_items, daily_max_items, sort, status FROM categories ORDER BY sort')
rows = c.fetchall()

print("数据库中所有分类:")
print(f"{'ID':<4} {'Code':<15} {'Name':<10} {'Ratio':<8} {'Freq':<6} {'MinItems':<10} {'MaxItems':<10} {'Sort':<6} {'Status':<6}")
print("-" * 85)
for r in rows:
    print(f"{r[0]:<4} {r[1]:<15} {r[2]:<10} {r[3]:<8} {r[4]:<6} {r[5]:<10} {r[6]:<10} {r[7]:<6} {r[8]:<6}")

# 检查是否有其他列
c.execute('PRAGMA table_info(categories)')
cols = c.fetchall()
print(f"\n数据表结构:")
for col in cols:
    print(f"  {col[1]}: {col[2]} (default={col[4]})")

conn.close()
