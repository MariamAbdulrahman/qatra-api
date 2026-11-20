using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;
using Qatra.Api.Interfaces;
using Qatra.Api.Options;
using Qatra.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<INeighborhoodService, NeighborhoodService>();
builder.Services.Configure<ReportingOptions>(
    builder.Configuration.GetSection(ReportingOptions.SectionName));
builder.Services.AddScoped<IReportIngestionService, ReportIngestionService>();

builder.Services.AddHttpClient<IPredictionService, PredictionService>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["PredictionService:BaseUrl"]!);
    c.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("PredictionService:TimeoutSeconds", 20));
});
builder.Services.AddHttpClient<IAiExtractionService, AiExtractionService>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["AiExtraction:BaseUrl"] ?? "https://api.anthropic.com");
    c.Timeout = TimeSpan.FromSeconds(30);
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
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();
app.Run();