using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using ArangoDBNetStandard.Transport.Http;
using Microsoft.AspNetCore.Authentication;
using StudyGraph.Api.Repositories;
using StudyGraph.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- ArangoDB client (ArangoDBNetStandard) — singleton, dùng chung HttpClient ----
builder.Services.AddSingleton<IArangoDBClient>(_ =>
{
    var cfg = builder.Configuration.GetSection("Arango");
    var transport = HttpApiTransport.UsingBasicAuth(
        new Uri(cfg["Url"]!), cfg["Database"]!, cfg["User"]!, cfg["Password"]!);
    return new ArangoDBClient(transport);
});

builder.Services.AddScoped<CourseRepository>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<EnrollmentRepository>();
builder.Services.AddScoped<RecommendationRepository>();
builder.Services.AddScoped<QuizRepository>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<QuizService>();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<AuthenticationMiddleware>();
app.MapControllers();

app.UseHttpsRedirection();

app.Run();
