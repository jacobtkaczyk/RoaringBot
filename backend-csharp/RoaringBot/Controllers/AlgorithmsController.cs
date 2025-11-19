using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AlgorithmsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAlgorithms()
    {
        var path = "/algos-python"; // path inside Docker container

        if (!Directory.Exists(path))
            return Ok(new List<string>());

        var files = Directory.GetFiles(path, "*.py")
                             .Select(Path.GetFileName)
                             .ToList();

        return Ok(files);
    }
}