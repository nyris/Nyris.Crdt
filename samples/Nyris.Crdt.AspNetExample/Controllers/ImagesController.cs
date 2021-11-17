using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.Crdt.Distributed.Model;
using Nyris.Model.Ids;

namespace Nyris.Crdt.AspNetExample.Controllers
{
    [ApiController]
    [Route("images")]
    public sealed class ImagesController : ControllerBase
    {
        private readonly MyContext _context;
        private readonly NodeId _thisNodeId;

        /// <inheritdoc />
        public ImagesController(MyContext context, NodeInfo thisNode)
        {
            _context = context;
            _thisNodeId = thisNode.Id;
        }

        [HttpGet("{indexId:guid}")]
        public async Task<IActionResult> GetAll(Guid indexId)
        {
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(new IndexId(indexId),
                () => (_thisNodeId, new ImageInfoLwwRegistry(indexId.ToString("N"))));
            return Ok(new { data = index.Values });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(_context.ImageCollectionsRegistry.Value);

        [HttpPost]
        public async Task<IActionResult> ImageDataSet([FromBody] ImageDataSetEvent data)
        {
            var indexId = new IndexId(data.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwRegistry(data.IndexId.ToString("N"))));
            if (!index.TrySet(data.ImageUuid, new ImageInfo(data.DownloadUri, data.ImageId), DateTime.UtcNow,
                out var img))
            {
                return Conflict(img);
            }
            return Ok(img);
        }

        [HttpDelete]
        public async Task<IActionResult> ImageDeleted([FromBody] ImageDeletedEvent data)
        {
            var indexId = new IndexId(data.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwRegistry(data.IndexId.ToString("N"))));
            if (!index.TryRemove(data.ImageUuid, DateTime.UtcNow, out var image))
            {
                return NotFound();
            }
            return Ok(image);
        }
    }
}