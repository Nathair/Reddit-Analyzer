using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RedditAnalyzer.Server.Models;
using RedditAnalyzer.Server.Services;

namespace RedditAnalyzer.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RedditController : ControllerBase
    {
        private readonly IRedditService _redditService;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public RedditController(IRedditService redditService)
        {
            _redditService = redditService;
        }

        [HttpPost]
        public async Task<IActionResult> GetPosts([FromBody] RedditSearchRequest request, [FromQuery] bool verbose = false)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return BadRequest("Invalid request format.");
            }

            var result = await _redditService.GetAnalyzedPostsAsync(request, verbose);
            return new JsonResult(result, _jsonOptions);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Ok(new List<string>());
            var results = await _redditService.SearchSubredditsAsync(query);
            return Ok(results);
        }

        [HttpPost("download")]
        public async Task<IActionResult> DownloadPosts([FromBody] RedditSearchRequest request, [FromQuery] bool verbose = false)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return BadRequest("Invalid request format.");
            }

            var result = await _redditService.GetAnalyzedPostsAsync(request, verbose);
            var jsonString = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(jsonString);
            
            return File(bytes, "application/json", "reddit_analysis.json");
        }
    }
}
