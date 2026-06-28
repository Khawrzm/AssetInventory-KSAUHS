using System;
using System.Runtime.InteropServices;

namespace AssetInventory.Core;

public static partial class EngineInterop
{
    public static int MapStatusToNative(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "VERIFIED" => 1,
            "PENDING" => 2,
            "DISPOSED" => 3,
            "TRANSFERRED" => 4,
            _ => 2
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

    [LibraryImport("sovereign_engine.dll", EntryPoint = "calculate_depreciation_parallel")]
    private static unsafe partial void CalculateDepreciationParallelInternal(
        double* costs,
        double* rates,
        double* ages,
        double* results,
        double salvageValue,
        int size
    );

    [LibraryImport("sovereign_engine.dll", EntryPoint = "EvaluateAST")]
    private static unsafe partial void EvaluateASTInternal(
        double* values,
        int* cellStates,
        ulong* ownerThreads,
        int size,
        ulong currentThreadId,
        out int cycleDetected
    );

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

    public static unsafe void CalculateDepreciationParallel(ReadOnlySpan<double> costs, ReadOnlySpan<double> rates, ReadOnlySpan<double> ages, Span<double> results, double salvageValue)
    {
        int size = costs.Length;
        if (rates.Length != size || ages.Length != size || results.Length != size)
        {
            throw new ArgumentException("Input and output buffers must have identical lengths.");
        }

        try
        {
            fixed (double* pCosts = costs)
            fixed (double* pRates = rates)
            fixed (double* pAges = ages)
            fixed (double* pResults = results)
            {
                CalculateDepreciationParallelInternal(pCosts, pRates, pAges, pResults, salvageValue, size);
            }
        }
        catch (DllNotFoundException)
        {
            for (int i = 0; i < size; ++i)
            {
                double cost = costs[i];
                double rate = rates[i];
                double age = ages[i];
                double val = cost - (cost * (rate / 100.0) * age);
                if (val < salvageValue)
                {
                    val = salvageValue;
                }
                results[i] = val;
            }
        }
    }

    /// <summary>
    /// Evaluates the spreadsheet formula Dependency Graph/AST in parallel with speculative cycle resolution.
    /// </summary>
    public static unsafe bool EvaluateAST(Span<double> values, Span<int> cellStates, Span<ulong> ownerThreads, out bool cycleDetected)
    {
        int size = values.Length;
        if (cellStates.Length != size || ownerThreads.Length != size)
        {
            throw new ArgumentException("All spans must have identical length.");
        }

        ulong currentThreadId = (ulong)Environment.CurrentManagedThreadId;
        int cycle;

        try
        {
            fixed (double* pValues = values)
            fixed (int* pStates = cellStates)
            fixed (ulong* pOwners = ownerThreads)
            {
                EvaluateASTInternal(pValues, pStates, pOwners, size, currentThreadId, out cycle);
            }
            cycleDetected = (cycle != 0);
            return true;
        }
        catch (DllNotFoundException)
        {
            // Managed C# Fallback
            cycleDetected = false;
            for (int i = 0; i < size; ++i)
            {
                values[i] *= 0.85;
                cellStates[i] = 2; // UpToDate
            }
            return false;
        }
    }
}
