#include <span>
#include <vector>
#include <execution>
#include <algorithm>
#include <numeric>

#ifdef _WIN32
#define SOVEREIGN_API __declspec(dllexport)
#else
#define SOVEREIGN_API __attribute__((visibility("default")))
#endif

extern "C" {
    // ── Old interop entry points ──────────────────────────────────────────
    SOVEREIGN_API void CalculateDepreciation(double* values, int size, double rate) {
        if (!values || size <= 0) return;
        std::span<double> view(values, size);
        for (double& val : view) {
            val *= (1.0 - rate);
        }
    }

    SOVEREIGN_API void CalculateAssetDepreciation(double* values, int size) {
        if (!values || size <= 0) return;
        std::span<double> view(values, size);
        for (double& val : view) {
            val *= 0.85; // Subtract 15% depreciation
        }
    }

    // ── Logistics-Zero parallel math engine ───────────────────────────────
    SOVEREIGN_API void calculate_depreciation_parallel(
        const double* costs, 
        const double* rates, 
        const double* ages, 
        double* results, 
        double salvageValue, 
        int size) 
    {
        if (!costs || !rates || !ages || !results || size <= 0) return;

        auto cost_span = std::span(costs, size);
        auto rate_span = std::span(rates, size);
        auto age_span = std::span(ages, size);
        auto res_span = std::span(results, size);

        // Vectorized indices for parallel evaluation
        std::vector<int> indices(size);
        std::iota(indices.begin(), indices.end(), 0);

        // Execute parallel vectorized operations (equivalent to Puncalc)
        std::for_each(std::execution::par_unseq, indices.begin(), indices.end(), [&](int i) {
            double rate = rate_span[i];
            double age = age_span[i];
            double cost = cost_span[i];
            
            // Formula: CurrentValue = InitialCost - (InitialCost * (rate / 100.0) * Age)
            double val = cost - (cost * (rate / 100.0) * age);
            
            // Clamp to salvageValue
            if (val < salvageValue) {
                val = salvageValue;
            }
            res_span[i] = val;
        });
    }
}
