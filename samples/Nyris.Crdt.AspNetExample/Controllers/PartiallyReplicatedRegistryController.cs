using Microsoft.AspNetCore.Mvc;
using Nyris.Crdt.Distributed.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nyris.Crdt.Model;

namespace Nyris.Crdt.AspNetExample.Controllers;

[ApiController]
[Route("pr")]
public sealed class PartiallyReplicatedRegistryController : ControllerBase
{
    private readonly MyContext _context;

    public PartiallyReplicatedRegistryController(MyContext context)
    {
        _context = context;
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItemsAsync()
    {
        return Ok(await _context.PartiallyReplicatedImageCollectionsRegistry.GetLocalShardsAsync());
    }

    [HttpGet("dtos")]
    public async Task<IActionResult> GetItemDtosAsync()
    {
        return Ok(await _context.PartiallyReplicatedImageCollectionsRegistry.GetLocalShardDtosAsync());
    }

    [HttpGet("items/{shardId}")]
    public async Task<IActionResult> GetItemsAsync(string id)
    {
        var r = await _context.PartiallyReplicatedImageCollectionsRegistry.GetLocalShardsAsync();
        var shardId = new ShardId(id);
        if (!r.ContainsKey(shardId)) return NotFound();

        return Ok(r[shardId].OrderBy(pair => pair.Key));
    }

    [HttpGet("dtos/{shardId}")]
    public async Task<IActionResult> GetItemDtosAsync(string id)
    {
        var r = await _context.PartiallyReplicatedImageCollectionsRegistry.GetLocalShardDtosAsync();
        var shardId = new ShardId(id);
        if (!r.ContainsKey(shardId)) return NotFound();

        var hash = Convert.ToHexString(_context.GetHash(new InstanceId(id)));

        var result = new Dictionary<ImageGuid, TimeStampedItem<ImageInfo, DateTime>>();
        foreach (var dto in r[shardId])
        {
            foreach (var (imageGuid, item) in dto!.Items!)
            {
                result.Add(imageGuid, item);
            }
        }

        return Ok(new { hash, dtos = result.OrderBy(pair => pair.Key) });
    }
}
