#include <span>
#include <numeric>
#include <algorithm>
#include <vector>
#include <memory_resource>

#ifdef _WIN32
#define SOVEREIGN_API __declspec(dllexport)
#else
#define SOVEREIGN_API __attribute__((visibility("default")))
#endif

extern "C" {
    SOVEREIGN_API double ComputeSum(const double* data, int size) {
        if (!data || size <= 0) return 0.0;
        std::span<const double> view(data, size);
        return std::accumulate(view.begin(), view.end(), 0.0);
    }

    SOVEREIGN_API void CalculateStats(
        const int* statusArray,
        int size,
        int* outTotal,
        int* outVerified,
        int* outPending,
        int* outDisposed,
        int* outTransferred
    ) {
        if (!statusArray || size <= 0) {
            if (outTotal) *outTotal = 0;
            if (outVerified) *outVerified = 0;
            if (outPending) *outPending = 0;
            if (outDisposed) *outDisposed = 0;
            if (outTransferred) *outTransferred = 0;
            return;
        }

        std::span<const int> statuses(statusArray, size);
        int total = static_cast<int>(statuses.size());
        int verified = 0, pending = 0, disposed = 0, transferred = 0;

        for (int status : statuses) {
            switch (status) {
                case 1: verified++; break;
                case 2: pending++; break;
                case 3: disposed++; break;
                case 4: transferred++; break;
                default: break;
            }
        }

        if (outTotal) *outTotal = total;
        if (outVerified) *outVerified = verified;
        if (outPending) *outPending = pending;
        if (outDisposed) *outDisposed = disposed;
        if (outTransferred) *outTransferred = transferred;
    }

    SOVEREIGN_API void ProjectDepreciationMatrix(
        const double* initialValues,
        int rows,
        const double* depreciationRates,
        int cols,
        double* resultMatrix
    ) {
        if (!initialValues || rows <= 0 || !depreciationRates || cols <= 0 || !resultMatrix) {
            return;
        }

        std::span<const double> initValSpan(initialValues, rows);
        std::span<const double> ratesSpan(depreciationRates, cols);

        std::vector<std::byte> buffer(rows * cols * sizeof(double) * 2);
        std::pmr::monotonic_buffer_resource mbr(buffer.data(), buffer.size());
        std::pmr::polymorphic_allocator<double> alloc(&mbr);

        double* tempMatrix = alloc.allocate(rows * cols);

        for (int r = 0; r < rows; ++r) {
            double value = initValSpan[r];
            for (int c = 0; c < cols; ++c) {
                value = value * (1.0 - ratesSpan[c]);
                tempMatrix[r * cols + c] = value;
            }
        }

        std::copy(tempMatrix, tempMatrix + (rows * cols), resultMatrix);
        alloc.deallocate(tempMatrix, rows * cols);
    }
}
