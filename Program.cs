using Microsoft.AspNetCore.Authentication;
using TmsApi.Middleware;
using Scalar.AspNetCore;



var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// 1. REGISTER SERVICES
builder.Services.AddControllers();
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

// Register our training scheme mock services
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();


var app = builder.Build();

app.UseHttpsRedirection();

app.UseRouting();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthentication();

app.UseAuthorization();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/api/error", () =>
{
    throw new InvalidOperationException("This is a test exception for error handling.");
});
app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
})).RequireAuthorization();
// app.MapGet("/api/enrollments/worker-smoke", (EnrollmentWorker worker) =>
// {
//     worker.ProcessBatch();
//     return Results.Ok("Processed cleanly without leaks.");
// });
app.MapControllers();
app.Run();