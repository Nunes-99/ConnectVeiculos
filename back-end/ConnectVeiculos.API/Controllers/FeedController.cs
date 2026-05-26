using ConnectVeiculos.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConnectVeiculos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class FeedController : ControllerBase
    {
        // Cache curto (60s) pra balancear: feeds sao puxados ~1x/hora por
        // Facebook/Google, e mudancas no admin (preco, novo veiculo, vendido)
        // devem refletir rapidamente. 30 min era atritoso pra debug + cadastro.
        [HttpGet("facebook")]
        [ResponseCache(Duration = 60)]
        public async Task<IActionResult> FacebookFeed([FromServices] IFeedService feedService)
        {
            var feed = await feedService.GerarFeedFacebookAsync();
            return Content(feed, "text/tab-separated-values", System.Text.Encoding.UTF8);
        }

        [HttpGet("google")]
        [ResponseCache(Duration = 60)]
        public async Task<IActionResult> GoogleFeed([FromServices] IFeedService feedService)
        {
            var feed = await feedService.GerarFeedGoogleAsync();
            return Content(feed, "application/xml", System.Text.Encoding.UTF8);
        }
    }
}
