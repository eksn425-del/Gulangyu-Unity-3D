from fastapi import FastAPI
from pydantic import BaseModel
from datetime import datetime
import csv
import os

app = FastAPI()

# 定义日志文件路径
LOG_FILE = "accident_logs.csv"

# 初始化 CSV 文件头（如果文件不存在）
if not os.path.exists(LOG_FILE):
    with open(LOG_FILE, mode='w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow(["Timestamp", "ObstacleType", "X", "Y", "Z"])

# 定义 Pydantic 数据模型
class HazardRecord(BaseModel):
    x: float
    y: float
    z: float
    obstacle_type: str

@app.post("/api/record_hazard")
def record_hazard(record: HazardRecord):
    """
    接收前端 POST 发送的危险预警数据，并写入 CSV。
    """
    # 获取当前时间
    current_time = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    
    # 1. 在控制台打印醒目提示
    print(f"[{current_time}] [警报] 坐标({record.x:.2f}, {record.z:.2f}) 触发危险: {record.obstacle_type}")
    
    # 2. 将数据写入 CSV 文件
    try:
        with open(LOG_FILE, mode='a', newline='', encoding='utf-8') as f:
            writer = csv.writer(f)
            writer.writerow([
                current_time, 
                record.obstacle_type, 
                record.x, 
                record.y, 
                record.z
            ])
    except Exception as e:
        print(f"Error writing to CSV: {e}")
        return {"status": "error", "message": str(e)}
    
    # 返回成功状态
    return {
        "status": "success",
        "message": f"Hazard '{record.obstacle_type}' recorded.",
        "data": {
            "timestamp": current_time,
            **record.dict()
        }
    }

if __name__ == "__main__":
    import uvicorn
    # 启动服务
    uvicorn.run(app, host="127.0.0.1", port=8000)
