using monitor_access_agent_ms.Application.Records.Response;
using MediatR;

public record SoftDeleteExampleCommand(long Id) : IRequest<ApiResponse<bool>>;

