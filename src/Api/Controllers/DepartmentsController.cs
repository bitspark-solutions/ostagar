using Ostagar.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Api.Controllers;

/// <summary>
/// Departments within the caller's own company.
/// </summary>
/// <remarks>
/// Deliberately the first real endpoint, and deliberately trivial. It exists so the isolation claim is
/// testable through HTTP rather than only through the database: a request authenticated as Acme must
/// not be able to read, create, or address a row belonging to another tenant, and there is currently
/// no other way to prove that end to end.
/// </remarks>
[ApiController]
[Route("api/v1/departments")]
public class DepartmentsController(OstagarDbContext context) : ControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        // Ordering by Bangla collation is a P4 concern and is deliberately not approximated here with
        // an ordinal sort that would look correct in English and be wrong in Bangla.
        var departments = await context.Departments
            .AsNoTracking()
            .OrderBy(department => department.SortOrder)
            .ThenBy(department => department.Name)
            .Select(department => new
            {
                department.Id,
                department.Code,
                department.Name,
                department.NameBangla,
                department.IsActive,
            })
            .ToListAsync(cancellationToken);

        return Ok(departments);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { reason = "code_and_name_required" });
        }

        // The tenant is taken from the token, never from the body. Accepting a tenant id here would
        // let a caller with write access to its own company place a row inside another one.
        var department = new Platform.Tenancy.Department
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantContext.TenantId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            NameBangla = request.NameBangla?.Trim() ?? string.Empty,
            SortOrder = request.SortOrder,
            IsActive = true,
        };

        context.Departments.Add(department);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), new { id = department.Id }, new { department.Id, department.Code, department.Name });
    }
}
