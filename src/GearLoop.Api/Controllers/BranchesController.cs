using GearLoop.Api.Data;
using GearLoop.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GearLoop.Api.Controllers;

[ApiController]
[Route("api/branches")]
public class BranchesController : ControllerBase
{
    private readonly GearLoopDbContext _db;

    public BranchesController(GearLoopDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<BranchDto>>> GetAll(CancellationToken cancellationToken)
    {
        var branches = await _db.Branches
            .OrderBy(b => b.Name)
            .Select(b => new BranchDto(b.Id, b.Name, b.City, b.Address))
            .ToListAsync(cancellationToken);

        return Ok(branches);
    }
}
