using MediatR;
using Microsoft.AspNetCore.Mvc;
using PractitionerService.Api.Fhir;
using PractitionerService.Api.Fhir.Models;
using PractitionerService.Api.Controllers.Dtos;

namespace PractitionerService.Api.Controllers;

[ApiController]
[Route("Practitioner")]
[Produces(FhirConstants.MediaTypeFhirJson)]
public sealed class PractitionerController : ControllerBase
{
    private readonly IMediator _mediator;

    public PractitionerController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    [ProducesResponseType(typeof(FhirPractitionerResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] FhirPractitionerResource resource, CancellationToken ct)
    {
        var sourceEventId = PractitionerFhirMapper.ExtractSourceEventId(resource);
        if (string.IsNullOrEmpty(sourceEventId))
        {
            var key = Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(key)) sourceEventId = $"idem:{key}";
        }

        var command = PractitionerFhirMapper.ToCreateCommand(resource, sourceEventId);
        var result = await _mediator.Send(command, ct);
        var readModel = await _mediator.Send(new GetPractitionerByIdQuery(result.Id), ct);

        var location = $"{Request.Scheme}://{Request.Host}/Practitioner/{result.Id}";
        Response.Headers.Location = location;
        Response.Headers.ETag = $"\"{result.Version}\"";

        return StatusCode(StatusCodes.Status201Created, PractitionerFhirMapper.ToFhir(readModel));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FhirPractitionerResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetPractitionerByIdQuery(id), ct);
        Response.Headers.ETag = $"\"{dto.Version}\"";
        return Ok(PractitionerFhirMapper.ToFhir(dto));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FhirPractitionerResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] FhirPractitionerResource resource, CancellationToken ct)
    {
        int? expectedVersion = null;
        var ifMatch = Request.Headers.IfMatch.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var raw = ifMatch.Replace("W/", "", StringComparison.Ordinal).Trim('"', ' ');
            if (int.TryParse(raw, out var v)) expectedVersion = v;
        }

        var command = PractitionerFhirMapper.ToUpdateCommand(id, resource, expectedVersion);
        var result = await _mediator.Send(command, ct);
        var readModel = await _mediator.Send(new GetPractitionerByIdQuery(result.Id), ct);

        Response.Headers.ETag = $"\"{result.Version}\"";
        return Ok(PractitionerFhirMapper.ToFhir(readModel));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeletePractitionerCommand(id), ct);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(FhirBundle), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] PractitionerSearchQuery query, CancellationToken ct)
    {
        var filter = new PractitionerFilter(
            NameContains: query.Name,
            FamilyContains: query.Family,
            GivenContains: query.Given,
            SpecialityContains: query.Specialty,
            PlaceOfWorkContains: query.PlaceOfWork,
            EmailEquals: query.Email,
            PhoneContains: query.Phone,
            Active: query.Active,
            Offset: query.Offset ?? 0,
            Count: query.Count ?? 20,
            IncludeDeleted: query.IncludeDeleted ?? false);

        var page = await _mediator.Send(new SearchPractitionersQuery(filter), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/Practitioner";
        var normalizedFilter = filter with { Offset = page.Offset, Count = page.Count };

        var bundle = PractitionerFhirMapper.ToBundle(page, normalizedFilter, baseUrl);
        PractitionerFhirMapper.AppendNextIfAny(bundle, normalizedFilter, baseUrl);

        return Ok(bundle);
    }
}