using Microsoft.AspNetCore.Mvc;
using Nyris.Crdt.Distributed.Model;

namespace Nyris.Crdt.AspNetExample.Controllers;

[ApiController]
[Route("/")]
public class HomeController : ControllerBase
{
    private readonly NodeId _thisNodeId;

    public HomeController(NodeInfo nodeInfo)
    {
        _thisNodeId = nodeInfo.Id;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return Ok(_thisNodeId);
    }
}
