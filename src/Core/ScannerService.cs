using System.Text.RegularExpressions;

namespace AssetInventory.Core
{
    public static class ScannerService
    {
        // يتأكد أن الباركود يحتوي فقط على أرقام وحروف (يمنع أي حقن برمجيات خبيثة)
        public static string Sanitize(string input)
        {
            return Regex.Replace(input, @"[^a-zA-Z0-9-]", "");
        }
    }
}
