using System;
using System.Security.Cryptography;
using System.Text;

namespace AssetInventory.Core
{
    public static class IntegrityGuard
    {
        // إصلاح #6: إزالة DateTime.UtcNow من حساب الـ hash
        // سابقاً: الـ hash كان يتغير كل يوم على نفس السجل، مما يُبطل التحقق من السلامة
        // الآن: الـ hash ثابت طالما البيانات لم تتغير
        public static string CalculateRecordHash(string tag, string loc)
        {
            var data = $"{tag}|{loc}";
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
        }
    }
}
