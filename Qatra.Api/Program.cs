using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;
using Qatra.Api.Interfaces;
using Qatra.Api.Options;
using Qatra.Api.Services;
using Qatra.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<INeighborhoodService, NeighborhoodService>();
builder.Services.Configure<ReportingOptions>(
    builder.Configuration.GetSection(ReportingOptions.SectionName));
builder.Services.AddScoped<IReportIngestionService, ReportIngestionService>();
builder.Services.Configure<IncidentsOptions>(
    builder.Configuration.GetSection(IncidentsOptions.SectionName));
builder.Services.AddScoped<IIncidentMergingService, IncidentMergingService>();
builder.Services.Configure<RankingOptions>(
    builder.Configuration.GetSection(RankingOptions.SectionName));
builder.Services.AddScoped<IRankingAndWarningService, RankingAndWarningService>();
builder.Services.AddHostedService<IncidentExpirationWorker>();
builder.Services.AddScoped<IIncidentMergingService, IncidentMergingService>();
builder.Services.AddHostedService<ReportProcessingWorker>();
builder.Services.AddHttpClient<IAiExtractionService, AiExtractionService>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["AiExtraction:BaseUrl"]!);
    c.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("AiExtraction:TimeoutSeconds", 30));
});

builder.Services.AddHttpClient<IAiExplanationService, AiExplanationService>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["AiExplanation:BaseUrl"]
        ?? builder.Configuration["AiExtraction:BaseUrl"]!);
    c.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("AiExplanation:TimeoutSeconds", 30));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddScoped<IIncidentMergingService, IncidentMergingService>();
builder.Services.AddHttpClient<IPredictionService, PredictionService>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["PredictionService:BaseUrl"]!);
    c.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("PredictionService:TimeoutSeconds", 20));
});
// builder.Services.AddHttpClient<IAiExplanationService, AiExplanationService>(c =>
// {
//     c.BaseAddress = new Uri(builder.Configuration["AiExplanation:BaseUrl"]
//         ?? builder.Configuration["AiExtraction:BaseUrl"]!);
//     c.Timeout = TimeSpan.FromSeconds(
//         builder.Configuration.GetValue("AiExplanation:TimeoutSeconds", 30));
// });

builder.Services.AddHttpClient<IAiExplanationService, AiExplanationService>((c, sp) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config["AiExplanation:BaseUrl"]
              ?? config["AiExtraction:BaseUrl"]
              ?? "https://generativelanguage.googleapis.com";
    c.BaseAddress = new Uri(url);
    c.Timeout = TimeSpan.FromSeconds(
        config.GetValue("AiExplanation:TimeoutSeconds", 30));
    return new AiExplanationService(
        c,
        config,
        sp.GetRequiredService<ILogger<AiExplanationService>>());
});

var app = builder.Build();

// تشغيل الـ Seeder مرة واحدة (في كل البيئات، مع حماية من الأخطاء)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedNeighborhoodsAsync(db);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database seeding failed");
}

app.MapOpenApi();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Qatra API v1");
    options.RoutePrefix = "swagger";
});

// احذف HttpsRedirection إن سبّب مشاكل في الاستضافة
 app.UseHttpsRedirection();
app.UseCors("AllowDashboard");
app.UseAuthorization();
app.MapControllers();
app.Run();