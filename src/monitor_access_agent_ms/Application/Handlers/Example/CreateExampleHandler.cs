using monitor_access_agent_ms.Application.Records.Response;
using monitor_access_agent_ms.Domain.Entities;
using MediatR;
using MicroservicesTemplate.Domain.Repositories;

public class CreateExampleHandler : IRequestHandler<CreateExampleCommand, ApiResponse<bool>>
{
    private readonly IBaseRepository<Example> _repository;
    public CreateExampleHandler(IBaseRepository<Example> repository) => _repository = repository;

    public async Task<ApiResponse<bool>> Handle(CreateExampleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var entity = new Example { Name = request.Request.Name.Trim(), Status = true };
            await _repository.AddAsync(entity);
            return new ApiResponse<bool>(Guid.NewGuid(), "BOOLEAN", true, "Created successfully");
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>(Guid.NewGuid(), "ERROR", false, ex.Message);
        }
    }
}