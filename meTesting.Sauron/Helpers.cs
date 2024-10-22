using meTesting.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Settings.Configuration;
using Serilog.Sinks.MSSqlServer;
using System.Configuration;
using System.Net.Http.Headers;
using System.Xml.Serialization;
using static Serilog.Sinks.MSSqlServer.ColumnOptions;

namespace meTesting.Sauron;

public static class Helpers
{
    public static IServiceCollection AddSauron(this IServiceCollection services, IConfiguration configuration, Action<SauronConfig> config)
    {
        var conf = new SauronConfig();
        config(conf);
        return services.AddSauron(configuration, conf);
    }

    public static IServiceCollection AddSauron(this IServiceCollection services, IConfiguration configuration, SauronConfig config)
    {

        var colOptions = new ColumnOptions();

        colOptions.Store.Add(StandardColumn.TraceId);
        colOptions.Store.Add(StandardColumn.SpanId);


        colOptions.AdditionalColumns = [
            new (){
                AllowNull = true,
                ColumnName = "TransactionId",
                DataLength = 50,
                DataType = System.Data.SqlDbType.NVarChar,
               },
               new (){
                AllowNull = true,
                ColumnName = "AggregateId",
                DataLength = 50,
                DataType = System.Data.SqlDbType.NVarChar,
               },               new (){
                AllowNull = true,
                ColumnName = "ApplicationId",
                DataLength = 50,
                DataType = System.Data.SqlDbType.NVarChar,
               },
            ];

        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration, new ConfigurationReaderOptions()
            {
                SectionName = config.SectionName,
            })
            .Enrich.FromLogContext()
            .Enrich.With(new SauronEnricher(config))
            .WriteTo.MSSqlServer(config.ConnectionString,
            sinkOptions: new MSSqlServerSinkOptions
            {
                TableName = "Log",
                AutoCreateSqlDatabase = true,
                AutoCreateSqlTable = true,
            },
            columnOptions: colOptions);

        var logger = loggerConfig.CreateLogger();

        services.AddSerilog(logger);

        return services.AddHttpContextAccessor();
    }
    public static IServiceCollection AddSauronHttpClient<T>(this IServiceCollection services, Action<IServiceProvider, HttpClient> config = null) where T : class
    {

        services.AddHttpClient<T>((s, h) =>
        {
            var t = s.GetRequiredService<IHttpContextAccessor>().HttpContext;

            if (t?.Request.Headers.TryGetValue(SauronConstants.TID_KEY, out var id) == true)
            {
                h.DefaultRequestHeaders.SetAggregateId(id);
            }
            if (config is not null)
                config(s, h);

        });

        return services;
    }
    public static void UseSauron(this WebApplication app)
    {
        app.UseMiddleware<RequestTraceIdMiddleware>();
    }
    public static bool GetAggregateId(this HttpContext context, out StringValues id)
    {
        return context.Request.Headers.TryGetValue(SauronConstants.TID_KEY, out id);
    }
    public static bool GetAggregateId(this HttpRequestHeaders headers, out StringValues id)
    {
        var res = headers.TryGetValues(SauronConstants.TID_KEY, out var ids);
        id = ids?.FirstOrDefault();
        return res;
    }
    public static void SetAggregateId(this HttpRequestHeaders headers, string id)
    {
        headers.Add(SauronConstants.TID_KEY, [id]);
    }

}


public class SauronEnricher(SauronConfig config) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ApplicationId", config.AppName));
    }
}