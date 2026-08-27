using MediatR;
using Microsoft.AspNetCore.Mvc;
using monitor_access_agent_ms.Application.Records.Request;
using monitor_access_agent_ms.Application.Records.Response;

namespace monitor_access_agent_ms.WebApi.Controllers
{
    // Indica que esta clase es un controlador de API
    [ApiController]

    // Define la ruta base para este controlador
    [Route("api/[Controller]")]
    public class ExampleController : ControllerBase
    {
        private readonly IMediator _mediator;

        // Constructor con inyección de dependencia del Mediator
        public ExampleController(IMediator mediator)
        {
            _mediator = mediator;
        }

        // GET api/examples
        // Obtiene todos los registros de Example
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _mediator.Send(new GetAllExamplesQuery()); // Envía la query a su handler correspondiente
            return Ok(result); // Devuelve la respuesta con estado 200
        }

        // GET api/examples/{id}
        // Obtiene un registro específico por su ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var result = await _mediator.Send(new GetExampleByIdQuery(id));
            return Ok(result);
        }

        // GET api/examples/status/{status}
        // Obtiene todos los registros activos o inactivos según el parámetro
        [HttpGet("status/{status}")]
        public async Task<IActionResult> GetByStatus(bool status)
        {
            var result = await _mediator.Send(new GetExamplesByStatusQuery(status));
            return Ok(result);
        }

        // POST api/examples
        // Crea un nuevo registro de Example
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ExampleRequest request)
        {
            var result = await _mediator.Send(new CreateExampleCommand(request));
            return Ok(result);
        }

        // PUT api/examples/{id}
        // Actualiza un registro existente de Example
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(long id, [FromBody] ExampleRequest request)
        {
            var result = await _mediator.Send(new UpdateExampleCommand(id, request));
            return Ok(result);
        }

        // DELETE api/examples/{id}
        // Elimina físicamente un registro
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _mediator.Send(new DeleteExampleCommand(id));
            return Ok(result);
        }

        // PUT api/examples/{id}/soft-delete
        // Elimina lógicamente un registro (cambia su status a false)
        [HttpPatch("{id}/soft-delete")]
        public async Task<IActionResult> SoftDelete(long id)
        {
            var result = await _mediator.Send(new SoftDeleteExampleCommand(id));
            return Ok(result);
        }
    }
}
