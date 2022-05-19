using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Nyris.Crdt.AspNetExample.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly MyContext _context;
    private readonly ILogger<UsersController> _logger;

    public UsersController(MyContext context, ILogger<UsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_context.UserObservedRemoveSet.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create()
    {
        var id = Guid.NewGuid();
        var user = new User(id, "first-name", "last-name");

        await _context.UserObservedRemoveSet.AddAsync(user);

        var result = new UserResponse
        {
            Guid = id.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
        };

        _logger.LogDebug("Created User: {UserId}", result.Guid);

        return Ok(result);
    }
}
