using Microsoft.AspNetCore.Authentication;
using TmsApi.Middleware;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;
using TmsApi.Services;



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
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();

// Register our training scheme mock services
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase")).LogTo(Console.WriteLine, LogLevel.Information).EnableSensitiveDataLogging()
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    Console.WriteLine("Running in Development environment. Detailed errors will be shown.");
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TMS API - Development Workspace")
               .WithTheme(ScalarTheme.Purple);
    });
}
else
{
    app.UseExceptionHandler();
}

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
app.MapGet("/debug/nplusone", async (TmsDbContext db, CancellationToken cancellationToken) =>
{
    var report = await db.Students
.AsNoTracking()
.Select(s => new
{
s.Name,
EnrollmentCount = s.Enrollments.Count
})
.ToListAsync(cancellationToken);
foreach (var r in report)
Console.WriteLine($"{r.Name}: {r.EnrollmentCount} enrollments");
    return Results.Ok("Done");
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

// Seed test data at startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    context.Database.Migrate(); // Applies any pending migrations; keeps migration history intact
    if (!context.Students.Any())
    {
        var students = new List<Student>
        {
            new() { RegistrationNumber = "TMS-2026-0001", Name = "Alice Smith", GPA = 3.8m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0002", Name = "Bob Jones", GPA = 2.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0003", Name = "Charlie Brown", GPA = 3.4m, IsActive = false },
            new() { RegistrationNumber = "TMS-2026-0004", Name = "Diana Prince", GPA = 3.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0005", Name = "Evan Wright", GPA = 2.5m, IsActive = true }
        };
        context.Students.AddRange(students);
        var courses = new List<Course>
        {
            new() { Code = 101, Title = "Introduction to Computer Science", MaxCapacity = 30 },
            new() { Code = 201, Title = "Data Structures and Algorithms", MaxCapacity = 25 },
            new() { Code = 101, Title = "Calculus I", MaxCapacity = 40 }
        };
        context.Courses.AddRange(courses);
        context.SaveChanges();
        var enrollments = new List<Enrollment>
        {
            new() { StudentId = students[0].Id, CourseId = courses[0].Id, Grade = 4.0m },
            new() { StudentId = students[0].Id, CourseId = courses[1].Id, Grade = 3.6m },
            new() { StudentId = students[1].Id, CourseId = courses[0].Id, Grade = 2.8m },
            new() { StudentId = students[3].Id, CourseId = courses[1].Id, Grade = 3.9m }
        };
        context.Enrollments.AddRange(enrollments);
        context.SaveChanges();
    }
}

app.Run();