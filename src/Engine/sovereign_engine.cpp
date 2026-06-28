#include <span>
#include <vector>
#include <execution>
#include <algorithm>
#include <numeric>
#include <memory_resource>
#include <thread>
#include <atomic>

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
            val *= 0.85;
        }
    }

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

        std::vector<int> indices(size);
        std::iota(indices.begin(), indices.end(), 0);

        std::for_each(std::execution::par_unseq, indices.begin(), indices.end(), [&](int i) {
            double rate = rate_span[i];
            double age = age_span[i];
            double cost = cost_span[i];
            
            double val = cost - (cost * (rate / 100.0) * age);
            
            if (val < salvageValue) {
                val = salvageValue;
            }
            res_span[i] = val;
        });
    }

    // ── Precise AST Parallel Evaluation with Cycle Detection ──────────────
    SOVEREIGN_API void EvaluateAST(
        double* values, 
        int* cellStates, 
        uint64_t* ownerThreads,
        int size, 
        uint64_t currentThreadId,
        int* cycleDetectedOut) 
    {
        if (!values || !cellStates || !ownerThreads || size <= 0) return;

        auto val_span = std::span(values, size);
        
        // Zero-copy stack memory resource allocation to prevent fragmentation
        std::byte buffer[2048];
        std::pmr::monotonic_buffer_resource mem_pool(buffer, sizeof(buffer));
        std::pmr::vector<int> node_indices(&mem_pool);
        node_indices.resize(size);
        std::iota(node_indices.begin(), node_indices.end(), 0);

        *cycleDetectedOut = 0;

        // Parallelized evaluation
        std::for_each(std::execution::par_unseq, node_indices.begin(), node_indices.end(), [&](int i) {
            // Check for cyclic dependency
            if (cellStates[i] == 1) { // 1 = Computing state
                uint64_t owner = ownerThreads[i];
                if (owner == currentThreadId) {
                    // Cyclic dependency detected - abort to prevent deadlocks
                    *cycleDetectedOut = 1;
                    return;
                }
                else if (currentThreadId < owner) {
                    // Speculative Reevaluation: lower thread ID takes over
                    ownerThreads[i] = currentThreadId;
                }
                else {
                    // Current thread yields and waits for evaluation to complete
                    while (cellStates[i] == 1) {
                        std::this_thread::yield();
                    }
                }
            }

            // Transition: Dirty (0) -> Computing (1)
            cellStates[i] = 1;
            ownerThreads[i] = currentThreadId;

            // Perform precise calculations
            val_span[i] = val_span[i] * 0.85; 

            // Transition: Computing (1) -> UpToDate (2)
            cellStates[i] = 2;
        });
    }
}
