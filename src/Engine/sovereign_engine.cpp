#include <span>

#ifdef _WIN32
#define SOVEREIGN_API __declspec(dllexport)
#else
#define SOVEREIGN_API __attribute__((visibility("default")))
#endif

extern "C" {
    SOVEREIGN_API void CalculateDepreciation(double* values, int size, double rate) {
        if (!values || size <= 0) return;
        std::span<double> view(values, size);
        for (double& val : view) {
            val *= (1.0 - rate);
        }
    }
}
