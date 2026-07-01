using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Application.Abstractions.Behaviors
{
    public sealed class LoggingPipelineBehavior<TRequest, TResponse>(
        ILogger<LoggingPipelineBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse> where TRequest : IBaseRequest
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var name = typeof(TRequest).Name;
            var sw = Stopwatch.StartNew();
            
            logger.LogInformation("[START] Handling {RequestName}", name);
            
            var result = await next();
            logger.LogInformation("[END] {RequestName} handled in {ElapsedMs}ms", name, sw.ElapsedMilliseconds);
            return result;
        }
    }
}
