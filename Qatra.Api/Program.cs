// using Microsoft.EntityFrameworkCore;
// using Qatra.Api.Data;
// using Qatra.Api.Interfaces;
// using Qatra.Api.Services;

// var builder = WebApplication.CreateBuilder(args);

// // Add services to the container.

// builder.Services.AddControllers();
// // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
// builder.Services.AddOpenApi();
// builder.Services.AddDbContext<AppDbContext>(options =>
//     options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// builder.Services.AddScoped<INeighborhoodService, NeighborhoodService>();

// var app = builder.Build();

// // Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.MapOpenApi();
//     app.UseSwaggerUI(options =>
//     {
//         options.SwaggerEndpoint("/openapi/v1.json", "Qatra API v1");
//         options.RoutePrefix = "swagger";
//     });
// }

// app.UseHttpsRedirection();

// app.UseAuthorization();

// app.MapControllers();

// app.Run();

using Microsoft.EntityFrameworkCore;
using Qatra.Api.Data;
using Qatra.Api.Interfaces;
using Qatra.Api.Options;
using Qatra.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<INeighborhoodService, NeighborhoodService>();
builder.Services.Configure<ReportingOptions>(
    builder.Configuration.GetSection(ReportingOptions.SectionName));

builder.Services.AddScoped<IReportIngestionService, ReportIngestionService>();
var app = builder.Build();

// إدخال الأحياء الأولية (مرة واحدة فقط إن كان الجدول فارغًا)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedNeighborhoodsAsync(db);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Qatra API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();