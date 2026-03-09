using BLL.DTOs;
using BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace NongXanhController.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RssController : BaseApiController
    {
        private readonly IArticleRssService _rssService;

        public RssController(IArticleRssService rssService)
        {
            _rssService = rssService;
        }

        /// <summary>
        /// Triggers the RSS fetching process to find and save new articles based on configured keywords.
        /// This is an admin-only endpoint.
        /// </summary>
        /// <returns>A report of how many new articles were saved.</returns>
        [AllowAnonymous]
        [HttpPost("fetch")]
        public async Task<ActionResult<ApiResponse<RssFetchResultDto>>> FetchRss()
        {
            if (!IsAdmin())
            {
                // Explicitly specify the type for ErrorResponse
                return ErrorResponse<RssFetchResultDto>("Forbidden: This action is restricted to administrators.", statusCode: 403);
            }

            var count = await _rssService.FetchAndSaveArticlesAsync();
            
            var result = new RssFetchResultDto { NewArticlesSaved = count };
            
            return SuccessResponse(result, $"RSS fetch process completed. {count} new articles were saved.");
        }
    }
}
