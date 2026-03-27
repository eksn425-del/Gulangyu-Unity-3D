import pandas as pd
import matplotlib.pyplot as plt
import os

# 设置中文字体，防止 matplotlib 显示中文乱码
plt.rcParams['font.sans-serif'] = ['SimHei']  # 用来正常显示中文标签
plt.rcParams['axes.unicode_minus'] = False  # 用来正常显示负号

LOG_FILE = "accident_logs.csv"

def analyze_data():
    if not os.path.exists(LOG_FILE):
        print(f"错误: 找不到日志文件 {LOG_FILE}。请先运行游戏并触发一些碰撞。")
        return

    # 读取 CSV 数据
    try:
        df = pd.read_csv(LOG_FILE)
    except Exception as e:
        print(f"读取 CSV 文件失败: {e}")
        return

    if df.empty:
        print("日志文件为空，没有数据可分析。")
        return

    print(f"成功读取 {len(df)} 条记录。正在生成图表...")

    # 绘制 2D 散点图 (X, Z 平面) - 游戏中最常用的俯视图
    plt.figure(figsize=(10, 8))
    
    # 根据障碍物类型上色
    # 获取唯一的障碍物类型
    obstacle_types = df['ObstacleType'].unique()
    
    for obs_type in obstacle_types:
        subset = df[df['ObstacleType'] == obs_type]
        plt.scatter(subset['X'], subset['Z'], label=obs_type, alpha=0.6, s=50)

    plt.title("鼓浪屿虚拟漫游 - 碰撞分布热力散点图 (X-Z 平面)")
    plt.xlabel("X 坐标 (Unity)")
    plt.ylabel("Z 坐标 (Unity)")
    plt.legend()
    plt.grid(True, linestyle='--', alpha=0.5)

    # 标记最新的碰撞点
    latest = df.iloc[-1]
    plt.annotate('最新碰撞', xy=(latest['X'], latest['Z']), xytext=(latest['X']+2, latest['Z']+2),
                 arrowprops=dict(facecolor='black', shrink=0.05))

    # 保存图片
    output_file = "collision_heatmap.png"
    plt.savefig(output_file)
    print(f"图表已保存为: {output_file}")
    
    # 显示图片
    plt.show()

if __name__ == "__main__":
    analyze_data()
