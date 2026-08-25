using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using OpenSpec.Application.Command;
using OpenSpec.Application.Query;
using OpenSpec.Domain.Models;
using static OpenSpec.Application.Query.GetItemsQuery;

namespace OpenSpec.API.Controllers
{
   
    public class ItemsController(IMediator mediator) : ControllerBase
    {
        private readonly IMediator Mediator = mediator;

        

        /// <summary>
        /// Obtiene una lista paginada de registros.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<ItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PagedResult<ItemDto>>> GetPaged(
            [FromQuery] GetItemsQuery query,
            CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(query, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Obtiene un registro por su ID único.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ItemDto>> GetById(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            var result = await Mediator.Send(new GetItemByIdQuery(id), cancellationToken);

            if (result is null)
                return NotFound();

            return Ok(result);
        }

        /// <summary>
        /// Crea un nuevo registro en el sistema.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<Guid>> Create(
            [FromBody] CreateItemCommand command,
            CancellationToken cancellationToken)
        {
            var id = await Mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }

        /// <summary>
        /// Actualiza completamente un registro existente.
        /// </summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            [FromRoute] Guid id,
            [FromBody] UpdateItemCommand command,
            CancellationToken cancellationToken)
        {
            if (id != command.Id)
                return BadRequest("El ID de la ruta no coincide con el cuerpo de la solicitud.");

            await Mediator.Send(command, cancellationToken);
            return NoContent();
        }

        /// <summary>
        /// Elimina un registro del sistema.
        /// </summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            [FromRoute] Guid id,
            CancellationToken cancellationToken)
        {
            await Mediator.Send(new DeleteItemCommand(id), cancellationToken);
            return NoContent();
        }
    }
}
