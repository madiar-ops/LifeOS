using LifeOS.API.Extensions;
using LifeOS.API.Filters;
using LifeOS.API.Middleware;
using LifeOS.API.Services;
using LifeOS.Application;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Infrastructure;
using Serilog;

// Bootstrap-логгер: ловит ошибки, случившиеся ДО построения хоста.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Запуск LifeOS.API...");

    var builder = WebApplication.CreateBuilder(args);

    // Полная конфигурация Serilog читается из appsettings — уровни и синки
    // меняются без пересборки образа.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ---- Слои приложения -------------------------------------------------
    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

    // ---- Аутентификация --------------------------------------------------
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
    builder.Services.AddJwtAuthentication(builder.Configuration);

    // ---- Web -------------------------------------------------------------
    builder.Services.AddScoped<ValidationFilter>();

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Enum'ы отдаются строками ("InProgress", а не 1): фронту не нужно
            // держать у себя копию числовых значений, а JSON читаем глазами.
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    // Штатная валидация ASP.NET отключена: за неё отвечает ValidationFilter,
    // иначе на одну ошибку клиент получал бы два разных формата ответа.
    builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
        options.SuppressModelStateInvalidFilter = true);

    builder.Services.AddProblemDetails();
    builder.Services.AddSwaggerDocumentation();
    builder.Services.AddCorsPolicy(builder.Configuration);

    var app = builder.Build();

    // ---- Конвейер обработки запроса --------------------------------------
    // Порядок критичен: обработчик исключений первым, чтобы перехватывать
    // ошибки всех последующих компонентов.
    app.UseGlobalExceptionHandling();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "LifeOS API v1");
            options.DocumentTitle = "LifeOS API";
        });

        await app.ApplyMigrationsAsync();
    }
    else
    {
        app.UseHttpsRedirection();
    }

    app.UseCors(CorsExtensions.PolicyName);

    // Строго в этом порядке: сначала «кто ты», потом «что тебе можно».
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "LifeOS.API аварийно завершился при старте.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Требуется для интеграционных тестов (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
