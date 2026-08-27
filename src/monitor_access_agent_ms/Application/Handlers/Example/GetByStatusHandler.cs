using monitor_access_agent_ms.Application.Records.Response;
using monitor_access_agent_ms.Domain.Entities;
using MediatR;
using MicroservicesTemplate.Domain.Repositories;

public class GetExamplesByStatusHandler : IRequestHandler<GetExamplesByStatusQuery, ApiResponse<IEnumerable<ExampleResponse>>>
{
    private readonly IBaseRepository<Example> _repository;
    public GetExamplesByStatusHandler(IBaseRepository<Example> repository) => _repository = repository;

    public async Task<ApiResponse<IEnumerable<ExampleResponse>>> Handle(GetExamplesByStatusQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = (await _repository.GetByStatusAsync(request.Status)).Select(e => new ExampleResponse(e.Id, e.Name?.Trim() ?? string.Empty, e.Status));
            return new ApiResponse<IEnumerable<ExampleResponse>>(Guid.NewGuid(), "LIST", result, "Retrieved by status");
        }
        catch (Exception ex)
        {
            return new ApiResponse<IEnumerable<ExampleResponse>>(Guid.NewGuid(), "ERROR", null, ex.Message);
        }
    }
}