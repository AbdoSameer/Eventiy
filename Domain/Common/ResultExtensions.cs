using System;

namespace Domain.Common
{
    public static class ResultExtensions
    {
        public static T GetValueOrThrow<T>(this Result<T> result, string errorMessage = null)
        {
            if (result.IsFailure)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Message));
                throw new InvalidOperationException(errorMessage ?? $"Operation failed: {errors}");
            }
            return result.Value;
        }

        public static void EnsureSuccess(this Result result, string errorMessage = null)
        {
            if (result.IsFailure)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Message));
                throw new InvalidOperationException(errorMessage ?? $"Operation failed: {errors}");
            }
        }
    }
}