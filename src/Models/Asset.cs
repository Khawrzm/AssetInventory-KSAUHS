using System;
using System.Security.Cryptography;
using System.Text;

namespace AssetInventory.Models;

public class Asset
{
    public string TagNumber        { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    public string MajorLoc         { get; set; } = string.Empty;
    public string MinorLoc         { get; set; } = string.Empty;
    public string Status           { get; set; } = "PENDING";
    public string DataHash         { get; set; } = string.Empty;
    public string Note             { get; set; } = string.Empty;

    public string GenerateHash() =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(TagNumber + MajorLoc + MinorLoc)));
}
