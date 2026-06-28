#include <span>
#include <vector>
#include <memory_resource>

#ifdef _WIN32
#define SOVEREIGN_API __declspec(dllexport)
#else
#define SOVEREIGN_API __attribute__((visibility("default")))
#endif

extern "C" {
    SOVEREIGN_API void CalculateAssetDepreciation(double* values, int size) {
        if (!values || size <= 0) return;
        
        // Zero-copy bounds-safe memory access via std::span
        std::span<double> view(values, size);
        
        // High-performance vectorised math loop applying 15% depreciation
        for (double& val : view) {
            val *= 0.85;
        }
    }
}
