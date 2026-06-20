using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using TmsApi.Data;
namespace TmsApi.Controllers;

[ApiController]
[Route("api/test")]
public class TestController(TmsDbContext context) : ControllerBase
{
    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }

    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine("\n>>> STEP 1: Building the query object (nodatabase contact)...");
        var query = context.Students.Where(s => s.GPA >= 3.0m);
        Console.WriteLine(">>> STEP 2: Appending a sorting clause...");
        var orderedQuery = query.OrderBy(s => s.Name);
        Console.WriteLine(">>> STEP 3: Materializing query into a C#List...");
        var results = orderedQuery.ToList(); // Execution is triggered here
        Console.WriteLine(">>> STEP 4: Materialization finished. List populated.\n");

        return Ok(results);
    }

    [HttpGet("students-page")]
    public async Task<IActionResult> GetStudentsPage([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return BadRequest("pageNumber and pageSize must be greater than zero.");
        }

        var offset = (pageNumber - 1) * pageSize;
        var page = await context.Students
            .OrderBy(s => s.Name)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = page
        });
    }

    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine("\n>>> STEP 1: Running non-translatable query...");
        try
        {
            var students = context.Students
                .Where(s => IsHonorRoll(s.GPA)) // EF Core does not know how to map this method to SQL
                .ToList();
            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> EXCEPTION CAUGHT: {ex.Message}\n");
            return BadRequest(new { Message = ex.Message });
        }
    }
    [HttpGet("students")]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var students = await context.Students
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
        return Ok(students);
    }
    [HttpGet("student-page")]
    public async Task<IActionResult> GetPage([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        if (pageNumber < 1 || pageSize < 1)
        {
            return BadRequest("pageNumber and pageSize must be greater than zero.");
        }

        var pageItems = await context.Students
            .OrderBy(s => s.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            Items = pageItems
        });
    }
    //create an end point to create a student using POST method and return the created student with 201 status code
    
}
