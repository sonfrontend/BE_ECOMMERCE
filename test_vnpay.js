const crypto = require('crypto');

async function testRefund() {
    const vnp_ApiUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    const vnp_TmnCode = "IRUB6KRM";
    const vnp_HashSecret = "6A0VG2KLUDRRLZBD9FE6ILPEE3SZH5HQ";

    const vnp_RequestId = crypto.randomUUID();
    const vnp_Version = "2.1.0";
    const vnp_Command = "refund";
    const vnp_TransactionType = "02"; // 02 for total
    const vnp_TxnRef = "1048_639169875635536721";
    const amount = 329000;
    const vnp_Amount = amount * 100;
    const vnp_OrderInfo = "Hoan tien don hang 1048";
    const vnp_TransactionNo = "15582105";
    const vnp_TransactionDate = "20260613224625";
    const vnp_CreateBy = "System";
    
    // Formatting date using JS
    const now = new Date();
    const pad = (n) => n.toString().padStart(2, '0');
    const vnp_CreateDate = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`;
    const vnp_IpAddr = "127.0.0.1";

    const signData = `${vnp_RequestId}|${vnp_Version}|${vnp_Command}|${vnp_TmnCode}|${vnp_TransactionType}|${vnp_TxnRef}|${vnp_Amount}|${vnp_TransactionNo}|${vnp_TransactionDate}|${vnp_CreateBy}|${vnp_CreateDate}|${vnp_IpAddr}|${vnp_OrderInfo}`;
    
    const hmac = crypto.createHmac('sha512', vnp_HashSecret);
    hmac.update(signData, 'utf8');
    const vnp_SecureHash = hmac.digest('hex');

    const requestData = {
        vnp_RequestId,
        vnp_Version,
        vnp_Command,
        vnp_TmnCode,
        vnp_TransactionType,
        vnp_TxnRef,
        vnp_Amount,
        vnp_OrderInfo,
        vnp_TransactionNo,
        vnp_TransactionDate,
        vnp_CreateBy,
        vnp_CreateDate,
        vnp_IpAddr,
        vnp_SecureHash
    };

    console.log("Request JSON:", JSON.stringify(requestData, null, 2));

    try {
        const response = await fetch(vnp_ApiUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestData)
        });
        const data = await response.json();
        console.log("Response:", data);
    } catch (e) {
        console.log("Error:", e.message);
    }
}

testRefund();
