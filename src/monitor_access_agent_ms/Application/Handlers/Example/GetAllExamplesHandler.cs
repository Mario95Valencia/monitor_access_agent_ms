using monitor_access_agent_ms.Application.Records.Response;
using monitor_access_agent_ms.Domain.Entities;
using MediatR;
using MicroservicesTemplate.Domain.Repositories;

public class GetAllExamplesHandler : IRequestHandler<GetAllExamplesQuery, ApiResponse<IEnumerable<ExampleResponse>>>
{
    private readonly IBaseRepository<Example> _repository;
    public GetAllExamplesHandler(IBaseRepository<Example> repository) => _repository = repository;

    public async Task<ApiResponse<IEnumerable<ExampleResponse>>> Handle(GetAllExamplesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var result = (await _repository.GetAllAsync()).Select(MapToResponse);
            return new ApiResponse<IEnumerable<ExampleResponse>>(Guid.NewGuid(), "LIST", result, "Retrieved successfully");
        }
        catch (Exception ex)
        {
            return new ApiResponse<IEnumerable<ExampleResponse>>(Guid.NewGuid(), "ERROR", null, ex.Message);
        }
    }

    private static ExampleResponse MapToResponse(Example e) => new(e.Id, e.Name?.Trim() ?? string.Empty, e.Status);
}