import json
import datetime

# Dựa trên ResolutionTemplates mà Admin sử dụng:
# 1. FULL_REFUND (Hoàn tiền 100%)
# 2. PARTIAL_REFUND (Hoàn tiền 1 phần)
# 3. EXCHANGE (Đổi trả)
# 4. NOT_RECEIVED (Chưa nhận hàng)
# 5. REJECTED (Từ chối)
# 13. OTHER (Khác)

# Chúng ta tạo lại danh sách ComplaintReasons (Lý do khiếu nại của user) sao cho khớp logic
complaint_reasons = [
    {
        "Id": 1,
        "Title": "Tôi chưa nhận được hàng (mặc dù hệ thống báo đã giao)",
        "Description": "Khách hàng khiếu nại về việc không nhận được hàng. Phù hợp với cách xử lý NOT_RECEIVED của Admin.",
        "IsActive": True,
        "IsActived": True,
        "CreatedAt": "2024-01-01T00:00:00",
        "UpdatedAt": "2024-01-01T00:00:00"
    },
    {
        "Id": 2,
        "Title": "Sản phẩm lỗi/hư hỏng nặng không thể sử dụng",
        "Description": "Sản phẩm bị hỏng hoàn toàn. Phù hợp với cách xử lý FULL_REFUND hoặc EXCHANGE.",
        "IsActive": True,
        "IsActived": True,
        "CreatedAt": "2024-01-01T00:00:00",
        "UpdatedAt": "2024-01-01T00:00:00"
    },
    {
        "Id": 3,
        "Title": "Sản phẩm bị lỗi nhẹ / Thiếu linh kiện, phụ kiện",
        "Description": "Hàng hóa có thể dùng được nhưng không trọn vẹn. Phù hợp với cách xử lý PARTIAL_REFUND.",
        "IsActive": True,
        "IsActived": True,
        "CreatedAt": "2024-01-01T00:00:00",
        "UpdatedAt": "2024-01-01T00:00:00"
    },
    {
        "Id": 4,
        "Title": "Giao sai sản phẩm / Sai màu / Sai kích cỡ",
        "Description": "Lỗi do người bán giao nhầm. Phù hợp với cách xử lý EXCHANGE hoặc FULL_REFUND.",
        "IsActive": True,
        "IsActived": True,
        "CreatedAt": "2024-01-01T00:00:00",
        "UpdatedAt": "2024-01-01T00:00:00"
    },
    {
        "Id": 5,
        "Title": "Hàng không giống mô tả / Nghi ngờ hàng giả",
        "Description": "Sản phẩm không đạt chất lượng cam kết. Tùy theo bằng chứng, admin có thể FULL_REFUND hoặc REJECTED.",
        "IsActive": True,
        "IsActived": True,
        "CreatedAt": "2024-01-01T00:00:00",
        "UpdatedAt": "2024-01-01T00:00:00"
    },
    {
        "Id": 13,
        "Title": "Lý do khác",
        "Description": "Các trường hợp đặc biệt không nằm trong danh sách. Phù hợp với cách xử lý OTHER.",
        "IsActive": True,
        "IsActived": True,
        "CreatedAt": "2024-01-01T00:00:00",
        "UpdatedAt": "2024-01-01T00:00:00"
    }
]

# 1. Sinh ra file JSON
with open('ComplaintReasons.json', 'w', encoding='utf-8') as f:
    json.dump(complaint_reasons, f, ensure_ascii=False, indent=4)

print("Đã tạo file ComplaintReasons.json")

# 2. Sinh ra mã SQL INSERT (để chạy thẳng vào Database SQL Server / MySQL)
sql_statements = []
for r in complaint_reasons:
    sql = f"INSERT INTO ComplaintReasons (Id, Title, IsActive, IsActived, CreatedAt, UpdatedAt) VALUES ({r['Id']}, N'{r['Title']}', 1, 1, '{r['CreatedAt']}', '{r['UpdatedAt']}');"
    sql_statements.append(sql)

with open('ComplaintReasons.sql', 'w', encoding='utf-8') as f:
    f.write("\n".join(sql_statements))

print("Đã tạo file ComplaintReasons.sql")

# 3. Sinh ra mã C# Entity Framework (HasData) để chép vào ApplicationDbContext.cs
csharp_statements = ["builder.Entity<ComplaintReason>().HasData("]
for i, r in enumerate(complaint_reasons):
    comma = "," if i < len(complaint_reasons) - 1 else ""
    cs_line = f'    new ComplaintReason {{ Id = {r["Id"]}, Title = "{r["Title"]}", IsActive = true, IsActived = true, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 1) }}{comma}'
    csharp_statements.append(cs_line)
csharp_statements.append(");")

with open('ComplaintReasons.cs.txt', 'w', encoding='utf-8') as f:
    f.write("\n".join(csharp_statements))

print("Đã tạo đoạn mã Entity Framework Seed Data tại ComplaintReasons.cs.txt")
