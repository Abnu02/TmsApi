using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// 1. REGISTER SERVICES
builder.Services.AddControllers();

// Register our training scheme mock services
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddAuthorization();

var app = builder.Build();

// 2. CONFIGURE PIPELINE MIDDLEWARE (ORDER MATTERS)
app.UseHttpsRedirection();

// Step 1: Routing must happen first so the app matches the URL to an endpoint
app.UseRouting();

// Step 2: Authentication checks WHO you are (processes our TrainingAuthHandler)
app.UseAuthentication();

// Step 3: Authorization checks IF you have permission to access the matched route
app.UseAuthorization();

// Step 4: Map the endpoint and explicitly lock it down
app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
})).RequireAuthorization(); // <-- This forces it to participate in security

app.MapControllers();
app.Run();