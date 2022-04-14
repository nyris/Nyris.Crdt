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
                () => (_thisNodeId, new ImageInfoLwwCollection(new InstanceId(indexId.ToString("N")))));
            return Ok(new { data = index.Values });
        }

        [HttpPost("{indexId:guid}")]
        public async Task<IActionResult> CreateIndexAsync(Guid indexId)
        {
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(CollectionId.FromGuid(indexId),
                () => (_thisNodeId, new ImageInfoLwwCollection(new InstanceId(indexId.ToString("N")))));
            return Ok(new { data = index.Values });
        }

        [HttpGet]
        public IActionResult GetAll() => Ok(_context.ImageCollectionsRegistry.Value(collection => collection.Value));

        [HttpPost]
        public async Task<IActionResult> ImageDataSetAsync([FromBody] ImageDataSetEvent data)
        {
            var indexId = CollectionId.FromGuid(data.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwCollection(new InstanceId(data.IndexId.ToString("N")))));

            var datetime = DateTime.UtcNow;
            var img = await index.SetAsync(ImageGuid.FromGuid(data.ImageUuid),
                new ImageInfo(data.DownloadUri, data.ImageId), datetime);
            return img.TimeStamp == datetime ? Ok(img) : Conflict(img);
        }

        [HttpDelete]
        public async Task<IActionResult> ImageDeletedAsync([FromBody] ImageDeletedEvent data)
        {
            var indexId = CollectionId.FromGuid(data.IndexId);
            var index = await _context.ImageCollectionsRegistry.GetOrCreateAsync(indexId,
                () => (_thisNodeId, new ImageInfoLwwCollection(new InstanceId(data.IndexId.ToString("N")))));
            var item = await index.RemoveAsync(ImageGuid.FromGuid(data.ImageUuid), DateTime.UtcNow);
            return item.Value == default ? NotFound() : Ok(item.Value);
        }
    }
}