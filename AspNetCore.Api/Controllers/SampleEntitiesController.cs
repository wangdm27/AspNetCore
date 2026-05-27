using AspNetCore.DataAccess.Abstractions;
using AspNetCore.DataAccess.Entities;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SampleEntitiesController : ControllerBase
    {
        private readonly IRepository<SampleEntity> _repository;
        private readonly IUnitOfWork _unitOfWork;

        public SampleEntitiesController(
            IRepository<SampleEntity> repository,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SampleEntity>>> GetAllAsync(CancellationToken cancellationToken)
        {
            var entities = await _repository.GetAllAsync(cancellationToken);
            return Ok(entities);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<SampleEntity>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var entity = await _repository.GetByIdAsync(id, cancellationToken);
            if (entity is null)
            {
                return NotFound();
            }

            return Ok(entity);
        }

        [HttpPost]
        public async Task<ActionResult> CreateAsync([FromBody] SampleEntity entity, CancellationToken cancellationToken)
        {
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.CreatedAt = entity.CreatedAt == default ? DateTime.UtcNow : entity.CreatedAt;

            await _repository.AddAsync(entity, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return CreatedAtAction(nameof(GetByIdAsync), new { id = entity.Id }, entity);
        }
    }
}
