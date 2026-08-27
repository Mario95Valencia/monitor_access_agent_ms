using monitor_access_agent_ms.Application.Records.Response;
using MediatR;

public record GetExampleByIdQuery(long Id) : IRequest<ApiResponse<ExampleResponse>>;
