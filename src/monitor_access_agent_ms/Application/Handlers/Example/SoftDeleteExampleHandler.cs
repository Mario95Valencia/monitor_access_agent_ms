using monitor_access_agent_ms.Application.Records.Response;
using monitor_access_agent_ms.Domain.Entities;
using MediatR;
using MicroservicesTemplate.Domain.Repositories;

public class SoftDeleteExampleHandler : IRequestHandler<SoftDeleteExampleCommand, ApiResponse<bool>>
{
    private readonly IBaseRepository<Example> _repository;
    public SoftDeleteExampleHandler(IBaseRepository<Example> repository) => _repository = repository;

    public async Task<ApiResponse<bool>> Handle(SoftDeleteExampleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.SoftDeleteAsync(request.Id);
            return new ApiResponse<bool>(Guid.NewGuid(), "BOOLEAN", true, "Soft deleted successfully");
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>(Guid.NewGuid(), "ERROR", false, ex.Message);
        }
    }
}