using AssetInventory.Models;
using System;

namespace AssetInventory.Core
{
    public static class AssetValidator
    {
        private static readonly string[] ValidStatuses = { "PENDING", "VERIFIED", "DISPOSED", "TRANSFERRED" };

        // إصلاح #7: توسيع التحقق ليشمل جميع الحقول الجوهرية، وليس TagNumber فقط
        public static bool Validate(Asset asset, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(asset.TagNumber))
            {
                errorMessage = "TAG NUMBER لا يمكن أن يكون فارغاً.";
                return false;
            }

            if (asset.TagNumber.Length > 50)
            {
                errorMessage = "TAG NUMBER لا يمكن أن يتجاوز 50 حرفاً.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(asset.AssetDescription))
            {
                errorMessage = "وصف الأصل (ASSET DESCRIPTION) لا يمكن أن يكون فارغاً.";
                return false;
            }

            if (asset.AssetDescription.Length > 500)
            {
                errorMessage = "وصف الأصل لا يمكن أن يتجاوز 500 حرف.";
                return false;
            }

            if (!Array.Exists(ValidStatuses, s => s == asset.Status))
            {
                errorMessage = $"قيمة الحالة '{asset.Status}' غير صالحة. القيم المسموح بها: {string.Join(", ", ValidStatuses)}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
