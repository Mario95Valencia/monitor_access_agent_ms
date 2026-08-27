using MediatR;
using monitor_access_agent_ms.Application.Records.Request;
using monitor_access_agent_ms.Application.Records.Response;

public record CreateExampleCommand(ExampleRequest Request) : IRequest<ApiResponse<bool>>;