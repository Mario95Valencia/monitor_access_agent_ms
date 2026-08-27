using monitor_access_agent_ms.Application.Records.Response;
using monitor_access_agent_ms.Domain.Entities;
using MediatR;
using MicroservicesTemplate.Domain.Repositories;

public class GetExampleByIdHandler : IRequestHandler<GetExampleByIdQuery, ApiResponse<ExampleResponse>>
{
    private readonly IBaseRepository<Example> _repository;
    public GetExampleByIdHandler(IBaseRepository<Example> repository) => _repository = repository;

    public async Task<ApiResponse<ExampleResponse>> Handle(GetExampleByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var e = await _repository.GetByIdAsync(request.Id);
            if (e == null) return new ApiResponse<ExampleResponse>(Guid.NewGuid(), "OBJECT", null, $"Example with ID {request.Id} not found.");
            return new ApiResponse<ExampleResponse>(Guid.NewGuid(), "OBJECT", new ExampleResponse(e.Id, e.Name?.Trim() ?? string.Empty, e.Status), "Success");
        }
        catch (Exception ex)
        {
            return new ApiResponse<ExampleResponse>(Guid.NewGuid(), "ERROR", null, ex.Message);
        }
    }
}