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

    [LibraryImport("sovereign_engine.dll", EntryPoint = "CalculateAssetDepreciation")]
    private static unsafe partial void CalculateAssetDepreciationInternal(
        double* values,
        int size
    );

    /// <summary>
    /// Computes summary statistics for assets using the optimized native C++ engine, with a managed fallback.
    /// </summary>
    public static unsafe void CalculateStats(ReadOnlySpan<int> statuses, out int total, out int verified, out int pending, out int disposed, out int transferred)
    {
        try
        {
            fixed (int* ptr = statuses)
            {
                CalculateStatsInternal(ptr, statuses.Length, out total, out verified, out pending, out disposed, out transferred);
            }
        }
        catch (DllNotFoundException)
        {
            total = statuses.Length;
            verified = 0;
            pending = 0;
            disposed = 0;
            transferred = 0;
            foreach (var status in statuses)
            {
                switch (status)
                {
                    case 1: verified++; break;
                    case 2: pending++; break;
                    case 3: disposed++; break;
                    case 4: transferred++; break;
                }
            }
        }
    }

    /// <summary>
    /// Performs depreciation projection calculations utilizing the native C++ matrix engine, with a managed fallback.
    /// </summary>
    public static unsafe void ProjectDepreciation(ReadOnlySpan<double> initialValues, ReadOnlySpan<double> rates, Span<double> result)
    {
        if (result.Length < initialValues.Length * rates.Length)
        {
            throw new ArgumentException("Result buffer is too small to hold the projected matrix.", nameof(result));
        }

        try
        {
            fixed (double* pInit = initialValues)
            fixed (double* pRates = rates)
            fixed (double* pResult = result)
            {
                ProjectDepreciationMatrixInternal(pInit, initialValues.Length, pRates, rates.Length, pResult);
            }
        }
        catch (DllNotFoundException)
        {
            int idx = 0;
            for (int r = 0; r < initialValues.Length; ++r)
            {
                double value = initialValues[r];
                for (int c = 0; c < rates.Length; ++c)
                {
                    value = value * (1.0 - rates[c]);
                    result[idx++] = value;
                }
            }
        }
    }

    /// <summary>
    /// Computes asset depreciation in-place using raw pointer spans, with a managed fallback.
    /// </summary>
    public static unsafe void CalculateAssetDepreciation(Span<double> values)
    {
        try
        {
            fixed (double* ptr = values)
            {
                CalculateAssetDepreciationInternal(ptr, values.Length);
            }
        }
        catch (DllNotFoundException)
        {
            for (int i = 0; i < values.Length; ++i)
            {
                values[i] *= 0.85;
            }
        }
    }
}
