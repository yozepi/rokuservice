using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RokuService.Services;
using yozepi.Roku;

namespace RokuService.Controllers
{
    [Route("api/rokus")]
    public class RokuController : Controller
    {
        int pause = 150;

        readonly RokuManager Manager;
        readonly ILogger Logger;

        public RokuController(RokuManager manager, ILogger<RokuController> logger)
        {
            this.Manager = manager;
            this.Logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ListRokus([FromQuery] bool forceDiscovery)
        {
            Logger.LogInformation("begin ListRokus" );

            var discovery = new RokuDiscovery();
            var rokus = await Manager.ListRokusAsync(forceDiscovery);
            Logger.LogInformation($"{rokus.Count} Roku(s) found.");
            for (int i = 0; i < rokus.Count; i++)
            {
                var roku = rokus[i];
                Logger.LogInformation($"{i + 1}) {roku.Name} (id {roku.Id})");
            }
  
            //Organize the list first by Alpha Name, then by ids
            var rokuList = new List<object>();

            rokuList.AddRange(rokus
                .Where(r => !string.IsNullOrEmpty(r.Name))
                .OrderBy(r => r.Name)
                .Select(r => new { Id = r.Id, name = r.Name }));

            rokuList.AddRange(rokus
                .Where(r => string.IsNullOrEmpty(r.Name))
                .OrderBy(r => r.Id)
                .Select(r => new { Id = r.Id }));

            Logger.LogInformation("end ListRokus");
            return Ok(rokuList);
        }

        [HttpGet]
        [Route("{rokuId}/info")]
        public async Task<IActionResult> GetInfo([FromRoute] string rokuId)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            return Ok(roku.Info);
        }

        [HttpGet]
        [Route("{rokuId}/apps")]
        public async Task<IActionResult> ListApps([FromRoute] string rokuId, [FromQuery] string filter)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            if (string.IsNullOrWhiteSpace(filter))
                return Json(roku.Apps.OrderBy(a => a.Text));

            return Json(FilteredAppList(roku, filter));
        }

        [HttpGet]
        [Route("{rokuId}/apps/launch")]
        public async Task<IActionResult> LaunchApp([FromRoute] string rokuId, [FromQuery] string filter)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            if (string.IsNullOrWhiteSpace(filter))
            {
                return BadRequest(new { message = "The filter query string value is required.", code = "NoFilter" });
            }

            var appList = FilteredAppList(roku, filter).ToArray();
            if (appList.Length == 0)
            {
                return NotFound(new { message = "no matching apps found", code = "NoMatch" });
            }

            if (appList.Length > 1)
            {
                return BadRequest(new { message = "More than one app matches.", code = "AmbiguousMatch", apps = appList });
            }

            var rokuResult = await roku.LaunchAppAsync(appList[0].Id);
            if (rokuResult.IsSuccess)
                return Ok(new { message = "app launched.", code = "AppLaunched", appName = appList[0].Text });

            return RokuErrorResponse(rokuResult);

        }

        [HttpGet]
        [Route("{rokuId}/apps/launch/{id}")]
        public async Task<IActionResult> LaunchApp([FromRoute] string rokuId, [FromRoute] int id)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            var appList = roku.Apps
                .Where(a => a.Id == id).ToArray();
            if (appList.Length == 0)
            {
                return NotFound(new { message = "no matching apps found", code = "NoMatch" });
            }

            //Should never happen?
            if (appList.Length > 1)
            {
                return BadRequest(new { message = "More than one app matches.", code = "AmbiguousMatch", apps = appList });
            }

            var rokuResult = await roku.LaunchAppAsync(appList[0].Id);
            if (rokuResult.IsSuccess)
                return Ok();

            return RokuErrorResponse(rokuResult);

        }

        private IEnumerable<RokuApp> FilteredAppList(IRokuRemote roku, string filter)
        {

            filter = filter.ToLowerInvariant();
            return roku.Apps
                .Where(a => a.Text.ToLowerInvariant().Contains(filter))
                .OrderBy(a => a.Text);

        }

        [HttpGet]
        [Route("{rokuId}/apps/active")]
        public async Task<IActionResult> GetActiveApp([FromRoute] string rokuId)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            var rokuResult = await roku.GetActiveAppAsync();
            if (rokuResult.IsSuccess)
                return Json(rokuResult);

            return RokuErrorResponse(rokuResult);
        }

        [HttpGet]
        [Route("{rokuId}/send/{command}/{count?}")]
        public async Task<IActionResult> Send([FromRoute] string rokuId, [FromRoute]CommandKeys command, [FromRoute]int count = 1)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            return await Send(() => roku.KeypressAsync(command), count);
        }

        [HttpGet]
        [Route("{rokuId}/send/{ch:regex(^.$)}/{count?}")]
        public async Task<IActionResult> Send([FromRoute] string rokuId, [FromRoute]char ch, [FromRoute]int count = 1)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            return await Send(() => roku.KeypressAsync(ch), count);
        }

        [HttpGet]
        [Route("{rokuId}/send/space/{count?}")]
        public async Task<IActionResult> SendSpace([FromRoute] string rokuId, [FromRoute]int count = 1)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            return await Send(() => roku.KeypressAsync(' '), count);
        }

        private async Task<IActionResult> Send(Func<Task<ICommandResponse>> sendCommand, int count)
        {
            if (count < 1)
            {
                return ErrorResponse("count must be 1 or greater.", HttpStatusCode.BadRequest);
            }

            for (int c = 0; c < count; c++)
            {
                var rokuResult = await sendCommand();
                if (!rokuResult.IsSuccess)
                    return RokuErrorResponse(rokuResult);

                await Task.Delay(pause);
            }
            return Ok();
        }

        [HttpGet]
        [Route("{rokuId}/type")]
        public async Task<IActionResult> SentText([FromRoute] string rokuId, [FromQuery] string text)
        {
            var roku = await Manager.GetRokuAsync(rokuId);
            if (roku == null)
                return RokuNotFoundResult(rokuId);

            if (string.IsNullOrEmpty(text))
            {
                return ErrorResponse("text is required.", HttpStatusCode.BadRequest);
            }

            foreach (var ch in text)
            {
                var rokuResult = await roku.KeypressAsync(ch);
                if (!rokuResult.IsSuccess)
                    return RokuErrorResponse(rokuResult);

                await Task.Delay(pause);
            }
            return Ok();

        }

        private IActionResult RokuErrorResponse(ICommandResponse rokuResult)
        {
            return ErrorResponse(rokuResult.StatusDescription, rokuResult.StatusCode);
        }

        private IActionResult ErrorResponse(string message, HttpStatusCode statusCode)
        {
            var errorResponse = Json(new { message = message });
            errorResponse.StatusCode = (int)statusCode;
            return errorResponse;
        }

        private IActionResult RokuNotFoundResult(string rokuId)
        {
            var response = NotFound(new { message = "Roku not found.", code = "RokuNotFound", id = rokuId });
            return response;
        }
    }
}
