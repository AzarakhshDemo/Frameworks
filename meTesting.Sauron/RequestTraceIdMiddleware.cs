using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;

namespace meTesting.Sauron;

public class RequestTraceIdMiddleware
{

    private readonly RequestDelegate _next;

    public RequestTraceIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        bool hasId = context.GetAggregateId(out var id);
        if (!hasId)
        {
            id = Guid.NewGuid().ToString("N");
            context.Request.Headers[SauronConstants.TID_KEY] = id;
        }
        using (LogContext.PushProperty(SauronConstants.AGGREGATE_ID_KEY, id.First()))
        {
            await _next(context);
        }
    }
}
