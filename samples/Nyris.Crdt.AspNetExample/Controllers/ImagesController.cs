using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nyris.Crdt.AspNetExample.Events;
using Nyris.Crdt.Distributed.Model;

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
        public async Task<IActionResult> GetIndexAsync(Guid indexId)
        {
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(CollectionId.FromGuid(indexId),
                () => (_thisNodeId, new ImageInfoLwwCollection(indexId.ToString("N"))));
            return Ok(new { data = index.Values });
        }

        [HttpGet("{indexId:guid}")]
        public async Task<IActionResult> CreateIndexAsync(Guid indexId)
        {
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(CollectionId.FromGuid(indexId),
                () => (_thisNodeId, new ImageInfoLwwCollection(indexId.ToString("N"))));
            return Ok(new { data = index.Values });
        }

        [HttpGet]
        public IActionResult GetAll() => Ok(_context.ImageCollectionsRegistry.Value);

        [HttpPost]
        public async Task<IActionResult> ImageDataSetAsync([FromBody] ImageDataSetEvent data)
        {
            var indexId = CollectionId.FromGuid(data.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwCollection(data.IndexId.ToString("N"))));
            if (!index.TrySet(ImageGuid.FromGuid(data.ImageUuid), new ImageInfo(data.DownloadUri, data.ImageId), DateTime.UtcNow,
                out var img))
            {
                return Conflict(img);
            }
            return Ok(img);
        }

        [HttpDelete]
        public async Task<IActionResult> ImageDeletedAsync([FromBody] ImageDeletedEvent data)
        {
            var indexId = CollectionId.FromGuid(data.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwCollection(data.IndexId.ToString("N"))));
            if (!index.TryRemove(ImageGuid.FromGuid(data.ImageUuid), DateTime.UtcNow, out var image))
            {
                return NotFound();
            }
            return Ok(image);
        }
    }
}