import pyodbc
import datetime

# Thông tin kết nối SQL Server (lấy từ appsettings.json)
server = 'DESKTOP-6MNENLA'
database = 'DB_ECOMMERCE'
username = 'sa'
password = '123456'
driver = '{ODBC Driver 17 for SQL Server}' # Hoặc tùy phiên bản driver cài đặt trên máy

# Chuỗi kết nối
connection_string = f'DRIVER={driver};SERVER={server};DATABASE={database};UID={username};PWD={password};TrustServerCertificate=yes;'

# Dữ liệu cần cập nhật
complaint_reasons = [
    (1, "Tôi chưa nhận được hàng (mặc dù hệ thống báo đã giao)"),
    (2, "Sản phẩm lỗi/hư hỏng nặng không thể sử dụng"),
    (3, "Sản phẩm bị lỗi nhẹ / Thiếu linh kiện, phụ kiện"),
    (4, "Giao sai sản phẩm / Sai màu / Sai kích cỡ"),
    (5, "Hàng không giống mô tả / Nghi ngờ hàng giả"),
    (13, "Lý do khác")
]

try:
    print("Đang kết nối đến Database...")
    conn = pyodbc.connect(connection_string)
    cursor = conn.cursor()
    
    # Bật IDENTITY_INSERT để có thể chèn cứng ID (nếu chưa có)
    cursor.execute("IF EXISTS (SELECT * FROM sys.identity_columns WHERE object_id = OBJECT_ID('ComplaintReasons')) SET IDENTITY_INSERT ComplaintReasons ON;")
    
    for req_id, title in complaint_reasons:
        # Kiểm tra xem record đã tồn tại chưa
        cursor.execute("SELECT Id FROM ComplaintReasons WHERE Id = ?", req_id)
        row = cursor.fetchone()
        
        now = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')
        
        if row:
            # Nếu tồn tại -> Cập nhật
            cursor.execute("""
                UPDATE ComplaintReasons 
                SET Title = ?, UpdatedAt = ?
                WHERE Id = ?
            """, title, now, req_id)
            print(f"Đã cập nhật ID {req_id}")
        else:
            # Nếu chưa tồn tại -> Thêm mới
            cursor.execute("""
                INSERT INTO ComplaintReasons (Id, Title, IsActive, IsActived, CreatedAt, UpdatedAt) 
                VALUES (?, ?, 1, 1, ?, ?)
            """, req_id, title, now, now)
            print(f"Đã thêm mới ID {req_id}")
            
    # Tắt IDENTITY_INSERT
    cursor.execute("IF EXISTS (SELECT * FROM sys.identity_columns WHERE object_id = OBJECT_ID('ComplaintReasons')) SET IDENTITY_INSERT ComplaintReasons OFF;")
    
    # Lưu thay đổi
    conn.commit()
    print("Cập nhật dữ liệu thành công!")
    
except Exception as e:
    print(f"Lỗi xảy ra: {e}")
finally:
    if 'conn' in locals():
        conn.close()
