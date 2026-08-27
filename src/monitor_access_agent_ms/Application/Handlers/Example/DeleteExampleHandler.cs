using monitor_access_agent_ms.Application.Records.Response;
using monitor_access_agent_ms.Domain.Entities;
using MediatR;
using MicroservicesTemplate.Domain.Repositories;

public class DeleteExampleHandler : IRequestHandler<DeleteExampleCommand, ApiResponse<bool>>
{
    private readonly IBaseRepository<Example> _repository;
    public DeleteExampleHandler(IBaseRepository<Example> repository) => _repository = repository;

    public async Task<ApiResponse<bool>> Handle(DeleteExampleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await _repository.DeleteAsync(request.Id);
            return new ApiResponse<bool>(Guid.NewGuid(), "BOOLEAN", true, "Deleted successfully");
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>(Guid.NewGuid(), "ERROR", false, ex.Message);
        }
    }
}