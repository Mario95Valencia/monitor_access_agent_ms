using monitor_access_agent_ms.Application.Records.Request;
using monitor_access_agent_ms.Application.Records.Response;
using MediatR;

public record UpdateExampleCommand(long Id, ExampleRequest Request) : IRequest<ApiResponse<bool>>;