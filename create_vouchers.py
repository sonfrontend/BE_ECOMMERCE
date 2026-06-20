import requests
import random

BASE_URL = "http://localhost:5207/api/Voucher/admin"

vouchers = [
    {"code": "WELCOME10K", "discountValue": 10000, "minOrderValue": 50000, "isActived": True},
    {"code": "SUMMER20K", "discountValue": 20000, "minOrderValue": 100000, "isActived": True},
    {"code": "FREESHIP30K", "discountValue": 30000, "minOrderValue": 150000, "isActived": True},
    {"code": "BIGSALE50K", "discountValue": 50000, "minOrderValue": 300000, "isActived": True},
    {"code": "VIP100K", "discountValue": 100000, "minOrderValue": 500000, "isActived": True}
]

print(f"Bắt đầu tạo {len(vouchers)} vouchers...")

for v in vouchers:
    try:
        response = requests.post(BASE_URL, json=v)
        if response.status_code == 200:
            print(f"✅ Đã tạo voucher {v['code']} - Giảm {v['discountValue']}đ (Đơn tối thiểu: {v['minOrderValue']}đ)")
        else:
            print(f"❌ Lỗi khi tạo {v['code']}: {response.text}")
    except Exception as e:
        print(f"⚠️ Lỗi kết nối khi tạo {v['code']}: {e}")

print("Hoàn tất!")
