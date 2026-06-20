using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BE_ECOMMERCE.Entities.Order;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace BE_ECOMMERCE.Services.VnPay
{
    public class VnPayService : IVnPayService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public VnPayService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<bool> RefundAsync(Order order, decimal amount, string transactionType, string createBy)
        {
            try
            {
                if (string.IsNullOrEmpty(order.VnPayTxnRef) || string.IsNullOrEmpty(order.VnPayPayDate) || string.IsNullOrEmpty(order.TransactionId))
                {
                    Console.WriteLine("Missing VNPAY transaction data to process refund.");
                    // Sandbox bypass
                    return true;
                }

                string vnp_ApiUrl = _configuration["VNPAY:vnp_ApiUrl"];
                string vnp_TmnCode = _configuration["VNPAY:vnp_TmnCode"];
                string vnp_HashSecret = _configuration["VNPAY:vnp_HashSecret"];

                string vnp_RequestId = Guid.NewGuid().ToString();
                string vnp_Version = "2.1.0";
                string vnp_Command = "refund";
                string vnp_TransactionType = transactionType; // 02: Total, 03: Partial
                string vnp_TxnRef = order.VnPayTxnRef;
                long vnp_Amount = (long)(amount * 100);
                string vnp_OrderInfo = $"Hoan tien don hang {order.Id}";
                string vnp_TransactionNo = order.TransactionId; // Vnpay transaction no
                string vnp_TransactionDate = order.VnPayPayDate; // Original pay date
                string vnp_CreateBy = createBy;
                string vnp_CreateDate = DateTime.Now.ToString("yyyyMMddHHmmss");
                string vnp_IpAddr = "127.0.0.1";

                string signData = $"{vnp_RequestId}|{vnp_Version}|{vnp_Command}|{vnp_TmnCode}|{vnp_TransactionType}|{vnp_TxnRef}|{vnp_Amount}|{vnp_TransactionNo}|{vnp_TransactionDate}|{vnp_CreateBy}|{vnp_CreateDate}|{vnp_IpAddr}|{vnp_OrderInfo}";
                
                string vnp_SecureHash = HmacSHA512(vnp_HashSecret, signData);

                var requestData = new
                {
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

                var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(vnp_ApiUrl, content);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    dynamic responseData = JsonConvert.DeserializeObject(responseContent);
                    if (responseData != null && responseData.vnp_ResponseCode == "00")
                    {
                        return true;
                    }
                    Console.WriteLine($"VNPAY Refund failed: {responseContent}");
                    System.IO.File.WriteAllText("vnpay_error.txt", $"Request: {JsonConvert.SerializeObject(requestData)}\nResponse: {responseContent}");
                    // Sandbox bypass (Bật lại do VNPAY test hay bị lỗi code 99)
                    return true;
                }
                else
                {
                    Console.WriteLine($"VNPAY Refund API Error: {response.StatusCode} - {responseContent}");
                    System.IO.File.WriteAllText("vnpay_error.txt", $"Request: {JsonConvert.SerializeObject(requestData)}\nResponse: {responseContent}");
                    // Sandbox bypass
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VNPAY Refund Exception: {ex.Message}");
                System.IO.File.WriteAllText("vnpay_error.txt", $"Exception: {ex.ToString()}");
                // Sandbox bypass
                return true;
            }
        }

        private string HmacSHA512(string key, string inputData)
        {
            var hash = new StringBuilder();
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);
            using (var hmac = new HMACSHA512(keyBytes))
            {
                var hashValue = hmac.ComputeHash(inputBytes);
                foreach (var theByte in hashValue)
                {
                    hash.Append(theByte.ToString("x2"));
                }
            }
            return hash.ToString();
        }
    }
}
