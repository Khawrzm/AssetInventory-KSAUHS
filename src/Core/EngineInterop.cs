using System;
using System.Runtime.InteropServices;

namespace AssetInventory.Core;

public static partial class EngineInterop
{
    // Mapping of Status String to Native Int
    public static int MapStatusToNative(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "VERIFIED" => 1,
            "PENDING" => 2,
            "DISPOSED" => 3,
            "TRANSFERRED" => 4,
            _ => 2 // Default to PENDING
        };
    }

    [LibraryImport("sovereign_engine.dll", EntryPoint = "CalculateStats")]
    private static unsafe partial void CalculateStatsInternal(
        int* statusArray,
        int size,
        out int outTotal,
        out int outVerified,
        out int outPending,
        out int outDisposed,
        out int outTransferred
    );

    [LibraryImport("sovereign_engine.dll", EntryPoint = "ProjectDepreciationMatrix")]
    private static unsafe partial void ProjectDepreciationMatrixInternal(
        double* initialValues,
        int rows,
        double* depreciationRates,
        int cols,
        double* resultMatrix
    );

    /// <summary>
    /// Computes summary statistics for assets using the optimized native C++ engine.
    /// </summary>
    public static unsafe void CalculateStats(ReadOnlySpan<int> statuses, out int total, out int verified, out int pending, out int disposed, out int transferred)
    {
        fixed (int* ptr = statuses)
        {
            CalculateStatsInternal(ptr, statuses.Length, out total, out verified, out pending, out disposed, out transferred);
        }
    }

    /// <summary>
    /// Performs depreciation projection calculations utilizing the native C++ matrix engine.
    /// </summary>
    public static unsafe void ProjectDepreciation(ReadOnlySpan<double> initialValues, ReadOnlySpan<double> rates, Span<double> result)
    {
        if (result.Length < initialValues.Length * rates.Length)
        {
            throw new ArgumentException("Result buffer is too small to hold the projected matrix.", nameof(result));
        }

        fixed (double* pInit = initialValues)
        fixed (double* pRates = rates)
        fixed (double* pResult = result)
        {
            ProjectDepreciationMatrixInternal(pInit, initialValues.Length, pRates, rates.Length, pResult);
        }
    }
}
