using GSO_Library.Models;
using GSO_Library.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace GSO_Library.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeriesController : ControllerBase
{
    private readonly SeriesRepository _seriesRepository;

    public SeriesController(SeriesRepository seriesRepository)
    {
        _seriesRepository = seriesRepository;
    }

    [HttpPost]
    public async Task<ActionResult<Series>> AddSeries([FromBody] Series series)
    {
        var createdSeries = await _seriesRepository.AddSeriesAsync(series);
        return CreatedAtAction(nameof(AddSeries), new { id = createdSeries.Id }, createdSeries);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSeries(int id)
    {
        await _seriesRepository.DeleteSeriesAsync(id);
        return NoContent();
    }
}
