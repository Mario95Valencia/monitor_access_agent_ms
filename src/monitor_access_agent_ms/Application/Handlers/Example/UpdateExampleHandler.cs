using monitor_access_agent_ms.Application.Records.Response;
using monitor_access_agent_ms.Domain.Entities;
using MediatR;
using MicroservicesTemplate.Domain.Repositories;

public class UpdateExampleHandler : IRequestHandler<UpdateExampleCommand, ApiResponse<bool>>
{
    private readonly IBaseRepository<Example> _repository;
    public UpdateExampleHandler(IBaseRepository<Example> repository) => _repository = repository;

    public async Task<ApiResponse<bool>> Handle(UpdateExampleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existing = await _repository.GetByIdAsync(request.Id);
            if (existing == null)
            {
                return new ApiResponse<bool>(
                    Guid.NewGuid(),
                    "OBJECT",
                    false,
                    $"Example with ID {request.Id} not found."
                );
            }

            // Solo se actualizan campos permitidos (sin modificar la clave primaria)
            existing.Name = request.Request.Name.Trim();
            existing.Status = request.Request.Status;

            await _repository.UpdateAsync(request.Id, existing);

            return new ApiResponse<bool>(
                Guid.NewGuid(),
                "BOOLEAN",
                true,
                "Updated successfully"
            );
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>(
                Guid.NewGuid(),
                "ERROR",
                false,
                ex.Message
            );
        }
    }
}
