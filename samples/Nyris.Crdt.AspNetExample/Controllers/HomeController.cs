using Microsoft.AspNetCore.Mvc;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.AspNetExample.Controllers;

[ApiController]
[Route("/")]
public class HomeController : ControllerBase
{
    private readonly MyContext _context;
    private readonly NodeId _thisNodeId;

    public HomeController(NodeInfo nodeInfo, MyContext context)
    {
        _context = context;
        _thisNodeId = nodeInfo.Id;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return Ok(_thisNodeId);
    }


    [HttpGet("nodes")]
    public IActionResult GetAllNodes()
    {
        return Ok(_context.Nodes.Value);
    }
}
