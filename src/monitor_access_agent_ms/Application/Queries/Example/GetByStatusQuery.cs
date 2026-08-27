using monitor_access_agent_ms.Application.Records.Response;
using MediatR;

public record GetExamplesByStatusQuery(bool Status) : IRequest<ApiResponse<IEnumerable<ExampleResponse>>>;
